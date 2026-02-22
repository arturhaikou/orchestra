using Bogus;
using Orchestra.Application.Auth.DTOs;
using Orchestra.Application.Agents.DTOs;
using Orchestra.Application.Tickets.DTOs;
using Orchestra.Application.Integrations.DTOs;
using Orchestra.Domain.Enums;

namespace Orchestra.Application.Tests.Builders;

// ============================================================================
// Auth DTOs
// ============================================================================

/// <summary>
/// Builder for LoginRequest DTOs.
/// </summary>
public class LoginRequestBuilder
{
    private string _email = new Faker().Internet.Email();
    private string _password = "ValidPassword123!";

    /// <summary>
    /// Sets the email.
    /// </summary>
    public LoginRequestBuilder WithEmail(string email)
    {
        _email = email;
        return this;
    }

    /// <summary>
    /// Sets the password.
    /// </summary>
    public LoginRequestBuilder WithPassword(string password)
    {
        _password = password;
        return this;
    }

    /// <summary>
    /// Builds the LoginRequest.
    /// </summary>
    public LoginRequest Build()
    {
        return new LoginRequest(_email, _password);
    }
}

/// <summary>
/// Builder for RegisterRequest DTOs.
/// </summary>
public class RegisterRequestBuilder
{
    private string _email = new Faker().Internet.Email();
    private string _name = new Faker().Name.FullName();
    private string _password = "ValidPassword123!";

    /// <summary>
    /// Sets the email.
    /// </summary>
    public RegisterRequestBuilder WithEmail(string email)
    {
        _email = email;
        return this;
    }

    /// <summary>
    /// Sets the name.
    /// </summary>
    public RegisterRequestBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    /// <summary>
    /// Sets the password.
    /// </summary>
    public RegisterRequestBuilder WithPassword(string password)
    {
        _password = password;
        return this;
    }

    /// <summary>
    /// Builds the RegisterRequest.
    /// </summary>
    public RegisterRequest Build()
    {
        return new RegisterRequest(_email, _password, _name);
    }
}

/// <summary>
/// Builder for UpdateProfileRequest DTOs.
/// </summary>
public class UpdateProfileRequestBuilder
{
    private string _email = new Faker().Internet.Email();
    private string _name = new Faker().Name.FullName();

    /// <summary>
    /// Sets the email.
    /// </summary>
    public UpdateProfileRequestBuilder WithEmail(string email)
    {
        _email = email;
        return this;
    }

    /// <summary>
    /// Sets the name.
    /// </summary>
    public UpdateProfileRequestBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    /// <summary>
    /// Builds the UpdateProfileRequest.
    /// </summary>
    public UpdateProfileRequest Build()
    {
        return new UpdateProfileRequest(_email, _name);
    }
}

/// <summary>
/// Builder for ChangePasswordRequest DTOs.
/// </summary>
public class ChangePasswordRequestBuilder
{
    private string _currentPassword = "CurrentPassword123!";
    private string _newPassword = "NewPassword456!";

    /// <summary>
    /// Sets the current password.
    /// </summary>
    public ChangePasswordRequestBuilder WithCurrentPassword(string password)
    {
        _currentPassword = password;
        return this;
    }

    /// <summary>
    /// Sets the new password.
    /// </summary>
    public ChangePasswordRequestBuilder WithNewPassword(string password)
    {
        _newPassword = password;
        return this;
    }

    /// <summary>
    /// Builds the ChangePasswordRequest.
    /// </summary>
    public ChangePasswordRequest Build()
    {
        return new ChangePasswordRequest(_currentPassword, _newPassword);
    }
}

// ============================================================================
// Agent DTOs
// ============================================================================

/// <summary>
/// Builder for CreateAgentRequest DTOs.
/// </summary>
public class CreateAgentRequestBuilder
{
    private Guid _workspaceId = Guid.NewGuid();
    private string _name = new Faker().Name.FirstName();
    private string _role = new Faker().Lorem.Word();
    private string[] _capabilities = new[] { "code_execution", "document_analysis" };
    private string[]? _toolActionIds;
    private string _customInstructions = new Faker().Lorem.Sentence(10);

