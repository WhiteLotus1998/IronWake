using Ironwake.Core;

namespace Ironwake.Core.Tests;

public class RulesVersionTests
{
    [Fact]
    public void RulesVersionStartsAtZeroBeforeAnyRuleExists()
    {
        Assert.Equal(0, RulesVersion.Current);
    }
}
