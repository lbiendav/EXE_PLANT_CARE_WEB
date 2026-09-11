using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HomePlant.Services;

public sealed class RegistrationException : Exception
{
    public RegistrationException(string message) : base(message) { }
}

// No raw provider response, token, password or request URL is included in errors.
public static class RegistrationEmailClient
{
    public const string RetryMessage = "Chưa thể gửi email xác minh. Tài khoản có thể đã được tạo. Vui lòng thử đăng ký lại bằng cùng email và mật khẩu sau ít phút.";

    public static void ValidateApiKey(string apiKey)
    {
        if (!Regex.IsMatch(apiKey, @"\AAIza[0-9A-Za-z_-]{35}\z"))
            throw new RegistrationException("Dịch vụ đăng ký chưa được cấu hình đúng. Vui lòng thử lại sau hoặc liên hệ quản trị viên.");
    }

    public static async Task SendAsync(HttpClient http, string apiKey, string email,
        string password, string uid, string continueUrl)
    {
        ValidateApiKey(apiKey);
        try
        {
            using var signIn = await http.PostAsJsonAsync(
                $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={apiKey}",
                new { email, password, returnSecureToken = true });
            if (!signIn.IsSuccessStatusCode)
                throw new RegistrationException("Không thể tiếp tục đăng ký. Hãy dùng đúng email và mật khẩu đã đăng ký; nếu quên mật khẩu, chọn Quên mật khẩu.");

            var result = await signIn.Content.ReadFromJsonAsync<TokenResponse>();
            // Prove ownership before resending for an existing account.
            if (result?.LocalId != uid || string.IsNullOrWhiteSpace(result.IdToken))
                throw new RegistrationException(RetryMessage);

            using var send = await http.PostAsJsonAsync(
                $"https://identitytoolkit.googleapis.com/v1/accounts:sendOobCode?key={apiKey}",
                new { requestType = "VERIFY_EMAIL", idToken = result.IdToken, continueUrl, canHandleCodeInApp = false });
            if (!send.IsSuccessStatusCode)
                throw new RegistrationException(RetryMessage);
        }
        catch (HttpRequestException) { throw new RegistrationException(RetryMessage); }
        catch (TaskCanceledException) { throw new RegistrationException(RetryMessage); }
        catch (JsonException) { throw new RegistrationException(RetryMessage); }
    }

    private sealed class TokenResponse
    {
        public string? LocalId { get; set; }
        public string? IdToken { get; set; }
    }
}
