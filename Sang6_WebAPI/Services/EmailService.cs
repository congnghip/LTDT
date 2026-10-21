using System.Net;
using System.Net.Mail;
using System.Text;

namespace Sang6_WebAPI.Services
{
    public interface IEmailService
    {
        Task<bool> SendOtpEmailAsync(string toEmail, string otp, string recipientName = "", string purpose = "Đăng nhập");
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> SendOtpEmailAsync(string toEmail, string otp, string recipientName = "", string purpose = "Đăng nhập")
        {
            try
            {
                var smtpServer = _configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com";
                var port = int.Parse(_configuration["EmailSettings:Port"] ?? "587");
                var senderEmail = _configuration["EmailSettings:SenderEmail"] ?? "quangbui.061073@gmail.com";
                var senderName = _configuration["EmailSettings:SenderName"] ?? "Hệ Thống Chấm Công Thông Minh";
                var password = _configuration["EmailSettings:Password"] ?? "fcae gffq nsog ylnj";
                var enableSsl = bool.Parse(_configuration["EmailSettings:EnableSsl"] ?? "true");

                var client = new SmtpClient(smtpServer, port)
                {
                    Credentials = new NetworkCredential(senderEmail, password),
                    EnableSsl = enableSsl
                };

                var displayName = string.IsNullOrWhiteSpace(recipientName) ? "Quý khách" : recipientName;

                var subject = $"[{otp}] Mã xác thực OTP {purpose} - Hệ Thống Chấm Công Thông Minh";

                var htmlBody = $@"
<!DOCTYPE html>
<html lang=""vi"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Mã xác thực OTP</title>
</head>
<body style=""font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #f4f6f9; margin: 0; padding: 24px; color: #333;"">
    <div style=""max-width: 580px; margin: 0 auto; background-color: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 12px rgba(0,0,0,0.08); border: 1px solid #e1e8ed;"">
        
        <!-- Header -->
        <div style=""background: linear-gradient(135deg, #1e3c72 0%, #2a5298 100%); padding: 28px 24px; text-align: center; color: #ffffff;"">
            <h1 style=""margin: 0; font-size: 22px; font-weight: 700; letter-spacing: 0.5px;"">HỆ THỐNG CHẤM CÔNG THÔNG MINH</h1>
            <p style=""margin: 6px 0 0; font-size: 13px; opacity: 0.9;"">Bùi Nguyễn Công Nghiệp - Xác thực bảo mật OTP</p>
        </div>

        <!-- Body -->
        <div style=""padding: 32px 28px;"">
            <p style=""font-size: 15px; margin-top: 0;"">Xin chào <strong>{displayName}</strong>,</p>
            <p style=""font-size: 14px; line-height: 1.6; color: #555;"">
                Hệ thống nhận được yêu cầu <strong>{purpose.ToLower()}</strong> từ địa chỉ email của bạn. Để hoàn tất, vui lòng sử dụng mã xác thực OTP 6 số bên dưới:
            </p>

            <!-- OTP Box -->
            <div style=""margin: 28px 0; text-align: center;"">
                <div style=""display: inline-block; background: #e8f4fd; border: 2px dashed #1e88e5; border-radius: 12px; padding: 18px 36px;"">
                    <span style=""font-size: 34px; font-weight: 800; letter-spacing: 8px; color: #0d47a1; font-family: 'Courier New', Courier, monospace;"">{otp}</span>
                </div>
                <p style=""margin: 10px 0 0; font-size: 13px; color: #e53935; font-weight: 600;"">
                    ⏱️ Mã OTP này có hiệu lực trong vòng 5 phút
                </p>
            </div>

            <!-- Security Warning -->
            <div style=""background-color: #fff8e1; border-left: 4px solid #ffb300; padding: 12px 16px; border-radius: 4px; margin-bottom: 24px;"">
                <p style=""margin: 0; font-size: 12.5px; color: #856404; line-height: 1.5;"">
                    🔒 <strong>Lưu ý bảo mật:</strong> Tuyệt đối không chia sẻ mã này với bất kỳ ai, kể cả nhân viên quản trị. Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email.
                </p>
            </div>

            <p style=""font-size: 13px; color: #777; line-height: 1.5; margin-bottom: 0;"">
                Trân trọng,<br>
                <strong>Ban Quản Trị Hệ Thống Chấm Công Thông Minh</strong>
            </p>
        </div>

        <!-- Footer -->
        <div style=""background-color: #f8fafc; padding: 16px; text-align: center; border-top: 1px solid #eef2f6; font-size: 12px; color: #94a3b8;"">
            Email tự động từ hệ thống chấm công • Vui lòng không trả lời thư này
        </div>
    </div>
</body>
</html>";

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(senderEmail, senderName),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true,
                    BodyEncoding = Encoding.UTF8,
                    SubjectEncoding = Encoding.UTF8
                };
                mailMessage.To.Add(toEmail);

                await client.SendMailAsync(mailMessage);
                _logger.LogInformation($"[EmailService] OTP email successfully sent to {toEmail}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[EmailService] Failed to send OTP email to {toEmail}");
                return false;
            }
        }
    }
}
