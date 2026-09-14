namespace FinvestimaAPI.Services.EmailSender;

public interface IEmailSender
{
    Task SendConfirmationEmail(
        string recipientEmail,
        string confirmationCode,
        string username);

    Task SendRegistrationEmail(
        string recipientEmail,
        string confirmationCode,
        string username);
}
