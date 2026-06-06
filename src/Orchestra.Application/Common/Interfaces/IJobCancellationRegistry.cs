namespace Orchestra.Application.Common.Interfaces;

public interface IJobCancellationRegistry
{
    void Register(Guid jobId, CancellationTokenSource userCts);
    void Unregister(Guid jobId);
    bool TryCancel(Guid jobId);
}
