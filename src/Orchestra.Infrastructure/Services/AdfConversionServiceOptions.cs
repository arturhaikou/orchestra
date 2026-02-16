namespace Orchestra.Infrastructure.Services;

/// <summary>
/// Configuration options for the ADF-to-Markdown conversion service.
/// </summary>
public class AdfConversionServiceOptions
{
    /// <summary>
    /// Timeout in seconds for HTTP requests to the adfgenerator service.
    /// Default: 5 seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;
}