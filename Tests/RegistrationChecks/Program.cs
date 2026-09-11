using System.Net;
using System.Text;
using System.Text.Json;
using HomePlant.Services;

var key = "AIza" + new string('a', 35); // Fake key; all HTTP calls are intercepted.
const string callback = "https://homeplant-staging.onrender.com/Account/VerifyEmail?uid=test-uid";
var passed = 0;

async Task Check(string name, Func<Task> action)
{
    await action();
    Console.WriteLine($"PASS {name}");
    passed++;
}

async Task ExpectFailure(Func<Task> action)
{
    try { await action(); }
    catch (RegistrationException ex)
    {
        if (ex.Message.Contains(key) || ex.Message.Contains("sensitive-provider-detail"))
            throw new Exception("Sensitive data leaked in user error");
        return;
    }
    throw new Exception("Expected safe registration failure");
}

HttpResponseMessage Response(HttpStatusCode status, string json) =>
    new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
Task Send(HttpClient client, string? apiKey = null) => RegistrationEmailClient.SendAsync(
    client, apiKey ?? key, "test@example.invalid", "test-password", "test-uid", callback);

await Check("malformed API key fails before HTTP", async () =>
{
    using var client = new HttpClient(new FakeHandler((_, _) => throw new Exception("HTTP must not run")));
    await ExpectFailure(() => Send(client, key + "\nFIREBASE_KEY=private-data"));
});
await Check("wrong password cannot send email", async () =>
{
    var handler = new FakeHandler((_, _) => Task.FromResult(Response(HttpStatusCode.BadRequest, "sensitive-provider-detail")));
    using var client = new HttpClient(handler);
    await ExpectFailure(() => Send(client));
    if (handler.Calls != 1) throw new Exception("Unexpected email request");
});
foreach (var json in new[] { "{}", "null", "not-json", "{\"localId\":\"other-uid\",\"idToken\":\"token\"}" })
{
    await Check("invalid token response cannot send email: " + json, async () =>
    {
        var handler = new FakeHandler((_, _) => Task.FromResult(Response(HttpStatusCode.OK, json)));
        using var client = new HttpClient(handler);
        await ExpectFailure(() => Send(client));
        if (handler.Calls != 1) throw new Exception("Unexpected email request");
    });
}
await Check("email provider failure gives retry message", async () =>
{
    using var client = new HttpClient(new FakeHandler((_, count) => Task.FromResult(count == 1
        ? Response(HttpStatusCode.OK, "{\"localId\":\"test-uid\",\"idToken\":\"test-token\"}")
        : Response(HttpStatusCode.TooManyRequests, "sensitive-provider-detail"))));
    await ExpectFailure(() => Send(client));
});
await Check("network failure gives safe message", async () =>
{
    using var client = new HttpClient(new FakeHandler((_, _) => throw new HttpRequestException("sensitive-provider-detail")));
    await ExpectFailure(() => Send(client));
});
await Check("timeout gives safe message", async () =>
{
    using var client = new HttpClient(new FakeHandler((_, _) => throw new TaskCanceledException("sensitive-provider-detail")));
    await ExpectFailure(() => Send(client));
});
await Check("successful request uses verified token and HTTPS callback", async () =>
{
    var handler = new FakeHandler(async (request, count) =>
    {
        using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
        if (count == 1)
        {
            if (body.RootElement.GetProperty("password").GetString() != "test-password") throw new Exception("Missing password proof");
            return Response(HttpStatusCode.OK, "{\"localId\":\"test-uid\",\"idToken\":\"test-token\"}");
        }
        if (body.RootElement.GetProperty("continueUrl").GetString() != callback ||
            body.RootElement.GetProperty("idToken").GetString() != "test-token" ||
            body.RootElement.GetProperty("requestType").GetString() != "VERIFY_EMAIL") throw new Exception("Wrong email payload");
        return Response(HttpStatusCode.OK, "{}");
    });
    using var client = new HttpClient(handler);
    await Send(client);
    if (handler.Calls != 2) throw new Exception("Expected sign-in and email requests");
});
Console.WriteLine($"{passed} registration checks passed; no real Firebase calls or emails.");

sealed class FakeHandler(Func<HttpRequestMessage, int, Task<HttpResponseMessage>> handle) : HttpMessageHandler
{
    public int Calls { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => handle(request, ++Calls);
}
