namespace SliceR.Authorization;

public interface IAuthenticatedRequest<out TResponse> : IAuthorizedRequest<TResponse>
{
    string? IAuthorizedRequest<TResponse>.PolicyName => null;
}