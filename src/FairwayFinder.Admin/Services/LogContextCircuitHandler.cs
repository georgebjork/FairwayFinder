using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Serilog.Context;

namespace FairwayFinder.Admin.Services;

/// <summary>
/// Puts the signed-in admin on every log event raised while a circuit handles an inbound activity
/// (a click, a form submit, a JS interop callback). Interactive Blazor runs outside an HTTP request,
/// so the request middleware that does this for plain HTTP never sees these events.
/// </summary>
public class LogContextCircuitHandler(AuthenticationStateProvider authenticationStateProvider) : CircuitHandler
{
    private ClaimsPrincipal? _user;

    public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(
        Func<CircuitInboundActivityContext, Task> next)
    {
        return async context =>
        {
            // The handler is circuit-scoped and an admin cannot change identity mid-circuit,
            // so the user is read once.
            _user ??= (await authenticationStateProvider.GetAuthenticationStateAsync()).User;

            using (LogContext.PushProperty("UserId", _user.FindFirstValue(ClaimTypes.NameIdentifier)))
            using (LogContext.PushProperty("UserEmail", _user.FindFirstValue(ClaimTypes.Email)))
            {
                await next(context);
            }
        };
    }
}
