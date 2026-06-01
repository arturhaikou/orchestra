using Microsoft.EntityFrameworkCore;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Infrastructure.Persistence;

namespace Orchestra.Infrastructure.Skills;

public class SkillFolderDataAccess : ISkillFolderDataAccess
{
    private readonly AppDbContext _context;

    public SkillFolderDataAccess(AppDbContext context)
    {
        _context = context;
    }

    public async Task<SkillFolder?> GetByIdAsync(Guid skillFolderId, CancellationToken cancellationToken = default)
    {
        return await _context.SkillFolders
            .FirstOrDefaultAsync(sf => sf.Id == skillFolderId, cancellationToken);
    }

    public async Task<List<SkillFolder>> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        return await _context.SkillFolders
            .AsNoTracking()
            .Where(sf => sf.WorkspaceId == workspaceId)
            .OrderByDescending(sf => sf.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(SkillFolder skillFolder, CancellationToken cancellationToken = default)
    {
        await _context.SkillFolders.AddAsync(skillFolder, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(SkillFolder skillFolder, CancellationToken cancellationToken = default)
    {
        _context.SkillFolders.Update(skillFolder);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid skillFolderId, CancellationToken cancellationToken = default)
    {
        var folder = await _context.SkillFolders.FindAsync([skillFolderId], cancellationToken);
        if (folder is not null)
        {
            _context.SkillFolders.Remove(folder);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> ExistsInWorkspaceAsync(Guid skillFolderId, Guid workspaceId, CancellationToken cancellationToken = default)
    {
        return await _context.SkillFolders
            .AsNoTracking()
            .AnyAsync(sf => sf.Id == skillFolderId && sf.WorkspaceId == workspaceId, cancellationToken);
    }
}
