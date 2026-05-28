# PoC.Improved

A reference .NET 10 PoC demonstrating **Clean Architecture**, **MediatR** with pipeline behaviors, **FluentResults** at the handler boundary, **Polly v8** resilience, and **RFC 9457 ProblemDetails** error responses.

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
| Hard to test | Every piece mockable through its interface (61 unit tests) |

## Solution layout

Clean Architecture: each layer is its own project. Dependencies point inward.

```
PoC.Improved.slnx
├── PoC.Improved.Domain/              (no dependencies)
│   └── Exceptions.cs                 # DomainException + subclasses (status code in domain)
├── PoC.Improved.Application/         (depends on Domain)
│   ├── Common/
│   │   └── Errors.cs                 # CategorizedError + NotFound/Validation/Conflict/Unauthorized/Forbidden
│   ├── Providers/
│   │   └── IStorageProvider.cs       # Abstraction owned by Application
│   ├── Behaviors/
│   │   ├── LoggingBehavior.cs        # MediatR pipeline: timing + logs
│   │   └── ValidationBehavior.cs     # MediatR pipeline: FluentValidation -> BadInputException
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
└── PoC.Improved.Tests/               (xUnit v3 + NSubstitute, 61 tests)
```

Dependency direction: `Api -> Infrastructure -> Application -> Domain`. The Application layer never references Infrastructure — `IStorageProvider` lives in Application so handlers depend on the abstraction.

## Request pipeline

```
HTTP endpoint
   -> IMediator.Send(query)
      -> LoggingBehavior         (logs request name + elapsed)
         -> ValidationBehavior   (FluentValidation; failure -> BadInputException)
            -> Handler           (IRequestHandler<TRequest, Result<TResponse>>)
               -> IStorageProvider
                  -> IServiceCallHandler   (Polly retry + timeout, maps infra exceptions)
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

**61 tests** across:
- Domain exceptions (status code + inner exception preserved)
- All `IExceptionMapper`s + registry fallback
- `LoggingBehavior`, `ValidationBehavior`
- Every handler (success path + business failure + exception propagation)
- All validators
- All FluentResults `CategorizedError` subclasses

xUnit v3, NSubstitute for mocks. No integration tests yet — the smoke path is exercised manually via curl.

## Notable design choices

- **`Result<T>` at the handler boundary only.** Validation + infrastructure failures still throw — they're caught by `GlobalExceptionHandler` and translated to ProblemDetails. Result is reserved for *expected business outcomes* (NotFound, Conflict, …).
- **`IStorageProvider` lives in Application.** Handlers depend on the abstraction, Infrastructure provides the implementation. This is the Clean Architecture dependency rule made concrete.
- **`DomainException` carries `StatusCode`.** Pragmatic shortcut — a stricter DDD would expose `ErrorCategory` and let the Api layer translate. The status code in Domain is the trade-off.
- **`IExceptionMapper` registry is Open/Closed.** Adding a new error type (e.g. `DbUpdateConcurrencyException` → 409) is one class + one DI registration, no central `switch` to edit.
- **Polly v8 `ResiliencePipelineBuilder`** with explicit retry-on `HttpRequestException` and `TimeoutRejectedException`. The `TimeoutMapper` includes both `TimeoutException` and `TimeoutRejectedException` (a common mistake — Polly v8 throws its own type).

## How to add a new exception type

For example, mapping `DbUpdateConcurrencyException` → `409 Conflict`:

1. Add a mapper in `PoC.Improved.Infrastructure/ExceptionMapping.cs`:
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
2. Register it in `Program.cs`:
   ```csharp
   builder.Services.AddSingleton<IExceptionMapper, DbConcurrencyMapper>();
   ```

Nothing else changes. That's the Open/Closed principle.
