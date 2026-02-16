using System.Text.Json;
using System.Text.Json.Serialization;

namespace Orchestra.Infrastructure.Tools.Models.Confluence;

/// <summary>
/// Response from Confluence search API (GET /rest/api/content/search).
/// </summary>
public class ConfluenceSearchResponse
{
    [JsonPropertyName("results")]
    public List<ConfluenceSearchResult> Results { get; set; } = new();

    [JsonPropertyName("start")]
    public int Start { get; set; }

    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    [JsonPropertyName("size")]
    public int Size { get; set; }

    [JsonPropertyName("_links")]
    public ConfluenceLinks? Links { get; set; }
}

/// <summary>
/// Individual search result from Confluence search.
/// </summary>
public class ConfluenceSearchResult
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("space")]
    public ConfluenceSpace? Space { get; set; }

    [JsonPropertyName("_links")]
    public ConfluenceLinks? Links { get; set; }
}

/// <summary>
/// Response from Confluence get page API (GET /rest/api/content/{pageId}).
/// </summary>
public class ConfluencePageResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("space")]
    public ConfluenceSpace? Space { get; set; }

    [JsonPropertyName("version")]
    public ConfluenceVersion? Version { get; set; }

    [JsonPropertyName("body")]
    public ConfluenceBody? Body { get; set; }

    [JsonPropertyName("_links")]
    public ConfluenceLinks? Links { get; set; }
}

/// <summary>
/// Confluence space information.
/// </summary>
public class ConfluenceSpace
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}

/// <summary>
/// Confluence page version information.
/// </summary>
public class ConfluenceVersion
{
    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("when")]
    public string? When { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("by")]
    public ConfluenceUser? By { get; set; }
}

/// <summary>
/// Confluence user information.
/// </summary>
public class ConfluenceUser
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("accountId")]
    public string AccountId { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string? Email { get; set; }
}

/// <summary>
/// Confluence page body content in various formats.
/// </summary>
public class ConfluenceBody
{
    [JsonPropertyName("storage")]
    public ConfluenceContent? Storage { get; set; }

    [JsonPropertyName("atlas_doc_format")]
    public ConfluenceContent? AtlasDocFormat { get; set; }

    [JsonPropertyName("view")]
    public ConfluenceContent? View { get; set; }
}

/// <summary>
/// Confluence content in specific format.
/// </summary>
public class ConfluenceContent
{
    [JsonPropertyName("value")]
    public JsonElement Value { get; set; }

    [JsonPropertyName("representation")]
    public string Representation { get; set; } = string.Empty;
}

/// <summary>
/// Confluence links (webui, self, etc.).
/// </summary>
public class ConfluenceLinks
{
    [JsonPropertyName("webui")]
    public string? WebUi { get; set; }

    [JsonPropertyName("self")]
    public string? Self { get; set; }

    [JsonPropertyName("base")]
    public string? Base { get; set; }

    [JsonPropertyName("context")]
    public string? Context { get; set; }
}

/// <summary>
/// Request to create a new Confluence page (POST /rest/api/content).
/// </summary>
public class CreateConfluencePageRequest
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "page";

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("space")]
    public CreateConfluenceSpace Space { get; set; } = new();

    [JsonPropertyName("body")]
    public ConfluenceBody Body { get; set; } = new();

    [JsonPropertyName("ancestors")]
    public List<ConfluenceAncestor>? Ancestors { get; set; }
}

/// <summary>
/// Space reference for page creation.
/// </summary>
public class CreateConfluenceSpace
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;
}

/// <summary>
/// Ancestor (parent page) reference.
/// </summary>
public class ConfluenceAncestor
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
}

/// <summary>
/// Request to update an existing Confluence page (PUT /rest/api/content/{pageId}).
/// </summary>
public class UpdateConfluencePageRequest
{
    [JsonPropertyName("version")]
    public ConfluenceVersionUpdate Version { get; set; } = new();

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "page";

    [JsonPropertyName("body")]
    public ConfluenceBody? Body { get; set; }
}

/// <summary>
/// Version information for page update.
/// </summary>
public class ConfluenceVersionUpdate
{
    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

/// <summary>
/// Response from creating or updating a page.
/// </summary>
public class ConfluencePageCreateUpdateResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("space")]
    public ConfluenceSpace? Space { get; set; }

    [JsonPropertyName("version")]
    public ConfluenceVersion? Version { get; set; }

    [JsonPropertyName("_links")]
    public ConfluenceLinks? Links { get; set; }
}
