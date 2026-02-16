namespace Orchestra.Domain.Enums;

public enum DangerLevel
{
    Safe = 0,        // Read operations, no data modification
    Moderate = 1,    // Create/update operations, reversible changes
    Destructive = 2  // Delete operations, permanent changes
}