    /// <summary>
    /// Sets the workspace ID.
    /// </summary>
    public CreateAgentRequestBuilder WithWorkspaceId(Guid workspaceId)
    {
        _workspaceId = workspaceId;
        return this;
    }

    /// <summary>
    /// Sets the agent name.
    /// </summary>
    public CreateAgentRequestBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    /// <summary>
    /// Sets the agent role.
    /// </summary>
    public CreateAgentRequestBuilder WithRole(string role)
    {
        _role = role;
        return this;
    }

    /// <summary>
    /// Sets the capabilities.
    /// </summary>
    public CreateAgentRequestBuilder WithCapabilities(params string[] capabilities)
    {
        _capabilities = capabilities;
        return this;
    }

    /// <summary>
    /// Sets the tool action IDs.
    /// </summary>
    public CreateAgentRequestBuilder WithToolActionIds(params string[] toolActionIds)
    {
        _toolActionIds = toolActionIds;
        return this;
    }

    /// <summary>
    /// Sets the custom instructions.
    /// </summary>
    public CreateAgentRequestBuilder WithCustomInstructions(string instructions)
    {
        _customInstructions = instructions;
        return this;
    }

    /// <summary>
    /// Builds the CreateAgentRequest.
    /// </summary>
    public CreateAgentRequest Build()
    {
        return new CreateAgentRequest(_workspaceId, _name, _role, _capabilities, _toolActionIds, _customInstructions);
    }
}

/// <summary>
/// Builder for UpdateAgentRequest DTOs.
/// </summary>
public class UpdateAgentRequestBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _name = new Faker().Name.FirstName();
    private string _role = new Faker().Lorem.Word();
    private string[] _capabilities = new[] { "code_execution" };
    private string[]? _toolActionIds;
    private string _customInstructions = new Faker().Lorem.Sentence(10);

    /// <summary>
    /// Sets the agent ID.
    /// </summary>
    public UpdateAgentRequestBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    /// <summary>
    /// Sets the agent name.
    /// </summary>
    public UpdateAgentRequestBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    /// <summary>
    /// Sets the agent role.
    /// </summary>
    public UpdateAgentRequestBuilder WithRole(string role)
    {
        _role = role;
        return this;
    }

    /// <summary>
    /// Sets the capabilities.
    /// </summary>
    public UpdateAgentRequestBuilder WithCapabilities(params string[] capabilities)
    {
        _capabilities = capabilities;
        return this;
    }

    /// <summary>
    /// Sets the tool action IDs.
    /// </summary>
    public UpdateAgentRequestBuilder WithToolActionIds(params string[] toolActionIds)
    {
        _toolActionIds = toolActionIds;
        return this;
    }

    /// <summary>
    /// Sets the custom instructions.
    /// </summary>
    public UpdateAgentRequestBuilder WithCustomInstructions(string instructions)
    {
        _customInstructions = instructions;
        return this;
    }

    /// <summary>
    /// Builds the UpdateAgentRequest.
    /// </summary>
    public UpdateAgentRequest Build()
    {
        return new UpdateAgentRequest(_name, _role, _capabilities, _toolActionIds, _customInstructions);
    }
}

// ============================================================================
// Ticket DTOs
// ============================================================================

/// <summary>
/// Builder for CreateTicketRequest DTOs.
/// </summary>
public class CreateTicketRequestBuilder
{
    private Guid _workspaceId = Guid.NewGuid();
    private string _title = new Faker().Lorem.Sentence();
    private string _description = new Faker().Lorem.Paragraph();
    private Guid _statusId = Guid.NewGuid();
    private Guid _priorityId = Guid.NewGuid();
    private bool _internal = true;
    private Guid? _assignedAgentId;
    private Guid? _assignedWorkflowId;

