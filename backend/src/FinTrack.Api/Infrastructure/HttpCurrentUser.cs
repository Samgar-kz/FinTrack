using System.Security.Claims;
using FinTrack.Application.Abstractions;
using FinTrack.Application.Common;
using FinTrack.Domain.Users;
using FinTrack.Infrastructure.Security;

namespace FinTrack.Api.Infrastructure;

internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid Id =>
        Guid.TryParse(Principal?.FindFirstValue(FinTrackClaims.Subject), out var id)
            ? id
            : throw new UnauthorizedException("Authentication is required.");

    public bool IsAdmin => Principal?.IsInRole(nameof(UserRole.Admin)) == true;

    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;
}
