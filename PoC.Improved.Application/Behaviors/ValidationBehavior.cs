using FluentValidation;
using PoC.Improved.Application.Cqrs;
using PoC.Improved.Domain;

namespace PoC.Improved.Application.Behaviors;

/// <summary>
/// Runs all registered FluentValidation validators for the request before the handler.
/// Failure -> BadInputException, which the GlobalExceptionHandler turns into 400.
/// Keeps validation out of handlers entirely.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        => _validators = validators;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);
        var failures = (await Task.WhenAll(
                _validators.Select(v => v.ValidateAsync(context, cancellationToken))))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count > 0)
        {
            var message = string.Join("; ", failures.Select(f => f.ErrorMessage));
            throw new BadInputException(message);
        }

        return await next();
    }
}
