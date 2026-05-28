using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace PoC.Improved.Application.Behaviors;

/// <summary>
/// Logs request name + elapsed time around every MediatR handler.
/// Sits outside ServiceCallHandler so we see total handler cost, retries included.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
        => _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var name = typeof(TRequest).Name;
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("Handling {Request}", name);
        try
        {
            var response = await next();
            sw.Stop();
            _logger.LogInformation(
                "Handled {Request} in {Elapsed}ms", name, sw.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(
                "Failed {Request} after {Elapsed}ms: {Exception}",
                name, sw.ElapsedMilliseconds, ex.GetType().Name);
            throw;
        }
    }
}
