using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using HomePlant.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.AddControllersWithViews()
    .AddViewLocalization(Microsoft.AspNetCore.Mvc.Razor.LanguageViewLocationExpanderFormat.Suffix)
    .AddDataAnnotationsLocalization();
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(12);
});

builder.Services.AddHttpContextAccessor();

builder.Services.AddHttpClient();

var credentialPath = Path.Combine(
builder.Environment.ContentRootPath,
"Firebase",
"firebase-key.json");

var credential =
GoogleCredential.FromFile(credentialPath);

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
        ProjectId = builder.Configuration["Firebase:ProjectId"],
        Credential = credential
    }.Build();
});

builder.Services.AddScoped<FirestoreService>();
builder.Services.AddScoped<FirebaseAuthService>();
builder.Services.AddScoped<CareLogService>();
builder.Services.AddScoped<ArticleService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<PlantSampleService>();
builder.Services.AddScoped<UserPlantService>();
builder.Services.AddScoped<PlantTemplateService>();
builder.Services.AddScoped<ImgBbService>();
builder.Services.AddScoped<CommunityPostService>();
builder.Services.AddScoped<QaThreadService>();
builder.Services.AddScoped<AiDiagnosisService>();

var app = builder.Build();

var supportedCultures = new[] { "en", "vi", "fr", "ja" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("en")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

app.UseRequestLocalization(localizationOptions);
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.MapControllerRoute(
name: "default",
pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
