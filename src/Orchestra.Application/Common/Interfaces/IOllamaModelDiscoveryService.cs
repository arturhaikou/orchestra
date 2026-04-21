using Orchestra.Application.Workspaces.DTOs;

namespace Orchestra.Application.Common.Interfaces;

/// <summary>
/// Probes an Ollama server and returns a structured discovery result containing
/// reachability status, the list of installed model identifiers, and an optional
/// failure description.
/// This service is stateless — it accepts a raw <paramref name="endpoint"/> URL rather than
/// a workspace ID, making it suitable for pre-commitment validation before any workspace
/// configuration is saved.
/// </summary>
public interface IOllamaModelDiscoveryService
{
    /// <summary>
    /// Connects to the Ollama server at <paramref name="endpoint"/> and returns
    /// the installed model identifiers.
    /// </summary>
    /// <param name="endpoint">
    /// The base URL of the Ollama server (e.g., <c>http://localhost:11434</c>).
    /// Must be non-null and non-whitespace — callers are responsible for this pre-condition.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// An <see cref="OllamaDiscoveryResult"/> that always succeeds at the method level:
    /// connectivity failures are captured in <see cref="OllamaDiscoveryResult.IsValid"/>
    /// and <see cref="OllamaDiscoveryResult.ErrorMessage"/> rather than thrown as exceptions.
    /// </returns>
    Task<OllamaDiscoveryResult> DiscoverModelsAsync(string endpoint, CancellationToken cancellationToken);
}
