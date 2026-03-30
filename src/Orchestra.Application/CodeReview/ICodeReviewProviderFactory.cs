using Orchestra.Domain.Entities;

namespace Orchestra.Application.CodeReview;

/// <summary>
/// Creates provider-specific <see cref="ICodeReviewProvider"/> instances
/// based on the integration configuration.
/// </summary>
public interface ICodeReviewProviderFactory
{
    ICodeReviewProvider Create(Integration integration);
}
