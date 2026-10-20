using Ruvarr.Infrastructure.Ruv.Models;

namespace Ruvarr.Testing.Builders;

internal sealed class RuvFeaturedTvBuilder
{
    private RuvTvProgram[] _programs = [new RuvTvProgramBuilder().Build()];

    public RuvFeaturedTvBuilder WithPrograms(params RuvTvProgram[] programs)
    {
        _programs = programs;
        return this;
    }

    public RuvFeaturedTv Build() => new(
        Panels: [new RuvPanel(Programs: _programs)]);
}
