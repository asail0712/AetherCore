using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using AetherCore.Utility.Lincense;

namespace AetherCore.WebSockets
{
    public sealed class WebSocketIdentity
    {
        public string ConnectionId { get; init; }   = default!;
        public string UserId { get; init; }         = default!;
        public string UserName { get; init; }       = default;
        public string? Role { get; init; }
        public IReadOnlyDictionary<string, string> Claims { get; init; }
    }

    static public class WebsocketTools
    {
        static public RouteHandlerBuilder Map<T>(this WebApplication app, string path) where T : WebsocketHub
        {
            return app.Map(path, async (
                HttpContext ctx, 
                T hub,
                IAuthenticationService authService,
                IOptions<AuthenticationOptions> authOptions
                ) =>
            {                
                if (!ctx.WebSockets.IsWebSocketRequest)
                {
                    ctx.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    return;
                }

                await XPlanLicenseRuntime.EnsureValidOrThrowAsync();

                AuthenticateResult? authResult = null;
                
                try
                {
                    // 依照 program 設定來決定預設的scheme
                    authResult = await authService.AuthenticateAsync
                    (
                        ctx,
                        authOptions.Value.DefaultAuthenticateScheme
                    );
                }
                catch(Exception e)
                {
                    ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    return;
                }

                if (authResult == null || !authResult.Succeeded || authResult.Principal == null)
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }

                var principal       = authResult.Principal;

                var identity        = new WebSocketIdentity
                {
                    ConnectionId    = ctx.Connection.Id,

                    UserId          = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                            ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                            ?? throw new InvalidOperationException("uid missing"),

                    UserName        = principal.FindFirstValue(ClaimTypes.Name),

                    Role            = principal.FindFirstValue(ClaimTypes.Role),

                    Claims          = principal.Claims
                        .GroupBy(c => c.Type)
                        .ToDictionary(g => g.Key, g => g.First().Value)
                };

                if (string.IsNullOrWhiteSpace(identity.UserId))
                {
                    ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }

                // 2) 獲取query string
                var headerDict = ctx.Request.Headers.ToDictionary
                (
                    kv => kv.Key,
                    kv => kv.Value,
                    comparer: StringComparer.OrdinalIgnoreCase
                );

                // 3) 申請 websocket 並使用uid註冊
                using var socket = await ctx.WebSockets.AcceptWebSocketAsync();

                // 4) 交給Hub管理 websocket
                await hub.RunAsync(identity, socket, headerDict, ctx.RequestAborted);
            });
        }
    }
}
