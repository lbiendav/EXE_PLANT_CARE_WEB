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
        VerifiedPayment verified;
        try
        {
            verified = await provider.VerifyWebhook(webhook);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Rejected invalid payOS webhook.");
            return BadRequest(new { success = false, code = "invalid_webhook" });
        }

        try
        {
            var result = await payments.Ingest(verified);
            return Ok(new { success = true, result.Status });
        }
        catch (Exception ex)
        {
            // A verified callback must receive a retryable status when persistence
            // is temporarily unavailable. Returning 400 here would lose payment.
            logger.LogError(ex, "Could not persist verified payOS webhook.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { success = false, code = "temporary_failure" });
        }
    }
}
