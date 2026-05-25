namespace Orchestra.Application.Common.Interfaces;

public interface IAzureOpenAILimitsService
{
    Task<bool> IsModelAccessibleAsync(
        string endpoint,
        string apiKey,
        string modelId,
        CancellationToken cancellationToken);
}
