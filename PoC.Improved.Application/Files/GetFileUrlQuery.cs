using FluentResults;
using PoC.Improved.Application.Cqrs;

namespace PoC.Improved.Application.Files;

public sealed record GetFileUrlQuery(string? Path) : IRequest<Result<GetFileUrlResult>>;

public sealed record GetFileUrlResult(string Url);
