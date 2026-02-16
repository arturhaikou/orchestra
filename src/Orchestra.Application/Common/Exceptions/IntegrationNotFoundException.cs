using System;

namespace Orchestra.Application.Common.Exceptions;

/// <summary>
/// Exception thrown when an integration is not found.
/// </summary>
public class IntegrationNotFoundException : Exception
{
    public Guid IntegrationId { get; }

    public IntegrationNotFoundException(Guid integrationId)
        : base($"Integration with ID '{integrationId}' was not found.")
    {
        IntegrationId = integrationId;
    }

    public IntegrationNotFoundException(Guid integrationId, Exception innerException)
        : base($"Integration with ID '{integrationId}' was not found.", innerException)
    {
        IntegrationId = integrationId;
    }
}