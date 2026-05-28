using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PoC.Improved.Domain;

namespace PoC.Improved.Api;

/// <summary>
/// Translates DomainException -> RFC 9457 ProblemDetails via IProblemDetailsService.
/// Non-DomainException falls through to the framework's default handler (500).
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(
        IProblemDetailsService problemDetailsService,
        ILogger<GlobalExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not DomainException domainEx)
            return false;

        _logger.LogWarning(
            "Domain exception {Type} -> HTTP {Status}: {Message}",
            domainEx.GetType().Name, domainEx.StatusCode, domainEx.UserMessage);

        httpContext.Response.StatusCode = domainEx.StatusCode;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = domainEx,
            ProblemDetails = new ProblemDetails
            {
                Type = $"https://poc.improved/errors/{domainEx.GetType().Name}",
                Title = domainEx.GetType().Name,
                Status = domainEx.StatusCode,
                Detail = domainEx.UserMessage,
                Instance = httpContext.Request.Path,
            },
        });
    }
}
