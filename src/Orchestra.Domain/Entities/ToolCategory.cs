using System;
using Orchestra.Domain.Enums;

namespace Orchestra.Domain.Entities;

/// <summary>
/// Represents a category for organizing tools within a workspace.
/// </summary>
public class ToolCategory
{
    /// <summary>
    /// Gets the unique identifier of the tool category.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the name of the tool category.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the description of the tool category.
    /// </summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the provider type for this tool category.
    /// </summary>
    public ProviderType ProviderType { get; private set; }

    /// <summary>
    /// Gets the service class name for dynamic resolution. Null for MCP-backed categories.
    /// </summary>
    public string? ServiceClassName { get; private set; }

    /// <summary>
    /// Gets the ID of the MCP integration that backs this category. Null for native categories.
    /// </summary>
    public Guid? IntegrationId { get; private set; }

    /// <summary>
    /// Gets the ID of the MCP server that backs this category.
    /// Null for native (non-MCP) categories.
    /// Replaces IntegrationId for MCP-sourced tool categories.
    /// </summary>
    public Guid? McpServerId { get; private set; }

    /// <summary>
    /// Gets the creation timestamp of the tool category.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Gets the last modification timestamp of the tool category.
    /// </summary>
    public DateTime? UpdatedAt { get; private set; }

    /// <summary>
    /// Gets a value indicating whether this tool category is active.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Private constructor to enforce factory method usage.
    /// </summary>
    private ToolCategory() { }

    /// <summary>
    /// Creates a new tool category instance with validated parameters.
    /// </summary>
    /// <param name="name">The category name (required, max 100 characters).</param>
    /// <param name="description">The category description (optional, max 500 characters).</param>
    /// <param name="providerType">The provider type for this category.</param>
    /// <param name="serviceClassName">The service class name for dynamic resolution (required, max 200 characters).</param>
    /// <returns>A new ToolCategory instance.</returns>
    /// <exception cref="ArgumentException">Thrown when validation fails.</exception>
    public static ToolCategory Create(string name, string description, ProviderType providerType, string serviceClassName)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be null or empty.", nameof(name));

        if (name.Length > 100)
            throw new ArgumentException("Name cannot exceed 100 characters.", nameof(name));

        if (description.Length > 500)
            throw new ArgumentException("Description cannot exceed 500 characters.", nameof(description));

        if (string.IsNullOrWhiteSpace(serviceClassName))
            throw new ArgumentException("ServiceClassName cannot be null or empty.", nameof(serviceClassName));

        if (serviceClassName.Length > 200)
            throw new ArgumentException("ServiceClassName cannot exceed 200 characters.", nameof(serviceClassName));

        return new ToolCategory
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Description = description.Trim(),
            ProviderType = providerType,
            ServiceClassName = serviceClassName.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a new MCP-backed tool category with no .NET service class binding.
    /// </summary>
    /// <param name="name">The category name derived from the integration name.</param>
    /// <param name="description">The category description.</param>
    /// <param name="providerType">The provider type (e.g., FIGMA, MCP_GENERIC).</param>
    /// <param name="integrationId">The ID of the MCP integration that backs this category.</param>
    /// <returns>A new ToolCategory instance with ServiceClassName = null.</returns>
    /// <exception cref="ArgumentException">Thrown when name is empty or integrationId is empty.</exception>
    public static ToolCategory CreateMcpCategory(
        string name,
        string description,
        ProviderType providerType,
        Guid integrationId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be null or empty.", nameof(name));

        if (name.Length > 100)
            throw new ArgumentException("Name cannot exceed 100 characters.", nameof(name));

        if (description.Length > 500)
            throw new ArgumentException("Description cannot exceed 500 characters.", nameof(description));

        if (integrationId == Guid.Empty)
            throw new ArgumentException("Integration ID is required for MCP categories.", nameof(integrationId));

        return new ToolCategory
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Description = description.Trim(),
            ProviderType = providerType,
            ServiceClassName = null,
            IntegrationId = integrationId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a new MCP-backed tool category linked to a McpServer record.
    /// </summary>
    public static ToolCategory CreateForMcpServer(
        string name,
        string description,
        ProviderType providerType,
        Guid mcpServerId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be null or empty.", nameof(name));

        if (name.Length > 100)
            throw new ArgumentException("Name cannot exceed 100 characters.", nameof(name));

        return new ToolCategory
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Description = description.Trim(),
            ProviderType = providerType,
            McpServerId = mcpServerId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Updates the tool category's information.
    /// </summary>
    /// <param name="name">The new name (required, max 100 characters).</param>
    /// <param name="description">The new description (max 500 characters).</param>
    /// <exception cref="ArgumentException">Thrown when validation fails.</exception>
    public void Update(string name, string description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be null or empty.", nameof(name));

        if (name.Length > 100)
            throw new ArgumentException("Name cannot exceed 100 characters.", nameof(name));

        if (description.Length > 500)
            throw new ArgumentException("Description cannot exceed 500 characters.", nameof(description));

        Name = name.Trim();
        Description = description.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }
}