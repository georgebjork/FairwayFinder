using FairwayFinder.Features.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Features.Services;

public class DevEmailSender : IEmailSender
{
    private readonly ILogger<DevEmailSender> _logger;

    public DevEmailSender(ILogger<DevEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendConfirmationEmailAsync(string toEmail, string confirmationLink)
    {
        _logger.LogInformation("[DEV EMAIL] Confirmation email to {Email}. Click to confirm: {Link}",
            toEmail, confirmationLink);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetEmailAsync(string toEmail, string resetLink)
    {
        _logger.LogInformation("[DEV EMAIL] Password reset email to {Email}. Click to reset: {Link}",
            toEmail, resetLink);
        return Task.CompletedTask;
    }
}
