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
        var bin = order.BankBin?.Trim() ?? "";
        var account = order.BankAccountNumber?.Trim() ?? "";
        var name = order.BankAccountName?.Trim() ?? "";
        var content = order.TransferReference?.Trim() ?? "";
        var valid = bin.Length is >= 6 and <= 8 && bin.All(char.IsDigit) &&
            account.Length is >= 4 and <= 19 && account.All(char.IsDigit) &&
            name.Length is >= 2 and <= 100 &&
            content.Length is >= 1 and <= 50 && content.All(c => char.IsAsciiLetterOrDigit(c) || c == ' ');

        if (!valid)
        {
            return new(false, null, "QR ngân hàng chưa được cấu hình. Bạn vẫn có thể dùng mô phỏng thanh toán.");
        }

        var url = $"https://img.vietqr.io/image/{Uri.EscapeDataString(bin)}-{Uri.EscapeDataString(account)}-compact2.png" +
            $"?amount={order.AmountVnd}&addInfo={Uri.EscapeDataString(content)}&accountName={Uri.EscapeDataString(name)}";
        return new(true, url, "QR chứa thông tin chuyển khoản của đơn. Bản demo không tự xác nhận giao dịch ngân hàng.");
    }
}
