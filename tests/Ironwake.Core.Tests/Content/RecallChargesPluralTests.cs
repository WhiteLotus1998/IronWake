using Ironwake.Cli;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// The Recall count reads as English (issue 1066): one charge is "1 charge left", any other
/// count is "N charges left", in the <c>recall</c> listing and the recall event line alike.
/// </summary>
public class RecallChargesPluralTests
{
    [Theory]
    [InlineData(0, "0 charges left")]
    [InlineData(1, "1 charge left")]
    [InlineData(2, "2 charges left")]
    [InlineData(3, "3 charges left")]
    public void OneRecallChargeIsSingularAndAnyOtherCountIsPlural(int charges, string expected)
    {
        Assert.Equal(expected, PlaySession.ChargesLeft(charges));
    }
}
