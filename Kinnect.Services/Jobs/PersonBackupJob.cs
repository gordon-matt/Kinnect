using Hangfire;
using Kinnect.Infrastructure;
using Kinnect.Services.Abstractions;

namespace Kinnect.Services.Jobs;

[HangfireSkipWhenPreviousInstanceIsRunningFilter]
[AutomaticRetry(Attempts = 0)]
public sealed class PersonBackupJob(ILogger<PersonBackupJob> logger, IPersonBackupService personBackupService)
{
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Starting scheduled person tree backup job.");
        }

        await personBackupService.ExportToNewFileAsync(cancellationToken);
    }
}
