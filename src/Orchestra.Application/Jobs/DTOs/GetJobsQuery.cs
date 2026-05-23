using Orchestra.Domain.Enums;

namespace Orchestra.Application.Jobs.DTOs;

public record GetJobsQuery(
    JobStatus? Status = null,
    int Page = 1,
    int PageSize = 20);
