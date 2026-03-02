using FairwayFinder.Agents.Factory.Interface;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace FairwayFinder.Agents.Agents.Base;

public abstract class BaseAgent<T>
{
    private readonly IAgentFactory _agentFactory;

    protected BaseAgent(IAgentFactory agentFactory)
    {
        _agentFactory = agentFactory;
    }

    /// <summary>
    /// Unique identifier for this agent. Used as the agent name when creating the AIAgent.
    /// </summary>
    public abstract string AgentIdentifier { get; }

    /// <summary>
    /// System instructions/prompt for the agent. Override in subclasses to provide agent-specific instructions.
    /// </summary>
    protected virtual string GetInstructions => "";

    /// <summary>
    /// Optional description of the agent's purpose. Override in subclasses if needed.
    /// </summary>
    protected virtual string? GetDescription => null;

    /// <summary>
    /// Optional tools the agent can use. Override in subclasses to provide agent-specific tools.
    /// </summary>
    protected virtual IList<AITool>? GetTools() => null;

    /// <summary>
    /// Builds the AIAgent using the factory with this agent's configuration.
    /// </summary>
    public AIAgent GetAgent()
    {
        return _agentFactory.Create(
            AgentIdentifier,
            GetInstructions,
            GetTools(),
            GetDescription);
    }

    /// <summary>
    /// Runs the agent with a text message and returns a typed response.
    /// </summary>
    public async Task<AgentResponse<T>> RunAsync(
        string message,
        AgentSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var agent = GetAgent();
        session ??= await agent.CreateSessionAsync(cancellationToken);
        return await agent.RunAsync<T>(message, session, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Runs the agent with a collection of chat messages and returns a typed response.
    /// </summary>
    public async Task<AgentResponse<T>> RunAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var agent = GetAgent();
        session ??= await agent.CreateSessionAsync(cancellationToken);
        return await agent.RunAsync<T>(messages, session, cancellationToken: cancellationToken);
    }
}
