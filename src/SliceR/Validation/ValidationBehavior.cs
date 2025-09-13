using FluentValidation;
using MediatR;

namespace SliceR.Validation;

internal sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators) :
    IPipelineBehavior<TRequest, TResponse> where TRequest : class
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!validators.Any())
            return await next(cancellationToken).ConfigureAwait(false);

        await ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        return await next(cancellationToken).ConfigureAwait(false);
    }

    private async Task ValidateAsync(TRequest request, CancellationToken cancellationToken)
    {
        var context = new ValidationContext<TRequest>(request);

        var results = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, cancellationToken))
        ).ConfigureAwait(false);

        var failures = results
            .SelectMany(r => r.Errors)
            .ToList();

        if (failures.Count > 0)
        {
            throw new ValidationException("Request has one or more validation errors.", failures);
        }
    }
}
