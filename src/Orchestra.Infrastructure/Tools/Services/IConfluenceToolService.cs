using System.ComponentModel;
using Orchestra.Domain.Enums;
using Orchestra.Infrastructure.Tools.Attributes;

namespace Orchestra.Infrastructure.Tools.Services;

[ToolCategory("Confluence", ProviderType.CONFLUENCE, "Manage Confluence pages")]
public interface IConfluenceToolService
{
    [ToolAction("search", "Search Confluence content by keywords and return full page contents in markdown. Query is split by whitespace into keywords, each keyword is searched separately, duplicates are automatically excluded.", DangerLevel.Safe)]
    [Description("Search Confluence content by text query. The query is automatically split into keywords (by whitespace) and each keyword is searched separately. Results are deduplicated automatically to avoid returning the same page multiple times. Returns full page contents in markdown format.")]
    Task<object> SearchAsync(
        [Description("The workspace ID where the Confluence integration is configured")] string workspaceId,
        [Description("The text query to search for in Confluence pages. Will be split into keywords by whitespace. Example: 'api documentation' searches for 'api' and 'documentation' separately.")] string query,
        [Description("Maximum number of pages to return across all keywords (1-100, default 10)")] int limit = 10);

    [ToolAction("get_page", "Get a specific Confluence page content in markdown", DangerLevel.Safe)]
    [Description("Retrieve a specific Confluence page by ID and return its content in markdown format")]
    Task<object> GetPageAsync(
        [Description("The workspace ID where the Confluence integration is configured")] string workspaceId,
        [Description("The Confluence page ID to retrieve")] string pageId);

    //[ToolAction("create_page", "Create a new Confluence page from markdown content", DangerLevel.Moderate)]
    //[Description("Create a new Confluence page from markdown content in a specified space")]
    //Task<object> CreatePageAsync(
    //    [Description("The workspace ID where the Confluence integration is configured")] string workspaceId,
    //    [Description("The space key where the page will be created")] string spaceKey,
    //    [Description("The title of the new page")] string title,
    //    [Description("The page content in markdown format")] string content,
    //    [Description("Optional parent page ID to nest this page under")] string? parentPageId = null);

    //[ToolAction("update_page", "Update an existing Confluence page with markdown content", DangerLevel.Moderate)]
    //[Description("Update an existing Confluence page's title and/or content (markdown format)")]
    //Task<object> UpdatePageAsync(
    //    [Description("The workspace ID where the Confluence integration is configured")] string workspaceId,
    //    [Description("The Confluence page ID to update")] string pageId,
    //    [Description("Optional new title for the page")] string? title = null,
    //    [Description("Optional new content in markdown format")] string? content = null);
}