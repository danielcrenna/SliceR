using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace SliceR.Authorization;

public interface IAuthorizationProvider
{
    Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, string policyName, object? resource = null);
}
