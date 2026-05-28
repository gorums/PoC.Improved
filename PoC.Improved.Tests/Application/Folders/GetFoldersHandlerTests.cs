using PoC.Improved.Application.Folders;
using PoC.Improved.Application.Providers;

namespace PoC.Improved.Tests.Application.Folders;

public class GetFoldersHandlerTests
{
    [Fact]
    public async Task Forwards_feature_to_storage_and_returns_success_result()
    {
        var storage = Substitute.For<IStorageProvider>();
        storage.GetFoldersAsync("photos", Arg.Any<CancellationToken>())
            .Returns(new List<string> { "photos/2025" });
        var sut = new GetFoldersHandler(storage);

        var result = await sut.Handle(new GetFoldersQuery("photos"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("photos", result.Value.Feature);
        Assert.Equal(new[] { "photos/2025" }, result.Value.Folders);
        await storage.Received(1).GetFoldersAsync("photos", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Propagates_exceptions_from_storage()
    {
        var storage = Substitute.For<IStorageProvider>();
        storage.GetFoldersAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));
        var sut = new GetFoldersHandler(storage);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.Handle(new GetFoldersQuery("x"), CancellationToken.None));
    }
}
