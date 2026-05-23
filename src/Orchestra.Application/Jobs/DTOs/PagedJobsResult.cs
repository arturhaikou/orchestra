namespace Orchestra.Application.Jobs.DTOs;

public record PagedJobsResult(
    IReadOnlyList<JobSummaryDto> Items,
    int Total,
    int Page,
    int PageSize);
