namespace FairwayFinder.Tests.Integration;

public class ProfileControllerTests(ConfigureFactory factory) : IClassFixture<ConfigureFactory>
{
    private readonly ConfigureFactory _factory = factory;

    [Fact]
    public async Task Test()
    {
        await Task.Delay(5000);
    }
}