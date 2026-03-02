using FairwayFinder.Agents.Agents;
using FairwayFinder.Agents.Factory;
using FairwayFinder.Agents.Factory.Interface;
using Microsoft.Extensions.DependencyInjection;

namespace FairwayFinder.Agents;

public static class ServiceRegistration
{
    public static IServiceCollection AddAgentServices(this IServiceCollection services)
    {
        services.AddTransient<IAgentFactory, OpenAiAgentFactory>();
        services.AddTransient<ScorecardScoresReaderAgent>();
        return services;
    }
}
