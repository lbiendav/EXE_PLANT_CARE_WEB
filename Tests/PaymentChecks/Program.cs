using Google.Cloud.Firestore;
using HomePlant.Models;
using HomePlant.Services;
using Microsoft.Extensions.Configuration;
using PayOS.Models.Webhooks;

var oldEmulator = Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST");
Environment.SetEnvironmentVariable("FIRESTORE_EMULATOR_HOST", null);
try
{
    Check(Policy().Mode == PaymentRuntimeMode.Disabled, "unknown mode fails closed");
    Check(!Policy(Demo()).CanCreateCheckout("u1").Allowed, "demo denied in Production");
    Check(Policy(Demo(stage: "Staging", project: "wrong")).CanCreateCheckout("u1").Code == "demo_project_not_allowed", "demo denied for wrong project");
    Check(Policy(Demo(stage: "Staging")).CanCreateCheckout("u1").Allowed, "demo allowed for explicitly allowed staging project");
    Check(Policy(Live(stage: "Staging")).CanCreateCheckout("pilot").Code == "live_stage_not_allowed", "live denied outside Production");

    var disabled = Live(); disabled["Payments:NewCheckoutsEnabled"] = "false";
    Check(Policy(disabled).CanCreateCheckout("pilot").Code == "new_checkouts_disabled", "live kill switch enforced");
    var incomplete = Live(); incomplete.Remove("Payments:PayOS:ChecksumKey");
    Check(Policy(incomplete).CanCreateCheckout("pilot").Code == "live_configuration_incomplete", "live credentials required");
    Check(Policy(Live()).CanCreateCheckout("other").Code == "pilot_user_not_allowed", "pilot allowlist enforced");
    Check(Policy(Live()).CanCreateCheckout("pilot").Allowed, "complete pilot configuration accepted by policy");

    var demoPolicy = Policy(Demo(stage: "Staging"));
    Check(!demoPolicy.CanSimulate(Order("Live", false)).Allowed, "simulator rejects live order");
    Check(demoPolicy.CanSimulate(Order("Demo", true)).Allowed, "simulator accepts demo order");
    Check(demoPolicy.IsDemoSubscriptionAllowed(new SubscriptionModel { IsDemo = true }), "demo entitlement accepted only in allowed demo environment");
    Check(!Policy(Demo()).IsDemoSubscriptionAllowed(new SubscriptionModel { IsDemo = true }), "demo entitlement rejected in Production");
    Check(!Policy().IsDemoSubscriptionAllowed(new SubscriptionModel { IsDemo = true }), "demo entitlement rejected while payments are disabled");
    Check(Policy().IsDemoSubscriptionAllowed(new SubscriptionModel { IsDemo = false }), "live entitlement is not controlled by demo switch");
    await CheckThrows<InvalidOperationException>(
        () => new PayOsPaymentProvider(new ConfigurationBuilder().Build()).VerifyWebhook(new Webhook()),
        "payOS provider fails closed without credentials");

    var qr = new VietQrService();
    Check(!qr.Build(Order("Demo", true)).IsConfigured, "QR absent when bank snapshot is absent");
    var validOrder = Order("Demo", true);
    validOrder.AmountVnd = 48_000; validOrder.TransferReference = "HPABC123";
    validOrder.BankBin = "970422"; validOrder.BankAccountNumber = "123456789"; validOrder.BankAccountName = "HOME PLANT";
    var result = qr.Build(validOrder);
    Check(result.IsConfigured && result.ImageUrl!.StartsWith("https://img.vietqr.io/image/970422-123456789-compact2.png", StringComparison.Ordinal), "QR uses VietQR HTTPS endpoint");
    Check(result.ImageUrl!.Contains("amount=48000") && result.ImageUrl.Contains("addInfo=HPABC123") && result.ImageUrl.Contains("accountName=HOME%20PLANT"), "QR encodes immutable order snapshot");
    validOrder.BankAccountNumber = new string('1', 20);
    Check(!qr.Build(validOrder).IsConfigured, "QR rejects overlong account number");
    validOrder.BankAccountNumber = "123456789"; validOrder.TransferReference = "HP&UNSAFE";
    Check(!qr.Build(validOrder).IsConfigured, "QR rejects unsafe transfer content");

    var liveOrder = Order("Live", false);
    liveOrder.Provider = "PayOS"; liveOrder.ProviderOrderCode = 1234567; liveOrder.ProviderPaymentLinkId = "link-1"; liveOrder.AmountVnd = 48_000;
    var verified = new VerifiedPayment(1234567, 48_000, "VND", "link-1", "bank-ref-1", "HP1234567", "receiver", "2026-09-21 21:00:00", "00");
    Check(LivePaymentService.ValidateEvidence(liveOrder, verified) == "ok", "verified exact payOS evidence is grantable");
    Check(LivePaymentService.ValidateEvidence(liveOrder, verified with { Amount = 47_000 }) == "amount_mismatch", "wrong amount is held for review");
    Check(LivePaymentService.ValidateEvidence(liveOrder, verified with { PaymentLinkId = "other" }) == "payment_link_mismatch", "wrong payment link is held for review");
    Check(LivePaymentService.ValidateEvidence(liveOrder, verified with { Reference = "" }) == "reference_missing", "missing bank reference is held for review");
    liveOrder.IsDemo = true;
    Check(LivePaymentService.ValidateEvidence(liveOrder, verified) == "order_not_live_payos", "live webhook cannot grant a demo order");

    Console.WriteLine("Payment checks passed.");
}
finally
{
    Environment.SetEnvironmentVariable("FIRESTORE_EMULATOR_HOST", oldEmulator);
}

static PaymentModePolicy Policy(Dictionary<string, string?>? values = null) =>
    new(new ConfigurationBuilder().AddInMemoryCollection(values ?? new()).Build());

static Dictionary<string, string?> Demo(string stage = "Production", string project = "staging-project") => new()
{
    ["Subscriptions:Enabled"] = "true", ["Payments:Mode"] = "Demo", ["Payments:DemoEnabled"] = "true",
    ["App:DeploymentStage"] = stage, ["Firebase:ProjectId"] = project, ["Payments:AllowedDemoProjectIds:0"] = "staging-project"
};

static Dictionary<string, string?> Live(string stage = "Production") => new()
{
    ["Subscriptions:Enabled"] = "true", ["Payments:Mode"] = "Live", ["Payments:NewCheckoutsEnabled"] = "true",
    ["App:DeploymentStage"] = stage, ["App:PublicBaseUrl"] = "https://pay.example.test", ["Firebase:ProjectId"] = "live-project",
    ["Payments:AllowedLiveProjectIds:0"] = "live-project", ["Payments:PilotOnly"] = "true", ["Payments:PilotUserIds:0"] = "pilot",
    ["Payments:Provider"] = "PayOS", ["Payments:PayOS:ChannelId"] = "channel", ["Payments:PayOS:ClientId"] = "client",
    ["Payments:PayOS:ApiKey"] = "api", ["Payments:PayOS:ChecksumKey"] = "checksum"
};

static SubscriptionOrderModel Order(string mode, bool isDemo) => new()
{
    PaymentMode = mode, IsDemo = isDemo, ExpiresAt = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow.AddMinutes(10))
};

static void Check(bool condition, string name)
{
    if (!condition) throw new Exception($"FAIL {name}");
    Console.WriteLine($"PASS {name}");
}

static async Task CheckThrows<T>(Func<Task> action, string name) where T : Exception
{
    try { await action(); }
    catch (T) { Console.WriteLine($"PASS {name}"); return; }
    throw new Exception($"FAIL {name}");
}
