using Hangfire;
using Kinnect.Data;
using Microsoft.EntityFrameworkCore;

namespace Kinnect.Services.Jobs;

[AutomaticRetry(Attempts = 0)]
public sealed class UnreadMessageEmailJob(
    ApplicationDbContext dbContext,
    IUserInfoService userInfoService,
    IEmailService emailService,
    ILogger<UnreadMessageEmailJob> logger)
{
    private static readonly TimeSpan MinAge = TimeSpan.FromMinutes(30);

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Starting unread message email job.");

        var cutoff = DateTime.UtcNow - MinAge;

        var pending = await dbContext.MessageNotifications
            .Where(n => !n.IsRead && !n.EmailSent && n.CreatedAtUtc <= cutoff)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            logger.LogInformation("No pending unread message notifications to email.");
            return;
        }

        var recipientIds = pending.Select(n => n.ToUserId).Distinct().ToList();
        var userInfo = await userInfoService.GetUserInfoAsync(recipientIds, cancellationToken);

        var byRecipient = pending.GroupBy(n => n.ToUserId);

        foreach (var group in byRecipient)
        {
            string toUserId = group.Key;

            if (!userInfo.TryGetValue(toUserId, out var info) || string.IsNullOrWhiteSpace(info.Email))
            {
                logger.LogWarning("Skipping unread email for user {UserId}: no email address found.", toUserId);
                continue;
            }

            int count = group.Count();
            string subject = count == 1
                ? "You have an unread message on Kinnect"
                : $"You have {count} unread messages on Kinnect";

            string body =
                $"Hi {info.Username},\r\n\r\n" +
                $"You have {count} unread private message{(count == 1 ? "" : "s")} waiting for you on Kinnect.\r\n\r\n" +
                "Log in to read and reply.\r\n\r\n" +
                "— The Kinnect team";

            try
            {
                await emailService.SendAsync(info.Email, subject, body, cancellationToken);

                foreach (var notification in group)
                    notification.EmailSent = true;

                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogInformation(
                    "Sent unread message email to {Email} covering {Count} notification(s).",
                    info.Email, count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send unread message email to {Email}.", info.Email);
            }
        }
    }
}
