using Microsoft.EntityFrameworkCore;
using Orchestra.Application.Agents.DTOs;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Infrastructure.Persistence;

namespace Orchestra.Infrastructure.Repositories;

public class AgentQuestionRepository(AppDbContext db) : IAgentQuestionRepository
{
    public Task<AgentQuestion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.AgentQuestions.FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

    public Task<List<AgentQuestion>> GetPendingByWorkspaceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
        => db.AgentQuestions
            .Where(q => q.WorkspaceId == workspaceId && q.Status == QuestionStatus.Pending)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<List<GlobalAgentQuestionDto>> GetGlobalPendingByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
        => (from q in db.AgentQuestions
            join j in db.Jobs on q.JobId equals j.Id
            join w in db.Workspaces on q.WorkspaceId equals w.Id
            join uw in db.UserWorkspaces on w.Id equals uw.WorkspaceId
            where uw.UserId == userId && q.Status == QuestionStatus.Pending
            orderby q.CreatedAt descending
            select new GlobalAgentQuestionDto(
                q.WorkspaceId,
                w.Name,
                q.Id,
                j.TicketId,
                j.TicketTitle,
                j.AgentName,
                q.QuestionsJson,
                q.CreatedAt))
           .ToListAsync(cancellationToken);

    public async Task SaveAsync(AgentQuestion question, CancellationToken cancellationToken = default)
    {
        db.AgentQuestions.Add(question);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveAnswerAsync(
        Guid questionId,
        string answersJson,
        CancellationToken cancellationToken = default)
    {
        var question = await db.AgentQuestions
            .FirstOrDefaultAsync(q => q.Id == questionId, cancellationToken)
            ?? throw new InvalidOperationException($"AgentQuestion {questionId} not found.");

        question.RecordAnswer(answersJson);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<List<AgentQuestion>> GetPendingByJobAsync(Guid jobId, CancellationToken cancellationToken = default)
        => db.AgentQuestions
            .Where(q => q.JobId == jobId && q.Status == QuestionStatus.Pending)
            .ToListAsync(cancellationToken);

    public async Task UpdateAsync(AgentQuestion question, CancellationToken cancellationToken = default)
    {
        db.AgentQuestions.Update(question);
        await db.SaveChangesAsync(cancellationToken);
    }
}
