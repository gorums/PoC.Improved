using PoC.Improved.Application.Folders;
using PoC.Improved.Application.Providers;

namespace PoC.Improved.Tests.Application.Folders;

public class StreamFoldersHandlerTests
{
    [Fact]
    public async Task Yields_each_folder_path_from_storage_in_order()
    {
        var storage = Substitute.For<IStorageProvider>();
        storage.GetFoldersAsync("photos", Arg.Any<CancellationToken>())
            .Returns(new List<string> { "photos/2024", "photos/2025", "photos/2026" });
        var sut = new StreamFoldersHandler(storage);

        var items = new List<string>();
        await foreach (var path in sut.Handle(new StreamFoldersQuery("photos", DelayMs: 0), CancellationToken.None))
            items.Add(path);

        Assert.Equal(new[] { "photos/2024", "photos/2025", "photos/2026" }, items);
    }

    [Fact]
    public async Task Empty_storage_yields_empty_stream()
    {
        var storage = Substitute.For<IStorageProvider>();
        storage.GetFoldersAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<string>());
        var sut = new StreamFoldersHandler(storage);

        var items = new List<string>();
        await foreach (var path in sut.Handle(new StreamFoldersQuery("unknown", DelayMs: 0), CancellationToken.None))
            items.Add(path);

        Assert.Empty(items);
    }

    [Fact]
    public async Task Honors_cancellation_between_items()
    {
        var storage = Substitute.For<IStorageProvider>();
        storage.GetFoldersAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<string> { "a", "b", "c" });
        var sut = new StreamFoldersHandler(storage);
        using var cts = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in sut.Handle(new StreamFoldersQuery("x", DelayMs: 50), cts.Token))
                cts.Cancel(); // cancel after first item, before the delay finishes
        });
    }
}
