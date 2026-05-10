---
name: aspire-development
description: IMPLEMENTATION SKILL — Add integrations, configure orchestration, or troubleshoot .NET Aspire distributed apps. USE FOR: adding database/messaging/AI/Azure NuGet integrations; configuring AppHost and service references; wiring service discovery; managing container resources; diagnosing Aspire startup or network errors. DO NOT USE FOR: general .NET coding (use default agent); non-Aspire Azure deployments. INVOKES: NuGet MCP tools to discover up-to-date package names and versions before every implementation.
compatibility: Requires .NET Aspire workload, Aspire CLI, and network access for NuGet packages
---

# .NET Aspire Development

This skill provides comprehensive guidance for working with .NET Aspire distributed applications, including integration discovery, implementation patterns, and troubleshooting.

## When to Use This Skill

- **Adding integrations**: Databases, messaging systems, AI services, Azure resources
- **Orchestrating services**: Configuring AppHost, managing service references
- **Service discovery**: Implementing inter-service communication
- **Container management**: Working with Docker containers in Aspire
- **Troubleshooting**: Diagnosing Aspire application issues

## Critical Principle: Fresh Information First

**⚠️ LLM training data for Aspire is outdated.** The framework evolves rapidly with new integrations, breaking changes, and updated patterns.

### Mandatory Pre-Implementation Checklist

Before implementing ANY Aspire integration:

1. ✅ Query `mcp_aspire_list_integrations` to discover available integrations
2. ✅ Call `mcp_aspire_get_integration_docs(packageId, version)` for specific package documentation
3. ✅ Verify exact package IDs and versions from MCP responses
4. ✅ Review NuGet documentation URLs provided in MCP responses
5. ❌ **NEVER** rely on cached LLM knowledge for package names, versions, or patterns
6. ❌ **NEVER** guess or assume integration details

### Tool Selection Rules

**For Aspire-Specific Queries** (integrations, packages, AppHost patterns):
- `mcp_aspire_list_integrations` - List all available Aspire hosting integrations
- `mcp_aspire_get_integration_docs(packageId, version)` - Get specific integration documentation

## Standard Workflow

### Phase 1: Discovery & Planning

1. **Identify Requirements**
   - Understand what integration or service is needed
   - Clarify configuration requirements

2. **Query Available Integrations**
   ```
   mcp_aspire_list_integrations
   → Review results, note package IDs
   → Filter by category (Database, Messaging, AI, etc.)
   ```

3. **Fetch Integration Documentation**
   ```
   mcp_aspire_get_integration_docs(packageId, version)
   → Review NuGet documentation URL
   → Verify usage patterns
   → Check prerequisites and dependencies
   ```

4. **Validate Compatibility**
   - Check version compatibility with existing packages
   - Review configuration requirements
   - Identify client packages needed

### Phase 2: Implementation

1. **Add Package to AppHost**
   ```powershell
   cd unifiedaitracker.AppHost
   dotnet add package <PackageId> --version <Version>
   ```

2. **Configure in AppHost.cs**
   ```csharp
   // Follow exact patterns from MCP documentation
   var resource = builder.Add<Integration>("resource-name")
       .WithConfiguration()
       .WithHealthCheck();
   ```

3. **Add Service References**
   ```csharp
   builder.AddProject<Projects.ApiService>("api")
       .WithReference(resource);
   ```

4. **Configure Client Services**
   - Add client NuGet packages to consuming projects
   - Configure dependency injection
   - Set up connection handling

### Phase 3: Validation

1. **Build Solution**
   ```powershell
   dotnet build unifiedaitracker.slnx
   ```

## Quick Reference: Common Operations

### Service Discovery Pattern

```csharp
// In AppHost.cs - Define services with names
var api = builder.AddProject<Projects.ApiService>("api");
var service = builder.AddProject<Projects.Worker>("worker")
    .WithReference(api);  // 'worker' can discover 'api'

// In consuming service - Use service name as HTTP client base address
builder.Services.AddHttpClient<IMyService, MyService>(client =>
{
    client.BaseAddress = new Uri("http://api");  // Matches AppHost name
});
```

## Integration Verification Checklist

Before considering an integration complete:

- [ ] Package added with exact version from MCP
- [ ] AppHost.cs configuration matches official documentation
- [ ] Consumer services have client packages installed
- [ ] Service references configured with `WithReference()`
- [ ] Connection strings/endpoints named correctly
- [ ] Health checks configured (for containers)
- [ ] Container starts successfully in Aspire Dashboard
- [ ] Service discovery working (if applicable)
- [ ] Logs show successful connection
- [ ] No errors in Aspire Dashboard traces


## Best Practices

1. **Always use MCP tools** for integration discovery - never rely on cached knowledge
2. **Use exact versions** from MCP responses when adding packages
3. **Configure health checks** for all container-based integrations
4. **Name resources consistently** between AppHost and consuming services
5. **Use service discovery** instead of hardcoded endpoints
6. **Monitor Aspire Dashboard** during development for immediate feedback
7. **Enable telemetry** to debug distributed application issues
8. **Persist data volumes** for databases to avoid data loss during restarts

## Additional Resources

- **NuGet Package Pages**: Official documentation URLs provided by MCP tools

---

**Remember**: The cornerstone of successful Aspire development is using MCP tools (`mcp_aspire_list_integrations`, `mcp_aspire_get_integration_docs`) to get current, accurate information before every implementation.
