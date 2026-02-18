namespace Orchestra.Infrastructure.Integrations.Providers.Confluence;

/// <summary>
/// Represents a Confluence page.
/// </summary>
public class ConfluencePage
{
    public string? Id { get; set; }
    public string? Type { get; set; }
    public string? Status { get; set; }
    public string? Title { get; set; }
    public ConfluencePageBody? Body { get; set; }
    public ConfluenceVersion? Version { get; set; }
    public ConfluenceSpace? Space { get; set; }
    public Dictionary<string, object>? Links { get; set; }
}

/// <summary>
/// Represents the body/content of a Confluence page.
/// </summary>
public class ConfluencePageBody
{
    public ConfluenceContent? AtlasDocFormat { get; set; }
    public ConfluenceContent? Storage { get; set; }
}

/// <summary>
/// Represents content in a specific format.
/// </summary>
public class ConfluenceContent
{
    public string? Value { get; set; }
    public string? Representation { get; set; }
}

/// <summary>
/// Represents version information of a Confluence page.
/// </summary>
public class ConfluenceVersion
{
    public int? Number { get; set; }
    public ConfluenceVersionedBy? By { get; set; }
    public DateTime? When { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// Represents the user who created a version.
/// </summary>
public class ConfluenceVersionedBy
{
    public string? Type { get; set; }
    public string? Username { get; set; }
    public string? UserKey { get; set; }
    public string? DisplayName { get; set; }
}

/// <summary>
/// Represents a Confluence space.
/// </summary>
public class ConfluenceSpace
{
    public int? Id { get; set; }
    public string? Key { get; set; }
    public string? Name { get; set; }
    public string? Type { get; set; }
    public Dictionary<string, object>? Links { get; set; }
}

/// <summary>
/// Represents a list of Confluence spaces with pagination.
/// </summary>
public class ConfluenceSpaceList
{
    public List<ConfluenceSpace> Results { get; set; } = new();
    public int? Start { get; set; }
    public int? Limit { get; set; }
    public int? Size { get; set; }
}

/// <summary>
/// Represents search results for Confluence pages.
/// </summary>
public class ConfluenceSearchResponse
{
    public List<ConfluencePage> Results { get; set; } = new();
    public int? Start { get; set; }
    public int? Limit { get; set; }
    public int? Size { get; set; }
    public int? TotalSize { get; set; }
}
