using FluentResults;
using MediatR;
using PoC.Improved.Application.Providers;

namespace PoC.Improved.Application.Files;

public sealed class GetFileUrlHandler : IRequestHandler<GetFileUrlQuery, Result<GetFileUrlResult>>
{
    private readonly IStorageProvider _storage;

    public GetFileUrlHandler(IStorageProvider storage) => _storage = storage;

    public async Task<Result<GetFileUrlResult>> Handle(GetFileUrlQuery request, CancellationToken ct)
    {
        var url = await _storage.GetFileUrlAsync(request.Path ?? string.Empty, ct);
        return Result.Ok(new GetFileUrlResult(url));
    }
}
