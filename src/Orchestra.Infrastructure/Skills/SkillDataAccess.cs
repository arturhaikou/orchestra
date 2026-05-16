using Microsoft.EntityFrameworkCore;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Infrastructure.Persistence;

namespace Orchestra.Infrastructure.Skills;

public class SkillDataAccess : ISkillDataAccess
{
    private readonly AppDbContext _context;

    public SkillDataAccess(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Skill?> GetByIdAsync(Guid skillId, CancellationToken cancellationToken = default)
    {
        return await _context.Skills
            .FirstOrDefaultAsync(s => s.Id == skillId, cancellationToken);
    }

    public async Task<List<Skill>> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        return await _context.Skills
            .AsNoTracking()
            .Where(s => s.WorkspaceId == workspaceId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Skill skill, CancellationToken cancellationToken = default)
    {
        await _context.Skills.AddAsync(skill, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Skill skill, CancellationToken cancellationToken = default)
    {
        _context.Skills.Update(skill);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid skillId, CancellationToken cancellationToken = default)
    {
        var skill = await _context.Skills.FindAsync([skillId], cancellationToken);
        if (skill is not null)
        {
            _context.Skills.Remove(skill);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> ExistsInWorkspaceAsync(Guid skillId, Guid workspaceId, CancellationToken cancellationToken = default)
    {
        return await _context.Skills
            .AsNoTracking()
            .AnyAsync(s => s.Id == skillId && s.WorkspaceId == workspaceId, cancellationToken);
    }
}
