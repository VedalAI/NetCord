// ReSharper disable once CheckNamespace
namespace System.Threading.Tasks;

public class TaskCompletionSource : TaskCompletionSource<bool>
{
    public TaskCompletionSource()
    {
    }

    public TaskCompletionSource(object? state) : base(state)
    {
    }

    public TaskCompletionSource(object? state, TaskCreationOptions creationOptions) : base(state, creationOptions)
    {
    }

    public TaskCompletionSource(TaskCreationOptions creationOptions) : base(creationOptions)
    {
    }

    public void SetFromTask(Task completedTask)
    {
        if (completedTask is null)
            throw new ArgumentNullException(nameof(completedTask));

        if (!TrySetFromTask(completedTask))
            throw new InvalidOperationException("An attempt was made to transition a task to a final state when it had already completed.");
    }

    public void SetResult() => SetResult(true);

    public bool TrySetFromTask(Task completedTask)
    {
        if (completedTask is null)
            throw new ArgumentNullException(nameof(completedTask));

        if (!completedTask.IsCompleted)
            throw new ArgumentException("The task has not yet completed.", nameof(completedTask));

        return completedTask.Status switch
        {
            TaskStatus.RanToCompletion => TrySetResult(true),
            TaskStatus.Canceled => TrySetCanceled(GetCancellationToken(completedTask)),
            TaskStatus.Faulted => TrySetException(completedTask.Exception!.InnerExceptions),
            _ => false
        };
    }

    public bool TrySetResult() => TrySetResult(true);
    
    private static CancellationToken GetCancellationToken(Task task)
    {
        try
        {
            task.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException oce)
        {
            return oce.CancellationToken;
        }
        catch
        {
            // ignore
        }

        return CancellationToken.None;
    }
}
