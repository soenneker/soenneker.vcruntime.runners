using Soenneker.Vcruntime.Runners.Utils.Abstract;
using Soenneker.Tests.HostedUnit;

namespace Soenneker.Vcruntime.Runners.Tests;

[ClassDataSource<Host>(Shared = SharedType.PerTestSession)]
public sealed class VcruntimeRunnerTests : HostedUnitTest
{
    private readonly IFileOperationsUtil _runner;

    public VcruntimeRunnerTests(Host host) : base(host)
    {
        _runner = Resolve<IFileOperationsUtil>(true);
    }

    [Test]
    public void Default()
    {

    }
}
