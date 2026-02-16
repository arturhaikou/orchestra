# Integration Authorization Pattern

## Overview
All integration operations require workspace membership validation to enforce multi-tenancy isolation. This document describes the consistent authorization pattern used across all integration service methods and controller endpoints.

## Service Layer Pattern

### 1. Authorization Must Be First
Authorization checks MUST be the **first operation** in every service method, before any business logic or database queries (except loading the entity to get its workspace ID for update/delete operations).

### 2. Using EnsureUserIsMemberAsync

#### For Create Operations (Workspace ID from Request)
```csharp
public async Task<IntegrationDto> CreateIntegrationAsync(
    Guid userId, 
    CreateIntegrationRequest request, 
    CancellationToken cancellationToken = default)
{
    // 1. FIRST: Verify user has access to workspace
    await _workspaceAuthorizationService.EnsureUserIsMemberAsync(
        userId, 
        request.WorkspaceId, 
        cancellationToken);

    // 2. THEN: Proceed with business logic
    // ... validation, encryption, persistence
}
```

#### For List Operations (Workspace ID from Parameter)
```csharp
public async Task<List<IntegrationDto>> GetWorkspaceIntegrationsAsync(
    Guid userId, 
    Guid workspaceId, 
    CancellationToken cancellationToken = default)
{
    // 1. FIRST: Verify user has access to workspace
    await _workspaceAuthorizationService.EnsureUserIsMemberAsync(
        userId, 
        workspaceId, 
        cancellationToken);

    // 2. THEN: Retrieve integrations
    var integrations = await _integrationDataAccess.GetByWorkspaceIdAsync(
        workspaceId, 
        cancellationToken);

    // 3. Map and return
    return integrations.Select(MapToDto).ToList();
}
```

#### For Update/Delete Operations (Load Integration First)
```csharp
public async Task<IntegrationDto> UpdateIntegrationAsync(
    Guid userId, 
    Guid integrationId, 
    UpdateIntegrationRequest request, 
    CancellationToken cancellationToken = default)
{
    // 1. FIRST: Load integration to get workspace ID
    var integration = await _integrationDataAccess.GetByIdAsync(integrationId, cancellationToken)
        ?? throw new IntegrationNotFoundException(integrationId);

    // 2. SECOND: Verify user has access to the workspace
    await _workspaceAuthorizationService.EnsureUserIsMemberAsync(
        userId, 
        integration.WorkspaceId, 
        cancellationToken);

    // 3. THEN: Proceed with update logic
    // ... validation, update, persistence
}
```

### 3. Exception Behavior
- **Success:** Method returns normally, allowing business logic to proceed
- **Failure:** Throws `UnauthorizedWorkspaceAccessException` containing user ID and workspace ID
- **No partial execution:** Business logic never executes if authorization fails

## Controller Layer Pattern

### 1. Apply [Authorize] Attribute
All integration endpoints MUST have the `[Authorize]` attribute to enforce JWT authentication.

```csharp
[ApiController]
[Route("v1/integrations")]
[Authorize]  // Requires JWT authentication
public class IntegrationController : ControllerBase
{
    // ...
}
```

### 2. Exception Handling for 403 Forbidden
Controllers MUST catch `UnauthorizedWorkspaceAccessException` and return 403 Forbidden with a descriptive error message.

#### Example: GET Endpoint
```csharp
[HttpGet]
public async Task<IActionResult> GetWorkspaceIntegrations(
    [FromQuery] Guid workspaceId,
    CancellationToken cancellationToken)
{
    try
    {
        var userId = GetUserIdFromClaims();
        var integrations = await _integrationService.GetWorkspaceIntegrationsAsync(
            userId, 
            workspaceId, 
            cancellationToken);
        
        return Ok(integrations);
    }
    catch (UnauthorizedWorkspaceAccessException ex)
    {
        return StatusCode(403, new ErrorResponse(ex.Message));
    }
}
```

