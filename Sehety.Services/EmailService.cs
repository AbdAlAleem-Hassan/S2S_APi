using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using MimeKit.Utils;
using S2S.ServicesAbstraction;

namespace S2S.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        private const string PrimaryColor = "#E85D3F";
        private const string SecondaryColor = "#1E3A5F";
        private const string LightBg = "#F8F9FA";
        private const string TextColor = "#333333";
        private const string MutedText = "#6C757D";

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            var smtpSettings = _configuration.GetSection("SmtpSettings");
            var host = smtpSettings["Host"]!;
            var port = int.Parse(smtpSettings["Port"]!);
            var email = smtpSettings["Email"]!;
            var password = smtpSettings["Password"]!;

            _logger.LogInformation("Sending email to {Recipient}, subject: {Subject}", to, subject);

            int maxRetries = 3;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    using var client = new SmtpClient
                    {
                        Timeout = 60_000
                    };

                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                    var token = cts.Token;

                    await client.ConnectAsync(host, 465, SecureSocketOptions.SslOnConnect, token);
                    await client.AuthenticateAsync(email, password, token);

                    var message = new MimeMessage();
                    message.MessageId = MimeUtils.GenerateMessageId();
                    message.From.Add(new MailboxAddress("S2S App", email));
                    message.To.Add(MailboxAddress.Parse(to));
                    message.Subject = subject;

                    var builder = new BodyBuilder
                    {
                        HtmlBody = body,
                        TextBody = $"S2S App - {subject}\n\nPlease open this email in an HTML-compatible viewer.\n\nOr visit https://s2sai.online"
                    };
                    message.Body = builder.ToMessageBody();

                    await client.SendAsync(message, token);
                    await client.DisconnectAsync(true, token);

                    _logger.LogInformation("Email sent successfully to {Recipient}", to);
                    return;
                }
                catch (AuthenticationException ex)
                {
                    _logger.LogError(ex, "SMTP authentication failed for {Recipient}", to);
                    throw;
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    _logger.LogWarning(ex,
                        "Attempt {Attempt}/{MaxRetries} failed for {Recipient}",
                        attempt, maxRetries, to);
                    await Task.Delay(TimeSpan.FromSeconds(2));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "All {MaxRetries} attempts failed for {Recipient}",
                        maxRetries, to);
                    throw;
                }
            }
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

        public async Task SendEmailChangeOtpAsync(string to, string otp, string displayName)
        {
            string subject = "Verify Your New Email - S2S";

            string content = $@"
                <h2 style='margin: 0 0 20px 0; color: {SecondaryColor}; font-size: 24px; text-align: center;'>
                    Verify Your New Email ✉️
                </h2>
                <p style='margin: 0 0 25px 0; color: {TextColor}; font-size: 16px; line-height: 1.6; text-align: center;'>
                    Hi <strong>{displayName}</strong>, you requested to change your S2S account email to this address. Please use the verification code below to confirm:
                </p>
                <div style='background: linear-gradient(135deg, {PrimaryColor} 0%, #FF7B5C 100%); padding: 25px; border-radius: 12px; text-align: center; margin: 0 0 25px 0;'>
                    <span style='font-size: 36px; font-weight: bold; letter-spacing: 8px; color: #ffffff;'>{otp}</span>
                </div>
                <p style='margin: 0 0 10px 0; color: {MutedText}; font-size: 14px; text-align: center;'>
                    ⏰ This code will expire in <strong>10 minutes</strong>.
                </p>
                <p style='margin: 0 0 10px 0; color: {MutedText}; font-size: 13px; text-align: center;'>
                    You have a maximum of <strong>3 attempts</strong> before the code is invalidated.
                </p>
                <p style='margin: 0; color: {MutedText}; font-size: 13px; text-align: center;'>
                    If you didn't request this change, you can safely ignore this email.
                </p>";

            string body = GetEmailTemplate(content);
            await SendEmailAsync(to, subject, body);
        }

        public async Task SendEmailChangedNotificationAsync(string oldEmail, string newEmail, string displayName)
        {
            var changedAt = DateTime.UtcNow.ToString("MMMM dd, yyyy 'at' HH:mm 'UTC'");

            string oldEmailSubject = "Your Email Address Has Been Changed - S2S";
            string oldEmailContent = $@"
                <h2 style='margin: 0 0 20px 0; color: {SecondaryColor}; font-size: 24px; text-align: center;'>
                    Email Address Changed ✉️
                </h2>
                <p style='margin: 0 0 25px 0; color: {TextColor}; font-size: 16px; line-height: 1.6; text-align: center;'>
                    Hi <strong>{displayName}</strong>, the email address associated with your S2S account was recently changed.
                </p>

                <div style='background: linear-gradient(135deg, {SecondaryColor} 0%, #2D4A6F 100%); padding: 20px; border-radius: 12px; text-align: center; margin: 0 0 25px 0;'>
                    <p style='margin: 0; color: rgba(255,255,255,0.8); font-size: 13px;'>Changed on</p>
                    <p style='margin: 6px 0 0 0; color: #ffffff; font-size: 16px; font-weight: bold;'>⏱ {changedAt}</p>
                </div>

                <div style='background-color: {LightBg}; padding: 16px 20px; border-radius: 8px; margin: 0 0 25px 0;'>
                    <p style='margin: 0 0 4px 0; color: {MutedText}; font-size: 13px;'>Old Email</p>
                    <p style='margin: 0 0 12px 0; color: {TextColor}; font-size: 15px; font-weight: bold;'>{oldEmail}</p>
                    <p style='margin: 0 0 4px 0; color: {MutedText}; font-size: 13px;'>New Email</p>
                    <p style='margin: 0; color: {PrimaryColor}; font-size: 15px; font-weight: bold;'>{newEmail}</p>
                </div>

                <div style='background-color: #FFF3F0; border-left: 4px solid {PrimaryColor}; padding: 16px 20px; border-radius: 8px; margin: 0 0 25px 0;'>
                    <p style='margin: 0 0 8px 0; color: {PrimaryColor}; font-weight: bold; font-size: 14px;'>⚠️ Didn't make this change?</p>
                    <p style='margin: 0; color: {TextColor}; font-size: 14px; line-height: 1.6;'>
                        If you did not make this change, your account may be compromised. 
                        Please contact our support team immediately.
                    </p>
                </div>

                <p style='margin: 0; color: {MutedText}; font-size: 13px; text-align: center;'>
                    All active sessions have been signed out for your security.
                </p>";

            string newEmailSubject = "Email Address Confirmed - S2S";
            string newEmailContent = $@"
                <h2 style='margin: 0 0 20px 0; color: {SecondaryColor}; font-size: 24px; text-align: center;'>
                    Email Updated Successfully ✅
                </h2>
                <p style='margin: 0 0 25px 0; color: {TextColor}; font-size: 16px; line-height: 1.6; text-align: center;'>
                    Hi <strong>{displayName}</strong>, your S2S account email has been successfully changed to this address.
                </p>

                <div style='background: linear-gradient(135deg, {PrimaryColor} 0%, #FF7B5C 100%); padding: 20px; border-radius: 12px; text-align: center; margin: 0 0 25px 0;'>
                    <p style='margin: 0; color: rgba(255,255,255,0.8); font-size: 13px;'>Your new email</p>
                    <p style='margin: 6px 0 0 0; color: #ffffff; font-size: 18px; font-weight: bold;'>{newEmail}</p>
                </div>

                <p style='margin: 0 0 15px 0; color: {TextColor}; font-size: 14px; text-align: center; line-height: 1.6;'>
                    Please use this email address to log in from now on. Your previous sessions have been signed out.
                </p>

                <p style='margin: 0; color: {MutedText}; font-size: 13px; text-align: center;'>
                    If you did not make this change, please contact our support team immediately.
                </p>";

            string oldBody = GetEmailTemplate(oldEmailContent);
            string newBody = GetEmailTemplate(newEmailContent);

            await Task.WhenAll(
                SendEmailAsync(oldEmail, oldEmailSubject, oldBody),
                SendEmailAsync(newEmail, newEmailSubject, newBody)
            );
        }

        public async Task SendTierChangedEmailAsync(string to, string displayName, string oldTier, string newTier, DateTime changedAt)
        {
            string subject = "Your Subscription Tier Has Been Updated - S2S";
            string formattedDate = changedAt.ToString("dd MMM yyyy HH:mm 'UTC'");

            string content = $@"
                <h2 style='margin: 0 0 20px 0; color: {SecondaryColor}; font-size: 24px; text-align: center;'>
                    Subscription Update 🔄
                </h2>
                <p style='margin: 0 0 25px 0; color: {TextColor}; font-size: 16px; line-height: 1.6; text-align: center;'>
                    Hi <strong>{displayName}</strong>, your S2S subscription tier has been updated.
                </p>

                <div style='background-color: {LightBg}; padding: 20px; border-radius: 12px; margin: 0 0 25px 0;'>
                    <div style='margin-bottom: 16px;'>
                        <p style='margin: 0 0 4px 0; color: {MutedText}; font-size: 13px;'>Previous Tier</p>
                        <p style='margin: 0; color: {TextColor}; font-size: 16px; font-weight: bold;'>{oldTier}</p>
                    </div>
                    <div style='margin-bottom: 16px;'>
                        <p style='margin: 0 0 4px 0; color: {MutedText}; font-size: 13px;'>New Tier</p>
                        <div style='display: inline-block; background: linear-gradient(135deg, {PrimaryColor} 0%, #FF7B5C 100%); padding: 8px 20px; border-radius: 12px;'>
                            <span style='color: #ffffff; font-size: 16px; font-weight: bold;'>{newTier}</span>
                        </div>
                    </div>
                    <div style='border-top: 1px solid #eee; padding-top: 16px;'>
                        <p style='margin: 0 0 4px 0; color: {MutedText}; font-size: 13px;'>Changed At</p>
                        <p style='margin: 0; color: {TextColor}; font-size: 15px;'>⏱ {formattedDate}</p>
                    </div>
                </div>

                <p style='margin: 0 0 10px 0; color: {TextColor}; font-size: 14px; line-height: 1.6; text-align: center;'>
                    If you have any questions about this change, please contact our support team.
                </p>
                <p style='margin: 0; color: {PrimaryColor}; font-size: 14px; text-align: center; font-weight: bold;'>
                    support@s2sai.online
                </p>";

            string body = GetEmailTemplate(content);
            await SendEmailAsync(to, subject, body);
        }
    }
}
