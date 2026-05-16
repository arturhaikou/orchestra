using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Skills.DTOs;
using Orchestra.Application.Skills.Services;

namespace Orchestra.Application.Tests.Tests.Skills;

public class SkillServiceTests
{
    private readonly ISkillDataAccess _skillDataAccess;
    private readonly IWorkspaceAuthorizationService _authorizationService;
    private readonly SkillService _sut;

    public SkillServiceTests()
    {
        _skillDataAccess = Substitute.For<ISkillDataAccess>();
        _authorizationService = Substitute.For<IWorkspaceAuthorizationService>();
        _sut = new SkillService(_skillDataAccess, _authorizationService);
    }

    // ─── GetSkillsAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetSkillsAsync_WithValidMember_ReturnsSkillsForWorkspace()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var skill = new SkillBuilder().WithWorkspaceId(workspaceId).Build();
        _skillDataAccess.GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Domain.Entities.Skill> { skill });

        // Act
        var result = await _sut.GetSkillsAsync(userId, workspaceId);

        // Assert
        Assert.Single(result);
        Assert.Equal(skill.Name, result[0].Name);
        Assert.Equal(skill.Description, result[0].Description);
        Assert.Equal(skill.Instructions, result[0].Instructions);
    }

    [Fact]
    public async Task GetSkillsAsync_AuthorizationFails_PropagatesException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        _authorizationService
            .EnsureUserIsMemberAsync(userId, workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new UnauthorizedWorkspaceAccessException(userId, workspaceId)));

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedWorkspaceAccessException>(() =>
            _sut.GetSkillsAsync(userId, workspaceId));
    }

    // ─── GetSkillByIdAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetSkillByIdAsync_WhenSkillExistsInWorkspace_ReturnsSkillDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var skill = new SkillBuilder().WithWorkspaceId(workspaceId).Build();
        _skillDataAccess.GetByIdAsync(skill.Id, Arg.Any<CancellationToken>()).Returns(skill);

        // Act
        var result = await _sut.GetSkillByIdAsync(userId, workspaceId, skill.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(skill.Id.ToString(), result!.Id);
        Assert.Equal(skill.Name, result.Name);
    }

    [Fact]
    public async Task GetSkillByIdAsync_WhenSkillBelongsToDifferentWorkspace_ReturnsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var skill = new SkillBuilder().WithWorkspaceId(Guid.NewGuid()).Build(); // different workspace
        _skillDataAccess.GetByIdAsync(skill.Id, Arg.Any<CancellationToken>()).Returns(skill);

        // Act
        var result = await _sut.GetSkillByIdAsync(userId, workspaceId, skill.Id);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetSkillByIdAsync_WhenSkillNotFound_ReturnsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        _skillDataAccess.GetByIdAsync(skillId, Arg.Any<CancellationToken>()).Returns((Domain.Entities.Skill?)null);

        // Act
        var result = await _sut.GetSkillByIdAsync(userId, workspaceId, skillId);

        // Assert
        Assert.Null(result);
    }

    // ─── CreateSkillAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task CreateSkillAsync_WithValidRequest_CreatesAndPersistsSkill()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var request = new CreateSkillRequest(workspaceId, "python-expert", "Python expertise", "Use Python best practices.");

        // Act
        var result = await _sut.CreateSkillAsync(userId, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("python-expert", result.Name);
        Assert.Equal("Python expertise", result.Description);
        Assert.Equal(workspaceId.ToString(), result.WorkspaceId);
        await _skillDataAccess.Received(1).AddAsync(
            Arg.Is<Domain.Entities.Skill>(s => s.Name == "python-expert" && s.WorkspaceId == workspaceId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateSkillAsync_AuthorizationFails_DoesNotPersist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var request = new CreateSkillRequest(workspaceId, "test-skill", "desc", "instructions");
        _authorizationService
            .EnsureUserIsMemberAsync(userId, workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new UnauthorizedWorkspaceAccessException(userId, workspaceId)));

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedWorkspaceAccessException>(() =>
            _sut.CreateSkillAsync(userId, request));
        await _skillDataAccess.DidNotReceive().AddAsync(Arg.Any<Domain.Entities.Skill>(), Arg.Any<CancellationToken>());
    }

    // ─── UpdateSkillAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateSkillAsync_WhenSkillExists_UpdatesAndPersists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var skill = new SkillBuilder().WithWorkspaceId(workspaceId).Build();
        _skillDataAccess.GetByIdAsync(skill.Id, Arg.Any<CancellationToken>()).Returns(skill);
        var request = new UpdateSkillRequest("updated-skill", "new description", "new instructions");

        // Act
        var result = await _sut.UpdateSkillAsync(userId, workspaceId, skill.Id, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("updated-skill", result!.Name);
        Assert.Equal("new description", result.Description);
        await _skillDataAccess.Received(1).UpdateAsync(
            Arg.Is<Domain.Entities.Skill>(s => s.Name == "updated-skill"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateSkillAsync_WhenSkillNotFound_ReturnsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        _skillDataAccess.GetByIdAsync(skillId, Arg.Any<CancellationToken>()).Returns((Domain.Entities.Skill?)null);
        var request = new UpdateSkillRequest("n", "d", "i");

        // Act
        var result = await _sut.UpdateSkillAsync(userId, workspaceId, skillId, request);

        // Assert
        Assert.Null(result);
        await _skillDataAccess.DidNotReceive().UpdateAsync(Arg.Any<Domain.Entities.Skill>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateSkillAsync_WhenSkillBelongsToDifferentWorkspace_ReturnsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var skill = new SkillBuilder().WithWorkspaceId(Guid.NewGuid()).Build();
        _skillDataAccess.GetByIdAsync(skill.Id, Arg.Any<CancellationToken>()).Returns(skill);
        var request = new UpdateSkillRequest("n", "d", "i");

        // Act
        var result = await _sut.UpdateSkillAsync(userId, workspaceId, skill.Id, request);

        // Assert
        Assert.Null(result);
        await _skillDataAccess.DidNotReceive().UpdateAsync(Arg.Any<Domain.Entities.Skill>(), Arg.Any<CancellationToken>());
    }

    // ─── DeleteSkillAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteSkillAsync_WhenSkillExists_DeletesIt()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        _skillDataAccess.ExistsInWorkspaceAsync(skillId, workspaceId, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        await _sut.DeleteSkillAsync(userId, workspaceId, skillId);

        // Assert
        await _skillDataAccess.Received(1).DeleteAsync(skillId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteSkillAsync_WhenSkillDoesNotExist_IsNoOp()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        _skillDataAccess.ExistsInWorkspaceAsync(skillId, workspaceId, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        await _sut.DeleteSkillAsync(userId, workspaceId, skillId);

        // Assert
        await _skillDataAccess.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteSkillAsync_AuthorizationFails_DoesNotDelete()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        _authorizationService
            .EnsureUserIsMemberAsync(userId, workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new UnauthorizedWorkspaceAccessException(userId, workspaceId)));

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedWorkspaceAccessException>(() =>
            _sut.DeleteSkillAsync(userId, workspaceId, skillId));
        await _skillDataAccess.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
