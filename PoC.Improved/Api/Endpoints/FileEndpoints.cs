using PoC.Improved.Application.Cqrs;
using PoC.Improved.Application.Files;

namespace PoC.Improved.Api.Endpoints;

public static class FileEndpoints
{
    public static IEndpointRouteBuilder MapFileEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/file-url",
            async (string? path, IMediator mediator, CancellationToken ct) =>
            {
                var result = await mediator.Send(new GetFileUrlQuery(path), ct);
                return result.ToHttpResult();
            });

        return app;
    }
}
