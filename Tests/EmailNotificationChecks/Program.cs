using System.Net;
using System.Net.Sockets;
using System.Text;
using HomePlant.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

static EmailNotificationService Service(Dictionary<string, string?> values) => new(
    new ConfigurationBuilder().AddInMemoryCollection(values).Build(),
    NullLogger<EmailNotificationService>.Instance, new HttpClient());
static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
var settings = new Dictionary<string, string?>
{
    ["Email:Provider"] = "Smtp",
    ["Smtp:Host"] = "127.0.0.1",
    ["Smtp:FromAddress"] = "reminders@example.test",
    ["Smtp:EnableSsl"] = "false"
};
Check(!Service(new()).IsConfigured, "Missing SMTP must be unavailable");
Check(!Service(new(settings) { ["Smtp:Port"] = "invalid" }).IsConfigured, "Invalid port");
Check(!Service(new(settings) { ["Smtp:FromAddress"] = "invalid" }).IsConfigured, "Invalid sender");
Check(!Service(new(settings) { ["Smtp:Username"] = "user" }).IsConfigured, "Missing password");
Check(!await Service(settings).SendCareReminder("invalid", "Test", "Test", default), "Invalid recipient");

using var listener = new TcpListener(IPAddress.Loopback, 0);
listener.Start();
settings["Smtp:Port"] = ((IPEndPoint)listener.LocalEndpoint).Port.ToString();
using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
var received = new StringBuilder();
var server = Task.Run(async () =>
{
    using var client = await listener.AcceptTcpClientAsync(deadline.Token);
    using var stream = client.GetStream();
    using var reader = new StreamReader(stream);
    using var writer = new StreamWriter(stream) { AutoFlush = true, NewLine = "\r\n" };
    await writer.WriteLineAsync("220 localhost SMTP");
    var data = false;
    while (await reader.ReadLineAsync(deadline.Token) is { } line)
    {
        if (data)
        {
            if (line == ".") { data = false; await writer.WriteLineAsync("250 accepted"); }
            else received.AppendLine(line);
        }
        else if (line.StartsWith("DATA")) { data = true; await writer.WriteLineAsync("354 send data"); }
        else if (line.StartsWith("QUIT")) { await writer.WriteLineAsync("221 bye"); break; }
        else await writer.WriteLineAsync("250 OK");
    }
});
Check(await Service(settings).SendCareReminder("owner@example.test", "HomePlant · Tưới nước", "Cây đến lịch tưới.", deadline.Token), "SMTP delivery");
await server;
Check(received.ToString().Contains("owner@example.test"), "Recipient missing");
Check(received.ToString().Contains("utf-8", StringComparison.OrdinalIgnoreCase), "Vietnamese encoding missing");
Check(!received.ToString().Contains(": /Notifications"), "Broken relative link");
listener.Stop();
Check(!await Service(settings).SendCareReminder("owner@example.test", "Test", "Test", deadline.Token), "SMTP failure must return false");
Console.WriteLine("Email checks passed: configuration, recipient validation, SMTP delivery, UTF-8 and connection failure.");

var brevoSettings = new Dictionary<string, string?>
{
    ["Brevo:ApiKey"] = "test-key-not-a-real-secret",
    ["Brevo:FromAddress"] = "reminders@example.test",
    ["App:PublicBaseUrl"] = "",
    ["RENDER_EXTERNAL_URL"] = "https://homeplant.example.test"
};
var handler = new BrevoHandler();
var brevo = new EmailNotificationService(new ConfigurationBuilder().AddInMemoryCollection(brevoSettings).Build(),
    NullLogger<EmailNotificationService>.Instance, new HttpClient(handler));
Check(brevo.IsConfigured, "Brevo should be default");
Check(!Service(new(brevoSettings) { ["Brevo:ApiKey"] = "" }).IsConfigured, "Missing API key");
Check(!Service(new(brevoSettings) { ["Brevo:FromAddress"] = "bad" }).IsConfigured, "Invalid Brevo sender");
Check(!Service(new(brevoSettings) { ["Email:Provider"] = "unknown" }).IsConfigured, "Unknown provider");
Check(await brevo.SendCareReminder("owner@example.test", "Đến lịch tưới", "Cây cần nước", default), "Brevo accepted");
using (var payload = System.Text.Json.JsonDocument.Parse(handler.Body!))
{
    var root = payload.RootElement;
    Check(root.GetProperty("to")[0].GetProperty("email").GetString() == "owner@example.test", "Brevo recipient");
    Check(root.GetProperty("subject").GetString() == "Đến lịch tưới", "Vietnamese subject");
    Check(root.GetProperty("textContent").GetString()!.Contains("https://homeplant.example.test/Notifications"), "Render URL fallback");
}
foreach (var status in new[] { HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests, HttpStatusCode.InternalServerError, HttpStatusCode.Redirect })
{
    handler.Status = status;
    Check(!await brevo.SendCareReminder("owner@example.test", "Test", "Test", default), $"Brevo failure {status}");
}
handler.FailNetwork = true;
Check(!await brevo.SendCareReminder("owner@example.test", "Test", "Test", default), "Network failure");
handler.FailNetwork = false;
handler.Cancel = true;
Check(!await brevo.SendCareReminder("owner@example.test", "Test", "Test", default), "Timeout failure");
using var cancelled = new CancellationTokenSource();
cancelled.Cancel();
try
{
    await brevo.SendCareReminder("owner@example.test", "Test", "Test", cancelled.Token);
    throw new Exception("Caller cancellation must propagate");
}
catch (OperationCanceledException) { }
Console.WriteLine("Brevo checks passed: payload, authentication, validation, provider errors, network errors and cancellation.");

sealed class BrevoHandler : HttpMessageHandler
{
    public HttpStatusCode Status { get; set; } = HttpStatusCode.Created;
    public string? Body { get; private set; }
    public bool FailNetwork { get; set; }
    public bool Cancel { get; set; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (FailNetwork) throw new HttpRequestException("Simulated network failure");
        if (Cancel) throw new OperationCanceledException();
        if (request.RequestUri?.ToString() != "https://api.brevo.com/v3/smtp/email" || request.Method != HttpMethod.Post)
            throw new Exception("Wrong Brevo endpoint");
        if (request.Headers.GetValues("api-key").Single() != "test-key-not-a-real-secret")
            throw new Exception("Missing API key header");
        Body = await request.Content!.ReadAsStringAsync(cancellationToken);
        return new HttpResponseMessage(Status) { Content = new StringContent("{\"messageId\":\"test\"}") };
    }
}
