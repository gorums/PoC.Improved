using PoC.Improved.Application.Common;
using PoC.Improved.Application.Folders;
using PoC.Improved.Application.Providers;

namespace PoC.Improved.Tests.Application.Folders;

public class CreateFolderHandlerTests
{
    [Fact]
    public async Task Returns_success_when_path_does_not_exist()
    {
        var storage = Substitute.For<IStorageProvider>();
        storage.GetFoldersAsync("photos", Arg.Any<CancellationToken>())
            .Returns(new List<string> { "photos/2024" });
        var sut = new CreateFolderHandler(storage);

        var result = await sut.Handle(new CreateFolderCommand("photos", 2030), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("photos", result.Value.Feature);
        Assert.Equal(2030, result.Value.Year);
        Assert.Equal("photos/2030", result.Value.Path);
    }

    [Fact]
    public async Task Returns_ConflictError_when_path_already_exists()
    {
        var storage = Substitute.For<IStorageProvider>();
        storage.GetFoldersAsync("photos", Arg.Any<CancellationToken>())
            .Returns(new List<string> { "photos/2024", "photos/2025" });
        var sut = new CreateFolderHandler(storage);

        var result = await sut.Handle(new CreateFolderCommand("photos", 2025), CancellationToken.None);

        Assert.True(result.IsFailed);
        var error = Assert.Single(result.Errors);
        var conflict = Assert.IsType<ConflictError>(error);
        Assert.Equal("Folder.AlreadyExists", conflict.Code);
        Assert.Contains("photos/2025", conflict.Message);
    }

    [Fact]
    public async Task Propagates_exceptions_from_storage()
    {
        var storage = Substitute.For<IStorageProvider>();
        storage.GetFoldersAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));
        var sut = new CreateFolderHandler(storage);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.Handle(new CreateFolderCommand("x", 2025), CancellationToken.None));
    }
}

public class CreateFolderValidatorTests
{
    private readonly CreateFolderValidator _sut = new();

    [Fact]
    public void Empty_feature_fails()
        => Assert.False(_sut.Validate(new CreateFolderCommand("", 2025)).IsValid);

    [Fact]
    public void Year_below_range_fails()
        => Assert.False(_sut.Validate(new CreateFolderCommand("photos", 1999)).IsValid);

    [Fact]
    public void Valid_input_passes()
        => Assert.True(_sut.Validate(new CreateFolderCommand("photos", 2025)).IsValid);
}
