using Microsoft.Extensions.Configuration;
using S2S.ServicesAbstraction;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace S2S.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            var smtpSettings = _configuration.GetSection("SmtpSettings");
            var host = smtpSettings["Host"];
            var port = int.Parse(smtpSettings["Port"]!);
            var email = smtpSettings["Email"];
            var password = smtpSettings["Password"];

            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(email, password),
                EnableSsl = true
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(email!),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            mailMessage.To.Add(to);

            await client.SendMailAsync(mailMessage);
        }

        public async Task SendOtpEmailAsync(string to, string otp)
        {
            string subject = "تفعيل حسابك - مشروع S2S";
            string body = $@"
                <div dir='rtl' style='font-family: Arial, sans-serif; border: 1px solid #ddd; padding: 20px; border-radius: 10px;'>
                    <h2 style='color: #2c3e50;'>أهلاً بك في تطبيق S2S</h2>
                    <p>كود التفعيل الخاص بك هو:</p>
                    <div style='background: #f4f4f4; padding: 10px; text-align: center; font-size: 24px; font-weight: bold; letter-spacing: 5px; color: #e74c3c;'>
                        {otp}
                    </div>
                    <p>هذا الكود صالح لمدة 10 دقائق فقط.</p>
                    <p>إذا لم تطلب هذا الكود، يرجى تجاهل هذه الرسالة.</p>
                </div>";

            await SendEmailAsync(to, subject, body);
        }
    }
}
