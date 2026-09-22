using System.Net;
using System.Net.Mail;
using System.Net.Http.Json;

namespace HomePlant.Services;

public sealed class EmailNotificationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailNotificationService> _logger;
    private readonly HttpClient _http;

    public EmailNotificationService(
        IConfiguration configuration,
        ILogger<EmailNotificationService> logger,
        HttpClient http)
    {
        _configuration = configuration;
        _logger = logger;
        _http = http;
    }

    private bool UsesBrevo => string.Equals(_configuration["Email:Provider"] ?? "Brevo",
        "Brevo", StringComparison.OrdinalIgnoreCase);

    public bool IsConfigured => UsesBrevo
        ? !string.IsNullOrWhiteSpace(_configuration["Brevo:ApiKey"]) &&
          MailAddress.TryCreate(_configuration["Brevo:FromAddress"], out _)
        : string.Equals(_configuration["Email:Provider"], "Smtp", StringComparison.OrdinalIgnoreCase) &&
          !string.IsNullOrWhiteSpace(_configuration["Smtp:Host"]) &&
          MailAddress.TryCreate(_configuration["Smtp:FromAddress"], out _) &&
          int.TryParse(_configuration["Smtp:Port"] ?? "587", out var port) && port is > 0 and <= 65535 &&
          (string.IsNullOrWhiteSpace(_configuration["Smtp:Username"]) ||
           !string.IsNullOrWhiteSpace(_configuration["Smtp:Password"]));

    public async Task<bool> SendCareReminder(
        string recipient,
        string subject,
        string message,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured || !MailAddress.TryCreate(recipient, out _))
            return false;

        var publicBaseUrl = _configuration["App:PublicBaseUrl"];
        if (string.IsNullOrWhiteSpace(publicBaseUrl))
            publicBaseUrl = _configuration["RENDER_EXTERNAL_URL"];
        publicBaseUrl = (publicBaseUrl ?? "").TrimEnd('/');
        var body = $"{message}\n\n" +
            (Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out var baseUri) &&
             (baseUri.Scheme == "https" || baseUri.Scheme == "http")
                ? $"Mở HomePlant để xem và ghi lại hoạt động chăm sóc: {publicBaseUrl}/Notifications"
                : "Mở HomePlant, vào mục Nhắc việc để xem lịch chăm sóc.") +
            "\n\nBạn có thể tắt email bất cứ lúc nào tại mục Nhắc việc trong HomePlant.";

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            if (UsesBrevo)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
                request.Headers.Add("api-key", _configuration["Brevo:ApiKey"]);
                request.Content = JsonContent.Create(new
                {
                    sender = new { email = _configuration["Brevo:FromAddress"], name = _configuration["Brevo:FromName"] ?? "HomePlant" },
                    to = new[] { new { email = recipient } },
                    subject,
                    textContent = body
                });
                using var response = await _http.SendAsync(request, timeout.Token);
                if (response.StatusCode == HttpStatusCode.Created)
                    return true;

                // Do not log provider response bodies: they may contain recipient data.
                _logger.LogWarning("Brevo rejected care email with HTTP {StatusCode}.", (int)response.StatusCode);
                return false;
            }

            using var mail = new MailMessage
            {
                From = new MailAddress(_configuration["Smtp:FromAddress"]!, _configuration["Smtp:FromName"] ?? "HomePlant"),
                Subject = subject,
                Body = body,
                BodyEncoding = System.Text.Encoding.UTF8,
                SubjectEncoding = System.Text.Encoding.UTF8,
                IsBodyHtml = false
            };
            mail.To.Add(recipient);
            using var smtp = new SmtpClient(_configuration["Smtp:Host"]!, int.Parse(_configuration["Smtp:Port"] ?? "587"))
            {
                EnableSsl = !bool.TryParse(_configuration["Smtp:EnableSsl"], out var ssl) || ssl,
                UseDefaultCredentials = false
            };
            if (!string.IsNullOrWhiteSpace(_configuration["Smtp:Username"]))
                smtp.Credentials = new NetworkCredential(_configuration["Smtp:Username"], _configuration["Smtp:Password"]);
            await smtp.SendMailAsync(mail, timeout.Token);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Care reminder email delivery timed out.");
            return false;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning("Could not send a care reminder email ({ExceptionType}).", exception.GetType().Name);
            return false;
        }
    }

    public Task<bool> SendTransactional(string recipient, string subject, string message, CancellationToken cancellationToken) =>
        SendCareReminder(recipient, subject, message, cancellationToken);
}
