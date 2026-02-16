using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Domain.Interfaces;
using Orchestra.Infrastructure.Tools.Models.Confluence;

namespace Orchestra.Infrastructure.Tools.Services;

public class ConfluenceToolService : IConfluenceToolService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ICredentialEncryptionService _credentialEncryptionService;
    private readonly IIntegrationDataAccess _integrationDataAccess;
    private readonly IAdfConversionService _adfConversionService;
    private readonly ILogger<ConfluenceToolService> _logger;

    public ConfluenceToolService(
        IHttpClientFactory httpClientFactory,
        ICredentialEncryptionService credentialEncryptionService,
        IIntegrationDataAccess integrationDataAccess,
        IAdfConversionService adfConversionService,
        ILogger<ConfluenceToolService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _credentialEncryptionService = credentialEncryptionService;
        _integrationDataAccess = integrationDataAccess;
        _adfConversionService = adfConversionService;
        _logger = logger;
    }

    public async Task<object> SearchAsync(string workspaceId, string query, int limit = 10)
    {
        try
        {
            _logger.LogInformation(
                "Searching Confluence content in workspace {WorkspaceId} with query: '{Query}', limit: {Limit}",
                workspaceId,
                query,
                limit);

            // Validate inputs
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

            // Step 1: Load and validate integration
            var integration = await GetAndValidateIntegrationAsync(Guid.Parse(workspaceId));

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
                errorCode = ex.Message.Contains("not active") ? "INTEGRATION_INACTIVE" :
                           ex.Message.Contains("not a CONFLUENCE provider") ? "INTEGRATION_WRONG_PROVIDER" :
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

    public async Task<object> GetPageAsync(string workspaceId, string pageId)
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
            var integration = await GetAndValidateIntegrationAsync(workspaceGuid);

            // Step 2: Fetch page content
            using var client = GetHttpClient(integration);
            var pageUrl = $"wiki/rest/api/content/{pageId}?expand=body.atlas_doc_format,version,space";

            _logger.LogDebug("Fetching Confluence page: {PageUrl}", pageUrl);

            var response = await client.GetAsync(pageUrl);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "Failed to fetch Confluence page {PageId} with status {StatusCode}: {ErrorContent}",
                    pageId,
                    response.StatusCode,
                    errorContent);

                var errorCode = response.StatusCode switch
                {
                    System.Net.HttpStatusCode.NotFound => "PAGE_NOT_FOUND",
                    System.Net.HttpStatusCode.Forbidden => "CONFLUENCE_FORBIDDEN",
                    System.Net.HttpStatusCode.Unauthorized => "CONFLUENCE_AUTH_FAILED",
                    _ => "CONFLUENCE_API_ERROR"
                };

                return new
                {
                    success = false,
                    error = $"Confluence API returned status {response.StatusCode}",
                    errorCode = errorCode
                };
            }

            var page = await response.Content.ReadFromJsonAsync<ConfluencePageResponse>();
            if (page == null)
            {
                return new
                {
                    success = false,
                    error = "Failed to parse Confluence page response",
                    errorCode = "PARSE_ERROR"
                };
            }

            // Step 3: Convert ADF to markdown
            var markdownContent = "";
            if (page.Body?.AtlasDocFormat?.Value != null)
            {
                var adfElement = page.Body.AtlasDocFormat.Value;
                // Handle case where Value is a JSON string that needs parsing
                if (adfElement.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    var adfString = adfElement.GetString();
                    if (!string.IsNullOrEmpty(adfString))
                    {
                        adfElement = JsonSerializer.SerializeToElement(JsonSerializer.Deserialize<object>(adfString));
                    }
                }
                markdownContent = await _adfConversionService.ConvertAdfToMarkdownAsync(adfElement, CancellationToken.None);
            }
            else
            {
                _logger.LogWarning(
                    "Page {PageId} does not have ADF content available",
                    pageId);
            }

            var baseUrl = integration.Url?.TrimEnd('/') ?? "";
            var pageUrl_web = page.Links?.WebUi != null ? $"{baseUrl}{page.Links.WebUi}" : $"{baseUrl}/pages/{page.Id}";

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
                    url = pageUrl_web,
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
                errorCode = ex.Message.Contains("not active") ? "INTEGRATION_INACTIVE" :
                           ex.Message.Contains("not a CONFLUENCE provider") ? "INTEGRATION_WRONG_PROVIDER" :
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
            var integration = await GetAndValidateIntegrationAsync(workspaceGuid);

            // Step 2: Convert markdown to ADF
            var adfJson = await _adfConversionService.ConvertMarkdownToAdfAsync(content, CancellationToken.None);

            // Step 3: Prepare request
            var createRequest = new CreateConfluencePageRequest
            {
                Type = "page",
                Title = title,
                Space = new CreateConfluenceSpace { Key = spaceKey },
                Body = new ConfluenceBody
                {
                    AtlasDocFormat = new ConfluenceContent
                    {
                        Value = adfJson,
                        Representation = "atlas_doc_format"
                    }
                }
            };

            // Add parent page if specified
            if (!string.IsNullOrWhiteSpace(parentPageId))
            {
                createRequest.Ancestors = new List<ConfluenceAncestor>
                {
                    new ConfluenceAncestor { Id = parentPageId }
                };
            }

            // Step 4: Create the page
            using var client = GetHttpClient(integration);
            var response = await client.PostAsJsonAsync("wiki/rest/api/content", createRequest);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "Failed to create Confluence page with status {StatusCode}: {ErrorContent}",
                    response.StatusCode,
                    errorContent);

                var errorMessage = response.StatusCode switch
                {
                    System.Net.HttpStatusCode.BadRequest => "Invalid request data. Check space key, title, and content.",
                    System.Net.HttpStatusCode.NotFound => "Space or parent page not found.",
                    System.Net.HttpStatusCode.Forbidden => "No permission to create page in this space.",
                    System.Net.HttpStatusCode.Unauthorized => "Authentication failed.",
                    _ => $"Confluence API returned status {response.StatusCode}"
                };

                return new
                {
                    success = false,
                    error = errorMessage,
                    errorCode = "CONFLUENCE_CREATE_FAILED",
                    details = errorContent
                };
            }

            var createdPage = await response.Content.ReadFromJsonAsync<ConfluencePageCreateUpdateResponse>();
            if (createdPage == null)
            {
                return new
                {
                    success = false,
                    error = "Failed to parse Confluence create page response",
                    errorCode = "PARSE_ERROR"
                };
            }

            var baseUrl = integration.Url?.TrimEnd('/') ?? "";
            var pageUrl = createdPage.Links?.WebUi != null ? $"{baseUrl}{createdPage.Links.WebUi}" : $"{baseUrl}/pages/{createdPage.Id}";

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
                errorCode = ex.Message.Contains("not active") ? "INTEGRATION_INACTIVE" :
                           ex.Message.Contains("not a CONFLUENCE provider") ? "INTEGRATION_WRONG_PROVIDER" :
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
            var integration = await GetAndValidateIntegrationAsync(workspaceGuid);

            // Step 2: Get current page to retrieve version number (required by Confluence)
            using var client = GetHttpClient(integration);
            var currentPageUrl = $"wiki/rest/api/content/{pageId}?expand=version";
            
            _logger.LogDebug("Fetching current Confluence page version: {PageUrl}", currentPageUrl);
            
            var currentResponse = await client.GetAsync(currentPageUrl);
            if (!currentResponse.IsSuccessStatusCode)
            {
                var errorContent = await currentResponse.Content.ReadAsStringAsync();
                _logger.LogError(
                    "Failed to fetch current page {PageId} with status {StatusCode}: {ErrorContent}",
                    pageId,
                    currentResponse.StatusCode,
                    errorContent);

                var errorCode = currentResponse.StatusCode switch
                {
                    System.Net.HttpStatusCode.NotFound => "PAGE_NOT_FOUND",
                    System.Net.HttpStatusCode.Forbidden => "CONFLUENCE_FORBIDDEN",
                    System.Net.HttpStatusCode.Unauthorized => "CONFLUENCE_AUTH_FAILED",
                    _ => "CONFLUENCE_API_ERROR"
                };

                return new
                {
                    success = false,
                    error = $"Failed to fetch current page: {currentResponse.StatusCode}",
                    errorCode = errorCode
                };
            }

            var currentPage = await currentResponse.Content.ReadFromJsonAsync<ConfluencePageResponse>();
            if (currentPage == null || currentPage.Version == null)
            {
                return new
                {
                    success = false,
                    error = "Failed to parse current page or version not available",
                    errorCode = "PARSE_ERROR"
                };
            }

            var currentVersion = currentPage.Version.Number;
            var newVersion = currentVersion + 1;

            _logger.LogDebug(
                "Current page version: {CurrentVersion}, new version will be: {NewVersion}",
                currentVersion,
                newVersion);

            // Step 3: Prepare update request
            var updateRequest = new UpdateConfluencePageRequest
            {
                Type = "page",
                Title = title ?? currentPage.Title,
                Version = new ConfluenceVersionUpdate
                {
                    Number = newVersion,
                    Message = "Updated via Orchestra"
                }
            };

            // Convert markdown to ADF if content is provided
            if (!string.IsNullOrWhiteSpace(content))
            {
                var adfJson = await _adfConversionService.ConvertMarkdownToAdfAsync(content, CancellationToken.None);
                updateRequest.Body = new ConfluenceBody
                {
                    AtlasDocFormat = new ConfluenceContent
                    {
                        Value = adfJson,
                        Representation = "atlas_doc_format"
                    }
                };
            }

            // Step 4: Update the page
            var updateUrl = $"wiki/rest/api/content/{pageId}";
            var response = await client.PutAsJsonAsync(updateUrl, updateRequest);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "Failed to update Confluence page {PageId} with status {StatusCode}: {ErrorContent}",
                    pageId,
                    response.StatusCode,
                    errorContent);

                var errorMessage = response.StatusCode switch
                {
                    System.Net.HttpStatusCode.BadRequest => "Invalid request data. Check title and content.",
                    System.Net.HttpStatusCode.NotFound => "Page not found.",
                    System.Net.HttpStatusCode.Conflict => "Page has been updated by someone else. Version conflict.",
                    System.Net.HttpStatusCode.Forbidden => "No permission to update this page.",
                    System.Net.HttpStatusCode.Unauthorized => "Authentication failed.",
                    _ => $"Confluence API returned status {response.StatusCode}"
                };

                return new
                {
                    success = false,
                    error = errorMessage,
                    errorCode = "CONFLUENCE_UPDATE_FAILED",
                    details = errorContent
                };
            }

            var updatedPage = await response.Content.ReadFromJsonAsync<ConfluencePageCreateUpdateResponse>();
            if (updatedPage == null)
            {
                return new
                {
                    success = false,
                    error = "Failed to parse Confluence update page response",
                    errorCode = "PARSE_ERROR"
                };
            }

            var baseUrl = integration.Url?.TrimEnd('/') ?? "";
            var pageUrl = updatedPage.Links?.WebUi != null ? $"{baseUrl}{updatedPage.Links.WebUi}" : $"{baseUrl}/pages/{updatedPage.Id}";

            _logger.LogInformation(
                "Successfully updated Confluence page {PageId} ('{Title}') to version {Version} for workspace {WorkspaceId}",
                updatedPage.Id,
                updatedPage.Title,
                updatedPage.Version?.Number ?? newVersion,
                workspaceId);

            return new
            {
                success = true,
                pageId = updatedPage.Id,
                title = updatedPage.Title,
                url = pageUrl,
                version = updatedPage.Version?.Number ?? newVersion,
                previousVersion = currentVersion,
                message = $"Successfully updated page '{updatedPage.Title}' to version {newVersion}"
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
                errorCode = ex.Message.Contains("not active") ? "INTEGRATION_INACTIVE" :
                           ex.Message.Contains("not a CONFLUENCE provider") ? "INTEGRATION_WRONG_PROVIDER" :
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
        Integration integration,
        string keyword,
        List<string> excludeContentIds,
        int remainingLimit)
    {
        var pages = new List<object>();

        using var client = GetHttpClient(integration);
        
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

        var encodedQuery = HttpUtility.ParseQueryString(string.Empty);
        encodedQuery["cql"] = cql;
        encodedQuery["limit"] = remainingLimit.ToString();
        encodedQuery["type"] = "page";
        var searchUrl = $"wiki/rest/api/content/search?{encodedQuery}";

        _logger.LogDebug("Executing Confluence search: {SearchUrl}", searchUrl);

        var response = await client.GetAsync(searchUrl);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "Confluence search failed for keyword '{Keyword}' with status {StatusCode}: {ErrorContent}",
                keyword,
                response.StatusCode,
                errorContent);
            
            // Return empty list to continue with other keywords
            return pages;
        }

        var searchResponse = await response.Content.ReadFromJsonAsync<ConfluenceSearchResponse>();
        if (searchResponse == null || searchResponse.Results == null)
        {
            _logger.LogWarning("Failed to parse Confluence search response for keyword '{Keyword}'", keyword);
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
                var pageUrl = $"wiki/rest/api/content/{result.Id}?expand=body.atlas_doc_format,version,space";
                var pageResponse = await client.GetAsync(pageUrl);
                
                if (!pageResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Failed to fetch page {PageId}: {StatusCode}",
                        result.Id,
                        pageResponse.StatusCode);
                    continue;
                }

                var test = await pageResponse.Content.ReadAsStringAsync();
                var page = JsonSerializer.Deserialize<ConfluencePageResponse>(test);
                //var page = await pageResponse.Content.ReadFromJsonAsync<ConfluencePageResponse>();
                if (page == null)
                {
                    _logger.LogWarning("Failed to parse page {PageId}", result.Id);
                    continue;
                }

                // Convert ADF to markdown
                var markdownContent = "";
                if (page.Body?.AtlasDocFormat?.Value != null)
                {
                    var adfElement = page.Body.AtlasDocFormat.Value;
                    // Handle case where Value is a JSON string that needs parsing
                    if (adfElement.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        var adfString = adfElement.GetString();
                        if (!string.IsNullOrEmpty(adfString))
                        {
                            adfElement = JsonSerializer.SerializeToElement(JsonSerializer.Deserialize<object>(adfString));
                        }
                    }
                    markdownContent = await _adfConversionService.ConvertAdfToMarkdownAsync(adfElement, CancellationToken.None);
                }

                var baseUrl = integration.Url?.TrimEnd('/') ?? "";
                var pageUrl_web = page.Links?.WebUi != null ? $"{baseUrl}{page.Links.WebUi}" : $"{baseUrl}/pages/{page.Id}";

                pages.Add(new
                {
                    id = page.Id,
                    title = page.Title,
                    content = markdownContent,
                    spaceKey = page.Space?.Key ?? "",
                    spaceName = page.Space?.Name ?? "",
                    url = pageUrl_web,
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

        return pages;
    }

    private HttpClient GetHttpClient(Integration integration)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();

            if (string.IsNullOrEmpty(integration.Url))
            {
                throw new ArgumentException("Integration URL is required for Confluence API calls.", nameof(integration.Url));
            }
            
            if (string.IsNullOrEmpty(integration.EncryptedApiKey))
            {
                throw new ArgumentException("Integration encrypted API key is required for Confluence API calls.", nameof(integration.EncryptedApiKey));
            }
            
            client.BaseAddress = new Uri(integration.Url);
            
            // Decrypt API key (format: "email:apiToken" for Confluence Cloud)
            var apiKey = _credentialEncryptionService.Decrypt(integration.EncryptedApiKey);
            
            // Set Basic Auth header
            var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{integration.Username}:{apiKey}"));
            client.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Basic", authValue);
            
            return client;
        }
        catch (UriFormatException ex)
        {
            _logger.LogError(ex, "Invalid URL format for Confluence integration '{IntegrationName}': '{Url}'", 
                integration.Name, integration.Url);
            throw new ArgumentException("Invalid integration URL format.", nameof(integration.Url), ex);
        }
        catch (Exception ex) when (ex is ArgumentException || ex is CryptographicException)
        {
            _logger.LogError(ex, "Failed to configure HttpClient for Confluence integration '{IntegrationName}'", 
                integration.Name);
            throw;
        }
    }

    private async Task<Integration> GetAndValidateIntegrationAsync(
        Guid workspaceId, 
        CancellationToken cancellationToken = default)
    {
        var integrations = await _integrationDataAccess.GetByWorkspaceIdAsync(workspaceId, cancellationToken);
        var integration = integrations.FirstOrDefault(i => i.Provider == ProviderType.CONFLUENCE);
        
        if (integration == null)
        {
            _logger.LogError(
                "No active Confluence integration found for workspace {WorkspaceId}", 
                workspaceId);
            throw new InvalidOperationException(
                $"No active Confluence integration found for workspace {workspaceId}");
        }

        return integration;
    }

    #endregion
}