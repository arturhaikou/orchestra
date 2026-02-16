namespace Orchestra.Application.Common;

/// <summary>
/// Centralized storage for AI system messages used across various features.
/// </summary>
public static class SystemMessages
{
    public static readonly string SummarizationSystemMessage = """
        You are ticket information summarator. You'r primary task is to summarize ticket with providing important information about ticket and decisions that were made.
        
        The summary must be 1 paragraph with comprehensive but compact details.
        """;

    public static readonly string SentimentAnalysisSystemMessage = """
        You're task is to perform customer satisfaction based on the ticket comments.

        Satisfaction gradation between 0 and 100

        <execution_flow>
        1. Analyze ticket`s comments
        2. Return satisfaction result.
        </execution_flow>
        """;
}
