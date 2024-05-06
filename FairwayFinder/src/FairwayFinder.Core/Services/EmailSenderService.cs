using System.Net;
using FairwayFinder.Core.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace FairwayFinder.Core.Services;

public interface IEmailSenderService
{
    Task SendRegisterEmailAsync(string email, string inviteLink);
    Task SendConfirmationEmailAsync(string email, string confirmationLink);
    Task SendResetPasswordEmailAsync(string email, string resetLink);
}

public class SendGridEmailService(ILogger<SendGridEmailService> logger, IOptions<SendGridSettings> settings) : IEmailSenderService
{
    private readonly SendGridSettings _settings = settings.Value;
    
    private SendGridClient Client => new(_settings.ApiKey);

    public async Task SendRegisterEmailAsync(string email, string inviteLink)
    {
        var from = new EmailAddress(_settings.FromEmail, _settings.FromName);
        var to = new EmailAddress(email);
        
        var htmlContent = $@"
        <html>
            <body>
                <p>You've been invited to join FairwayFinder!</p>
                <p>Click <a href='{inviteLink}' target='_blank'>{inviteLink}</a> to register.</p>
            </body>
        </html>";
    
        // Create plain text content (for email clients that don't support HTML)
        var plainTextContent = $"You've been invited to join FairwayFinder! Click here to register: {inviteLink}";

        // Create the email message with both plain text and HTML content
        var msg = MailHelper.CreateSingleEmail(from, to, "Register", plainTextContent, htmlContent);
        
        // Send the email
        await SendMessage(msg);
    }

    public async Task SendConfirmationEmailAsync(string email, string confirmationLink)
    {
        var from = new EmailAddress(_settings.FromEmail, _settings.FromName);
        var to = new EmailAddress(email);
        
        var htmlContent = $@"
        <html>
            <body>
                <p>Your email needs to be confirmed.</p>
                <p>Click <a href='{confirmationLink}' target='_blank'>{confirmationLink}</a> to confirm.</p>
            </body>
        </html>";
    
        // Create plain text content (for email clients that don't support HTML)
        var plainTextContent = $"Your email needs to be confirmed! Click here to confirm: {confirmationLink}";

        // Create the email message with both plain text and HTML content
        var msg = MailHelper.CreateSingleEmail(from, to, "Confirm your email", plainTextContent, htmlContent);
        
        // Send the email
        await SendMessage(msg);
    }
    public async Task SendResetPasswordEmailAsync(string email, string resetLink)
    {
        var from = new EmailAddress(_settings.FromEmail, _settings.FromName);
        var to = new EmailAddress(email);
        
        var htmlContent = $@"
        <html>
            <body>
                <p>You requested a password reset.</p>
                <p>Click <a href='{resetLink}' target='_blank'>{resetLink}</a> to reset your password.</p>
                <p>If you did not request this, ignore this email.</p>
            </body>
        </html>";
    
        // Create plain text content (for email clients that don't support HTML)
        var plainTextContent = $"You requested a password reset, click here to reset: {resetLink}. If you did not request this, ignore this email.";

        // Create the email message with both plain text and HTML content
        var msg = MailHelper.CreateSingleEmail(from, to, "Password Reset", plainTextContent, htmlContent);
        
        // Send the email
        await SendMessage(msg);
    }

    private async Task SendMessage(SendGridMessage message)
    {
        // Send the email
        var response = await Client.SendEmailAsync(message);
        if(response.StatusCode != HttpStatusCode.Accepted)
        {
            logger.LogError($"Error sending email with status code {response.StatusCode}");
        }
        
        logger.LogInformation($"Email sent with status code {response.StatusCode}");
    }
}