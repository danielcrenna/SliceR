using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace SliceR.Authorization;

internal sealed class AuthorizationProvider(IAuthorizationService authorizationService) : IAuthorizationProvider
{
    public async Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, string policyName, object? resource = null)
    {
        return await authorizationService.AuthorizeAsync(user, resource, policyName);
    }
}