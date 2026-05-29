# PoC.Improved

A reference .NET 10 PoC demonstrating **Clean Architecture**, a **custom mediator** with pipeline behaviors (zero MediatR dependency), **FluentResults** at the handler boundary, **Polly v8** resilience, and **RFC 9457 ProblemDetails** error responses.

It is the *improved* counterpart of a legacy `ServiceCallHandlerWrapper` and exists to show — concretely and runnably — what each refactor buys you.

## What it improves over the original code

| Original | Improved |
|---|---|
| Giant centralized `switch` on exception type | `IExceptionMapper` registry, open for extension |
| `new ServiceCallHandlerWrapper<...>` in the constructor | Injected via DI (`IServiceCallHandler`) |
| Mixed `Result` and `throw` semantics | FluentResults `Result<T>` at handler boundary + `DomainException` for infra failures |
| No retry: one transient blip = failure | Polly v8 pipeline: retry (exponential backoff + jitter) + timeout |
| `ExecuteServiceException(ex.Message, ex)` loses context | `DomainException` carries `StatusCode` and `UserMessage` |
| HTTP status codes scattered in `Infrastructure` | HTTP lives only in `Api/` via `IProblemDetailsService` |
| Hard to test | Every piece mockable through its interface (66 unit tests) |
| Third-party mediator dependency | In-house mediator (~150 LOC) under our control |

## Solution layout

Clean Architecture: each layer is its own project. Dependencies point inward.

```
PoC.Improved.slnx
├── PoC.Improved.Domain/              (no dependencies)
│   └── Exceptions.cs                 # DomainException + subclasses (status code in domain)
├── PoC.Improved.Application/         (depends on Domain)
│   ├── Cqrs/                         # Custom mediator (replaces MediatR, ~150 LOC)
│   │   ├── IRequest.cs               # IRequest<TResponse> marker
│   │   ├── IRequestHandler.cs        # IRequestHandler<TRequest, TResponse>
│   │   ├── IPipelineBehavior.cs      # IPipelineBehavior<,> + RequestHandlerDelegate<>
│   │   ├── ISender.cs                # Narrow Send<T>(...) surface for callers
│   │   ├── IMediator.cs              # Extends ISender; reserved for future Publish etc.
│   │   ├── Mediator.cs               # Typed wrapper cache, no per-call reflection
│   │   └── MediatorServiceCollectionExtensions.cs   # AddMediator(cfg => ...)
│   ├── Common/
│   │   └── Errors.cs                 # CategorizedError + NotFound/Validation/Conflict/Unauthorized/Forbidden
│   ├── Providers/
│   │   └── IStorageProvider.cs       # Abstraction owned by Application
│   ├── Behaviors/
│   │   ├── LoggingBehavior.cs        # Pipeline behavior: timing + logs
│   │   └── ValidationBehavior.cs     # Pipeline behavior: FluentValidation -> BadInputException
│   ├── Folders/
│   │   ├── GetFoldersQuery / Handler / Validator
│   │   ├── GetFolderQuery / Handler / Validator        (-> Result.Fail(NotFoundError))
│   │   └── CreateFolderCommand / Handler / Validator   (-> Result.Fail(ConflictError))
│   └── Files/
│       └── GetFileUrlQuery / Handler / Validator
├── PoC.Improved.Infrastructure/      (depends on Application + Domain)
│   ├── ExceptionMapping.cs           # IExceptionMapper + registry + concrete mappers
│   ├── ServiceCallHandler.cs         # Polly retry + timeout wrapper
│   └── Providers/FakeStorageProvider.cs
├── PoC.Improved/                     (web host, depends on all)
│   ├── Api/
│   │   ├── GlobalExceptionHandler.cs # DomainException -> ProblemDetails (IProblemDetailsService)
│   │   ├── ResultExtensions.cs       # Result<T> -> 200 OK or ProblemDetails
│   │   └── Endpoints/                # Folder + File + Root endpoints
│   └── Program.cs
└── PoC.Improved.Tests/               (xUnit v3 + NSubstitute, 66 tests)
```

