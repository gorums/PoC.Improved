using FluentResults;
using PoC.Improved.Application.Cqrs;
using PoC.Improved.Application.Common;
using PoC.Improved.Application.Providers;

namespace PoC.Improved.Application.Folders;

/// <summary>
/// Demonstrates Result.Fail with a typed NotFoundError.
/// Infrastructure failures still throw (caught by GlobalExceptionHandler);
/// "folder doesn't exist" is an expected outcome, so it flows back as a failed Result.
/// </summary>
public sealed class GetFolderHandler : IRequestHandler<GetFolderQuery, Result<FolderDetails>>
{
    private readonly IStorageProvider _storage;

    public GetFolderHandler(IStorageProvider storage) => _storage = storage;

    public async Task<Result<FolderDetails>> Handle(GetFolderQuery request, CancellationToken ct)
    {
        var folders = await _storage.GetFoldersAsync(request.Feature, ct);
        var path = $"{request.Feature}/{request.Year}";

        if (!folders.Contains(path))
        {
            return Result.Fail(new NotFoundError(
                "Folder.NotFound",
                $"No folder found for feature '{request.Feature}' year {request.Year}."));
        }

        return Result.Ok(new FolderDetails(request.Feature, request.Year, path));
    }
}
