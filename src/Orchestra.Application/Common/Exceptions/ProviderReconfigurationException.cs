namespace Orchestra.Application.Common.Exceptions;

/// <summary>
/// Thrown by <c>IWorkspaceProviderService.ReconfigureProviderAsync</c> when the
/// provider reconfiguration cannot be completed:
/// <list type="bullet">
///   <item>The live credential probe fails (provider unreachable or credentials rejected).</item>
///   <item>The supplied <c>defaultModelId</c> is not present in the validated model list.</item>
/// </list>
/// Maps to <c>422 Unprocessable Entity</c> at the API layer.
/// </summary>
/// <remarks>
/// <b>Security contract:</b> The message passed to this exception MUST NOT contain
/// credential values, raw provider error payloads, stack traces, or internal URLs.
/// The message is forwarded directly to the API caller as a human-readable error body.
/// </remarks>
public sealed class ProviderReconfigurationException : Exception
{
    /// <summary>
    /// Initialises a new instance with a human-readable, sanitised error message
    /// suitable for direct inclusion in an API response body.
    /// </summary>
    /// <param name="message">
    /// A sanitised, user-facing description of why the reconfiguration failed.
    /// Must not include API keys, endpoint URLs, or raw provider error detail.
    /// </param>
    public ProviderReconfigurationException(string message)
        : base(message)
    {
    }
}