Dependency direction: `Api -> Infrastructure -> Application -> Domain`. The Application layer never references Infrastructure — `IStorageProvider` lives in Application so handlers depend on the abstraction.

## Request pipeline

```
HTTP endpoint
   -> IMediator.Send(query)              (PoC.Improved.Application.Cqrs — custom impl)
      -> LoggingBehavior                 (logs request name + elapsed)
         -> ValidationBehavior           (FluentValidation; failure -> BadInputException)
            -> Handler                   (IRequestHandler<TRequest, Result<TResponse>>)
               -> IStorageProvider
                  -> IServiceCallHandler (Polly retry + timeout, maps infra exceptions)
                     -> external call
```

## Failure paths

Five distinct outcomes flow through the same endpoint code (`return result.ToHttpResult();`):

| Outcome | HTTP | How |
|---|---|---|
| `Result.Ok(value)` | `200 OK` (JSON) | Endpoint returns the value |
| `Result.Fail(NotFoundError)` | `404` `application/problem+json` | `ResultExtensions.ToHttpResult` pattern-matches the error subclass |
| `Result.Fail(ConflictError)` | `409` `application/problem+json` | Same — different subclass |
| Validation failure (throw) | `400` `application/problem+json` | `ValidationBehavior` throws `BadInputException`; `GlobalExceptionHandler` writes ProblemDetails via `IProblemDetailsService` |
| Infrastructure failure (throw) | `503` / `408` / `500` | `IExceptionMapper` translates infra exception to `DomainException`; `GlobalExceptionHandler` writes ProblemDetails |

All error responses follow RFC 9457: `type` (URI), `title`, `status`, `detail`, `instance`, plus a `traceId` extension injected by `AddProblemDetails(options => options.CustomizeProblemDetails = ...)`.

## Requirements

- .NET 10 SDK
- `curl` (or any HTTP client)

## How to run

```bash
git clone https://github.com/gorums/PoC.Improved
cd PoC.Improved
dotnet run --project PoC.Improved
```

By default it listens on `http://localhost:5000`.

## Endpoints to try

| Method | URL | Demonstrates | Result |
|---|---|---|---|
| GET | `/` | Discovery index | `200` |
| GET | `/folders/photos` | Happy path | `200` |
| GET | `/folders/flaky` | Polly retry (silent recovery after 2 failures) | `200` |
| GET | `/folders/down` | Infra failure → `ExternalServiceException` | `503` |
| GET | `/folders/slow` | Polly timeout (~21s total across retries) | `408` |
| GET | `/folders/` | `ValidationBehavior` rejects empty feature | `400` |
| GET | `/folders/photos/2025` | `Result.Ok(FolderDetails)` | `200` |
| GET | `/folders/photos/2030` | `Result.Fail(NotFoundError)` | `404` |
| POST | `/folders/photos/2030` | New folder | `200` |
| POST | `/folders/photos/2025` | `Result.Fail(ConflictError)` | `409` |
| GET | `/file-url?path=a/b.jpg` | Happy path | `200` |
| GET | `/file-url` | `ValidationBehavior` rejects missing path | `400` |

```bash
curl -i http://localhost:5000/folders/photos
curl -i http://localhost:5000/folders/flaky        # watch the logs for "Retry 1..."
curl -i http://localhost:5000/folders/down
curl -i http://localhost:5000/folders/slow         # ~21s, then 408
curl -i http://localhost:5000/folders/photos/2030  # 404 Result.Fail(NotFoundError)
curl -i -X POST http://localhost:5000/folders/photos/2025  # 409 Result.Fail(ConflictError)
```

## Tests

```bash
dotnet test
```

**66 tests** across:
- Domain exceptions (status code + inner exception preserved)
- All `IExceptionMapper`s + registry fallback
- `LoggingBehavior`, `ValidationBehavior`
- Every handler (success path + business failure + exception propagation)
- All validators
- All FluentResults `CategorizedError` subclasses
- Custom mediator (handler routing, ISender/IMediator share instance, missing-handler error, behavior order, `AddOpenBehavior` validation)

