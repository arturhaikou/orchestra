using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Workspaces.DTOs;

namespace Orchestra.ApiService.Controllers;

/// <summary>
/// Provides workspace-agnostic, stateless operations scoped to AI provider surfaces.
/// </summary>
[ApiController]
[Route("v1/provider")]
[Authorize]
public sealed class ProviderController : ControllerBase
{
    private readonly IAzureOpenAIModelDiscoveryService _modelDiscoveryService;
    private readonly IOllamaModelDiscoveryService _ollamaDiscoveryService;

    public ProviderController(
        IAzureOpenAIModelDiscoveryService modelDiscoveryService,
        IOllamaModelDiscoveryService ollamaDiscoveryService)
    {
        _modelDiscoveryService = modelDiscoveryService;
        _ollamaDiscoveryService = ollamaDiscoveryService;
    }

    /// <summary>
    /// Validates caller-supplied Azure OpenAI credentials and returns the list of
    /// available model deployments. This endpoint is stateless — no workspace record
    /// is created, read, or written.
    /// </summary>
    /// <param name="request">Body containing the Azure OpenAI <c>endpoint</c> and <c>apiKey</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A flat, ordered list of deployment name strings.</returns>
    /// <response code="200">Credentials accepted. Returns the deployment list (may be empty).</response>
    /// <response code="400">A required field is missing, or Azure rejected the supplied credentials.</response>
    /// <response code="401">The request carries no valid JWT bearer token.</response>
    [HttpPost("azure/models")]
    [ProducesResponseType(typeof(AIModelsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DiscoverAzureModels(
        [FromBody] DiscoverAzureModelsRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Endpoint))
        {
            return BadRequest(new { error = "endpoint is required." });
        }

        if (string.IsNullOrWhiteSpace(request.ApiKey))
        {
            return BadRequest(new { error = "apiKey is required." });
        }

        try
        {
            var models = await _modelDiscoveryService.DiscoverModelsAsync(
                request.Endpoint,
                request.ApiKey,
                cancellationToken);

            return Ok(new AIModelsResponse(models));
        }
        catch (AIProviderCommunicationException)
        {
            // The raw exception message must NOT be forwarded — it could contain
            // diagnostic detail or partial credential information.
            // Return a fixed, sanitised message. The supplied apiKey must never
            // appear in the response body.
            return BadRequest(new
            {
                error = "Could not connect to the Azure OpenAI endpoint or the supplied credentials were rejected. Verify the endpoint URL and API key."
            });
        }
    }

    /// <summary>
    /// Probes the supplied Ollama server URL and returns the list of installed model
    /// identifiers. This endpoint is stateless — no workspace record is created, read,
    /// or written.
    /// </summary>
    /// <param name="request">Body containing the Ollama server <c>endpoint</c> URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// HTTP 200 with a structured <see cref="OllamaDiscoveryResult"/> in all cases where
    /// the request is valid. Connectivity failures are encoded in the payload
    /// (<c>isValid: false</c>) rather than mapped to error HTTP status codes.
    /// </returns>
    /// <response code="200">Request processed. Check <c>isValid</c> in the response body for connectivity outcome.</response>
    /// <response code="400">The <c>endpoint</c> field is missing or blank.</response>
    /// <response code="401">The request carries no valid JWT bearer token.</response>
    [HttpPost("ollama/models")]
    [ProducesResponseType(typeof(OllamaDiscoveryResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DiscoverOllamaModels(
        [FromBody] DiscoverOllamaModelsRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Endpoint))
        {
            return BadRequest(new { error = "endpoint is required." });
        }

        var result = await _ollamaDiscoveryService.DiscoverModelsAsync(
            request.Endpoint,
            cancellationToken);

        return Ok(result);
    }


}
