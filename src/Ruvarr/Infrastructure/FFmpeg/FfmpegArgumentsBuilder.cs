using System.Globalization;

namespace Ruvarr.Infrastructure.FFmpeg;

internal sealed class FfmpegArgumentsBuilder
{
    private readonly List<string> _arguments = [];
    private readonly List<string> _outputOptions = [];
    private string _output = string.Empty;

    public FfmpegArgumentsBuilder WithInput(Uri input)
    {
        _arguments.Add("-i");
        _arguments.Add(input.ToString());
        return this;
    }

    public FfmpegArgumentsBuilder WithInput(string filepath)
    {
        _arguments.Add("-i");
        _arguments.Add(filepath);
        return this;
    }

    public FfmpegArgumentsBuilder WithAudioFilter(string filter)
    {
        _arguments.Add("-af");
        _arguments.Add(filter);
        return this;
    }

    public FfmpegArgumentsBuilder WithVideoFilter(string filter)
    {
        _arguments.Add("-vf");
        _arguments.Add(filter);
        return this;
    }

    public FfmpegArgumentsBuilder WithDuration(double seconds)
    {
        _arguments.Add("-t");
        _arguments.Add(seconds.ToString(CultureInfo.InvariantCulture));
        return this;
    }

    public FfmpegArgumentsBuilder WithSeekOutput(double seconds)
    {
        _outputOptions.Add("-ss");
        _outputOptions.Add(seconds.ToString(CultureInfo.InvariantCulture));
        return this;
    }

    public FfmpegArgumentsBuilder WithNoAudio()
    {
        _arguments.Add("-an");
        return this;
    }

    public FfmpegArgumentsBuilder WithNullOutput()
    {
        _arguments.Add("-f");
        _arguments.Add("null");
        _output = "-";
        return this;
    }

    public FfmpegArgumentsBuilder WithCodec(string codec)
    {
        _arguments.Add("-c");
        _arguments.Add(codec);
        return this;
    }

    public FfmpegArgumentsBuilder WithLogLevel(string loglevel)
    {
        _arguments.Add("-loglevel");
        _arguments.Add(loglevel);
        return this;
    }

    public FfmpegArgumentsBuilder WithAudioBitStreamFilter(string filter)
    {
        _arguments.Add("-bsf:a");
        _arguments.Add(filter);
        return this;
    }

    public FfmpegArgumentsBuilder OverwriteOutputFiles(bool overwrite = true)
    {
        if (overwrite)
        {
            _arguments.Add("-y");
        }

        return this;
    }

    public FfmpegArgumentsBuilder ShowStats(bool show = true)
    {
        if (show)
        {
            _arguments.Add("-stats");
        }

        return this;
    }

    public FfmpegArgumentsBuilder HideCopyrightBanner(bool hide = true)
    {
        if (hide)
        {
            _arguments.Add("-hide_banner");
        }

        return this;
    }

    public FfmpegArgumentsBuilder WithMetadata(string key, string value)
    {
        _arguments.Add("-metadata");
        _arguments.Add($"{key}={value}");
        return this;
    }

    public FfmpegArgumentsBuilder WithOutput(string output)
    {
        _output = output;
        return this;
    }

    public List<string> Build()
    {
        List<string> result = [.. _arguments, .. _outputOptions];
        result.Add(_output);
        return result;
    }
}