using FluentResults;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PoC.Improved.Application.Common;

namespace PoC.Improved.Api;

/// <summary>
/// Maps FluentResults Result&lt;T&gt; to HTTP. Success -> 200 OK; failure -> ProblemDetails
/// with status driven by the first error's subclass.
/// </summary>
public static class ResultExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
            return Results.Ok(result.Value);

        var error = result.Errors[0];
        var (status, title, code) = Categorize(error);

        return Results.Problem(new ProblemDetails
        {
            Type = $"https://poc.improved/errors/{code}",
            Title = title,
            Status = status,
            Detail = error.Message,
        });
    }

    private static (int Status, string Title, string Code) Categorize(IError error) => error switch
    {
        NotFoundError n     => (StatusCodes.Status404NotFound,     "NotFound",     n.Code),
        ValidationError v   => (StatusCodes.Status400BadRequest,   "Validation",   v.Code),
        ConflictError c     => (StatusCodes.Status409Conflict,     "Conflict",     c.Code),
        UnauthorizedError u => (StatusCodes.Status401Unauthorized, "Unauthorized", u.Code),
        ForbiddenError f    => (StatusCodes.Status403Forbidden,    "Forbidden",    f.Code),
        _                   => (StatusCodes.Status500InternalServerError, "Failure", "Unknown"),
    };
}
