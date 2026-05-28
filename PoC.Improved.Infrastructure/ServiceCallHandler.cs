using PoC.Improved.Domain;
using PoC.Improved.Infrastructure.ExceptionMapping;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Polly.Timeout;

namespace PoC.Improved.Infrastructure.Resilience;

public interface IServiceCallHandler
{
    Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        string operationDescription,
        string serviceName,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Wraps external service calls with: structured logging, cancellation handling,
/// Polly resilience (retry + timeout), and domain-exception mapping.
///
/// Compared to the original static ExecuteAsync:
///  - Injectable (DI-friendly, mockable in tests)
///  - Resilience built in (no more "first transient blip kills the request")
///  - Exception translation delegated to a registry (Open/Closed)
///  - No mixed Result/throw semantics: always throws DomainException on failure
/// </summary>
public sealed class ServiceCallHandler : IServiceCallHandler
{
    private readonly ILogger<ServiceCallHandler> _logger;
    private readonly IExceptionMapperRegistry _mappers;
    private readonly ResiliencePipeline _pipeline;

    public ServiceCallHandler(
        ILogger<ServiceCallHandler> logger,
        IExceptionMapperRegistry mappers)
    {
        _logger = logger;
        _mappers = mappers;

        _pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(200),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutRejectedException>(),
                OnRetry = args =>
                {
                    logger.LogWarning(
                        "Retry {Attempt} after {Delay}ms due to {Exception}",
                        args.AttemptNumber, args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.GetType().Name);
                    return ValueTask.CompletedTask;
                }
            })
            .AddTimeout(TimeSpan.FromSeconds(5))
            .Build();
    }

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        string operationDescription,
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _pipeline.ExecuteAsync(
                async ct => await operation(ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Caller-initiated cancellation: propagate as-is, don't mask as a domain error.
            _logger.LogInformation(
                "{Service}: caller cancelled while {Operation}",
                serviceName, operationDescription);
            throw;
        }
        catch (DomainException)
        {
            // Already a translated domain error (e.g. validation thrown inside `operation`).
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "{Service}: failure while {Operation}",
                serviceName, operationDescription);
            throw _mappers.Map(ex, operationDescription);
        }
    }
}
