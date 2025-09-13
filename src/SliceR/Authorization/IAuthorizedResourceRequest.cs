namespace SliceR.Authorization;

public interface IAuthorizedResourceRequest<out TResponse> : IAuthorizedRequest<TResponse>;

public interface IAuthorizedResourceRequest<TResource, out TResponse> : IAuthorizedResourceRequest<TResponse>
{
    // ReSharper disable once UnusedMemberInSuper.Global
    TResource? Resource { get; set; }
}
