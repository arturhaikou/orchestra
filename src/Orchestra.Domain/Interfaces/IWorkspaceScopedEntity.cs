namespace Orchestra.Domain.Interfaces;

/// <summary>
/// Marker interface for entities that are scoped to a workspace.
/// All entities implementing this interface MUST enforce workspace isolation
/// in their data access layer by filtering queries with WorkspaceId.
/// </summary>
public interface IWorkspaceScopedEntity
{
    /// <summary>
    /// The workspace that owns this entity.
    /// This property MUST NOT be Guid.Empty and MUST reference a valid workspace.
    /// </summary>
    Guid WorkspaceId { get; }
}