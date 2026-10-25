using EasyChatz;
using EasyChatz.Services;
using Hangfire;

namespace Kinnect.Services.Jobs;

[AutomaticRetry(Attempts = 0)]
public sealed class UnreadMessageEmailJob(
    IChatDeliveryQueue deliveryQueue,
    IUserInfoService userInfoService,
    IEmailService emailService,
    ILogger<UnreadMessageEmailJob> logger)
{
    private static readonly TimeSpan MinAge = TimeSpan.FromMinutes(30);

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Starting unread message email job.");
        }

        var cutoff = DateTime.UtcNow - MinAge;
        var pending = await deliveryQueue.ListPendingAsync(cutoff, cancellationToken);
        if (pending.Count == 0)
        {
            logger.LogInformation("No pending unread message notifications to email.");
            return;
        }

        var recipientIds = pending.Select(n => n.ToUserId).Distinct().ToList();
        var userInfo = await userInfoService.GetUserInfoAsync(recipientIds, cancellationToken);

        foreach (var group in pending.GroupBy(n => n.ToUserId))
        {
            string toUserId = group.Key;
            if (!userInfo.TryGetValue(toUserId, out var info) || string.IsNullOrWhiteSpace(info.Email))
            {
                logger.LogWarning("Skipping unread email for user {UserId}: no email address found.", toUserId);
                continue;
            }

            int count = group.Count();
            bool mentioned = group.Any(n => ChatMentions.Mentions(n.Content, toUserId));
            string subject = mentioned
                ? "You were mentioned on Kinnect"
                : count == 1
                    ? "You have an unread message on Kinnect"
                    : $"You have {count} unread messages on Kinnect";

            string preview = string.Join("\r\n", group.Take(3).Select(n => "- " + ChatMentions.ToPlainText(n.Content)));
            string waiting = mentioned
                ? $"You were mentioned on Kinnect ({count} unread message{(count == 1 ? "" : "s")})."
                : $"You have {count} unread message{(count == 1 ? "" : "s")} waiting for you on Kinnect.";
            string body =
                $"Hi {info.Username},\r\n\r\n" +
                waiting + "\r\n\r\n" +
                preview + "\r\n\r\n" +
                "Log in to read and reply.\r\n\r\n" +
                "— The Kinnect team";

            try
            {
                await emailService.SendAsync(info.Email, subject, body, cancellationToken);
                await deliveryQueue.MarkDeliveredAsync(group.Select(n => n.Id).ToArray(), cancellationToken);
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
