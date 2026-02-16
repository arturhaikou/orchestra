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
    /// Gets the service class name for dynamic resolution.
    /// </summary>
    public string ServiceClassName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the creation timestamp of the tool category.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Gets the last modification timestamp of the tool category.
    /// </summary>
    public DateTime? UpdatedAt { get; private set; }

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
}