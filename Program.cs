using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using HomePlant.Services;
using Microsoft.Extensions.FileProviders;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Render supplies PORT; local launch profiles continue to work without it.
var port = builder.Configuration["PORT"];
if (!string.IsNullOrWhiteSpace(port))
{
    if (!int.TryParse(port, out var portNumber) || portNumber is < 1 or > 65535)
        throw new InvalidOperationException("PORT must be a valid TCP port.");

    builder.WebHost.UseUrls($"http://0.0.0.0:{portNumber}");
}

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<HomePlant.Filters.ActiveUserFilter>();
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
});

builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(12);
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

builder.Services.AddHttpContextAccessor();

builder.Services.AddHttpClient();
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("ai", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 4,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

var credentialPath = builder.Configuration["Firebase:CredentialPath"];
var firebaseJson = builder.Configuration["FIREBASE_KEY"];
var localCredentialPath = Path.Combine(
    builder.Environment.ContentRootPath, "Firebase", "firebase-key.json");

// A mounted secret (GOOGLE_APPLICATION_CREDENTIALS) or platform identity is
// preferred in hosted environments. Keep the existing local development setup.
GoogleCredential credential;
if (!string.IsNullOrWhiteSpace(credentialPath))
{
    credential = CredentialFactory.FromFile<ServiceAccountCredential>(
        Path.GetFullPath(credentialPath, builder.Environment.ContentRootPath)).ToGoogleCredential();
}
else if (!string.IsNullOrWhiteSpace(firebaseJson))
{
    // Compatibility with the existing GitHub deployment configuration.
    credential = CredentialFactory.FromJson<ServiceAccountCredential>(firebaseJson).ToGoogleCredential();
}
else if (builder.Environment.IsDevelopment()
    && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS"))
    && File.Exists(localCredentialPath))
{
    credential = CredentialFactory.FromFile<ServiceAccountCredential>(localCredentialPath).ToGoogleCredential();
}
else
{
    credential = await GoogleCredential.GetApplicationDefaultAsync();
}

var firebaseProjectId = builder.Configuration["Firebase:ProjectId"];
if (string.IsNullOrWhiteSpace(firebaseProjectId))
    throw new InvalidOperationException("Configure Firebase__ProjectId before starting HomePlant.");

if (FirebaseApp.DefaultInstance == null)
{
    FirebaseApp.Create(new AppOptions
    {
        Credential = credential
    });
}

builder.Services.AddSingleton(provider =>
{
    return new FirestoreDbBuilder
    {
        ProjectId = firebaseProjectId,
        Credential = credential
    }.Build();
});

builder.Services.AddScoped<FirestoreService>();
builder.Services.AddSingleton<PlanCatalogService>();
builder.Services.AddSingleton<ISubscriptionClock, SystemSubscriptionClock>();
builder.Services.AddScoped<EntitlementService>();
builder.Services.AddScoped<UsageService>();
builder.Services.AddScoped<AiQuotaService>();
builder.Services.AddScoped<SubscriptionOrderService>();
builder.Services.AddScoped<DemoPaymentService>();
builder.Services.AddSingleton<PaymentModePolicy>();
builder.Services.AddSingleton<IBankQrService, VietQrService>();
builder.Services.AddScoped<FirebaseAuthService>();
builder.Services.AddScoped<CareLogService>();
builder.Services.AddScoped<ArticleService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<PlantSampleService>();
builder.Services.AddScoped<UserPlantService>();
builder.Services.AddScoped<PlantTemplateService>();
builder.Services.AddScoped<ImageStorageService>();
builder.Services.AddScoped<CommunityPostService>();
builder.Services.AddScoped<QaThreadService>();
builder.Services.AddScoped<AiDiagnosisService>();
builder.Services.AddHttpClient<PlantExpertAiService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(45);
});
builder.Services.AddHttpClient<EmailNotificationService>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddScoped<CareReminderService>();
builder.Services.AddHostedService<CareReminderBackgroundService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// The platform probes this endpoint over HTTP; it must not redirect to HTTPS.
app.UseWhen(context => !context.Request.Path.Equals("/healthz"), branch =>
{
    branch.UseHttpsRedirection();
});

var landingPageProvider = new PhysicalFileProvider(
    Path.Combine(builder.Environment.ContentRootPath, "3D_UI"));

app.UseDefaultFiles(new DefaultFilesOptions
{
    FileProvider = landingPageProvider,
    RequestPath = ""
});

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = landingPageProvider,
    RequestPath = ""
});

app.UseStaticFiles();

app.UseRouting();

app.UseRateLimiter();

app.UseSession();

// Liveness only: no Firestore reads and no external dependency on every probe.
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.MapControllerRoute(
name: "default",
pattern: "{controller}/{action=Index}/{id?}");

app.Run();
