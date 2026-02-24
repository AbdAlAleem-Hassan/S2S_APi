using System.Threading.Tasks;

namespace S2S.ServicesAbstraction
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body);
        Task SendOtpEmailAsync(string to, string otp);
        Task SendForgotPasswordEmailAsync(string to, string resetLink);
        Task SendPasswordChangedEmailAsync(string to, string displayName);
    }
}
