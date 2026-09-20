using HomePlant.Models;

namespace HomePlant.Services;

public sealed record BankQrDetails(bool IsConfigured, string? ImageUrl, string Message);

public interface IBankQrService
{
    BankQrDetails Build(SubscriptionOrderModel order);
}

public sealed class VietQrService : IBankQrService
{
    public BankQrDetails Build(SubscriptionOrderModel order)
    {
        if (string.IsNullOrWhiteSpace(order.BankBin) ||
            string.IsNullOrWhiteSpace(order.BankAccountNumber) ||
            string.IsNullOrWhiteSpace(order.BankAccountName))
        {
            return new(false, null, "QR ngân hàng chưa được cấu hình. Bạn vẫn có thể dùng mô phỏng thanh toán.");
        }

        var bin = Uri.EscapeDataString(order.BankBin);
        var account = Uri.EscapeDataString(order.BankAccountNumber);
        var name = Uri.EscapeDataString(order.BankAccountName);
        var content = Uri.EscapeDataString(order.TransferReference);
        var url = $"https://img.vietqr.io/image/{bin}-{account}-compact2.png?amount={order.AmountVnd}&addInfo={content}&accountName={name}";
        return new(true, url, "QR chứa thông tin chuyển khoản của đơn. Bản demo không tự xác nhận giao dịch ngân hàng.");
    }
}
