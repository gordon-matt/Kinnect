using Hangfire.Client;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;

namespace Kinnect.Infrastructure;

public class HangfireSkipWhenPreviousInstanceIsRunningFilter : JobFilterAttribute, IClientFilter, IApplyStateFilter
{
    public void OnCreating(CreatingContext context)
    {
        if (context.Connection is not JobStorageConnection connection)
        {
            return;
        }

        if (!context.Parameters.ContainsKey("RecurringJobId"))
        {
            return;
        }

        string? recurringJobId = context.Parameters["RecurringJobId"] as string;
        if (string.IsNullOrWhiteSpace(recurringJobId))
        {
            return;
        }

        string running = connection.GetValueFromHash($"recurring-job:{recurringJobId}", "Running");
        context.Canceled = "true".Equals(running, StringComparison.OrdinalIgnoreCase);
    }

    public void OnCreated(CreatedContext filterContext)
    {
    }

    public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        if (context.NewState is EnqueuedState)
        {
            SetRunning(context, transaction, true);
        }
        else if (context.NewState.IsFinal || context.NewState is FailedState)
        {
            SetRunning(context, transaction, false);
        }
    }

    public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
    }

    private static void SetRunning(ApplyStateContext context, IWriteOnlyTransaction transaction, bool running)
    {
        string recurringJobId = SerializationHelper.Deserialize<string>(
            context.Connection.GetJobParameter(context.BackgroundJob.Id, "RecurringJobId"));

        if (string.IsNullOrWhiteSpace(recurringJobId))
        {
            return;
        }

        transaction.SetRangeInHash($"recurring-job:{recurringJobId}",
            [new KeyValuePair<string, string>("Running", running ? "true" : "false")]);
    }
}