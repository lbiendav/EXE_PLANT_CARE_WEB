using FirebaseAdmin.Auth;
using Google.Cloud.Firestore;
using HomePlant.Models;
using HomePlant.ViewModels;
using System.Net.Http.Json;

namespace HomePlant.Services;

public class FirebaseAuthService
{
    private readonly FirestoreDb _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly string _apiKey;

    public FirebaseAuthService(
        FirestoreDb db,
        IHttpClientFactory httpClientFactory,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _httpContextAccessor = httpContextAccessor;
        _apiKey = configuration["Firebase:ApiKey"];
    }

    public async Task<string> Register(
        RegisterVM model)
    {
        var userArgs = new UserRecordArgs()
        {
            Email = model.Email,
            Password = model.Password,
            DisplayName = model.FullName
        };

        UserRecord firebaseUser =
            await FirebaseAuth.DefaultInstance
            .CreateUserAsync(userArgs);

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
        var http = _httpClientFactory.CreateClient();

        var signInResponse = await http.PostAsJsonAsync(
            $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={_apiKey}",
            new
            {
                email,
                password,
                returnSecureToken = true
            });

        signInResponse.EnsureSuccessStatusCode();

        var signInResult = await signInResponse.Content
            .ReadFromJsonAsync<SignInWithPasswordResponse>();

        var request = _httpContextAccessor.HttpContext!.Request;
        var continueUrl = $"{request.Scheme}://{request.Host}/Account/VerifyEmail?uid={uid}";

        var sendCodeResponse = await http.PostAsJsonAsync(
            $"https://identitytoolkit.googleapis.com/v1/accounts:sendOobCode?key={_apiKey}",
            new
            {
                requestType = "VERIFY_EMAIL",
                idToken = signInResult!.IdToken,
                continueUrl,
                canHandleCodeInApp = false
            });

        sendCodeResponse.EnsureSuccessStatusCode();
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
        try
        {
            await FirebaseAuth.DefaultInstance.GetUserByEmailAsync(email);
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
            return new SignInResult { Success = false };

        var result = await response.Content
            .ReadFromJsonAsync<SignInWithPasswordResponse>();

        if (result?.LocalId == null)
            return new SignInResult { Success = false };

        var user = await GetUser(result.LocalId);

        if (user == null)
            return new SignInResult { Success = false, EmailNotVerified = true };

        if (user.IsLocked)
            return new SignInResult { Success = false, IsLocked = true, User = user };

        return new SignInResult { Success = true, User = user };
    }

    public async Task<bool> SendPasswordResetEmail(
        string email)
    {
        var http = _httpClientFactory.CreateClient();

        var request = _httpContextAccessor.HttpContext!.Request;
        var continueUrl = $"{request.Scheme}://{request.Host}/Account/Login";

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

    private class SignInWithPasswordResponse
    {
        public string? LocalId { get; set; }

        public string? IdToken { get; set; }
    }
}

public class SignInResult
{
    public bool Success { get; set; }

    public bool IsLocked { get; set; }

    public bool EmailNotVerified { get; set; }

    public bool AccountNotFound { get; set; }

    public UserModel? User { get; set; }
}
