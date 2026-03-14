using Ruvarr.Programs.Domain;

namespace Ruvarr.Testing.Builders;

internal sealed class RuvEpisodeBuilder
{
    private RuvProgram _program = new RuvProgramBuilder().Build();
    private string _ruvId = Guid.NewGuid().ToString()[..6];
    private Uri _uri = new("http://test.com");
    private string _title = "Test Episode";
    private string _description = "Description";
    private DateTime _firstRun = DateTime.UtcNow;

    public RuvEpisodeBuilder WithProgram(RuvProgram program)
    {
        _program = program;
        return this;
    }

    public RuvEpisodeBuilder WithRuvId(string ruvId)
    {
        _ruvId = ruvId;
        return this;
    }

    public RuvEpisodeBuilder WithUri(Uri uri)
    {
        _uri = uri;
        return this;
    }

    public RuvEpisodeBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public RuvEpisodeBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    public RuvEpisodeBuilder WithFirstRun(DateTime firstRun)
    {
        _firstRun = firstRun;
        return this;
    }

    public RuvEpisode Build() => RuvEpisode.Create(
        program: _program,
        id: _ruvId,
        uri: _uri,
        title: _title,
        description: _description,
        firstRun: _firstRun);
}
