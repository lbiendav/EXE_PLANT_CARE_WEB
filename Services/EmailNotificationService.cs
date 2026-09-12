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
        !string.IsNullOrWhiteSpace(_configuration["Smtp:FromAddress"]);

    public async Task<bool> SendCareReminder(
        string recipient,
        string subject,
        string message,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(recipient))
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
                Body = $"{message}\n\nMở HomePlant để xem và ghi lại hoạt động chăm sóc: {publicBaseUrl}/Notifications",
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

            await smtp.SendMailAsync(mail).WaitAsync(cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Could not send a care reminder email.");
            return false;
        }
    }
}
