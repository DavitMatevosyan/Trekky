using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Trekky.Modules.Identity.Infrastructure;

namespace Trekky.Modules.Identity.Api.Endpoints;

/// <summary>OpenIddict's authorization and token endpoints, outside <c>/api/v1</c> (ADR-0003); the handlers live in Infrastructure.</summary>
internal static class ConnectEndpoints
{
    public static IEndpointRouteBuilder MapConnectEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapMethods(
                "/connect/authorize",
                [HttpMethods.Get, HttpMethods.Post],
                (HttpContext http, ConnectHandlers handlers, CancellationToken cancellationToken) => handlers.AuthorizeAsync(http, cancellationToken))
            .AllowAnonymous()
            .ExcludeFromDescription();

        endpoints.MapPost(
                "/connect/token",
                (HttpContext http, ConnectHandlers handlers, CancellationToken cancellationToken) => handlers.ExchangeAsync(http, cancellationToken))
            .AllowAnonymous()
            .ExcludeFromDescription();

        return endpoints;
    }
}
