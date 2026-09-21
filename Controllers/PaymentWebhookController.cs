using HomePlant.Services;
using Microsoft.AspNetCore.Mvc;
using PayOS.Models.Webhooks;

namespace HomePlant.Controllers;

[ApiController]
public sealed class PaymentWebhookController(
    IPaymentProvider provider,
    LivePaymentService payments,
    ILogger<PaymentWebhookController> logger) : ControllerBase
{
    [HttpPost("/api/payments/payos/webhook")]
    [IgnoreAntiforgeryToken]
    [RequestSizeLimit(65_536)]
    public async Task<IActionResult> PayOs([FromBody] Webhook webhook)
    {
        try
        {
            var verified = await provider.VerifyWebhook(webhook);
            var result = await payments.Ingest(verified);
            return Ok(new { success = true, result.Status });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Rejected or failed to persist payOS webhook.");
            return BadRequest(new { success = false, code = "invalid_webhook" });
        }
    }
}
