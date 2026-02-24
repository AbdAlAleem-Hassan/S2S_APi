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
        
        // S2S Brand Colors
        private const string PrimaryColor = "#E85D3F";    // Orange/Coral
        private const string SecondaryColor = "#1E3A5F";  // Dark Blue
        private const string LightBg = "#F8F9FA";
        private const string TextColor = "#333333";
        private const string MutedText = "#6C757D";

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
                From = new MailAddress(email!, "S2S App"),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            mailMessage.To.Add(to);

            await client.SendMailAsync(mailMessage);
        }

        private string GetEmailTemplate(string content)
        {
            return $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='margin: 0; padding: 0; font-family: Arial, Helvetica, sans-serif; background-color: {LightBg};'>
    <table role='presentation' width='100%' cellspacing='0' cellpadding='0' style='background-color: {LightBg}; padding: 40px 20px;'>
        <tr>
            <td align='center'>
                <table role='presentation' width='100%' cellspacing='0' cellpadding='0' style='max-width: 500px; background-color: #ffffff; border-radius: 16px; box-shadow: 0 4px 20px rgba(0,0,0,0.1); overflow: hidden;'>
                    
                    <!-- Header with Logo -->
                    <tr>
                        <td style='background: linear-gradient(135deg, {SecondaryColor} 0%, #2D4A6F 100%); padding: 30px; text-align: center;'>
                            <h1 style='margin: 0; color: #ffffff; font-size: 32px; font-weight: bold;'>
                                <span style='color: {PrimaryColor};'>S</span>2<span style='color: {PrimaryColor};'>S</span>
                            </h1>
                            <p style='margin: 8px 0 0 0; color: rgba(255,255,255,0.8); font-size: 14px;'>Sign to Speech</p>
                        </td>
                    </tr>
                    
                    <!-- Content -->
                    <tr>
                        <td style='padding: 40px 30px;'>
                            {content}
                        </td>
                    </tr>
                    
                    <!-- Footer -->
                    <tr>
                        <td style='background-color: {LightBg}; padding: 20px 30px; text-align: center; border-top: 1px solid #eee;'>
                            <p style='margin: 0; color: {MutedText}; font-size: 12px;'>
                                © 2026 S2S App. All rights reserved.
                            </p>
                            <p style='margin: 8px 0 0 0; color: {MutedText}; font-size: 11px;'>
                                This is an automated message. Please do not reply.
                            </p>
                        </td>
                    </tr>
                    
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
        }

        public async Task SendOtpEmailAsync(string to, string otp)
        {
            string subject = "Verify Your Email - S2S";
            
            string content = $@"
                <h2 style='margin: 0 0 20px 0; color: {SecondaryColor}; font-size: 24px; text-align: center;'>
                    Welcome to S2S! 👋
                </h2>
                <p style='margin: 0 0 25px 0; color: {TextColor}; font-size: 16px; line-height: 1.6; text-align: center;'>
                    Thank you for signing up! Please use the verification code below to activate your account:
                </p>
                <div style='background: linear-gradient(135deg, {PrimaryColor} 0%, #FF7B5C 100%); padding: 25px; border-radius: 12px; text-align: center; margin: 0 0 25px 0;'>
                    <span style='font-size: 36px; font-weight: bold; letter-spacing: 8px; color: #ffffff;'>{otp}</span>
                </div>
                <p style='margin: 0 0 10px 0; color: {MutedText}; font-size: 14px; text-align: center;'>
                    ⏰ This code will expire in <strong>10 minutes</strong>.
                </p>
                <p style='margin: 0; color: {MutedText}; font-size: 13px; text-align: center;'>
                    If you didn't create an account, you can safely ignore this email.
                </p>";

            string body = GetEmailTemplate(content);
            await SendEmailAsync(to, subject, body);
        }

        public async Task SendForgotPasswordEmailAsync(string to, string resetLink)
        {
            string subject = "Reset Your Password - S2S";
            
            string content = $@"
                <h2 style='margin: 0 0 20px 0; color: {SecondaryColor}; font-size: 24px; text-align: center;'>
                    Password Reset Request 🔐
                </h2>
                <p style='margin: 0 0 25px 0; color: {TextColor}; font-size: 16px; line-height: 1.6; text-align: center;'>
                    We received a request to reset your password. Click the button below to create a new one:
                </p>
                <div style='text-align: center; margin: 0 0 30px 0;'>
                    <a href='{resetLink}' style='display: inline-block; background: linear-gradient(135deg, {PrimaryColor} 0%, #FF7B5C 100%); color: #ffffff; padding: 16px 40px; text-decoration: none; border-radius: 50px; font-weight: bold; font-size: 16px; box-shadow: 0 4px 15px rgba(232, 93, 63, 0.4);'>
                        Reset Password
                    </a>
                </div>
                <p style='margin: 0 0 15px 0; color: {MutedText}; font-size: 14px; text-align: center;'>
                    ⏰ This link will expire in <strong>30 minutes</strong>.
                </p>
                <div style='background-color: {LightBg}; padding: 15px; border-radius: 8px; margin-top: 20px;'>
                    <p style='margin: 0 0 8px 0; color: {MutedText}; font-size: 12px;'>
                        If the button doesn't work, copy and paste this link:
                    </p>
                    <p style='margin: 0; color: {SecondaryColor}; font-size: 12px; word-break: break-all;'>
                        {resetLink}
                    </p>
                </div>
                <p style='margin: 25px 0 0 0; color: {MutedText}; font-size: 13px; text-align: center;'>
                    If you didn't request this, you can safely ignore this email.
                </p>";

            string body = GetEmailTemplate(content);
            await SendEmailAsync(to, subject, body);
        }
        public async Task SendPasswordChangedEmailAsync(string to, string displayName)
        {
            string subject = "Your Password Has Been Changed - S2S";
            var changedAt = DateTime.UtcNow.ToString("MMMM dd, yyyy 'at' HH:mm 'UTC'");

            string content = $@"
                <h2 style='margin: 0 0 20px 0; color: {SecondaryColor}; font-size: 24px; text-align: center;'>
                    Password Changed Successfully 🔒
                </h2>
                <p style='margin: 0 0 25px 0; color: {TextColor}; font-size: 16px; line-height: 1.6; text-align: center;'>
                    Hi <strong>{displayName}</strong>, your S2S account password was recently changed.
                </p>

                <div style='background: linear-gradient(135deg, {SecondaryColor} 0%, #2D4A6F 100%); padding: 20px; border-radius: 12px; text-align: center; margin: 0 0 25px 0;'>
                    <p style='margin: 0; color: rgba(255,255,255,0.8); font-size: 13px;'>Changed on</p>
                    <p style='margin: 6px 0 0 0; color: #ffffff; font-size: 16px; font-weight: bold;'>⏱ {changedAt}</p>
                </div>

                <div style='background-color: #FFF3F0; border-left: 4px solid {PrimaryColor}; padding: 16px 20px; border-radius: 8px; margin: 0 0 25px 0;'>
                    <p style='margin: 0 0 8px 0; color: {PrimaryColor}; font-weight: bold; font-size: 14px;'>⚠️ Didn't change your password?</p>
                    <p style='margin: 0; color: {TextColor}; font-size: 14px; line-height: 1.6;'>
                        If you did not make this change, your account may be compromised. 
                        Please reset your password immediately and contact our support team.
                    </p>
                </div>

                <p style='margin: 0; color: {MutedText}; font-size: 13px; text-align: center;'>
                    All active sessions have been signed out for your security.
                </p>";

            string body = GetEmailTemplate(content);
            await SendEmailAsync(to, subject, body);
        }
    }
}
