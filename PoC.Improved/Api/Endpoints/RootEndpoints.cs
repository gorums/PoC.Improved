namespace PoC.Improved.Api.Endpoints;

public static class RootEndpoints
{
    public static IEndpointRouteBuilder MapRootEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", () => Results.Ok(new
        {
            message = "PoC.Improved demo - all endpoints flow through the custom mediator",
            endpoints = new[]
            {
                "GET  /folders/photos             -> 200 (happy path)",
                "GET  /folders/flaky              -> 200 after Polly retries",
                "GET  /folders/down               -> 503 ExternalServiceException",
                "GET  /folders/slow               -> 408 Polly timeout (~21s)",
                "GET  /folders/                   -> 400 ValidationBehavior",
                "GET  /folders/photos/2025        -> 200 Result.Ok(FolderDetails)",
                "GET  /folders/photos/2030        -> 404 Result.Fail(NotFoundError)",
                "POST /folders/photos/2030        -> 200 created",
                "POST /folders/photos/2025        -> 409 Result.Fail(ConflictError)",
                "GET  /folders/stream/photos     -> 200 streaming JSON array via IStreamRequest",
                "GET  /file-url?path=a/b.jpg      -> 200",
                "GET  /file-url                   -> 400 ValidationBehavior",
            }
        }));

        return app;
    }
}
