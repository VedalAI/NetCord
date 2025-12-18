// ReSharper disable once CheckNamespace
namespace System.Threading.Tasks;

public static class TaskExtensions
{
    extension(Task self)
    {
        public async Task WaitAsync(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            await using (cancellationToken.Register(s => ((TaskCompletionSource<bool>)s!).TrySetCanceled(cancellationToken), tcs, useSynchronizationContext: false))
            {
                if (await Task.WhenAny(self, tcs.Task).ConfigureAwait(false) == tcs.Task)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                await self.ConfigureAwait(false);
            }
        }

        public static Task WhenAll(Span<Task> tasks)
        {
            return Task.WhenAll(tasks.ToArray());
        }
    }
    
    // https://github.com/dotnet/roslyn/issues/78487
    public static async Task<T> WaitAsync<T>(this Task<T> self, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using (cancellationToken.Register(s => ((TaskCompletionSource<bool>)s!).TrySetCanceled(cancellationToken), tcs, useSynchronizationContext: false))
        {
            if (await Task.WhenAny(self, tcs.Task).ConfigureAwait(false) == tcs.Task)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            return await self.ConfigureAwait(false);
        }
    }
}
