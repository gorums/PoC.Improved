using FluentResults;
using MediatR;
using PoC.Improved.Application.Providers;

namespace PoC.Improved.Application.Folders;

/// <summary>
/// Thin orchestration layer. Returns Result&lt;T&gt; (FluentResults) for business outcomes.
/// Infrastructure failures still throw DomainException and are translated by GlobalExceptionHandler.
/// </summary>
public sealed class GetFoldersHandler : IRequestHandler<GetFoldersQuery, Result<GetFoldersResult>>
{
    private readonly IStorageProvider _storage;

    public GetFoldersHandler(IStorageProvider storage) => _storage = storage;

    public async Task<Result<GetFoldersResult>> Handle(GetFoldersQuery request, CancellationToken ct)
    {
        var folders = await _storage.GetFoldersAsync(request.Feature, ct);
        return Result.Ok(new GetFoldersResult(request.Feature, folders));
    }
}
