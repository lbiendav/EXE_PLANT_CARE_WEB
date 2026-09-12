using System.Net;
using System.Net.Sockets;
using System.Text;
using HomePlant.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

static EmailNotificationService Service(Dictionary<string, string?> values) => new(
    new ConfigurationBuilder().AddInMemoryCollection(values).Build(),
    NullLogger<EmailNotificationService>.Instance);
static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
var settings = new Dictionary<string, string?>
{
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
