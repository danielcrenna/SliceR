using MediatR;

namespace SliceR.Authorization;

public interface IAuthorizedRequest<out TResponse> : IRequest<TResponse>
{
    string? PolicyName { get; }
}