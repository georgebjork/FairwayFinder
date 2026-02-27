using Anthropic;
using FairwayFinder.Agents.Factory.Interface;
using FairwayFinder.Shared.Settings;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FairwayFinder.Agents.Factory;

public class ClaudeAgentFactory : IAgentFactory
{
    private readonly ClaudeSettings _claudeSettings;
    private readonly AnthropicClient _client;
    private readonly ILogger<ClaudeAgentFactory> _logger;

    public ClaudeAgentFactory(IOptions<ClaudeSettings> claudeSettings, ILogger<ClaudeAgentFactory> logger)
    {
        _claudeSettings = claudeSettings.Value;
        _logger = logger;
        
        _client = new AnthropicClient { ApiKey = _claudeSettings.ApiKey };
    }

    public AIAgent Create(string name, string instructions, IList<AITool>? tools = null, string? description = null)
    {
        return _client.AsAIAgent(
            model: _claudeSettings.Model,
            name: name,
            instructions: instructions,
            tools: tools,
            description: description);
    }
}
