using Microsoft.Extensions.Hosting;
using Orchestra.Application.Common.Interfaces;
using StackExchange.Redis;

namespace Orchestra.Infrastructure.Jobs;

public sealed class JobCancellationSubscriber(
    IConnectionMultiplexer redis,
    IJobCancellationRegistry cancellationRegistry) : BackgroundService
{
    private const string CancellationChannel = "orchestra:job-cancellations";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sub = redis.GetSubscriber();

        await sub.SubscribeAsync(
            RedisChannel.Literal(CancellationChannel),
            (_, value) =>
            {
                if (Guid.TryParse((string?)value, out var jobId))
                    cancellationRegistry.TryCancel(jobId);
            });

        await Task.Delay(Timeout.Infinite, stoppingToken);

        await sub.UnsubscribeAsync(RedisChannel.Literal(CancellationChannel));
    }
}
