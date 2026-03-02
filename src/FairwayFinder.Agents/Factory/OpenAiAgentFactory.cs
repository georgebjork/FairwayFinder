using FairwayFinder.Agents.Factory.Interface;
using FairwayFinder.Shared.Settings;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

namespace FairwayFinder.Agents.Factory;

public class OpenAiAgentFactory : IAgentFactory
{
    private readonly OpenAiSettings _openAiSettings;
    private readonly ChatClient _chatClient;
    private readonly ILogger<OpenAiAgentFactory> _logger;

    public OpenAiAgentFactory(IOptions<OpenAiSettings> openAiSettings, ILogger<OpenAiAgentFactory> logger)
    {
        _openAiSettings = openAiSettings.Value;
        _logger = logger;

        var client = new OpenAIClient(_openAiSettings.ApiKey);
        _chatClient = client.GetChatClient(_openAiSettings.Model);
    }

    public AIAgent Create(string name, string instructions, IList<AITool>? tools = null, string? description = null)
    {
        return _chatClient.AsAIAgent(
            instructions: instructions,
            name: name,
            description: description,
            tools: tools);
    }
}
