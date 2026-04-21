namespace Orchestra.Application.Common.Exceptions;

/// <summary>
/// Exception thrown when an outbound call to an external AI provider fails
/// for any reason: unreachable host, TLS error, authentication failure
/// (invalid or expired API key), or a non-success HTTP response.
/// </summary>
/// <remarks>
/// <b>Security contract:</b> The message passed to this exception MUST NOT
/// contain credential values (API keys, endpoints, or any raw provider
/// response payload). Callers that catch this exception and map it to an
/// HTTP response must use a generic, sanitised message — never <c>ex.Message</c>.
/// </remarks>
public sealed class AIProviderCommunicationException : Exception
{
    /// <summary>
    /// Initialises a new instance with a developer-readable sanitised message.
    /// The message must not contain credential values or raw provider error detail.
    /// </summary>
    /// <param name="message">
    /// A sanitised, developer-readable description of the failure.
    /// Must not include API keys, endpoint URLs, or raw provider response payloads.
    /// </param>
    public AIProviderCommunicationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initialises a new instance preserving the original cause for logging/diagnostics.
    /// The inner exception detail must not be forwarded to callers.
    /// </summary>
    /// <param name="message">
    /// A sanitised, developer-readable description of the failure.
    /// </param>
    /// <param name="innerException">
    /// The underlying exception from the HTTP or network layer.
    /// Retained for internal diagnostics only — never serialised into HTTP responses.
    /// </param>
    public AIProviderCommunicationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
