using PayOS;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;

namespace HomePlant.Services;

public sealed record ProviderCheckoutRequest(
    string OrderId,
    long OrderCode,
    long AmountVnd,
    string Description,
    DateTimeOffset ExpiresAt,
    string ReturnUrl,
    string CancelUrl);

public sealed record ProviderCheckout(
    long OrderCode,
    long AmountVnd,
    string PaymentLinkId,
    string CheckoutUrl,
    string QrCode,
    string Bin,
    string AccountNumber,
    string AccountName,
    string Description,
    string Status);

public sealed record VerifiedPayment(
    long OrderCode,
    long Amount,
    string Currency,
    string PaymentLinkId,
    string Reference,
    string Description,
    string AccountNumber,
    string TransactionDateTime,
    string ProviderCode);

public sealed record ProviderPaymentStatus(
    long OrderCode,
    long Amount,
    long AmountPaid,
    string PaymentLinkId,
    string Status,
    VerifiedPayment? Payment);

public interface IPaymentProvider
{
    Task<ProviderCheckout> CreateCheckout(ProviderCheckoutRequest request, CancellationToken cancellationToken = default);
    Task CancelCheckout(long orderCode, string reason, CancellationToken cancellationToken = default);
    Task<ProviderPaymentStatus> GetCheckout(long orderCode, CancellationToken cancellationToken = default);
    Task<VerifiedPayment> VerifyWebhook(Webhook webhook);
}

public sealed class PayOsPaymentProvider(IConfiguration configuration) : IPaymentProvider
{
    public async Task<ProviderCheckout> CreateCheckout(ProviderCheckoutRequest request, CancellationToken cancellationToken = default)
    {
        var client = CreateClient();
        var response = await client.PaymentRequests.CreateAsync(new CreatePaymentLinkRequest
        {
            OrderCode = request.OrderCode,
            Amount = request.AmountVnd,
            Description = request.Description,
            ReturnUrl = request.ReturnUrl,
            CancelUrl = request.CancelUrl,
            ExpiredAt = request.ExpiresAt.ToUnixTimeSeconds()
        });

        return new ProviderCheckout(
            response.OrderCode,
            response.Amount,
            response.PaymentLinkId,
            response.CheckoutUrl,
            response.QrCode,
            response.Bin,
            response.AccountNumber,
            response.AccountName,
            response.Description,
            response.Status.ToString());
    }

    public async Task CancelCheckout(long orderCode, string reason, CancellationToken cancellationToken = default)
    {
        await CreateClient().PaymentRequests.CancelAsync(orderCode, reason);
    }

    public async Task<ProviderPaymentStatus> GetCheckout(long orderCode, CancellationToken cancellationToken = default)
    {
        var response = await CreateClient().PaymentRequests.GetAsync(orderCode);
        var transaction = response.Transactions?.FirstOrDefault();
        var payment = transaction == null ? null : new VerifiedPayment(
            response.OrderCode,
            transaction.Amount,
            "VND",
            response.Id,
            transaction.Reference,
            transaction.Description,
            transaction.CounterAccountNumber ?? "",
            transaction.TransactionDateTime,
            "00");
        return new ProviderPaymentStatus(response.OrderCode, response.Amount, response.AmountPaid,
            response.Id, response.Status.ToString(), payment);
    }

    public async Task<VerifiedPayment> VerifyWebhook(Webhook webhook)
    {
        var data = await CreateClient().Webhooks.VerifyAsync(webhook);
        return new VerifiedPayment(
            data.OrderCode,
            data.Amount,
            data.Currency,
            data.PaymentLinkId,
            data.Reference,
            data.Description,
            data.AccountNumber,
            data.TransactionDateTime,
            data.Code);
    }

    private PayOSClient CreateClient()
    {
        var clientId = configuration["Payments:PayOS:ClientId"]?.Trim();
        var apiKey = configuration["Payments:PayOS:ApiKey"]?.Trim();
        var checksumKey = configuration["Payments:PayOS:ChecksumKey"]?.Trim();
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(checksumKey))
            throw new InvalidOperationException("payOS credentials are not configured.");
        return new PayOSClient(clientId, apiKey, checksumKey);
    }
}
