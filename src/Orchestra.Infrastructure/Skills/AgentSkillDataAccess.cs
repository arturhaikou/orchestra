using Microsoft.EntityFrameworkCore;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Infrastructure.Persistence;

namespace Orchestra.Infrastructure.Skills;

public class AgentSkillDataAccess : IAgentSkillDataAccess
{
    private readonly AppDbContext _context;

    public AgentSkillDataAccess(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Skill>> GetSkillsByAgentIdAsync(
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        return await _context.AgentSkills
            .AsNoTracking()
            .Where(ask => ask.AgentId == agentId)
            .Join(_context.Skills, ask => ask.SkillId, s => s.Id, (_, s) => s)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task AssignSkillsAsync(
        Guid agentId,
        IReadOnlyList<Guid> skillIds,
        CancellationToken cancellationToken = default)
    {
        if (skillIds.Count == 0)
            return;

        var existingSkillIds = await _context.AgentSkills
            .AsNoTracking()
            .Where(ask => ask.AgentId == agentId)
            .Select(ask => ask.SkillId)
            .ToListAsync(cancellationToken);

        var newAssignments = skillIds
            .Except(existingSkillIds)
            .Select(skillId => AgentSkill.Create(agentId, skillId))
            .ToList();

        if (newAssignments.Count == 0)
            return;

        await _context.AgentSkills.AddRangeAsync(newAssignments, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAllSkillsAsync(
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        var assignments = await _context.AgentSkills
            .Where(ask => ask.AgentId == agentId)
            .ToListAsync(cancellationToken);

        if (assignments.Count == 0)
            return;

        _context.AgentSkills.RemoveRange(assignments);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task ReplaceSkillsAsync(
        Guid agentId,
        IReadOnlyList<Guid> skillIds,
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.AgentSkills
            .Where(ask => ask.AgentId == agentId)
            .ToListAsync(cancellationToken);

        _context.AgentSkills.RemoveRange(existing);

        if (skillIds.Count > 0)
        {
            var newAssignments = skillIds
                .Select(skillId => AgentSkill.Create(agentId, skillId))
                .ToList();

            await _context.AgentSkills.AddRangeAsync(newAssignments, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
