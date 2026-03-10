namespace Orchestra.Application.Workspaces.DTOs;

/// <summary>
/// Response payload for the AI models endpoint.
/// Contains the list of model names available from the currently-configured provider.
/// </summary>
public sealed record AIModelsResponse(IReadOnlyList<string> Models);
