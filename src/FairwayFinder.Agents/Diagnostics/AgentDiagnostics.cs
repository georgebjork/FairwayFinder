using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace FairwayFinder.Agents.Diagnostics;

public static class AgentDiagnostics
{
    public const string Name = "FairwayFinder.Agents";

    public static readonly Meter Meter = new(Name, "1.0.0");
    public static readonly ActivitySource Activity = new(Name);

    public static readonly Counter<long> AgentCalls =
        Meter.CreateCounter<long>("fairwayfinder.agents.calls", description: "Agent runs invoked");
    public static readonly Counter<long> AgentCallErrors =
        Meter.CreateCounter<long>("fairwayfinder.agents.call.errors", description: "Agent runs that threw");
    public static readonly Counter<long> AgentInputTokens =
        Meter.CreateCounter<long>("fairwayfinder.agents.tokens.input", description: "Input tokens consumed by agent runs");
    public static readonly Counter<long> AgentOutputTokens =
        Meter.CreateCounter<long>("fairwayfinder.agents.tokens.output", description: "Output tokens produced by agent runs");
    public static readonly Histogram<double> AgentCallDuration =
        Meter.CreateHistogram<double>("fairwayfinder.agents.call.duration", unit: "ms", description: "Agent run duration");

    public static class ActivityNames
    {
        public const string AgentRun = "agent.run";
    }

    public static class Tags
    {
        public const string Agent = "agent";
        public const string Error = "error";
    }

    public static class ActivityTags
    {
        public const string AgentIdentifier = "agent.identifier";
    }
}
