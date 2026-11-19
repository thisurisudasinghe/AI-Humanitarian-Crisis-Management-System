using Cms.Application.Cases.Common;
using Cms.Infrastructure.Cases;

namespace Cms.Tests.Cases;

public class LocalFileStoreTests
{
    [Fact]
    public async Task Stored_file_name_must_be_guid_based()
    {
        var store = CreateStore();

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.OpenReadAsync("not-a-guid.png"));
    }

    [Fact]
    public async Task Path_traversal_is_rejected_even_with_a_guid_file_name()
    {
        var store = CreateStore();
        var traversal = $"..\\..\\..\\{Guid.NewGuid():N}.png";

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.OpenReadAsync(traversal));
    }

    private static LocalFileStore CreateStore() => new(new CasesOptions
    {
        UploadRoot = Path.Combine(Path.GetTempPath(), $"cms-file-store-tests-{Guid.NewGuid():N}")
    });
}
