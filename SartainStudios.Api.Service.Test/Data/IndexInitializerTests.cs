using SartainStudios.Api.Service.Data;
using SartainStudios.Api.Service.Test.Infrastructure;

namespace SartainStudios.Api.Service.Test.Data;

public sealed class IndexInitializerTests
{
    [Fact]
    public async Task InitializeIndexesAsync_CompletesSuccessfully()
    {
        var harness = new MongoHarness();
        var initializer = new IndexInitializer(harness.Database);

        await initializer.InitializeIndexesAsync();
    }
}