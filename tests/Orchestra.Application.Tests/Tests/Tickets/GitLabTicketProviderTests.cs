using Microsoft.Extensions.Logging;
using Orchestra.Application.Tests.Builders;
using Orchestra.Application.Tests.Fixtures;
using Orchestra.Infrastructure.Integrations.Providers.GitLab;
using Orchestra.Infrastructure.Integrations.Providers.GitLab.Models;

namespace Orchestra.Application.Tests.Tests.Tickets;

/// <summary>
/// Unit tests for <see cref="GitLabTicketProvider"/>.
/// </summary>
public class GitLabTicketProviderTests : ServiceTestFixture<GitLabTicketProvider>
{
    private readonly IGitLabApiClientFactory _apiClientFactory = Substitute.For<IGitLabApiClientFactory>();
    private readonly IGitLabApiClient _apiClient = Substitute.For<IGitLabApiClient>();
    private readonly ILogger<GitLabTicketProvider> _logger;
    private readonly GitLabTicketProvider _sut;

    public GitLabTicketProviderTests()
    {
        _logger = GetLoggerSubstitute<GitLabTicketProvider>();
        _apiClientFactory.CreateClient(Arg.Any<Integration>()).Returns(_apiClient);
        _sut = new GitLabTicketProvider(_apiClientFactory, _logger);
    }

    [Fact]
    public async Task FetchTicketsAsync_ReturnsTickets_WhenIssuesExist()
    {
        // Arrange
        var integration = IntegrationBuilder.GitLabIntegration();
        var issues = new List<GitLabIssue>
        {
            new() { Id = 1, Iid = 1, Title = "Bug fix", Description = "Crash on startup", State = "opened", WebUrl = "https://gitlab.com/myorg/myrepo/-/issues/1", Labels = new() },
            new() { Id = 2, Iid = 2, Title = "Feature request", Description = "Add dark mode", State = "closed", WebUrl = "https://gitlab.com/myorg/myrepo/-/issues/2", Labels = new() { "priority/high" } }
        };

        _apiClient.GetProjectIssuesAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(issues);
        _apiClient.GetIssueNotesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<GitLabNote>());

        // Act
        var (tickets, isLast, nextPageToken) = await _sut.FetchTicketsAsync(integration, maxResults: 50);

