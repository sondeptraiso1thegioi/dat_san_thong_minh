using System.Net;
using System.Net.Mail;
using NhaXe.Modules.TaiKhoan.Application;

namespace NhaXe.Modules.TaiKhoan.Infrastructure;

public record SmtpOptions(string Host, int Port, string From, bool EnableSsl = false, string? User = null, string? Password = null);

public sealed class GuiMaKhoiPhucSmtp(SmtpOptions options) : IGuiMaKhoiPhuc
{
    public async Task GuiAsync(string email, string ma, CancellationToken ct)
    {
        using var smtp = new SmtpClient(options.Host, options.Port) { EnableSsl = options.EnableSsl };
        if (!string.IsNullOrWhiteSpace(options.User)) smtp.Credentials = new NetworkCredential(options.User, options.Password);
        using var mail = new MailMessage(options.From, email, "Nhà xe – Khôi phục mật khẩu",
            $"Mã khôi phục mật khẩu của bạn:\n{ma}\n\nMã chỉ dùng một lần, có hiệu lực 15 phút. Nếu bạn không yêu cầu, hãy bỏ qua email này.");
        await smtp.SendMailAsync(mail, ct);
    }
}
