using FluentResults;
using MediatR;
using PoC.Improved.Application.Common;
using PoC.Improved.Application.Providers;

namespace PoC.Improved.Application.Folders;

/// <summary>
/// Demonstrates Result.Fail with a typed ConflictError.
/// "Folder already exists" is an expected business outcome, returned as a failed Result
/// (-> 409 Conflict ProblemDetails) rather than a thrown exception.
/// </summary>
public sealed class CreateFolderHandler : IRequestHandler<CreateFolderCommand, Result<FolderDetails>>
{
    private readonly IStorageProvider _storage;

    public CreateFolderHandler(IStorageProvider storage) => _storage = storage;

    public async Task<Result<FolderDetails>> Handle(CreateFolderCommand request, CancellationToken ct)
    {
        var existing = await _storage.GetFoldersAsync(request.Feature, ct);
        var path = $"{request.Feature}/{request.Year}";

        if (existing.Contains(path))
        {
            return Result.Fail(new ConflictError(
                "Folder.AlreadyExists",
                $"Folder '{path}' already exists."));
        }

        // Persistence is faked - real impl would call _storage.CreateFolderAsync(...).
        return Result.Ok(new FolderDetails(request.Feature, request.Year, path));
    }
}
