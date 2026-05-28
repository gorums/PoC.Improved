using FluentResults;
using MediatR;

namespace PoC.Improved.Application.Folders;

public sealed record CreateFolderCommand(string Feature, int Year) : IRequest<Result<FolderDetails>>;
