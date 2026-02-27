using FairwayFinder.Agents.Agents.Base;
using FairwayFinder.Agents.Factory.Interface;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Agents.Agents;

public class ScorecardScoresReaderAgent : BaseAgent<ScorecardScoresReaderAgentResponse>
{
    private readonly ILogger<ScorecardScoresReaderAgent> _logger;

    public ScorecardScoresReaderAgent(IAgentFactory agentFactory, ILogger<ScorecardScoresReaderAgent> logger)
        : base(agentFactory)
    {
        _logger = logger;
    }

    public override string AgentIdentifier { get; } = nameof(ScorecardScoresReaderAgent);

    protected override string GetInstructions => """
        You are a golf scorecard reader. You will receive an image of a golf scorecard.

        Your job is to extract the handwritten hole-by-hole scores for each player.

        For each player found on the scorecard:
        - Extract their name exactly as written.
        - Extract their score for each hole played.
        - Their scores should be all in a row. Usually left to right, but sometimes up and down.

        Rules:
        - HoleNumber must be the actual hole number (1–18).
        - HoleScore must be the handwritten stroke count for that hole.
        - Only include holes where a handwritten score is clearly visible.
        - Ignore printed values such as par, yardage, handicap, totals, IN/OUT scores, or net scores.
        - Ignore circles around numbers — the circled number is still the score.
        - Ignore X marks, GIR, fairway indicators, and putt tracking rows.
        - If the player's name is not legible, use "Unknown Player" followed by their row position.
        - If the image is rotated, mentally rotate it before reading.
        - Ensure hole numbers progress left to right from 1 through 18.
        """;

    /// <summary>
    /// Runs the agent with a single image and returns a typed response.
    /// </summary>
    public async Task<AgentResponse<ScorecardScoresReaderAgentResponse>> RunAsync(
        ReadOnlyMemory<byte> imageData,
        string mediaType,
        AgentSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var content = new DataContent(imageData, mediaType);
        var message = new ChatMessage(ChatRole.User, [content]);
        return await RunAsync([message], session, cancellationToken);
    }

    /// <summary>
    /// Runs the agent with multiple images and returns a typed response.
    /// </summary>
    public async Task<AgentResponse<ScorecardScoresReaderAgentResponse>> RunAsync(
        IEnumerable<DataContent> images,
        AgentSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var contents = images.Cast<AIContent>().ToList();
        var message = new ChatMessage(ChatRole.User, contents);
        return await RunAsync([message], session, cancellationToken);
    }
}

public class ScorecardScoresReaderAgentResponse
{
    public List<Player> Players { get; set; } = [];
}

public class Player
{
    public string PlayerName { get; set; } = "";
    public List<PlayerScores> Scores { get; set; } = [];
}

public class PlayerScores
{
    public int HoleNumber { get; set; }
    public int HoleScore { get; set; }
}
