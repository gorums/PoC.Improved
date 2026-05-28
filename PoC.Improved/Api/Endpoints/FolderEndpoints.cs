using PoC.Improved.Application.Cqrs;
using PoC.Improved.Application.Folders;

namespace PoC.Improved.Api.Endpoints;

public static class FolderEndpoints
{
    public static IEndpointRouteBuilder MapFolderEndpoints(this IEndpointRouteBuilder app)
    {
        // List folders for a feature.
        app.MapGet("/folders/{feature?}",
            async (string? feature, IMediator mediator, CancellationToken ct) =>
            {
                var result = await mediator.Send(new GetFoldersQuery(feature ?? ""), ct);
                return result.ToHttpResult();
            });

        // Look up one folder. Demonstrates Result.Fail(NotFoundError) -> 404.
        app.MapGet("/folders/{feature}/{year:int}",
            async (string feature, int year, IMediator mediator, CancellationToken ct) =>
            {
                var result = await mediator.Send(new GetFolderQuery(feature, year), ct);
                return result.ToHttpResult();
            });

        // Create folder. Demonstrates Result.Fail(ConflictError) -> 409.
        app.MapPost("/folders/{feature}/{year:int}",
            async (string feature, int year, IMediator mediator, CancellationToken ct) =>
            {
                var result = await mediator.Send(new CreateFolderCommand(feature, year), ct);
                return result.ToHttpResult();
            });

        return app;
    }
}
