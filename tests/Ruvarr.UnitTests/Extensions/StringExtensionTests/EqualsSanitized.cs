using Ruvarr.Extensions;

using Shouldly;

namespace Ruvarr.UnitTests.Extensions.StringExtensionTests;

public sealed class EqualsSanitized
{
    [Theory]
    [InlineData("Chicago PD", "Chicago P.D.")]
    [InlineData("Episode - Part 1", "Episode, Part 1")]
    [InlineData("Ormhildarsaga", "Orm­\u00ADhild­­\u00ADar\u00ADsaga")]
    public void ReturnsTrue(string a, string b) => a.EqualsSanitized(b).ShouldBeTrue();
}