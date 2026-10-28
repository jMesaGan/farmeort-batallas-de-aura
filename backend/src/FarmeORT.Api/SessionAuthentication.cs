using System.Security.Claims;
using System.Text.Encodings.Web;
using FarmeORT.Application;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
namespace FarmeORT.Api;

public class SessionAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, GameService game) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return AuthenticateResult.NoResult();
        var user = await game.Authenticate(header[7..]);
        if (user is null) return AuthenticateResult.Fail("Sesión inválida o vencida.");
        Context.Items["User"] = user;
        var identity = new ClaimsIdentity([new(ClaimTypes.NameIdentifier, user.Id.ToString()), new(ClaimTypes.Role, user.Role)], Scheme.Name);
        return AuthenticateResult.Success(new(new ClaimsPrincipal(identity), Scheme.Name));
    }
    protected override Task HandleChallengeAsync(AuthenticationProperties properties) { Response.StatusCode = 401; return Response.WriteAsJsonAsync(new { message = "Iniciá sesión para continuar." }); }
    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) { Response.StatusCode = 403; return Response.WriteAsJsonAsync(new { message = "No tenés permiso para esta acción." }); }
}
