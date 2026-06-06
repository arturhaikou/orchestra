using Orchestra.Infrastructure.Jobs;

namespace Orchestra.Infrastructure.Tests.Jobs;

public class JobCancellationRegistryTests
{
    [Fact]
    public void TryCancel_WhenRegistered_CancelsTokenSource()
    {
        var sut = new JobCancellationRegistry();
        var jobId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();

        sut.Register(jobId, cts);

        var cancelled = sut.TryCancel(jobId);

        Assert.True(cancelled);
        Assert.True(cts.IsCancellationRequested);
    }

    [Fact]
    public void TryCancel_WhenNotRegistered_MarksPendingAndCancelsOnRegister()
    {
        var sut = new JobCancellationRegistry();
        var jobId = Guid.NewGuid();

        var cancelled = sut.TryCancel(jobId);

        Assert.False(cancelled);

        using var cts = new CancellationTokenSource();
        sut.Register(jobId, cts);

        Assert.True(cts.IsCancellationRequested);
    }

    [Fact]
    public void Unregister_ClearsPendingCancellation()
    {
        var sut = new JobCancellationRegistry();
        var jobId = Guid.NewGuid();

        sut.TryCancel(jobId);
        sut.Unregister(jobId);

        using var cts = new CancellationTokenSource();
        sut.Register(jobId, cts);

        Assert.False(cts.IsCancellationRequested);
    }
}
