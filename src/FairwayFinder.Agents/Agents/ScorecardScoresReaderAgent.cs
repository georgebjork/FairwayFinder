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
        - If the player's name is not legible, use "Unknown Player" followed by their row position.
        - If the image is rotated, mentally rotate it before reading.
        - Ensure hole numbers progress left to right from 1 through 18.
        
        If you cannot find any scores or the image is not a scorecard... then return an empty list.
        """;
    
    /*
     * - Fairway and Green stats must be inferred consistently per player row, not per hole.

FAIRWAY STAT RULES:
1. Fairway stats only apply on Par 4 and Par 5 holes.
2. If the hole is Par 3, FairwayHit must always be null.

3. Determine the marking convention used by the player by scanning the entire row:
   - If both "X" and checkmarks appear:
       • Checkmark = Fairway Hit
       • X = Fairway Miss
   - If only "X" appears and no checkmarks exist anywhere in the row:
       • Treat X as Fairway Hit
       • Blank = Fairway Miss
   - If only checkmarks appear and no X exists:
       • Checkmark = Fairway Hit
       • Blank = Fairway Miss
   - If the marking pattern is inconsistent or unclear, return FairwayHit as null for that hole.

4. Blank cells should not automatically be treated as a miss unless a clear marking convention is established for that row.

GREEN IN REGULATION (GIR) RULES:
1. Apply the same row-level convention logic as fairways.
2. If both checkmarks and X appear:
       • Checkmark = Green Hit
       • X = Green Miss
3. If only one symbol type exists across the row:
       • That symbol indicates a positive result.
       • Blank = negative result.
4. If unclear or inconsistent, return null.

GENERAL:
- Determine symbol meaning at the row level first, then apply consistently across all holes for that player.
- Never reinterpret symbol meaning hole-by-hole.
     */
    
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
    //public bool? HitFairway { get; set; }
    //public bool? HitGreen { get; set; }
    //public int Putts { get; set; }
}
