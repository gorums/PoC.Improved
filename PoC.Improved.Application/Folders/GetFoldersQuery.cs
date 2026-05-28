using FluentResults;
using PoC.Improved.Application.Cqrs;

namespace PoC.Improved.Application.Folders;

public sealed record GetFoldersQuery(string Feature) : IRequest<Result<GetFoldersResult>>;

public sealed record GetFoldersResult(string Feature, IReadOnlyList<string> Folders);