    /// <summary>
    /// Sets the workspace ID.
    /// </summary>
    public CreateTicketRequestBuilder WithWorkspaceId(Guid workspaceId)
    {
        _workspaceId = workspaceId;
        return this;
    }

    /// <summary>
    /// Sets the title.
    /// </summary>
    public CreateTicketRequestBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    /// <summary>
    /// Sets the description.
    /// </summary>
    public CreateTicketRequestBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    /// <summary>
    /// Sets the status ID.
    /// </summary>
    public CreateTicketRequestBuilder WithStatusId(Guid statusId)
    {
        _statusId = statusId;
        return this;
    }

    /// <summary>
    /// Sets the priority ID.
    /// </summary>
    public CreateTicketRequestBuilder WithPriorityId(Guid priorityId)
    {
        _priorityId = priorityId;
        return this;
    }

    /// <summary>
    /// Sets whether the ticket is internal.
    /// </summary>
    public CreateTicketRequestBuilder AsInternal(bool internal_ = true)
    {
        _internal = internal_;
        return this;
    }

    /// <summary>
    /// Sets the assigned agent.
    /// </summary>
    public CreateTicketRequestBuilder WithAssignedAgent(Guid agentId)
    {
        _assignedAgentId = agentId;
        return this;
    }

    /// <summary>
    /// Sets the assigned workflow.
    /// </summary>
    public CreateTicketRequestBuilder WithAssignedWorkflow(Guid workflowId)
    {
        _assignedWorkflowId = workflowId;
        return this;
    }

    /// <summary>
    /// Builds the CreateTicketRequest.
    /// </summary>
    public CreateTicketRequest Build()
    {
        return new CreateTicketRequest(
            _workspaceId, _title, _description, _statusId, _priorityId, _internal, _assignedAgentId, _assignedWorkflowId);
    }
}

/// <summary>
/// Builder for AddCommentRequest DTOs.
/// </summary>
public class AddCommentRequestBuilder
{
    private Guid _ticketId = Guid.NewGuid();
    private string _author = new Faker().Name.FullName();
    private string _content = new Faker().Lorem.Paragraph();

    /// <summary>
    /// Sets the ticket ID.
    /// </summary>
    public AddCommentRequestBuilder WithTicketId(Guid ticketId)
    {
        _ticketId = ticketId;
        return this;
    }

    /// <summary>
    /// Sets the author.
    /// </summary>
    public AddCommentRequestBuilder WithAuthor(string author)
    {
        _author = author;
        return this;
    }

    /// <summary>
    /// Sets the content.
    /// </summary>
    public AddCommentRequestBuilder WithContent(string content)
    {
        _content = content;
        return this;
    }

    /// <summary>
    /// Builds the AddCommentRequest.
    /// </summary>
    public AddCommentRequest Build()
    {
        return new AddCommentRequest(_content);
    }
}

/// <summary>
/// Builder for ConvertTicketRequest DTOs.
/// </summary>
public class ConvertTicketRequestBuilder
{
    private Guid _ticketId = Guid.NewGuid();
    private Guid _integrationId = Guid.NewGuid();
    private string _externalTicketId = "PROJ-" + new Faker().Random.Int(1, 1000);

    /// <summary>
    /// Sets the ticket ID.
    /// </summary>
    public ConvertTicketRequestBuilder WithTicketId(Guid ticketId)
    {
        _ticketId = ticketId;
        return this;
    }

    /// <summary>
    /// Sets the integration ID.
    /// </summary>
    public ConvertTicketRequestBuilder WithIntegrationId(Guid integrationId)
    {
        _integrationId = integrationId;
        return this;
    }

    /// <summary>
    /// Sets the external ticket ID.
    /// </summary>
    public ConvertTicketRequestBuilder WithExternalTicketId(string externalTicketId)
    {
        _externalTicketId = externalTicketId;
        return this;
    }

