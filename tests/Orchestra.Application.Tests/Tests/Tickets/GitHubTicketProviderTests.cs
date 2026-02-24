using Microsoft.Extensions.Logging;
using Orchestra.Application.Tests.Builders;
using Orchestra.Application.Tests.Fixtures;
using Orchestra.Infrastructure.Integrations.Providers.GitHub;
using Orchestra.Infrastructure.Integrations.Providers.GitHub.Models;

namespace Orchestra.Application.Tests.Tests.Tickets;

/// <summary>
/// Unit tests for <see cref="GitHubTicketProvider"/>.
/// </summary>
public class GitHubTicketProviderTests : ServiceTestFixture<GitHubTicketProvider>
{
    private readonly IGitHubApiClientFactory _apiClientFactory = Substitute.For<IGitHubApiClientFactory>();
    private readonly IGitHubApiClient _apiClient = Substitute.For<IGitHubApiClient>();
    private readonly ILogger<GitHubTicketProvider> _logger;
    private readonly GitHubTicketProvider _sut;

    public GitHubTicketProviderTests()
    {
        _logger = GetLoggerSubstitute<GitHubTicketProvider>();
        _apiClientFactory.CreateClient(Arg.Any<Integration>()).Returns(_apiClient);
        _sut = new GitHubTicketProvider(_apiClientFactory, _logger);
    }

    [Fact]
    public async Task FetchTicketsAsync_ReturnsTickets_WhenIssuesExist()
    {
        // Arrange
        var integration = IntegrationBuilder.GitHubIntegration();
        var issues = new List<GitHubIssue>
        {
            new() { Number = 1, Title = "Bug fix", Body = "Crash on startup", State = "open",   HtmlUrl = "https://github.com/myorg/myrepo/issues/1", Labels = new() },
            new() { Number = 2, Title = "Feature",  Body = "Add dark mode",   State = "closed", HtmlUrl = "https://github.com/myorg/myrepo/issues/2", Labels = new() { new GitHubLabel { Name = "priority: high" } } }
        };

        // Under-full page, no next page
        _apiClient.GetRepositoryIssuesAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((issues, false));
        _apiClient.GetIssueCommentsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<GitHubComment>());

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
    public async Task FetchTicketsAsync_ReturnsNextPageToken_WhenLinkHeaderHasRelNext()
    {
        // Arrange
        var integration = IntegrationBuilder.GitHubIntegration();
        var issues = Enumerable.Range(1, 50).Select(i => new GitHubIssue
        {
            Number = i, Title = $"Issue {i}", State = "open", Labels = new()
        }).ToList();

        // Full page AND hasNextPage=true (GitHub returned rel="next" in Link header)
        _apiClient.GetRepositoryIssuesAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((issues, true));
        _apiClient.GetIssueCommentsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<GitHubComment>());

        // Act
        var (tickets, isLast, nextPageToken) = await _sut.FetchTicketsAsync(integration, maxResults: 50);

        // Assert
        Assert.Equal(50, tickets.Count);
        Assert.False(isLast);
        Assert.Equal("2", nextPageToken);
    }

    /// <summary>
    /// Regression test for the exact-boundary bug: when a provider returns exactly
    /// <c>maxResults</c> items but there are no more pages (Link header has no rel="next"),
    /// the old <c>count &lt; maxResults</c> heuristic would incorrectly set isLast=false,
    /// causing the UI to show a phantom "Load More" button that returns 0 tickets.
    /// </summary>
    [Fact]
    public async Task FetchTicketsAsync_ExactFullPage_NoRelNextInLinkHeader_ReturnsIsLastTrue()
    {
        // Arrange
        var integration = IntegrationBuilder.GitHubIntegration();
        // Exactly 50 issues fills the page, but GitHub did not return rel="next" in Link —
        // this is the last page even though count == perPage.
        var issues = Enumerable.Range(1, 50).Select(i => new GitHubIssue
        {
            Number = i, Title = $"Issue {i}", State = "open", Labels = new()
        }).ToList();

        _apiClient.GetRepositoryIssuesAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((issues, false)); // hasNextPage=false: no rel="next" in Link header
        _apiClient.GetIssueCommentsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<GitHubComment>());

        // Act
        var (tickets, isLast, nextPageToken) = await _sut.FetchTicketsAsync(integration, maxResults: 50);

        // Assert
        Assert.Equal(50, tickets.Count);
        Assert.True(isLast,
            "isLast must be true when the Link header has no rel=\"next\", even when the page is exactly full.");
        Assert.Null(nextPageToken);
    }

    [Fact]
    public async Task FetchTicketsAsync_MapsCommentsFromIssue()
    {
        // Arrange
        var integration = IntegrationBuilder.GitHubIntegration();
        var issues = new List<GitHubIssue>
        {
            new() { Number = 10, Title = "Commented issue", State = "open", Labels = new() }
        };
        var comments = new List<GitHubComment>
        {
            new() { Id = 100, Body = "First comment", User = new GitHubUser { Login = "alice" }, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Id = 101, Body = "Second comment", User = new GitHubUser { Login = "bob" },   CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        };

        _apiClient.GetRepositoryIssuesAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((issues, false));
        _apiClient.GetIssueCommentsAsync(10, Arg.Any<CancellationToken>()).Returns(comments);

        // Act
        var (tickets, _, _) = await _sut.FetchTicketsAsync(integration);

        // Assert
        Assert.Single(tickets);
        Assert.Equal(2, tickets[0].Comments.Count);
        Assert.Equal("alice", tickets[0].Comments[0].Author);
        Assert.Equal("First comment", tickets[0].Comments[0].Content);
        Assert.Equal("bob", tickets[0].Comments[1].Author);
    }

    [Fact]
    public async Task FetchTicketsAsync_ResumesFromPageToken()
    {
        // Arrange — pass pageToken="3" to resume from page 3
        var integration = IntegrationBuilder.GitHubIntegration();
        var issues = new List<GitHubIssue>
        {
            new() { Number = 101, Title = "Page 3 issue", State = "open", Labels = new() }
        };

        _apiClient.GetRepositoryIssuesAsync(3, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((issues, false));
        _apiClient.GetIssueCommentsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<GitHubComment>());

        // Act
        var (tickets, isLast, _) = await _sut.FetchTicketsAsync(integration, pageToken: "3");

        // Assert
        Assert.Single(tickets);
        Assert.True(isLast);
        await _apiClient.Received(1).GetRepositoryIssuesAsync(3, Arg.Any<int>(), Arg.Any<CancellationToken>());
    }
}
