using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using HomePlant.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

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
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = builder.Environment.IsDevelopment()
        ? "HomePlant.Antiforgery"
        : "__Host-HomePlant.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.Path = "/";
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    // Render is the only public ingress and its proxy addresses are dynamic.
    // Limit processing to the right-most hop so client-supplied values cannot
    // override the values appended by Render's edge.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var persistentSessionConfigured = !builder.Environment.IsDevelopment() ||
    builder.Configuration.GetValue<bool>("Session:UseFirestoreInDevelopment");
if (persistentSessionConfigured)
{
    builder.Services.AddSingleton<IDistributedCache, FirestoreDistributedCache>();
    builder.Services.AddSingleton<FirestoreXmlRepository>();
    builder.Services.AddSingleton<IConfigureOptions<KeyManagementOptions>, ConfigureFirestoreDataProtection>();
    builder.Services.AddHostedService<FirestoreCacheCleanupBackgroundService>();
}
else
{
    builder.Services.AddDistributedMemoryCache();
}
builder.Services.AddDataProtection().SetApplicationName("HomePlant");

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(12);
    options.Cookie.Name = builder.Environment.IsDevelopment()
        ? "HomePlant.Session"
        : "__Host-HomePlant.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.Path = "/";
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

builder.Services.AddHttpContextAccessor();

builder.Services.AddHttpClient();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
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

    options.AddPolicy("upload", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Session.GetString("Uid") ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 8,
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
        Credential = credential,
        ProjectId = firebaseProjectId
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
builder.Services.AddSingleton(credential);

builder.Services.AddScoped<FirestoreService>();
builder.Services.AddSingleton<PlanCatalogService>();
builder.Services.AddScoped<PlanSettingsService>();
builder.Services.AddSingleton<ISubscriptionClock, SystemSubscriptionClock>();
builder.Services.AddScoped<EntitlementService>();
builder.Services.AddScoped<UsageService>();
builder.Services.AddScoped<AiQuotaService>();
builder.Services.AddScoped<SubscriptionOrderService>();
builder.Services.AddScoped<DemoPaymentService>();
builder.Services.AddScoped<PaymentReceiptService>();
builder.Services.AddScoped<LivePaymentService>();
builder.Services.AddScoped<PaymentReconciliationService>();
builder.Services.AddScoped<RevenueAdminService>();
builder.Services.AddHttpClient<GoogleAnalyticsService>(client => client.Timeout = TimeSpan.FromSeconds(12));
builder.Services.AddSingleton<PaymentModePolicy>();
builder.Services.AddSingleton<IPaymentProvider, PayOsPaymentProvider>();
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
builder.Services.AddHostedService<PaymentOperationsBackgroundService>();

var app = builder.Build();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["X-Frame-Options"] = "DENY";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        headers["Content-Security-Policy"] = string.Join(" ",
            "default-src 'self';",
            "base-uri 'self';",
            "object-src 'none';",
            "frame-ancestors 'none';",
            "form-action 'self';",
            "img-src 'self' data: https:;",
            "font-src 'self' data:;",
            "style-src 'self' 'unsafe-inline';",
            "script-src 'self' 'unsafe-inline' https://www.googletagmanager.com;",
            "connect-src 'self' https://www.google-analytics.com https://region1.google-analytics.com;");
        if (context.Request.IsHttps)
            headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
        return Task.CompletedTask;
    });
    await next();
});

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
    RequestPath = "",
    OnPrepareResponse = context =>
    {
        if (context.Context.Request.Path.StartsWithSegments("/assets") ||
            context.Context.Request.Path.StartsWithSegments("/render_output"))
            context.Context.Response.Headers.CacheControl = "public,max-age=3600";
    }
});

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
        context.Context.Response.Headers.CacheControl = "public,max-age=604800"
});

app.UseRouting();

app.UseSession();

app.UseRateLimiter();

// Liveness only: no Firestore reads and no external dependency on every probe.
app.MapGet("/healthz", () => Results.Ok(new
{
    status = "ok",
    revision = Environment.GetEnvironmentVariable("RENDER_GIT_COMMIT") ?? "local"
}));

// Dependency readiness is intentionally separate from liveness so a transient
// Firestore outage does not cause the platform to restart a healthy process.
app.MapGet("/readyz", async (FirestoreDb firestore, CancellationToken cancellationToken) =>
{
    try
    {
        await firestore.Collection("users").Limit(1).GetSnapshotAsync(cancellationToken);
        if (persistentSessionConfigured)
            await firestore.Collection("system_cache").Limit(1).GetSnapshotAsync(cancellationToken);
        return Results.Ok(new { status = "ready" });
    }
    catch
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapControllerRoute(
name: "default",
pattern: "{controller}/{action=Index}/{id?}");

app.Run();
