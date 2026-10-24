using System.Diagnostics;

namespace FairwayFinder.ServiceDefaults;

/// <summary>
/// Makes every incoming request the root of its own trace. Our callers are phones, not services we
/// trace: a <c>traceparent</c> from a client SDK points at a parent span that never reaches our
/// backend, so honouring it leaves every request span orphaned under a "(missing)" root.
/// Outgoing context is still written, so calls we make to other services carry our trace.
/// </summary>
internal sealed class IgnoreIncomingTraceContextPropagator(DistributedContextPropagator inner)
    : DistributedContextPropagator
{
    public override IReadOnlyCollection<string> Fields => inner.Fields;

    public override void Inject(Activity? activity, object? carrier, PropagatorSetterCallback? setter) =>
        inner.Inject(activity, carrier, setter);

    public override void ExtractTraceIdAndState(
        object? carrier, PropagatorGetterCallback? getter, out string? traceId, out string? traceState)
    {
        traceId = null;
        traceState = null;
    }

    public override IEnumerable<KeyValuePair<string, string?>>? ExtractBaggage(
        object? carrier, PropagatorGetterCallback? getter) => null;
}
