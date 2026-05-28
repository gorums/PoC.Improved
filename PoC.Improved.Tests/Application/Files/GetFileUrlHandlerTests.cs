using PoC.Improved.Application.Files;
using PoC.Improved.Application.Providers;

namespace PoC.Improved.Tests.Application.Files;

public class GetFileUrlHandlerTests
{
    [Fact]
    public async Task Forwards_path_to_storage_and_returns_success_result()
    {
        var storage = Substitute.For<IStorageProvider>();
        storage.GetFileUrlAsync("a/b.jpg", Arg.Any<CancellationToken>())
            .Returns("https://signed/a/b.jpg");
        var sut = new GetFileUrlHandler(storage);

        var result = await sut.Handle(new GetFileUrlQuery("a/b.jpg"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("https://signed/a/b.jpg", result.Value.Url);
        await storage.Received(1).GetFileUrlAsync("a/b.jpg", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Substitutes_empty_string_when_path_is_null()
    {
        var storage = Substitute.For<IStorageProvider>();
        storage.GetFileUrlAsync(string.Empty, Arg.Any<CancellationToken>())
            .Returns("https://signed/");
        var sut = new GetFileUrlHandler(storage);

        await sut.Handle(new GetFileUrlQuery(null), CancellationToken.None);

        await storage.Received(1).GetFileUrlAsync(string.Empty, Arg.Any<CancellationToken>());
    }
}
