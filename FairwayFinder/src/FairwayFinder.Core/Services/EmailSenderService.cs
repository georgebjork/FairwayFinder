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
}

public class SendGridEmailService(ILogger<SendGridEmailService> logger, IOptions<SendGridSettings> settings) : IEmailSenderService
{
    private readonly ILogger<SendGridEmailService> _logger = logger;
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
        var msg = MailHelper.CreateSingleEmail(from, to, "FairwayFinder", plainTextContent, htmlContent);
        
        // Send the email
        var response = await Client.SendEmailAsync(msg);
        if(response.StatusCode != HttpStatusCode.Accepted)
        {
            _logger.LogError($"Error sending email to {email} with status code {response.StatusCode}");
        }
        
        _logger.LogInformation($"Email sent to {email} with status code {response.StatusCode}");
    }
}