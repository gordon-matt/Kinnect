using System.Net;
using System.Net.Mail;

namespace Kinnect.Services;

public class EmailService(ISettingsService settings, ILogger<EmailService> logger) : IEmailService
{
    public async Task SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken = default)
    {
        var opts = await settings.GetAsync<EmailSettings>(cancellationToken);
        if (!opts.IsConfigured)
        {
            throw new InvalidOperationException("Email is not set up. An administrator can add the SMTP server and the From address on the Settings page.");
        }

        using var client = new SmtpClient(opts.Host, opts.Port)
        {
            EnableSsl = opts.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        if (!string.IsNullOrWhiteSpace(opts.Username))
        {
            client.Credentials = new NetworkCredential(opts.Username, opts.Password);
        }

        var from = new MailAddress(opts.FromAddress, opts.FromName);
        var to = new MailAddress(toAddress);

        using var message = new MailMessage(from, to)
        {
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };

        try
        {
            await client.SendMailAsync(message, cancellationToken);
            logger.LogInformation("Email sent to {ToAddress} with subject '{Subject}'.", toAddress, subject);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email to {ToAddress}.", toAddress);
            throw;
        }
    }
}