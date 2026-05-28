# PoC.Improved - Improved architecture demo

Runnable .NET 10 example that demonstrates the suggested improvements over the
original `ServiceCallHandlerWrapper`. Every endpoint flows through MediatR.

## Requirements
- .NET 10 SDK
- `curl` (or any HTTP client)

## How to run

```bash
cd PoC.Improved
dotnet run
```

By default it listens on `http://localhost:5000` (or whatever port the console shows).

## Test endpoints

| Endpoint | Expected result | Demonstrates |
|---|---|---|
| `GET /folders/photos` | `200 OK` with folder list | Happy path (handler + provider) |
| `GET /folders/flaky`  | `200 OK` after 2 retries | Polly retry with backoff |
| `GET /folders/down`   | `503` with `ExternalServiceException` JSON | `HttpMapper` + global handler |
| `GET /folders/slow`   | `408` with `OperationTimeoutException` JSON | Polly timeout + `TimeoutMapper` |
| `GET /folders/`       | `400` with `BadInputException` JSON | `ValidationBehavior` (handler never runs) |
| `GET /file-url?path=a/b.jpg` | `200 OK` with a fake signed URL | Happy path |
| `GET /file-url`       | `400` with `BadInputException` JSON | `ValidationBehavior` |

```bash
curl -i http://localhost:5000/folders/photos
curl -i http://localhost:5000/folders/flaky    # check the logs: you'll see "Retry 1..."
curl -i http://localhost:5000/folders/down
curl -i http://localhost:5000/folders/slow     # takes ~21s and returns 408
curl -i http://localhost:5000/folders/
curl -i "http://localhost:5000/file-url?path=images/cat.jpg"
curl -i http://localhost:5000/file-url
```

## MediatR flow

Every endpoint follows the same path:

```
HTTP endpoint
   -> IMediator.Send(query)
      -> LoggingBehavior        (logs name + elapsed)
         -> ValidationBehavior  (FluentValidation -> BadInputException on failure)
            -> Handler          (IRequestHandler<TQuery, TResult>)
               -> IStorageProvider
                  -> IServiceCallHandler   (Polly retry + timeout + mapping)
                     -> external call
```

Why route through MediatR:
- Handlers stay thin and single-purpose; cross-cutting concerns live in behaviors.
- Validation is declarative (`AbstractValidator<TQuery>`) and runs automatically.
- New behaviors (caching, authorization, transactions) compose without touching handlers.
- The resilience wrapper (`IServiceCallHandler`) keeps its job: it lives one layer deeper, wrapping the actual external call inside the provider.

## What it improves over the original code

| Original | Improved |
|---|---|
| Giant centralized `switch` | `IExceptionMapper` registry, open for extension |
| `new ServiceCallHandlerWrapper<...>` in the constructor | Injected via DI (`IServiceCallHandler`) |
| Mixes `Result` and `throw` | Always throws `DomainException` |
| No retry: one blip = failure | Configurable Polly retry + timeout |
| `ExecuteServiceException(ex.Message, ex)` loses context | `DomainException` carries `StatusCode` and `UserMessage` |
| HTTP status codes inside `Infrastructure` | HTTP lives only in `Api/GlobalExceptionHandler` |
| Hard to test | Every piece is mockable through its interface |

## Structure

Clean Architecture: each layer is its own project. Dependencies point inward.

```
PoC.Improved.slnx
├── PoC.Improved.Domain/              (no dependencies)
│   └── Exceptions.cs                 # DomainException + subclasses
├── PoC.Improved.Application/         (depends on Domain)
│   ├── Providers/
│   │   └── IStorageProvider.cs       # Abstraction owned by Application
│   ├── Behaviors/
│   │   ├── LoggingBehavior.cs        # MediatR pipeline: timing + logs
│   │   └── ValidationBehavior.cs     # MediatR pipeline: validation -> BadInputException
│   ├── Folders/
│   │   ├── GetFoldersQuery.cs
│   │   ├── GetFoldersHandler.cs
│   │   └── GetFoldersValidator.cs
│   └── Files/
│       ├── GetFileUrlQuery.cs
│       ├── GetFileUrlHandler.cs
│       └── GetFileUrlValidator.cs
├── PoC.Improved.Infrastructure/      (depends on Application + Domain)
│   ├── ExceptionMapping.cs           # IExceptionMapper + registry + concrete mappers
│   ├── ServiceCallHandler.cs         # Polly retry + timeout wrapper
│   └── FakeStorageProvider.cs        # IStorageProvider implementation
├── PoC.Improved/                     (web host, depends on all)
│   ├── Api/
│   │   ├── GlobalExceptionHandler.cs # DomainException -> HTTP response
│   │   └── Endpoints/
│   │       ├── RootEndpoints.cs
│   │       ├── FolderEndpoints.cs
│   │       └── FileEndpoints.cs
│   └── Program.cs                    # DI wiring + endpoint registration
└── PoC.Improved.Tests/               (xUnit v3, NSubstitute)
    └── (mirrors layout above)
```

Dependency direction: `Api -> Infrastructure -> Application -> Domain`. The Application layer never references Infrastructure - `IStorageProvider` lives in Application so handlers can depend on the abstraction.

## How to add a new exception type

For example, mapping `DbUpdateConcurrencyException` -> `409 Conflict`:

1. Create a class in `Infrastructure/ExceptionMapping.cs`:
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

You don't touch anything else. That's the Open/Closed principle.
