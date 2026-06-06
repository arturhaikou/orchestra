using System.Collections.Concurrent;
using Orchestra.Application.Common.Interfaces;

namespace Orchestra.Infrastructure.Jobs;

public class JobCancellationRegistry : IJobCancellationRegistry
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _registry = new();
    private readonly ConcurrentDictionary<Guid, byte> _pendingCancellations = new();

    public void Register(Guid jobId, CancellationTokenSource userCts)
    {
        if (!_registry.TryAdd(jobId, userCts))
            return;

        if (_pendingCancellations.TryRemove(jobId, out _))
            userCts.Cancel();
    }

    public void Unregister(Guid jobId)
    {
        _registry.TryRemove(jobId, out _);
        _pendingCancellations.TryRemove(jobId, out _);
    }

    public bool TryCancel(Guid jobId)
    {
        if (_registry.TryGetValue(jobId, out var cts))
        {
            cts.Cancel();
            return true;
        }

        _pendingCancellations[jobId] = 0;
        return false;
    }
}
