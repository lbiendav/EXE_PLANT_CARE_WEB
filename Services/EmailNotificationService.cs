using System.Net;
using System.Net.Mail;

namespace HomePlant.Services;

public sealed class EmailNotificationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailNotificationService> _logger;

    public EmailNotificationService(
        IConfiguration configuration,
        ILogger<EmailNotificationService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_configuration["Smtp:Host"]) &&
        MailAddress.TryCreate(_configuration["Smtp:FromAddress"], out _) &&
        (!int.TryParse(_configuration["Smtp:Port"] ?? "587", out var port) ? false : port is > 0 and <= 65535) &&
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

        try
        {
            var host = _configuration["Smtp:Host"]!;
            var port = int.TryParse(_configuration["Smtp:Port"], out var configuredPort)
                ? configuredPort
                : 587;
            var username = _configuration["Smtp:Username"];
            var password = _configuration["Smtp:Password"];
            var publicBaseUrl = (_configuration["App:PublicBaseUrl"]
                ?? _configuration["RENDER_EXTERNAL_URL"]
                ?? "").TrimEnd('/');

            using var mail = new MailMessage
            {
                From = new MailAddress(
                    _configuration["Smtp:FromAddress"]!,
                    _configuration["Smtp:FromName"] ?? "HomePlant"),
                Subject = subject,
                Body = $"{message}\n\n" +
                    (Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out var baseUri) &&
                     (baseUri.Scheme == "https" || baseUri.Scheme == "http")
                        ? $"Mở HomePlant để xem và ghi lại hoạt động chăm sóc: {publicBaseUrl}/Notifications"
                        : "Mở HomePlant, vào mục Nhắc việc để xem lịch chăm sóc.") +
                    "\n\nBạn có thể tắt email bất cứ lúc nào tại mục Nhắc việc trong HomePlant.",
                BodyEncoding = System.Text.Encoding.UTF8,
                SubjectEncoding = System.Text.Encoding.UTF8,
                IsBodyHtml = false
            };
            mail.To.Add(recipient);

            using var smtp = new SmtpClient(host, port)
            {
                EnableSsl = !bool.TryParse(_configuration["Smtp:EnableSsl"], out var ssl) || ssl,
                UseDefaultCredentials = false
            };
            if (!string.IsNullOrWhiteSpace(username))
                smtp.Credentials = new NetworkCredential(username, password);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            try
            {
                await smtp.SendMailAsync(mail, timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Care reminder email delivery timed out.");
                return false;
            }
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Could not send a care reminder email.");
            return false;
        }
    }
}
