using FluentValidation;
using PoC.Improved.Api;
using PoC.Improved.Api.Endpoints;
using PoC.Improved.Application.Behaviors;
using PoC.Improved.Application.Cqrs;
using PoC.Improved.Application.Folders;
using PoC.Improved.Application.Providers;
using PoC.Improved.Infrastructure.ExceptionMapping;
using PoC.Improved.Infrastructure.Providers;
using PoC.Improved.Infrastructure.Resilience;

var builder = WebApplication.CreateBuilder(args);

// --- Exception mappers (Open/Closed: add a class, register it, done) ---
builder.Services.AddSingleton<IExceptionMapper, TimeoutMapper>();
builder.Services.AddSingleton<IExceptionMapper, HttpMapper>();
builder.Services.AddSingleton<IExceptionMapper, ArgumentMapper>();
builder.Services.AddSingleton<IExceptionMapper, S3LikeMapper>();
builder.Services.AddSingleton<IExceptionMapperRegistry, ExceptionMapperRegistry>();

// --- Resilience wrapper ---
builder.Services.AddSingleton<IServiceCallHandler, ServiceCallHandler>();

// --- Providers ---
builder.Services.AddSingleton<IStorageProvider, FakeStorageProvider>();

// --- Custom mediator + FluentValidation ---
builder.Services.AddMediator(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<GetFoldersQuery>();
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});
builder.Services.AddValidatorsFromAssemblyContaining<GetFoldersQuery>();

// --- Global API exception handling ---
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = ctx =>
    {
        ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;
    };
});

var app = builder.Build();
app.UseExceptionHandler();

app.MapRootEndpoints();
app.MapFolderEndpoints();
app.MapFileEndpoints();

app.Run();
