namespace Orchestra.Domain.Entities;

public class Workspace
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public Guid OwnerId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsAiSummarizationEnabled { get; private set; }
    public bool IsCustomerSatisfactionAnalysisEnabled { get; private set; }
    public string? AiSummarizationModelId { get; private set; }
    public string? CustomerSatisfactionAnalysisModelId { get; private set; }

    private Workspace() { } // For EF Core

    public static Workspace Create(
        string name, 
        Guid ownerId, 
        string? aiSummarizationModelId = null, 
        string? customerSatisfactionAnalysisModelId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name cannot be null or whitespace.", nameof(name));
        }

        var trimmedName = name.Trim();
        if (trimmedName.Length < 2 || trimmedName.Length > 100)
        {
            throw new ArgumentException("Name must be between 2 and 100 characters.", nameof(name));
        }

        return new Workspace
        {
            Id = Guid.NewGuid(),
            Name = trimmedName,
            OwnerId = ownerId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            AiSummarizationModelId = aiSummarizationModelId,
            CustomerSatisfactionAnalysisModelId = customerSatisfactionAnalysisModelId
        };
    }

    public void UpdateName(string newName)
    {
        var trimmedName = newName?.Trim() ?? string.Empty;
        
        if (trimmedName.Length < 2 || trimmedName.Length > 100)
        {
            throw new ArgumentException(
                "Workspace name must be between 2 and 100 characters.", 
                nameof(newName));
        }
        
        Name = trimmedName;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateAiSettings(
        bool isAiSummarizationEnabled, 
        bool isCustomerSatisfactionAnalysisEnabled,
        string? aiSummarizationModelId = null,
        string? customerSatisfactionAnalysisModelId = null,
        bool updateModelIds = false)
    {
        IsAiSummarizationEnabled = isAiSummarizationEnabled;
        IsCustomerSatisfactionAnalysisEnabled = isCustomerSatisfactionAnalysisEnabled;
        
        // Only update model IDs if the updateModelIds flag is true (partial-update semantics)
        if (updateModelIds)
        {
            AiSummarizationModelId = aiSummarizationModelId;
            CustomerSatisfactionAnalysisModelId = customerSatisfactionAnalysisModelId;
        }
        
        UpdatedAt = DateTime.UtcNow;
    }
}