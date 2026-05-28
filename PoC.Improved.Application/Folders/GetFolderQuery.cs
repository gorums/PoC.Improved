using FluentResults;
using MediatR;

namespace PoC.Improved.Application.Folders;

public sealed record GetFolderQuery(string Feature, int Year) : IRequest<Result<FolderDetails>>;

public sealed record FolderDetails(string Feature, int Year, string Path);
