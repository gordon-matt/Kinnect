using System.Net;
using System.Net.Mail;
using Kinnect.Models;
using Microsoft.Extensions.Options;

namespace Kinnect.Services;

public class EmailService(IOptions<SmtpOptions> options, ILogger<EmailService> logger) : IEmailService
{
    public async Task SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken = default)
    {
        var opts = options.Value;

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