xUnit v3, NSubstitute for mocks. No integration tests yet — the smoke path is exercised manually via curl.

## Notable design choices

- **`Result<T>` at the handler boundary only.** Validation + infrastructure failures still throw — they're caught by `GlobalExceptionHandler` and translated to ProblemDetails. Result is reserved for *expected business outcomes* (NotFound, Conflict, …).
- **`IStorageProvider` lives in Application.** Handlers depend on the abstraction, Infrastructure provides the implementation. This is the Clean Architecture dependency rule made concrete.
- **`DomainException` carries `StatusCode`.** Pragmatic shortcut — a stricter DDD would expose `ErrorCategory` and let the Api layer translate. The status code in Domain is the trade-off.
- **`IExceptionMapper` registry is Open/Closed.** Adding a new error type (e.g. `DbUpdateConcurrencyException` → 409) is one class + one DI registration, no central `switch` to edit.
- **Polly v8 `ResiliencePipelineBuilder`** with explicit retry-on `HttpRequestException` and `TimeoutRejectedException`. The `TimeoutMapper` includes both `TimeoutException` and `TimeoutRejectedException` (a common mistake — Polly v8 throws its own type).

## From MediatR to a custom mediator

The project originally used [MediatR](https://github.com/jbogard/MediatR). A constraint to remove the dependency drove the swap to an in-house implementation that lives in `PoC.Improved.Application/Cqrs/`.

### Why

- **No third-party package** to track for license changes, version churn, or transitive deps.
- **Only what we use** is implemented — ~150 LOC instead of MediatR's full surface area.
- **Names match MediatR's** so handler/behavior files migrate with a single `using` swap.

### Surface implemented (everything we actually used)

| Type | Purpose |
|---|---|
| `IRequest<out TResponse>` | Marker for a request that returns `TResponse` |
| `IRequestHandler<in TRequest, TResponse>` | Handler contract — single `Handle(request, ct)` method |
| `IPipelineBehavior<in TRequest, TResponse>` | Cross-cutting wrapper around the handler call |
| `RequestHandlerDelegate<TResponse>` | The `next` continuation passed to behaviors |
| `ISender` | Narrow surface: just `Send<TResponse>(IRequest<TResponse>, ct)`. Inject this at call sites that only send (endpoints, controllers) to keep the dependency surface minimal. |
| `IMediator` | Extends `ISender`. Reserved for additions like `IPublisher.Publish` if notifications are ever added. |
| `Mediator` | Implementation. Caches a per-request-type `RequestHandlerWrapper` so dispatch stays strongly typed (no per-call reflection on the handler interface) |
| `AddMediator(cfg => …)` | DI extension with `RegisterServicesFromAssemblyContaining<T>()` and `AddOpenBehavior(typeof(B<,>))` — same call shape as `AddMediatR(...)` |

### Surface NOT implemented (didn't need)

`INotification` / `INotificationHandler` (pub-sub), `IStreamRequest`, separate `ISender` / `IPublisher`, exception handlers, pre/post processors, notification-publisher strategies, request-without-response variant.

When any of these are needed, add a single interface + wrapper class to `Cqrs/`. The surface stays under our control.

### How the dispatch works

```csharp
public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
{
    // Cache a wrapper per concrete request type. The wrapper closes the open generic
    // (TRequest, TResponse), so the resolved handler + behaviors are strongly typed.
    var wrapper = (RequestHandlerWrapper<TResponse>)_wrappers.GetOrAdd(
        request.GetType(),
        reqType => (RequestHandlerWrapperBase)Activator.CreateInstance(
            typeof(RequestHandlerWrapperImpl<,>).MakeGenericType(reqType, typeof(TResponse)))!);

    return wrapper.Handle(request, _serviceProvider, ct);
}
```

The wrapper resolves the handler + all matching `IPipelineBehavior<TRequest, TResponse>` from DI and chains them in registration order (first-registered = outermost). No `dynamic`, no per-call reflection on the handler method.

### Migration: what changed in the rest of the solution

```diff
- using MediatR;
+ using PoC.Improved.Application.Cqrs;
```

That's it for handler, validator, behavior, query, command, and endpoint files (14 files in this codebase).

`Program.cs` got two small changes:

```diff
+ using PoC.Improved.Application.Cqrs;

- builder.Services.AddMediatR(cfg =>
+ builder.Services.AddMediator(cfg =>
  {
      cfg.RegisterServicesFromAssemblyContaining<GetFoldersQuery>();
      cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
      cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
  });
```

The `MediatR` `<PackageReference>` was dropped from `PoC.Improved.Application.csproj`; `Microsoft.Extensions.DependencyInjection.Abstractions` was added (needed for the DI extension method to live in this assembly).

### How to use it

**1. Define a request + response.**

```csharp
using PoC.Improved.Application.Cqrs;

public sealed record GetFolderQuery(string Feature, int Year) : IRequest<Result<FolderDetails>>;
public sealed record FolderDetails(string Feature, int Year, string Path);
```

**2. Implement the handler.**

```csharp
using PoC.Improved.Application.Cqrs;

public sealed class GetFolderHandler : IRequestHandler<GetFolderQuery, Result<FolderDetails>>
{
    private readonly IStorageProvider _storage;
    public GetFolderHandler(IStorageProvider storage) => _storage = storage;

    public async Task<Result<FolderDetails>> Handle(GetFolderQuery request, CancellationToken ct)
    {
        var folders = await _storage.GetFoldersAsync(request.Feature, ct);
        var path = $"{request.Feature}/{request.Year}";
        return folders.Contains(path)
            ? Result.Ok(new FolderDetails(request.Feature, request.Year, path))
            : Result.Fail(new NotFoundError("Folder.NotFound", $"No folder for {path}."));
    }
}
```

**3. (Optional) Add a pipeline behavior.**

```csharp
using PoC.Improved.Application.Cqrs;

public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        // before
        var response = await next();
        // after
        return response;
    }
}
```

**4. Register in `Program.cs`.**

```csharp
builder.Services.AddMediator(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<GetFolderQuery>();
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));   // order = outer-to-inner
});
```

`RegisterServicesFromAssemblyContaining<T>()` scans the assembly for all `IRequestHandler<,>` implementations and registers each as transient. `AddOpenBehavior(typeof(B<,>))` validates the type is an open generic, then registers it as `IPipelineBehavior<,>` so DI resolves the closed form per request.

**5. Send a request from an endpoint.**

Endpoints inject `ISender` instead of the full `IMediator` — narrower dependency,
clearer intent (this code only sends, it doesn't publish or anything else).

```csharp
app.MapGet("/folders/{feature}/{year:int}",
    async (string feature, int year, ISender sender, CancellationToken ct) =>
    {
        var result = await sender.Send(new GetFolderQuery(feature, year), ct);
        return result.ToHttpResult();
    });
```

Both `ISender` and `IMediator` resolve to the same scoped `Mediator` instance —
`MediatorTests.ISender_resolves_to_the_same_instance_as_IMediator` pins this.

### Behavior order

Behaviors wrap the handler in the order they were registered: **first-registered is outermost**. With `LoggingBehavior` then `ValidationBehavior` registered, the call order is:

```
LoggingBehavior.before
  ValidationBehavior.before
    Handler.Handle
  ValidationBehavior.after
LoggingBehavior.after
```

A unit test in `MediatorTests.Send_invokes_pipeline_behaviors_in_registration_order_outer_first` pins this ordering.

## Exception handling end-to-end

Exception handling is a **two-tier system**. Infrastructure exceptions (HTTP, S3, EF, timeouts) never reach the API layer directly — they're translated into `DomainException`s by the mapper registry, then `GlobalExceptionHandler` writes the ProblemDetails response. The Application/Domain layers never see SDK-specific exceptions; the Api layer never sees raw infra exceptions.

### Components

| Layer | Type | Job |
|---|---|---|
| Domain | `DomainException` (abstract) | Base for any exception that carries an HTTP `StatusCode` + a `UserMessage`. The whole system speaks this language. |
| Domain | `ExternalServiceException` (503), `OperationTimeoutException` (408), `ResourceConflictException` (409), `BadInputException` (400), `UnhandledExternalException` (500) | Concrete `DomainException`s. Each is one HTTP-level outcome. |
| Infrastructure | `IExceptionMapper` | Pair of methods: `CanMap(ex)` decides whether to handle this exception; `Map(ex, op)` returns the corresponding `DomainException`. Each mapper covers one *family* of exceptions (e.g. all HTTP, all timeouts, all S3). |
| Infrastructure | `ExceptionMapperRegistry` | Iterates registered mappers in DI order; **first match wins**. If nothing matches, falls back to `UnhandledExternalException` (500). |
| Infrastructure | `ServiceCallHandler` | Wraps every external call with the Polly pipeline + a `try/catch (Exception ex) { throw _mappers.Map(ex, op); }`. This is the *only* place infrastructure exceptions get translated. |
| Api | `GlobalExceptionHandler` | `IExceptionHandler` implementation. Catches any `DomainException` that escapes a handler and writes a ProblemDetails response via `IProblemDetailsService` using `domainEx.StatusCode` + `domainEx.UserMessage`. Non-`DomainException` falls through to the framework's default 500. |

### Flow

```
external call throws (e.g. HttpRequestException, AmazonS3Exception, TimeoutRejectedException)
   └─> ServiceCallHandler.ExecuteAsync catch block
          └─> IExceptionMapperRegistry.Map(ex, "listing folders for feature 'down'")
                 └─> first matching IExceptionMapper.Map(ex, op)
                        └─> throw new ExternalServiceException("HTTP", "Upstream HTTP call failed while listing folders for feature 'down'.", ex)
                               └─> bubbles up through handler / behaviors / endpoint
                                      └─> GlobalExceptionHandler.TryHandleAsync sees the DomainException
                                             └─> IProblemDetailsService.TryWriteAsync(ProblemDetails { Status = 503, Detail = ..., Title = "ExternalServiceException", ... })
                                                    └─> HTTP 503 application/problem+json
```

Three things to notice:

1. **No central `switch`.** `ExceptionMapperRegistry` is a `foreach` over an injected list. Adding a new exception type *cannot* require editing existing mappers.
2. **First-match-wins.** Order matters when families overlap. Register the most specific mapper first.
3. **`UnhandledExternalException` fallback.** An unknown exception always becomes a 500 with the original message + inner exception preserved — no silent swallowing.

### Why Open/Closed

Each `IExceptionMapper` covers one closed concern. Adding support for a new SDK means **adding a new class + one DI line**. No existing mapper is touched, no central file grows. That's the literal definition of Open/Closed: open for extension, closed for modification.

The original `ServiceCallHandlerWrapper` had a giant `switch (ex)` listing every exception type — every new SDK forced an edit. The registry pattern eliminates that.

## Adding new exception handling — developer workflow

There are two scenarios. Pick the one that matches.

### Scenario A — Map a new infra exception to an *existing* DomainException

Use this when the HTTP outcome you want already has a `DomainException` subclass (e.g. 409 Conflict is `ResourceConflictException`). You just need a mapper that recognises the new exception type and produces it.

**Example:** map `DbUpdateConcurrencyException` (EF Core optimistic-concurrency failure) → `409 Conflict`.

**Step 1.** Add the mapper next to the existing ones in `PoC.Improved.Infrastructure/ExceptionMapping.cs`:

```csharp
public sealed class DbConcurrencyMapper : IExceptionMapper
{
    public bool CanMap(Exception ex) =>
        ex.GetType().Name == "DbUpdateConcurrencyException";

    public DomainException Map(Exception ex, string op) =>
        new ResourceConflictException(
            $"Data was modified by another user while {op}.", ex);
}
```

The `GetType().Name` check is the trick to recognise an EF exception **without** adding an EF Core reference to `Infrastructure` (or worse, to `Domain`). This is the same pattern `S3LikeMapper` uses for `AmazonS3Exception`.

**Step 2.** Register it in `Program.cs` alongside the other mappers:

```csharp
builder.Services.AddSingleton<IExceptionMapper, DbConcurrencyMapper>();
```

**That's it.** No other file changes. The next request that hits a `DbUpdateConcurrencyException` returns:

```
HTTP/1.1 409 Conflict
Content-Type: application/problem+json

{
  "type":   "https://poc.improved/errors/ResourceConflictException",
  "title":  "ResourceConflictException",
  "status": 409,
  "detail": "Data was modified by another user while saving the order.",
  "instance": "/orders/42",
  "traceId": "..."
}
```

**Checklist for Scenario A:**
- [ ] One mapper class in `PoC.Improved.Infrastructure/ExceptionMapping.cs`
- [ ] One `AddSingleton<IExceptionMapper, …>` in `Program.cs`
- [ ] One unit test in `PoC.Improved.Tests/Infrastructure/ExceptionMappingTests.cs` (mirror the existing `HttpMapperTests` / `S3LikeMapperTests` pattern)

### Scenario B — Introduce a brand-new HTTP outcome

Use this when no existing `DomainException` subclass matches the HTTP status code you need (e.g. you want `429 Too Many Requests`).

**Example:** map upstream `RateLimitExceededException` → `429 Too Many Requests`.

**Step 1.** Add a new `DomainException` subclass in `PoC.Improved.Domain/Exceptions.cs`:

```csharp
public sealed class TooManyRequestsException : DomainException
{
    public TooManyRequestsException(string message, Exception? inner = null)
        : base(429, message, inner) { }
}
```

The status code is the only thing the new subclass needs. Everything else (base ctor, `StatusCode` property, `UserMessage` property, inner-exception preservation) is inherited.

**Step 2.** Add the mapper in `PoC.Improved.Infrastructure/ExceptionMapping.cs`:

```csharp
public sealed class RateLimitMapper : IExceptionMapper
{
    public bool CanMap(Exception ex) =>
        ex.GetType().Name == "RateLimitExceededException";

    public DomainException Map(Exception ex, string op) =>
        new TooManyRequestsException($"Rate limit hit while {op}.", ex);
}
```

**Step 3.** Register it in `Program.cs`:

```csharp
builder.Services.AddSingleton<IExceptionMapper, RateLimitMapper>();
```

**That's it.** `GlobalExceptionHandler` already knows how to translate any `DomainException` to ProblemDetails — it reads `StatusCode` and `UserMessage` and writes them out. No change to `GlobalExceptionHandler`, no change to `ServiceCallHandler`, no change to `ExceptionMapperRegistry`.

**Checklist for Scenario B:**
- [ ] One new subclass in `PoC.Improved.Domain/Exceptions.cs`
- [ ] One mapper class in `PoC.Improved.Infrastructure/ExceptionMapping.cs`
- [ ] One `AddSingleton<IExceptionMapper, …>` in `Program.cs`
- [ ] Two unit tests: one in `DomainExceptionTests.cs` (status code), one in `ExceptionMappingTests.cs` (mapper behaviour)

### Ordering caveats

- **Specific before generic.** `S3LikeMapper` is registered before `HttpMapper` so an `AmazonS3Exception` (which is also an HTTP-related exception under the hood) gets the `503 S3` outcome, not the generic 503.
- **`TimeoutMapper` covers four types** (`TaskCanceledException`, `OperationCanceledException`, `TimeoutException`, `TimeoutRejectedException`) — Polly v8 throws its own `TimeoutRejectedException`, which is the classic mistake to miss. Make sure new timeout sources extend this mapper, not a separate one.
- **Caller cancellation propagates as-is.** `ServiceCallHandler` re-throws `OperationCanceledException` when the caller's `CancellationToken` fired — it's not an infra failure, so it skips the mapper registry. The `TimeoutMapper` only catches *Polly-initiated* cancellations.

### What never changes when you add a mapper

- `ExceptionMapperRegistry` — never edited
- `ServiceCallHandler` — never edited
- `GlobalExceptionHandler` — never edited
- Any existing mapper — never edited
- Any handler, validator, behavior, or endpoint — never edited

That's the surface area Open/Closed gives you: one new class + one DI line, and the whole pipeline picks up the new outcome automatically.