    /// <summary>
    /// Builds the ConvertTicketRequest.
    /// </summary>
    public ConvertTicketRequest Build()
    {
        return new ConvertTicketRequest(_integrationId, "Task");
    }
}

// ============================================================================
// Integration DTOs
// ============================================================================

/// <summary>
/// Builder for CreateIntegrationRequest DTOs.
/// </summary>
public class CreateIntegrationRequestBuilder
{
    private Guid _workspaceId = Guid.NewGuid();
    private string _name = new Faker().Company.CompanyName();
    private string _type = "TRACKER";
    private string _provider = "JIRA";
    private string _url = "https://example.atlassian.net";
    private string? _username;
    private string _encryptedApiKey = "encrypted_key_" + Guid.NewGuid();
    private string? _filterQuery;
    private bool _vectorize = false;
    private bool? _connected;

    /// <summary>
    /// Sets the workspace ID.
    /// </summary>
    public CreateIntegrationRequestBuilder WithWorkspaceId(Guid workspaceId)
    {
        _workspaceId = workspaceId;
        return this;
    }

    /// <summary>
    /// Sets the name.
    /// </summary>
    public CreateIntegrationRequestBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    /// <summary>
    /// Sets the integration type.
    /// </summary>
    public CreateIntegrationRequestBuilder WithType(string type)
    {
        _type = type;
        return this;
    }

    /// <summary>
    /// Sets the provider type.
    /// </summary>
    public CreateIntegrationRequestBuilder WithProvider(string provider)
    {
        _provider = provider;
        return this;
    }

    /// <summary>
    /// Sets the URL.
    /// </summary>
    public CreateIntegrationRequestBuilder WithUrl(string url)
    {
        _url = url;
        return this;
    }

    /// <summary>
    /// Sets the username.
    /// </summary>
    public CreateIntegrationRequestBuilder WithUsername(string? username)
    {
        _username = username;
        return this;
    }

    /// <summary>
    /// Sets the encrypted API key.
    /// </summary>
    public CreateIntegrationRequestBuilder WithEncryptedApiKey(string apiKey)
    {
        _encryptedApiKey = apiKey;
        return this;
    }

    /// <summary>
    /// Sets the filter query.
    /// </summary>
    public CreateIntegrationRequestBuilder WithFilterQuery(string? filterQuery)
    {
        _filterQuery = filterQuery;
        return this;
    }

    /// <summary>
    /// Sets vectorization status.
    /// </summary>
    public CreateIntegrationRequestBuilder WithVectorize(bool vectorize)
    {
        _vectorize = vectorize;
        return this;
    }

    /// <summary>
    /// Builds the CreateIntegrationRequest.
    /// </summary>
    public CreateIntegrationRequest Build()
    {
        return new CreateIntegrationRequest(
            _workspaceId, _name, _type, _provider, _url, _username, _encryptedApiKey, _filterQuery, _vectorize, _connected);
    }
}

/// <summary>
/// Builder for UpdateIntegrationRequest DTOs.
/// </summary>
public class UpdateIntegrationRequestBuilder
{
    private string _name = new Faker().Company.CompanyName();
    private string _type = "TRACKER";
    private string? _provider;
    private string? _url;
    private string? _username;
    private string? _encryptedApiKey;
    private string? _filterQuery;
    private bool _vectorize = false;
    private bool? _connected;

    /// <summary>
    /// Sets the name.
    /// </summary>
    public UpdateIntegrationRequestBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    /// <summary>
    /// Sets the provider type.
    /// </summary>
    public UpdateIntegrationRequestBuilder WithProvider(string? provider)
    {
        _provider = provider;
        return this;
    }

    /// <summary>
    /// Builds the UpdateIntegrationRequest.
    /// </summary>
    public UpdateIntegrationRequest Build()
    {
        return new UpdateIntegrationRequest(_name, _type, _provider, _url, _username, _encryptedApiKey, _filterQuery, _vectorize, _connected);
    }
}