#### Example: POST Endpoint
```csharp
[HttpPost]
public async Task<IActionResult> CreateIntegration(
    [FromBody] CreateIntegrationRequest request,
    CancellationToken cancellationToken)
{
    try
    {
        var userId = GetUserIdFromClaims();
        var integration = await _integrationService.CreateIntegrationAsync(
            userId, 
            request, 
            cancellationToken);
        
        return CreatedAtAction(nameof(CreateIntegration), new { id = integration.Id }, integration);
    }
    catch (UnauthorizedWorkspaceAccessException ex)
    {
        return StatusCode(403, new ErrorResponse(ex.Message));
    }
    catch (DuplicateIntegrationException ex)
    {
        return Conflict(new ErrorResponse(ex.Message));
    }
    catch (ArgumentException ex)
    {
        return BadRequest(new ErrorResponse(ex.Message));
    }
}
```

#### Example: PUT/DELETE Endpoints
```csharp
[HttpPut("{id}")]
public async Task<IActionResult> UpdateIntegration(
    Guid id,
    [FromBody] UpdateIntegrationRequest request,
    CancellationToken cancellationToken)
{
    try
    {
        var userId = GetUserIdFromClaims();
        var integration = await _integrationService.UpdateIntegrationAsync(
            userId, 
            id, 
            request, 
            cancellationToken);
        
        return Ok(integration);
    }
    catch (IntegrationNotFoundException ex)
    {
        return NotFound(new ErrorResponse(ex.Message));
    }
    catch (UnauthorizedWorkspaceAccessException ex)
    {
        return StatusCode(403, new ErrorResponse(ex.Message));
    }
    catch (DuplicateIntegrationException ex)
    {
        return Conflict(new ErrorResponse(ex.Message));
    }
    catch (ArgumentException ex)
    {
        return BadRequest(new ErrorResponse(ex.Message));
    }
}

[HttpDelete("{id}")]
public async Task<IActionResult> DeleteIntegration(
    Guid id,
    CancellationToken cancellationToken)
{
    try
    {
        var userId = GetUserIdFromClaims();
        await _integrationService.DeleteIntegrationAsync(userId, id, cancellationToken);
        return NoContent();
    }
    catch (IntegrationNotFoundException ex)
    {
        return NotFound(new ErrorResponse(ex.Message));
    }
    catch (UnauthorizedWorkspaceAccessException ex)
    {
        return StatusCode(403, new ErrorResponse(ex.Message));
    }
}
```

### 3. Extract User ID from JWT Claims
All endpoints must extract the user ID from JWT claims:

```csharp
private Guid GetUserIdFromClaims()
{
    var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? throw new UnauthorizedAccessException("User ID not found in token claims.");
    return Guid.Parse(userIdClaim);
}
```

## HTTP Status Code Mapping

| Exception                              | HTTP Status | Description                          |
|----------------------------------------|-------------|--------------------------------------|
| UnauthorizedWorkspaceAccessException   | 403         | User not a member of workspace       |
| IntegrationNotFoundException           | 404         | Integration does not exist           |
| DuplicateIntegrationException          | 409         | Integration name already exists      |
| ArgumentException                      | 400         | Invalid input data                   |
| UnauthorizedAccessException (JWT)      | 401         | JWT missing or invalid               |

## Security Guarantees

1. **JWT Authentication:** All endpoints require valid JWT tokens ([Authorize] attribute)
2. **Workspace Isolation:** Users can only access integrations in workspaces they are members of
3. **Early Authorization:** Authorization checks occur before any business logic
4. **Fail-Safe:** Authorization failures prevent all subsequent operations
5. **Clear Error Messages:** 403 responses include descriptive error messages

## Testing Checklist

- [ ] Verify 401 Unauthorized when JWT is missing
- [ ] Verify 403 Forbidden when user is not a workspace member
- [ ] Verify 200/201/204 success when user is a workspace member
- [ ] Verify authorization check occurs before database modifications
- [ ] Verify error messages do not leak sensitive information

## Related Documentation
- [FR-006 Design Document](../fr-006/design.md)
- [FR-001: List Workspace Integrations](../fr-001/fr.md)
- [FR-002: Create Integration](../fr-002/fr.md)
- [FR-003: Update Integration](../fr-003/fr.md)
- [FR-004: Delete Integration](../fr-004/fr.md)