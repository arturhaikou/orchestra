using Microsoft.EntityFrameworkCore;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Infrastructure.Persistence;

namespace Orchestra.Infrastructure.Skills;

public class AgentSkillFolderDataAccess : IAgentSkillFolderDataAccess
{
    private readonly AppDbContext _context;

    public AgentSkillFolderDataAccess(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<SkillFolder>> GetFoldersByAgentIdAsync(
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        return await _context.AgentSkillFolders
            .AsNoTracking()
            .Where(asf => asf.AgentId == agentId)
            .Join(_context.SkillFolders, asf => asf.SkillFolderId, sf => sf.Id, (_, sf) => sf)
            .OrderBy(sf => sf.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task AssignFoldersAsync(
        Guid agentId,
        IReadOnlyList<Guid> skillFolderIds,
        CancellationToken cancellationToken = default)
    {
        if (skillFolderIds.Count == 0)
            return;

        var existingFolderIds = await _context.AgentSkillFolders
            .AsNoTracking()
            .Where(asf => asf.AgentId == agentId)
            .Select(asf => asf.SkillFolderId)
            .ToListAsync(cancellationToken);

        var newAssignments = skillFolderIds
            .Except(existingFolderIds)
            .Select(folderId => AgentSkillFolder.Create(agentId, folderId))
            .ToList();

        if (newAssignments.Count == 0)
            return;

        await _context.AgentSkillFolders.AddRangeAsync(newAssignments, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAllFoldersAsync(
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        var assignments = await _context.AgentSkillFolders
            .Where(asf => asf.AgentId == agentId)
            .ToListAsync(cancellationToken);

        if (assignments.Count == 0)
            return;

        _context.AgentSkillFolders.RemoveRange(assignments);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task ReplaceFoldersAsync(
        Guid agentId,
        IReadOnlyList<Guid> skillFolderIds,
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.AgentSkillFolders
            .Where(asf => asf.AgentId == agentId)
            .ToListAsync(cancellationToken);

        _context.AgentSkillFolders.RemoveRange(existing);

        if (skillFolderIds.Count > 0)
        {
            var newAssignments = skillFolderIds
                .Select(folderId => AgentSkillFolder.Create(agentId, folderId))
                .ToList();

            await _context.AgentSkillFolders.AddRangeAsync(newAssignments, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
