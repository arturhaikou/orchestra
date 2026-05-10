using System.Text.Json;
using Microsoft.Extensions.Logging;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Infrastructure.Integrations.Providers.Confluence;
using Orchestra.Infrastructure.Tools.Models.Confluence;
using ConfluenceApiContent = Orchestra.Infrastructure.Tools.Models.Confluence.ConfluenceContent;

namespace Orchestra.Infrastructure.Tools.Services;

public class ConfluenceToolService : IConfluenceToolService
{
    private readonly ConfluenceApiClientFactory _confluenceApiClientFactory;
    private readonly IIntegrationResolver _integrationResolver;
    private readonly IAdfConversionService _adfConversionService;
    private readonly ILogger<ConfluenceToolService> _logger;

    public ConfluenceToolService(
        ConfluenceApiClientFactory confluenceApiClientFactory,
        IIntegrationResolver integrationResolver,
        IAdfConversionService adfConversionService,
        ILogger<ConfluenceToolService> logger)
    {
        _confluenceApiClientFactory = confluenceApiClientFactory;
        _integrationResolver = integrationResolver;
        _adfConversionService = adfConversionService;
        _logger = logger;
    }

    public async Task<object> SearchAsync(string workspaceId, string integrationId, string query, int limit = 10)
    {
        try
        {
            _logger.LogInformation(
                "Searching Confluence content in workspace {WorkspaceId} with query: '{Query}', limit: {Limit}",
                workspaceId,
                query,
                limit);

            // Validate inputs
            if (string.IsNullOrWhiteSpace(integrationId))
            {
                return new
                {
                    success = false,
                    error = "integrationId is required",
                    errorCode = "INVALID_INTEGRATION_ID"
                };
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                return new
                {
                    success = false,
                    error = "Search query cannot be empty",
                    errorCode = "INVALID_QUERY"
                };
            }

            if (limit <= 0 || limit > 100)
            {
                limit = 10;
            }

            if (!Guid.TryParse(workspaceId, out var workspaceGuid))
            {
                return new
                {
                    success = false,
                    error = $"Invalid GUID format for workspaceId: {workspaceId}",
                    errorCode = "INVALID_WORKSPACE_ID"
                };
            }

            // Step 1: Load and validate integration
            var integration = await _integrationResolver.ResolveAsync(workspaceGuid, integrationId, ProviderType.CONFLUENCE);
            var apiClient = _confluenceApiClientFactory.CreateClient(integration);

            // Step 2: Split query into keywords and search for each
            var keywords = query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (keywords.Length == 0)
            {
                return new
                {
                    success = false,
                    error = "Search query cannot be empty",
                    errorCode = "INVALID_QUERY"
                };
            }

            _logger.LogInformation(
                "Split query into {KeywordCount} keywords: {Keywords}",
                keywords.Length,
                string.Join(", ", keywords));

            var allPages = new List<object>();
            var excludeContentIds = new List<string>();

            // Search for each keyword and accumulate results
            foreach (var keyword in keywords)
            {
                var remainingLimit = limit - allPages.Count;
                if (remainingLimit <= 0)
                {
                    _logger.LogInformation("Limit reached, stopping search at {Count} pages", allPages.Count);
                    break;
                }

                _logger.LogDebug(
                    "Searching for keyword '{Keyword}' with remaining limit {RemainingLimit}, excluding {ExcludeCount} pages",
                    keyword,
                    remainingLimit,
                    excludeContentIds.Count);

                try
                {
                    var keywordPages = await SearchByKeywordAsync(
                        apiClient,
                        integration,
                        keyword,
                        excludeContentIds,
                        remainingLimit);

                    if (keywordPages.Count > 0)
                    {
                        allPages.AddRange(keywordPages);

                        // Extract IDs to exclude from subsequent searches
                        foreach (var page in keywordPages)
                        {
                            var pageDict = page as IDictionary<string, object>;
                            if (pageDict != null && pageDict.TryGetValue("id", out var id))
                            {
                                excludeContentIds.Add(id.ToString()!);
                            }
                        }

                        _logger.LogInformation(
                            "Found {Count} new pages for keyword '{Keyword}', total: {TotalCount}",
                            keywordPages.Count,
                            keyword,
                            allPages.Count);
                    }
                    else
                    {
                        _logger.LogDebug("No results found for keyword '{Keyword}'", keyword);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to search for keyword '{Keyword}', continuing with remaining keywords",
                        keyword);
                    // Continue searching other keywords
                }
            }

            _logger.LogInformation(
                "Successfully retrieved {Count} Confluence pages with content for workspace {WorkspaceId}",
                allPages.Count,
                workspaceId);

            var keywordsMessage = keywords.Length == 1
                ? $"'{keywords[0]}'"
                : $"keywords: {string.Join(", ", keywords)}";

            return new
            {
                success = true,
                pages = allPages,
                totalResults = allPages.Count,
                message = $"Found {allPages.Count} pages matching {keywordsMessage}"
            };
        }
        catch (IntegrationNotFoundException ex)
        {
            _logger.LogError(ex,
                "Integration not found for workspace {WorkspaceId}",
                workspaceId);

            return new
            {
                success = false,
                error = $"No Confluence integration found for workspace {workspaceId}",
                errorCode = "CONFLUENCE_INTEGRATION_NOT_FOUND"
            };
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex,
                "Invalid operation while searching Confluence: {ErrorMessage}",
                ex.Message);

            return new
            {
                success = false,
                error = ex.Message,
                errorCode = ex.Message.Contains("integrationId is required") ? "INTEGRATION_ID_REQUIRED" :
                           ex.Message.Contains("No active integration found for the supplied ID") ? "INTEGRATION_NOT_FOUND" :
                           ex.Message.Contains("not a Confluence integration") ? "INTEGRATION_WRONG_PROVIDER" :
                           "INVALID_OPERATION"
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "HTTP request failed while searching Confluence for workspace {WorkspaceId}",
                workspaceId);

            return new
            {
                success = false,
                error = $"Failed to communicate with Confluence: {ex.Message}",
                errorCode = "CONFLUENCE_NETWORK_ERROR"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error while searching Confluence for workspace {WorkspaceId}",
                workspaceId);

            return new
            {
                success = false,
                error = $"Unexpected error: {ex.Message}",
                errorCode = "UNEXPECTED_ERROR"
            };
        }
    }

    public async Task<object> GetPageAsync(string workspaceId, string integrationId, string pageId)
    {
        try
        {
            _logger.LogInformation(
                "Getting Confluence page {PageId} from workspace {WorkspaceId}",
                pageId,
                workspaceId);

            // Validate inputs
            if (string.IsNullOrWhiteSpace(pageId))
            {
                return new
                {
                    success = false,
                    error = "Page ID cannot be empty",
                    errorCode = "INVALID_PAGE_ID"
                };
            }

            if (!Guid.TryParse(workspaceId, out var workspaceGuid))
            {
                return new
                {
                    success = false,
                    error = $"Invalid GUID format for workspaceId: {workspaceId}",
                    errorCode = "INVALID_WORKSPACE_ID"
                };
            }

            // Step 1: Load and validate integration
            var integration = await _integrationResolver.ResolveAsync(workspaceGuid, integrationId, ProviderType.CONFLUENCE);
            var apiClient = _confluenceApiClientFactory.CreateClient(integration);

            // Step 2: Fetch page content
            var page = await apiClient.GetPageAsync(pageId);

            if (page == null)
            {
                return new
                {
                    success = false,
                    error = $"Confluence page {pageId} not found",
                    errorCode = "PAGE_NOT_FOUND"
                };
            }

            // Step 3: Convert ADF to markdown
            var markdownContent = "";
            if (page.Body?.AtlasDocFormat?.Value != null)
            {
                var adfString = page.Body.AtlasDocFormat.Value;
                if (!string.IsNullOrEmpty(adfString))
                {
                    // Parse ADF string to JsonElement if needed
                    JsonElement adfElement;
                    try
                    {
                        var parsed = JsonSerializer.Deserialize<object>(adfString);
                        adfElement = JsonSerializer.SerializeToElement(parsed);
                    }
                    catch
                    {
                        // If it's already a JsonElement or raw string, try direct parsing
                        adfElement = JsonSerializer.SerializeToElement(adfString);
                    }

                    markdownContent = await _adfConversionService.ConvertAdfToMarkdownAsync(adfElement, CancellationToken.None);
                }
            }
            else
            {
                _logger.LogWarning(
                    "Page {PageId} does not have ADF content available",
                    pageId);
            }

            var baseUrl = integration.Url?.TrimEnd('/') ?? "";
            var pageUrl = page.Links?.TryGetValue("webui", out var webui) == true
                ? $"{baseUrl}{webui}"
                : $"{baseUrl}/pages/{page.Id}";

            _logger.LogInformation(
                "Successfully retrieved Confluence page {PageId} ('{Title}') from workspace {WorkspaceId}",
                pageId,
                page.Title,
                workspaceId);

            return new
            {
                success = true,
                page = new
                {
                    id = page.Id,
                    title = page.Title,
                    content = markdownContent,
                    spaceKey = page.Space?.Key ?? "",
                    spaceName = page.Space?.Name ?? "",
                    url = pageUrl,
                    version = page.Version?.Number ?? 0,
                    status = page.Status
                },
                message = $"Successfully retrieved page '{page.Title}'"
            };
        }
        catch (IntegrationNotFoundException ex)
        {
            _logger.LogError(ex,
                "Integration not found for workspace {WorkspaceId}",
                workspaceId);

            return new
            {
                success = false,
                error = $"No Confluence integration found for workspace {workspaceId}",
                errorCode = "CONFLUENCE_INTEGRATION_NOT_FOUND"
            };
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex,
                "Invalid operation while getting Confluence page {PageId}: {ErrorMessage}",
                pageId,
                ex.Message);

            return new
            {
                success = false,
                error = ex.Message,
                errorCode = ex.Message.Contains("integrationId is required") ? "INTEGRATION_ID_REQUIRED" :
                           ex.Message.Contains("No active integration found for the supplied ID") ? "INTEGRATION_NOT_FOUND" :
                           ex.Message.Contains("not a Confluence integration") ? "INTEGRATION_WRONG_PROVIDER" :
                           "INVALID_OPERATION"
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "HTTP request failed while getting Confluence page {PageId} for workspace {WorkspaceId}",
                pageId,
                workspaceId);

            return new
            {
                success = false,
                error = $"Failed to communicate with Confluence: {ex.Message}",
                errorCode = "CONFLUENCE_NETWORK_ERROR"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error while getting Confluence page {PageId} for workspace {WorkspaceId}",
                pageId,
                workspaceId);

            return new
            {
                success = false,
                error = $"Unexpected error: {ex.Message}",
                errorCode = "UNEXPECTED_ERROR"
            };
        }
    }

    public async Task<object> CreatePageAsync(
        string workspaceId,
        string integrationId,
        string spaceKey,
        string title,
        string content,
        string? parentPageId = null)
    {
        try
        {
            _logger.LogInformation(
                "Creating Confluence page in workspace {WorkspaceId}: Space='{SpaceKey}', Title='{Title}', ParentPageId='{ParentPageId}'",
                workspaceId,
                spaceKey,
                title,
                parentPageId ?? "none");

            // Validate inputs
            if (!Guid.TryParse(workspaceId, out var workspaceGuid))
            {
                return new
                {
                    success = false,
                    error = $"Invalid GUID format for workspaceId: {workspaceId}",
                    errorCode = "INVALID_WORKSPACE_ID"
                };
            }

            if (string.IsNullOrWhiteSpace(spaceKey))
            {
                return new
                {
                    success = false,
                    error = "Space key cannot be empty",
                    errorCode = "INVALID_SPACE_KEY"
                };
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                return new
                {
                    success = false,
                    error = "Page title cannot be empty",
                    errorCode = "INVALID_TITLE"
                };
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                return new
                {
                    success = false,
                    error = "Page content cannot be empty",
                    errorCode = "INVALID_CONTENT"
                };
            }

            // Step 1: Load and validate integration
            var integration = await _integrationResolver.ResolveAsync(workspaceGuid, integrationId, ProviderType.CONFLUENCE);
            var apiClient = _confluenceApiClientFactory.CreateClient(integration);

            // Step 2: Convert markdown to ADF
            var adfJson = await _adfConversionService.ConvertMarkdownToAdfAsync(content, CancellationToken.None);

            // Step 3: Prepare body object for API call
            var bodyObject = new
            {
                atlas_doc_format = new { value = adfJson, representation = "atlas_doc_format" }
            };

            // Step 4: Create the page
            var createdPage = await apiClient.CreatePageAsync(spaceKey, title, bodyObject, parentPageId);

            if (createdPage == null)
            {
                return new
                {
                    success = false,
                    error = "Failed to create Confluence page",
                    errorCode = "CONFLUENCE_CREATE_FAILED"
                };
            }

            var baseUrl = integration.Url?.TrimEnd('/') ?? "";
            var pageUrl = createdPage.Links?.TryGetValue("webui", out var webui) == true
                ? $"{baseUrl}{webui}"
                : $"{baseUrl}/pages/{createdPage.Id}";

            _logger.LogInformation(
                "Successfully created Confluence page {PageId} ('{Title}') in space {SpaceKey} for workspace {WorkspaceId}",
                createdPage.Id,
                createdPage.Title,
                spaceKey,
                workspaceId);

            return new
            {
                success = true,
                pageId = createdPage.Id,
                title = createdPage.Title,
                url = pageUrl,
                spaceKey = createdPage.Space?.Key ?? spaceKey,
                version = createdPage.Version?.Number ?? 1,
                message = $"Successfully created page '{createdPage.Title}' in space {spaceKey}"
            };
        }
        catch (IntegrationNotFoundException ex)
        {
            _logger.LogError(ex,
                "Integration not found for workspace {WorkspaceId}",
                workspaceId);

            return new
            {
                success = false,
                error = $"No Confluence integration found for workspace {workspaceId}",
                errorCode = "CONFLUENCE_INTEGRATION_NOT_FOUND"
            };
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex,
                "Invalid operation while creating Confluence page: {ErrorMessage}",
                ex.Message);

            return new
            {
                success = false,
                error = ex.Message,
                errorCode = ex.Message.Contains("integrationId is required") ? "INTEGRATION_ID_REQUIRED" :
                           ex.Message.Contains("No active integration found for the supplied ID") ? "INTEGRATION_NOT_FOUND" :
                           ex.Message.Contains("not a Confluence integration") ? "INTEGRATION_WRONG_PROVIDER" :
                           "INVALID_OPERATION"
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "HTTP request failed while creating Confluence page for workspace {WorkspaceId}",
                workspaceId);

            return new
            {
                success = false,
                error = $"Failed to communicate with Confluence: {ex.Message}",
                errorCode = "CONFLUENCE_NETWORK_ERROR"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error while creating Confluence page for workspace {WorkspaceId}",
                workspaceId);

            return new
            {
                success = false,
                error = $"Unexpected error: {ex.Message}",
                errorCode = "UNEXPECTED_ERROR"
            };
        }
    }

    public async Task<object> UpdatePageAsync(
        string workspaceId,
        string integrationId,
        string pageId,
        string? title = null,
        string? content = null)
    {
        try
        {
            _logger.LogInformation(
                "Updating Confluence page {PageId} in workspace {WorkspaceId}: Title='{Title}', HasContent={HasContent}",
                pageId,
                workspaceId,
                title ?? "unchanged",
                content != null);

            // Validate inputs
            if (string.IsNullOrWhiteSpace(pageId))
            {
                return new
                {
                    success = false,
                    error = "Page ID cannot be empty",
                    errorCode = "INVALID_PAGE_ID"
                };
            }

            if (!Guid.TryParse(workspaceId, out var workspaceGuid))
            {
                return new
                {
                    success = false,
                    error = $"Invalid GUID format for workspaceId: {workspaceId}",
                    errorCode = "INVALID_WORKSPACE_ID"
                };
            }

            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(content))
            {
                return new
                {
                    success = false,
                    error = "Either title or content must be provided for update",
                    errorCode = "NO_UPDATE_DATA"
                };
            }

            // Step 1: Load and validate integration
            var integration = await _integrationResolver.ResolveAsync(workspaceGuid, integrationId, ProviderType.CONFLUENCE);
            var apiClient = _confluenceApiClientFactory.CreateClient(integration);

            // Step 2: Get current page to retrieve version number (required by Confluence)
            var currentPage = await apiClient.GetPageAsync(pageId);
            if (currentPage == null)
            {
                return new
                {
                    success = false,
                    error = "Page not found",
                    errorCode = "PAGE_NOT_FOUND"
                };
            }

            var currentVersion = currentPage.Version?.Number ?? 0;

            _logger.LogDebug(
                "Current page version: {CurrentVersion}",
                currentVersion);

            // Step 3: Prepare body object if content is provided
            object? bodyObject = null;
            if (!string.IsNullOrWhiteSpace(content))
            {
                var adfJson = await _adfConversionService.ConvertMarkdownToAdfAsync(content, CancellationToken.None);
                bodyObject = new { atlas_doc_format = new { value = adfJson, representation = "atlas_doc_format" } };
            }

            // Step 4: Update the page
            var updatedPage = await apiClient.UpdatePageAsync(pageId, title, bodyObject, currentVersion);

            if (updatedPage == null)
            {
                return new
                {
                    success = false,
                    error = "Failed to update Confluence page",
                    errorCode = "CONFLUENCE_UPDATE_FAILED"
                };
            }

            var baseUrl = integration.Url?.TrimEnd('/') ?? "";
            var pageUrl = updatedPage.Links?.TryGetValue("webui", out var webui) == true
                ? $"{baseUrl}{webui}"
                : $"{baseUrl}/pages/{updatedPage.Id}";

            _logger.LogInformation(
                "Successfully updated Confluence page {PageId} ('{Title}') to version {Version} for workspace {WorkspaceId}",
                updatedPage.Id,
                updatedPage.Title,
                updatedPage.Version?.Number ?? (currentVersion + 1),
                workspaceId);

            return new
            {
                success = true,
                pageId = updatedPage.Id,
                title = updatedPage.Title,
                url = pageUrl,
                version = updatedPage.Version?.Number ?? (currentVersion + 1),
                previousVersion = currentVersion,
                message = $"Successfully updated page '{updatedPage.Title}' to version {currentVersion + 1}"
            };
        }
        catch (IntegrationNotFoundException ex)
        {
            _logger.LogError(ex,
                "Integration not found for workspace {WorkspaceId}",
                workspaceId);

            return new
            {
                success = false,
                error = $"No Confluence integration found for workspace {workspaceId}",
                errorCode = "CONFLUENCE_INTEGRATION_NOT_FOUND"
            };
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex,
                "Invalid operation while updating Confluence page {PageId}: {ErrorMessage}",
                pageId,
                ex.Message);

            return new
            {
                success = false,
                error = ex.Message,
                errorCode = ex.Message.Contains("integrationId is required") ? "INTEGRATION_ID_REQUIRED" :
                           ex.Message.Contains("No active integration found for the supplied ID") ? "INTEGRATION_NOT_FOUND" :
                           ex.Message.Contains("not a Confluence integration") ? "INTEGRATION_WRONG_PROVIDER" :
                           "INVALID_OPERATION"
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "HTTP request failed while updating Confluence page {PageId} for workspace {WorkspaceId}",
                pageId,
                workspaceId);

            return new
            {
                success = false,
                error = $"Failed to communicate with Confluence: {ex.Message}",
                errorCode = "CONFLUENCE_NETWORK_ERROR"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error while updating Confluence page {PageId} for workspace {WorkspaceId}",
                pageId,
                workspaceId);

            return new
            {
                success = false,
                error = $"Unexpected error: {ex.Message}",
                errorCode = "UNEXPECTED_ERROR"
            };
        }
    }

    #region Helper Methods

    private async Task<List<object>> SearchByKeywordAsync(
        IConfluenceApiClient apiClient,
        Integration integration,
        string keyword,
        List<string> excludeContentIds,
        int remainingLimit)
    {
        var pages = new List<object>();

        try
        {
            // Build CQL query with keyword and exclusions
            var cql = string.IsNullOrEmpty(integration.FilterQuery)
                ? $"text ~ {keyword}"
                : $"{integration.FilterQuery} AND text ~ {keyword}";

            // Add exclusion clause if we have IDs to exclude
            if (excludeContentIds.Count > 0)
            {
                var excludeClause = $"id not in ({string.Join(", ", excludeContentIds)})";
                cql = $"{cql} AND {excludeClause}";
            }

            _logger.LogDebug("Executing Confluence search with CQL: {CQL}", cql);

            var searchResponse = await apiClient.SearchPagesAsync(cql, remainingLimit);

            if (searchResponse?.Results == null || searchResponse.Results.Count == 0)
            {
                _logger.LogDebug("No results found for keyword '{Keyword}'", keyword);
                return pages;
            }

            _logger.LogDebug(
                "Found {Count} Confluence pages for keyword '{Keyword}'",
                searchResponse.Results.Count,
                keyword);

            // Fetch full content for each page and convert to markdown
            foreach (var result in searchResponse.Results)
            {
                try
                {
                    // Fetch full page content with ADF format
                    if (string.IsNullOrEmpty(result.Id))
                    {
                        _logger.LogWarning("Search result has no ID");
                        continue;
                    }

                    var page = await apiClient.GetPageAsync(result.Id);

                    if (page == null)
                    {
                        _logger.LogWarning("Failed to fetch page {PageId}", result.Id);
                        continue;
                    }

                    // Convert ADF to markdown
                    var markdownContent = "";
                    if (page.Body?.AtlasDocFormat?.Value != null)
                    {
                        var adfString = page.Body.AtlasDocFormat.Value;
                        if (!string.IsNullOrEmpty(adfString))
                        {
                            try
                            {
                                var parsed = JsonSerializer.Deserialize<object>(adfString);
                                var adfElement = JsonSerializer.SerializeToElement(parsed);
                                markdownContent = await _adfConversionService.ConvertAdfToMarkdownAsync(adfElement, CancellationToken.None);
                            }
                            catch
                            {
                                _logger.LogWarning("Failed to parse ADF for page {PageId}", result.Id);
                            }
                        }
                    }

                    var baseUrl = integration.Url?.TrimEnd('/') ?? "";
                    var pageUrl = page.Links?.TryGetValue("webui", out var webui) == true
                        ? $"{baseUrl}{webui}"
                        : $"{baseUrl}/pages/{page.Id}";

                    pages.Add(new
                    {
                        id = page.Id,
                        title = page.Title,
                        content = markdownContent,
                        spaceKey = page.Space?.Key ?? "",
                        spaceName = page.Space?.Name ?? "",
                        url = pageUrl,
                        version = page.Version?.Number ?? 0
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error processing search result for page {PageId}: {Message}",
                        result.Id,
                        ex.Message);
                    // Continue with other pages
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during keyword search for '{Keyword}'", keyword);
            // Return what we have so far
        }

        return pages;
    }

    #endregion
}