        // Assert
        Assert.Equal(2, tickets.Count);
        Assert.True(isLast);
        Assert.Null(nextPageToken);
        Assert.Equal("1", tickets[0].ExternalTicketId);
        Assert.Equal("Bug fix", tickets[0].Title);
        Assert.Equal("bg-blue-100 text-blue-800", tickets[0].StatusColor);
        Assert.Equal("2", tickets[1].ExternalTicketId);
        Assert.Equal("bg-red-100 text-red-800", tickets[1].StatusColor);
    }

    [Fact]
    public async Task FetchTicketsAsync_ReturnsNextPageToken_WhenMorePagesExist()
    {
        // Arrange
        var integration = IntegrationBuilder.GitLabIntegration();
        var issues = Enumerable.Range(1, 50).Select(i => new GitLabIssue
        {
            Id = i, Iid = i, Title = $"Issue {i}", State = "opened", Labels = new()
        }).ToList();

        _apiClient.GetProjectIssuesAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(issues);
        _apiClient.GetIssueNotesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<GitLabNote>());

        // Act
        var (tickets, isLast, nextPageToken) = await _sut.FetchTicketsAsync(integration, maxResults: 50);

        // Assert
        Assert.Equal(50, tickets.Count);
        Assert.False(isLast);
        Assert.Equal("2", nextPageToken);
    }

    [Fact]
    public async Task FetchTicketsAsync_MapsCommentsFromNotes()
    {
        // Arrange
        var integration = IntegrationBuilder.GitLabIntegration();
        var issues = new List<GitLabIssue>
        {
            new() { Id = 1, Iid = 1, Title = "Issue with notes", State = "opened", Labels = new() }
        };
        var notes = new List<GitLabNote>
        {
            new() { Id = 10, Body = "First note", Author = new GitLabUser { Username = "alice" }, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Id = 11, Body = "Second note", Author = new GitLabUser { Username = "bob" }, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        };

        _apiClient.GetProjectIssuesAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(issues);
        _apiClient.GetIssueNotesAsync(1, Arg.Any<CancellationToken>()).Returns(notes);

        // Act
        var (tickets, _, _) = await _sut.FetchTicketsAsync(integration);

        // Assert
        Assert.Single(tickets);
        Assert.Equal(2, tickets[0].Comments.Count);
        Assert.Equal("alice", tickets[0].Comments[0].Author);
        Assert.Equal("First note", tickets[0].Comments[0].Content);
    }

    [Fact]
    public async Task GetTicketByIdAsync_ReturnsTicket_WhenIssueExists()
    {
        // Arrange
        var integration = IntegrationBuilder.GitLabIntegration();
        var issue = new GitLabIssue
        {
            Id = 1, Iid = 42, Title = "Test issue", Description = "Description here",
            State = "opened", WebUrl = "https://gitlab.com/myorg/myrepo/-/issues/42",
            Labels = new() { "priority/critical" }
        };

        _apiClient.GetIssueAsync(42, Arg.Any<CancellationToken>()).Returns(issue);
        _apiClient.GetIssueNotesAsync(42, Arg.Any<CancellationToken>()).Returns(new List<GitLabNote>());

        // Act
        var ticket = await _sut.GetTicketByIdAsync(integration, "42");

        // Assert
        Assert.NotNull(ticket);
        Assert.Equal("42", ticket.ExternalTicketId);
        Assert.Equal("Test issue", ticket.Title);
        Assert.Equal("bg-red-100 text-red-800", ticket.PriorityColor);
        Assert.Equal(1, ticket.PriorityValue);
    }

    [Fact]
    public async Task GetTicketByIdAsync_ReturnsNull_WhenInvalidId()
    {
        // Arrange
        var integration = IntegrationBuilder.GitLabIntegration();

        // Act
        var ticket = await _sut.GetTicketByIdAsync(integration, "not-a-number");

        // Assert
        Assert.Null(ticket);
        await _apiClient.DidNotReceive().GetIssueAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetTicketByIdAsync_ReturnsNull_WhenIssueNotFound()
    {
        // Arrange
        var integration = IntegrationBuilder.GitLabIntegration();
        _apiClient.GetIssueAsync(99, Arg.Any<CancellationToken>()).ReturnsNull();

        // Act
        var ticket = await _sut.GetTicketByIdAsync(integration, "99");

        // Assert
        Assert.Null(ticket);
    }

    [Fact]
    public async Task AddCommentAsync_ReturnsCommentDto()
    {
        // Arrange
        var integration = IntegrationBuilder.GitLabIntegration();
        var now = DateTime.UtcNow;
        var note = new GitLabNote
        {
            Id = 55,
            Body = "Test comment",
            Author = new GitLabUser { Username = "testuser" },
            CreatedAt = now,
            UpdatedAt = now
        };

        _apiClient.AddNoteAsync(7, "Test comment", Arg.Any<CancellationToken>()).Returns(note);

        // Act
        var comment = await _sut.AddCommentAsync(integration, "7", "Test comment", "fallback-author");

        // Assert
        Assert.Equal("55", comment.Id);
        Assert.Equal("testuser", comment.Author);
        Assert.Equal("Test comment", comment.Content);
    }

    [Fact]
    public async Task AddCommentAsync_Throws_WhenInvalidIssueId()
    {
        // Arrange
        var integration = IntegrationBuilder.GitLabIntegration();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.AddCommentAsync(integration, "not-a-number", "body", "author"));
    }

    [Fact]
    public async Task CreateIssueAsync_ReturnsCreationResult()
    {
        // Arrange
        var integration = IntegrationBuilder.GitLabIntegration();
        var createdIssue = new GitLabIssue
        {
            Id = 100, Iid = 5, Title = "New issue",
            WebUrl = "https://gitlab.com/myorg/myrepo/-/issues/5",
            State = "opened", Labels = new()
        };

        _apiClient.CreateIssueAsync("New issue", "Some description", Arg.Any<List<string>>(), Arg.Any<CancellationToken>())
            .Returns(createdIssue);

        // Act
        var result = await _sut.CreateIssueAsync(integration, "New issue", "Some description", "bug");

        // Assert
        Assert.Equal("#5", result.IssueKey);
        Assert.Equal("5", result.IssueId);
        Assert.Equal("https://gitlab.com/myorg/myrepo/-/issues/5", result.IssueUrl);
    }

    [Fact]
    public async Task CreateIssueAsync_DoesNotAddLabel_WhenIssueTypeIsTask()
    {
        // Arrange
        var integration = IntegrationBuilder.GitLabIntegration();
        var createdIssue = new GitLabIssue { Id = 1, Iid = 1, WebUrl = "https://gitlab.com/myorg/myrepo/-/issues/1", State = "opened", Labels = new() };

        _apiClient.CreateIssueAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<List<string>>(), Arg.Any<CancellationToken>())
            .Returns(createdIssue);

        // Act
        await _sut.CreateIssueAsync(integration, "Task title", "desc", "task");

        // Assert
        await _apiClient.Received(1).CreateIssueAsync(
            "Task title",
            "desc",
            Arg.Is<List<string>>(l => l.Count == 0),
            Arg.Any<CancellationToken>());
    }
}
