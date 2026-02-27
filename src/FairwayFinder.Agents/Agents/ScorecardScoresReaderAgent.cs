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

    protected override string GetInstructions =>
        "Read in the scorecard data and output in the proper structured output.";

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
