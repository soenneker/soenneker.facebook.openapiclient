using Soenneker.Tests.HostedUnit;

namespace Soenneker.Facebook.OpenApiClient.Tests;

[ClassDataSource<Host>(Shared = SharedType.PerTestSession)]
public sealed class FacebookOpenApiClientTests : HostedUnitTest
{
    public FacebookOpenApiClientTests(Host host) : base(host)
    {
    }

    [Test]
    public void Default()
    {

    }
}
