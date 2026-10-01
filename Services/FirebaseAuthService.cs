using FirebaseAdmin.Auth;
using Google.Cloud.Firestore;
using HomePlant.Models;
using HomePlant.ViewModels;
using System.Net.Http.Json;
using System.Text.Json;

namespace HomePlant.Services;

public class FirebaseAuthService
{
    private readonly FirestoreDb _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<FirebaseAuthService> _logger;
    private readonly string _apiKey;
    private readonly string? _publicBaseUrl;

    public FirebaseAuthService(
        FirestoreDb db,
        IHttpClientFactory httpClientFactory,
        IHttpContextAccessor httpContextAccessor,
        ILogger<FirebaseAuthService> logger,
        IConfiguration configuration)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _apiKey = configuration["Firebase:ApiKey"]
            ?? throw new InvalidOperationException("Configure Firebase__ApiKey before using authentication.");
        var configuredUrl = configuration["App:PublicBaseUrl"];
        if (string.IsNullOrWhiteSpace(configuredUrl))
            configuredUrl = configuration["RENDER_EXTERNAL_URL"];

        if (!string.IsNullOrWhiteSpace(configuredUrl))
        {
            if (!Uri.TryCreate(configuredUrl, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
                || !string.IsNullOrEmpty(uri.UserInfo)
                || !string.IsNullOrEmpty(uri.Query)
                || !string.IsNullOrEmpty(uri.Fragment))
                throw new InvalidOperationException("App__PublicBaseUrl must be an absolute HTTP(S) URL without credentials, query or fragment.");

            _publicBaseUrl = uri.AbsoluteUri.TrimEnd('/');
        }
    }

    private string BuildContinueUrl(string path)
    {
        if (_publicBaseUrl != null)
            return _publicBaseUrl + path;

        var request = _httpContextAccessor.HttpContext?.Request
            ?? throw new InvalidOperationException("An HTTP request or App__PublicBaseUrl is required.");
        return $"{request.Scheme}://{request.Host}{request.PathBase}{path}";
    }

    public async Task<string> Register(
        RegisterVM model)
    {
        // Validate before creating an Auth user, not after leaving a partial account.
        RegistrationEmailClient.ValidateApiKey(_apiKey);
        var userArgs = new UserRecordArgs()
        {
            Email = model.Email,
            Password = model.Password,
            DisplayName = model.FullName
        };

        UserRecord firebaseUser;
        try
        {
            firebaseUser = await FirebaseAuth.DefaultInstance.CreateUserAsync(userArgs);
        }
        catch (FirebaseAuthException ex) when (ex.AuthErrorCode == AuthErrorCode.EmailAlreadyExists)
        {
            firebaseUser = await FirebaseAuth.DefaultInstance.GetUserByEmailAsync(model.Email);
            if (firebaseUser.EmailVerified || firebaseUser.Disabled)
                throw new RegistrationException("Không thể tiếp tục đăng ký. Hãy đăng nhập hoặc sử dụng chức năng Quên mật khẩu.");

            // Do not overwrite the existing user's password, name, claims or profile.
            // SendVerificationEmail checks the original password and matching UID first.
            await SendVerificationEmail(model.Email, model.Password, firebaseUser.Uid);
            return firebaseUser.Uid;
        }

        await FirebaseAuth.DefaultInstance.SetCustomUserClaimsAsync(
            firebaseUser.Uid,
            new Dictionary<string, object>
            {
                { "pendingPhone", model.Phone ?? "" }
            });

        await SendVerificationEmail(
            model.Email,
            model.Password,
            firebaseUser.Uid);

        return firebaseUser.Uid;
    }

    private async Task SendVerificationEmail(
        string email,
        string password,
        string uid)
    {
        var continueUrl = BuildContinueUrl($"/Account/VerifyEmail?uid={Uri.EscapeDataString(uid)}");
        using var http = _httpClientFactory.CreateClient();
        await RegistrationEmailClient.SendAsync(http, _apiKey, email, password, uid, continueUrl);
    }

    public async Task<bool> CompleteVerification(
        string uid)
    {
        var firebaseUser = await FirebaseAuth.DefaultInstance.GetUserAsync(uid);

        if (!firebaseUser.EmailVerified)
            return false;

        var existing = await GetUser(uid);

        if (existing == null)
        {
            var phone = "";

            if (firebaseUser.CustomClaims != null &&
                firebaseUser.CustomClaims.TryGetValue("pendingPhone", out var pendingPhone))
            {
                phone = pendingPhone?.ToString() ?? "";
            }

            var user = new UserModel
            {
                Uid = uid,
                FullName = firebaseUser.DisplayName,
                Email = firebaseUser.Email,
                Phone = phone,
                Role = "user",
                IsLocked = false,
                CreatedAt = DateTime.UtcNow
            };

            await _db.Collection("users")
                .Document(uid)
                .SetAsync(user);
        }

        return true;
    }

