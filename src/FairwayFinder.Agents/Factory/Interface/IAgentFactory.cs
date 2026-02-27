using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace FairwayFinder.Agents.Factory.Interface;

public interface IAgentFactory
{
    AIAgent Create(string name, string instructions, IList<AITool>? tools = null, string? description = null);
}
