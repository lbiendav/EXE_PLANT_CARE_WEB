using HomePlant.Models;

namespace HomePlant.Services;

public enum PaymentRuntimeMode
{
    Disabled,
    Demo,
    Live
}

public sealed record PaymentPolicyDecision(bool Allowed, PaymentRuntimeMode Mode, string Code, string Message);

public sealed class PaymentModePolicy(IConfiguration configuration)
{
    public PaymentRuntimeMode Mode => ParseMode(configuration["Payments:Mode"]);
    public string Stage => configuration["App:DeploymentStage"]?.Trim() ?? "Production";
    public string ProjectId => configuration["Firebase:ProjectId"]?.Trim() ?? "";

    public PaymentPolicyDecision CanCreateCheckout(string uid)
    {
        if (!(configuration.GetValue<bool?>("Subscriptions:Enabled") ?? false))
            return Deny("subscriptions_disabled", "Tính năng đăng ký hiện đang tạm đóng.");

        return Mode switch
        {
            PaymentRuntimeMode.Demo => CanUseDemo(),
            PaymentRuntimeMode.Live => CanUseLive(uid),
            _ => Deny("payments_disabled", "Thanh toán hiện đang tạm đóng.")
        };
    }

    public PaymentPolicyDecision CanSimulate(SubscriptionOrderModel order)
    {
        if (!order.IsDemo || !order.PaymentMode.Equals("Demo", StringComparison.OrdinalIgnoreCase))
            return Deny("live_order_not_simulatable", "Đơn thanh toán thật không thể được xác nhận bằng simulator.");
        return CanUseDemo();
    }

    public bool IsDemoSubscriptionAllowed(SubscriptionModel subscription) =>
        !subscription.IsDemo || (Mode == PaymentRuntimeMode.Demo && CanUseDemo().Allowed);

    private PaymentPolicyDecision CanUseDemo()
    {
        if (Stage.Equals("Production", StringComparison.OrdinalIgnoreCase) ||
            !(configuration.GetValue<bool?>("Payments:DemoEnabled") ?? false))
            return Deny("demo_disabled", "Mô phỏng thanh toán không được phép trong môi trường này.");

        var allowed = configuration.GetSection("Payments:AllowedDemoProjectIds").Get<string[]>() ?? [];
        var emulator = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST"));
        return emulator || allowed.Contains(ProjectId, StringComparer.Ordinal)
            ? Allow(PaymentRuntimeMode.Demo)
            : Deny("demo_project_not_allowed", "Firebase project này không được phép chạy simulator.");
    }

    private PaymentPolicyDecision CanUseLive(string uid)
    {
        if (!Stage.Equals("Production", StringComparison.OrdinalIgnoreCase))
            return Deny("live_stage_not_allowed", "Thanh toán thật chỉ được phép trên deployment Production.");
        if (!(configuration.GetValue<bool?>("Payments:NewCheckoutsEnabled") ?? false))
            return Deny("new_checkouts_disabled", "Hệ thống đang tạm dừng tạo thanh toán mới.");
        var allowedProjects = configuration.GetSection("Payments:AllowedLiveProjectIds").Get<string[]>() ?? [];
        if (!allowedProjects.Contains(ProjectId, StringComparer.Ordinal))
            return Deny("live_project_not_allowed", "Firebase project này không được phép nhận thanh toán thật.");
        if (configuration.GetValue<bool?>("Payments:PilotOnly") ?? true)
        {
            var pilotUsers = configuration.GetSection("Payments:PilotUserIds").Get<string[]>() ?? [];
            if (!pilotUsers.Contains(uid, StringComparer.Ordinal))
                return Deny("pilot_user_not_allowed", "Tài khoản này chưa nằm trong phạm vi pilot.");
        }
        var provider = configuration["Payments:Provider"]?.Trim();
        var channelId = configuration["Payments:PayOS:ChannelId"]?.Trim();
        var clientId = configuration["Payments:PayOS:ClientId"]?.Trim();
        var apiKey = configuration["Payments:PayOS:ApiKey"]?.Trim();
        var checksumKey = configuration["Payments:PayOS:ChecksumKey"]?.Trim();
        var publicBaseUrl = configuration["App:PublicBaseUrl"]?.Trim();
        if (!string.Equals(provider, "PayOS", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(channelId) || string.IsNullOrWhiteSpace(clientId) ||
            string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(checksumKey) ||
            !Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out var publicUri) || publicUri.Scheme != Uri.UriSchemeHttps)
            return Deny("live_configuration_incomplete", "Cấu hình payOS live chưa đầy đủ hoặc không hợp lệ.");
        return Allow(PaymentRuntimeMode.Live);
    }

    public static PaymentRuntimeMode ParseMode(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "demo" => PaymentRuntimeMode.Demo,
        "live" => PaymentRuntimeMode.Live,
        _ => PaymentRuntimeMode.Disabled
    };

    private static PaymentPolicyDecision Allow(PaymentRuntimeMode mode) => new(true, mode, "ok", "");
    private PaymentPolicyDecision Deny(string code, string message) => new(false, Mode, code, message);
}
