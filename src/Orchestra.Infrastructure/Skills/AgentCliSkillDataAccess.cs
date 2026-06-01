using Microsoft.EntityFrameworkCore;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Infrastructure.Persistence;

namespace Orchestra.Infrastructure.Skills;

public class AgentCliSkillDataAccess : IAgentCliSkillDataAccess
{
    private readonly AppDbContext _context;

    public AgentCliSkillDataAccess(AppDbContext context)
    {
        _context = context;
    }

    public async Task AssignSkillsAsync(
        Guid agentId,
        IReadOnlyList<string> skillNames,
        CancellationToken cancellationToken = default)
    {
        if (skillNames.Count == 0)
            return;

        var existingNames = await _context.AgentCliSkills
            .AsNoTracking()
            .Where(acs => acs.AgentId == agentId)
            .Select(acs => acs.SkillName)
            .ToListAsync(cancellationToken);

        var newAssignments = skillNames
            .Except(existingNames, StringComparer.OrdinalIgnoreCase)
            .Select(name => AgentCliSkill.Create(agentId, name))
            .ToList();

        if (newAssignments.Count == 0)
            return;

        await _context.AgentCliSkills.AddRangeAsync(newAssignments, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetSkillNamesAsync(
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        return await _context.AgentCliSkills
            .AsNoTracking()
            .Where(acs => acs.AgentId == agentId)
            .Select(acs => acs.SkillName)
            .OrderBy(name => name)
            .ToListAsync(cancellationToken);
    }

    public async Task ReplaceSkillsAsync(
        Guid agentId,
        IReadOnlyList<string> skillNames,
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.AgentCliSkills
            .Where(acs => acs.AgentId == agentId)
            .ToListAsync(cancellationToken);

        _context.AgentCliSkills.RemoveRange(existing);

        if (skillNames.Count > 0)
        {
            var newAssignments = skillNames
                .Select(name => AgentCliSkill.Create(agentId, name))
                .ToList();

            await _context.AgentCliSkills.AddRangeAsync(newAssignments, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
