using PoC.Improved.Application.Common;
using PoC.Improved.Application.Folders;
using PoC.Improved.Application.Providers;

namespace PoC.Improved.Tests.Application.Folders;

public class GetFolderHandlerTests
{
    [Fact]
    public async Task Returns_success_when_folder_path_is_present_in_storage()
    {
        var storage = Substitute.For<IStorageProvider>();
        storage.GetFoldersAsync("photos", Arg.Any<CancellationToken>())
            .Returns(new List<string> { "photos/2024", "photos/2025" });
        var sut = new GetFolderHandler(storage);

        var result = await sut.Handle(new GetFolderQuery("photos", 2025), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("photos", result.Value.Feature);
        Assert.Equal(2025, result.Value.Year);
        Assert.Equal("photos/2025", result.Value.Path);
    }

    [Fact]
    public async Task Returns_NotFoundError_when_year_is_missing()
    {
        var storage = Substitute.For<IStorageProvider>();
        storage.GetFoldersAsync("photos", Arg.Any<CancellationToken>())
            .Returns(new List<string> { "photos/2024", "photos/2025" });
        var sut = new GetFolderHandler(storage);

        var result = await sut.Handle(new GetFolderQuery("photos", 1999), CancellationToken.None);

        Assert.True(result.IsFailed);
        var error = Assert.Single(result.Errors);
        var notFound = Assert.IsType<NotFoundError>(error);
        Assert.Equal("Folder.NotFound", notFound.Code);
    }

    [Fact]
    public async Task Returns_NotFoundError_when_feature_yields_no_folders()
    {
        var storage = Substitute.For<IStorageProvider>();
        storage.GetFoldersAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<string>());
        var sut = new GetFolderHandler(storage);

        var result = await sut.Handle(new GetFolderQuery("unknown", 2025), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.IsType<NotFoundError>(result.Errors[0]);
    }

    [Fact]
    public async Task Propagates_exceptions_from_storage()
    {
        var storage = Substitute.For<IStorageProvider>();
        storage.GetFoldersAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));
        var sut = new GetFolderHandler(storage);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.Handle(new GetFolderQuery("x", 2025), CancellationToken.None));
    }
}

public class GetFolderValidatorTests
{
    private readonly GetFolderValidator _sut = new();

    [Fact]
    public void Empty_feature_fails()
        => Assert.False(_sut.Validate(new GetFolderQuery("", 2025)).IsValid);

    [Fact]
    public void Year_below_range_fails()
        => Assert.False(_sut.Validate(new GetFolderQuery("photos", 1999)).IsValid);

    [Fact]
    public void Year_above_range_fails()
        => Assert.False(_sut.Validate(new GetFolderQuery("photos", 2101)).IsValid);

    [Fact]
    public void Valid_input_passes()
        => Assert.True(_sut.Validate(new GetFolderQuery("photos", 2025)).IsValid);
}
