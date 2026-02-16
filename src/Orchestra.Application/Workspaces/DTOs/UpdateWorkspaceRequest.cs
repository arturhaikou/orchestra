namespace Orchestra.Application.Workspaces.DTOs;

public record UpdateWorkspaceRequest
{
    public required string Name { get; init; }
}