using FluentResults;
using MediatR;

namespace PoC.Improved.Application.Files;

public sealed record GetFileUrlQuery(string? Path) : IRequest<Result<GetFileUrlResult>>;

public sealed record GetFileUrlResult(string Url);