    public async Task<SignInResult> SignInWithPassword(
        string email,
        string password)
    {
        UserRecord authUser;
        try
        {
            authUser = await FirebaseAuth.DefaultInstance.GetUserByEmailAsync(email);
        }
        catch (FirebaseAuthException ex) when (ex.AuthErrorCode == AuthErrorCode.UserNotFound)
        {
            return new SignInResult { Success = false, AccountNotFound = true };
        }

        var http = _httpClientFactory.CreateClient();

        var response = await http.PostAsJsonAsync(
            $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={_apiKey}",
            new
            {
                email,
                password,
                returnSecureToken = true
            });

        if (!response.IsSuccessStatusCode)
        {
            var errorCode = await ReadFirebaseErrorCode(response);
            _logger.LogWarning(
                "Firebase password sign-in failed with HTTP {StatusCode} and code {ErrorCode}.",
                (int)response.StatusCode,
                errorCode);
            return new SignInResult { Success = false };
        }

        var result = await response.Content
            .ReadFromJsonAsync<SignInWithPasswordResponse>();

        if (result?.LocalId == null || !string.Equals(result.LocalId, authUser.Uid, StringComparison.Ordinal))
            return new SignInResult { Success = false };

        // Check only after the password succeeds so the response does not
        // disclose whether a registered address is awaiting verification.
        // A Firestore profile must not let an Auth account whose email later
        // became unverified bypass the verification gate.
        if (!authUser.EmailVerified)
            return new SignInResult { Success = false, EmailNotVerified = true };

        var user = await GetUser(result.LocalId);

        if (user == null)
            return new SignInResult { Success = false, EmailNotVerified = true };

        if (user.IsLocked)
            return new SignInResult { Success = false, IsLocked = true, User = user };

        return new SignInResult { Success = true, User = user };
    }

    private static async Task<string> ReadFirebaseErrorCode(HttpResponseMessage response)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var document = await JsonDocument.ParseAsync(stream);
            var message = document.RootElement
                .GetProperty("error")
                .GetProperty("message")
                .GetString();
            if (string.IsNullOrWhiteSpace(message))
                return "unknown";

            var separator = message.IndexOfAny([' ', ':']);
            return separator > 0 ? message[..separator] : message;
        }
        catch (JsonException)
        {
            return "unparseable";
        }
        catch (InvalidOperationException)
        {
            return "unparseable";
        }
    }

    public async Task<bool> SendPasswordResetEmail(
        string email)
    {
        var http = _httpClientFactory.CreateClient();

        var continueUrl = BuildContinueUrl("/Account/Login");

        var response = await http.PostAsJsonAsync(
            $"https://identitytoolkit.googleapis.com/v1/accounts:sendOobCode?key={_apiKey}",
            new
            {
                requestType = "PASSWORD_RESET",
                email,
                continueUrl,
                canHandleCodeInApp = false
            });

        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ChangePassword(
        string uid,
        string email,
        string currentPassword,
        string newPassword)
    {
        var verify = await SignInWithPassword(email, currentPassword);

        if (!verify.Success)
            return false;

        await FirebaseAuth.DefaultInstance.UpdateUserAsync(
            new UserRecordArgs
            {
                Uid = uid,
                Password = newPassword
            });

        return true;
    }

    public async Task<UserModel?> GetUser(
        string uid)
    {
        var doc =
            await _db.Collection("users")
            .Document(uid)
            .GetSnapshotAsync();

        if (!doc.Exists)
            return null;

        return doc.ConvertTo<UserModel>();
    }

    public async Task<UserSessionState> GetUserSessionState(string uid)
    {
        UserRecord authUser;
        try
        {
            authUser = await FirebaseAuth.DefaultInstance.GetUserAsync(uid);
        }
        catch (FirebaseAuthException ex) when (ex.AuthErrorCode == AuthErrorCode.UserNotFound)
        {
            return new UserSessionState(await GetUser(uid), true, false, null);
        }

        return new UserSessionState(
            await GetUser(uid),
            false,
            authUser.Disabled,
            authUser.TokensValidAfterTimestamp);
    }

    private class SignInWithPasswordResponse
    {
        public string? LocalId { get; set; }

        public string? IdToken { get; set; }
    }
}

public sealed record UserSessionState(
    UserModel? User,
    bool AuthUserMissing,
    bool AuthDisabled,
    DateTime? TokensValidAfter);

public class SignInResult
{
    public bool Success { get; set; }

    public bool IsLocked { get; set; }

    public bool EmailNotVerified { get; set; }

    public bool AccountNotFound { get; set; }

    public UserModel? User { get; set; }
}
