using System.Net;
using System.Net.Mail;

namespace FinvestimaAPI.Services.EmailSender;

public class EmailSender : IEmailSender
{
    private readonly ILogger<EmailSender> _logger;
    private readonly IConfiguration _config;

    public EmailSender(
        ILogger<EmailSender> logger,
        IConfiguration config)
    {
        _logger = logger;
        _config = config;
    }

    private SmtpClient CreateClient() => new SmtpClient("smtp.gmail.com")
    {
        Port = 587,
        Credentials = new NetworkCredential(
            _config["Email:SmtpUser"],
            _config["Email:SmtpPass"]
        ),
        EnableSsl = true
    };

    public async Task SendRegistrationEmail(
        string recipientEmail,
        string confirmationCode,
        string username)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            _logger.LogWarning("Registration email skipped: empty recipient email. Username={Username}", username);
            return;
        }

        try
        {
            var smtpClient = CreateClient();

            string body =
                $"Welcome to Finvestima, {username}!\n\n" +
                $"Your confirmation code is: {confirmationCode}\n\n" +
                $"Enter this code in the app to activate your account.";

            var message = new MailMessage
            {
                From = new MailAddress(_config["Email:From"]!),
                Subject = "Welcome to Finvestima — Confirm your account",
                Body = body,
                IsBodyHtml = false
            };

            message.To.Add(recipientEmail);
            await smtpClient.SendMailAsync(message);

            _logger.LogInformation("Registration email sent successfully. Email={Email}, Username={Username}", recipientEmail, username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send registration email. Email={Email}, Username={Username}", recipientEmail, username);
        }
    }

    public async Task SendConfirmationEmail(
        string recipientEmail,
        string confirmationCode,
        string username)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            _logger.LogWarning("Confirmation email skipped: empty recipient email. Username={Username}", username);
            return;
        }

        try
        {
            var smtpClient = CreateClient();

            string body =
                $"Hi {username}!\n" +
                $"Your reset password confirmation code is: {confirmationCode}";

            var message = new MailMessage
            {
                From = new MailAddress(_config["Email:From"]!),
                Subject = "Finvestima Confirmation Code",
                Body = body,
                IsBodyHtml = false
            };

            message.To.Add(recipientEmail);
            await smtpClient.SendMailAsync(message);

            _logger.LogInformation("Confirmation email sent successfully. Email={Email}, Username={Username}", recipientEmail, username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send confirmation email. Email={Email}, Username={Username}", recipientEmail, username);
        }
    }
}
