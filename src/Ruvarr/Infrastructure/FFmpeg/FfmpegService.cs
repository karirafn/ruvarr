using System.Diagnostics;

namespace Ruvarr.Infrastructure.FFmpeg;

internal sealed class FfmpegService(IConfiguration configuration, ILogger<FfmpegService> logger) : IFfmpegService
{
    private const double SilenceDetectionDuration = 15.0;
    private const double WindowPreSlack = 0.5;
    private const double WindowPostSlack = 1.0;

    private readonly string _ffmpegPath = configuration.GetValue("Ruvarr:FfmpegPath", "ffmpeg")!;

    public async Task DownloadAsync(Uri uri, string filepath, string title, IProgress<FfmpegProgressData>? progress = null, CancellationToken cancellationToken = default)
    {
        List<string> argumentList = new FfmpegArgumentsBuilder()
            .WithInput(uri)
            .WithLogLevel("verbose")
            .WithCodec("copy")
            .WithAudioBitStreamFilter("aac_adtstoasc")
            .WithOutput(filepath)
            .OverwriteOutputFiles()
            .ShowStats()
            .HideCopyrightBanner()
            .WithMetadata("title", title)
            .Build();

        ProcessStartInfo psi = CreateProcessStartInfo(argumentList);

        using Process process = new() { StartInfo = psi };

        long? estimatedTotalBytes = null;

        process.ErrorDataReceived += (_, e) =>
        {
            if (progress is null || e.Data is null)
            {
                return;
            }

            if (estimatedTotalBytes is null)
            {
                estimatedTotalBytes = FfmpegStderrParser.TryParseDuration(e.Data);
            }

            FfmpegProgressData? data = FfmpegStderrParser.TryParse(e.Data);
            if (data is not null)
            {
                progress.Report(data with { EstimatedTotalBytes = estimatedTotalBytes });
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await using (cancellationToken.Register(() =>
        {
            try { process.Kill(); }
#pragma warning disable CA1031 // Process may have already exited
            catch { /* Process may have already exited */ }
#pragma warning restore CA1031
        }))
        {
            await process.WaitForExitAsync(cancellationToken);
        }
    }

    public async Task<TimeSpan?> DetectTrimPointAsync(string filepath, CancellationToken cancellationToken = default)
    {
        List<string> silenceArgs = new FfmpegArgumentsBuilder()
            .WithInput(filepath)
            .WithAudioFilter("silencedetect=n=-40dB:d=0.2")
            .WithDuration(SilenceDetectionDuration)
            .HideCopyrightBanner()
            .WithNullOutput()
            .Build();

        List<string> stderrLines = await RunProcessCollectingStderrAsync(silenceArgs, cancellationToken);

        double? silenceStart = null;
        double? silenceEnd = null;

        foreach (string line in stderrLines)
        {
            silenceStart ??= FfmpegSilenceParser.TryParseSilenceStart(line);
            silenceEnd ??= FfmpegSilenceParser.TryParseSilenceEnd(line);

            if (silenceStart is not null && silenceEnd is not null)
            {
                break;
            }
        }

        if (silenceStart is null || silenceEnd is null)
        {
            logger.LogDebug("No silence detected in first {Duration}s of {FilePath}", SilenceDetectionDuration, filepath);
            return null;
        }

        double sceneDuration = silenceEnd.Value + WindowPostSlack;

        List<string> sceneArgs = new FfmpegArgumentsBuilder()
            .WithInput(filepath)
            .WithVideoFilter(@"select=gt(scene\,0.2),metadata=print:file=-")
            .WithDuration(sceneDuration)
            .WithNoAudio()
            .HideCopyrightBanner()
            .WithNullOutput()
            .Build();

        List<string> stdoutLines = await RunProcessCollectingStdoutAsync(sceneArgs, cancellationToken);

        List<(double PtsTime, double SceneScore)> sceneChanges = [];
        double? pendingPtsTime = null;

        foreach (string line in stdoutLines)
        {
            double? ptsTime = FfmpegSceneParser.TryParsePtsTime(line);
            if (ptsTime is not null)
            {
                pendingPtsTime = ptsTime;
                continue;
            }

            double? sceneScore = FfmpegSceneParser.TryParseSceneScore(line);
            if (sceneScore is not null && pendingPtsTime is not null)
            {
                sceneChanges.Add((pendingPtsTime.Value, sceneScore.Value));
                pendingPtsTime = null;
            }
        }

        return FindTrimPoint(silenceStart.Value, silenceEnd.Value, sceneChanges);
    }

    public async Task TrimStartAsync(string filepath, TimeSpan trimPoint, CancellationToken cancellationToken = default)
    {
        string tempFilePath = Path.ChangeExtension(filepath, ".tmp" + Path.GetExtension(filepath));

        List<string> trimArgs = new FfmpegArgumentsBuilder()
            .WithInput(filepath)
            .WithCodec("copy")
            .WithSeekOutput(trimPoint.TotalSeconds)
            .OverwriteOutputFiles()
            .HideCopyrightBanner()
            .WithOutput(tempFilePath)
            .Build();

        try
        {
            await RunProcessAsync(trimArgs, cancellationToken);
            File.Move(tempFilePath, filepath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }

            throw;
        }
    }

    internal static TimeSpan? FindTrimPoint(
        double silenceStart,
        double silenceEnd,
        IReadOnlyList<(double PtsTime, double SceneScore)> sceneChanges)
    {
        double windowStart = Math.Max(0, silenceStart - WindowPreSlack);
        double windowEnd = silenceEnd + WindowPostSlack;

        foreach ((double ptsTime, double _) in sceneChanges)
        {
            if (ptsTime >= windowStart && ptsTime <= windowEnd)
            {
                return TimeSpan.FromSeconds(ptsTime);
            }
        }

        return null;
    }

    private ProcessStartInfo CreateProcessStartInfo(List<string> argumentList)
    {
        ProcessStartInfo psi = new()
        {
            FileName = _ffmpegPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (string arg in argumentList)
        {
            psi.ArgumentList.Add(arg);
        }

        return psi;
    }

    private async Task<List<string>> RunProcessCollectingStderrAsync(
        List<string> argumentList,
        CancellationToken cancellationToken)
    {
        ProcessStartInfo psi = CreateProcessStartInfo(argumentList);
        using Process process = new() { StartInfo = psi };

        List<string> lines = [];

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                lines.Add(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await using (cancellationToken.Register(() =>
        {
            try { process.Kill(); }
#pragma warning disable CA1031 // Process may have already exited
            catch { /* Process may have already exited */ }
#pragma warning restore CA1031
        }))
        {
            await process.WaitForExitAsync(cancellationToken);
        }

        return lines;
    }

    private async Task<List<string>> RunProcessCollectingStdoutAsync(
        List<string> argumentList,
        CancellationToken cancellationToken)
    {
        ProcessStartInfo psi = CreateProcessStartInfo(argumentList);
        using Process process = new() { StartInfo = psi };

        List<string> lines = [];

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                lines.Add(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await using (cancellationToken.Register(() =>
        {
            try { process.Kill(); }
#pragma warning disable CA1031 // Process may have already exited
            catch { /* Process may have already exited */ }
#pragma warning restore CA1031
        }))
        {
            await process.WaitForExitAsync(cancellationToken);
        }

        return lines;
    }

    private async Task RunProcessAsync(
        List<string> argumentList,
        CancellationToken cancellationToken)
    {
        ProcessStartInfo psi = CreateProcessStartInfo(argumentList);
        using Process process = new() { StartInfo = psi };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await using (cancellationToken.Register(() =>
        {
            try { process.Kill(); }
#pragma warning disable CA1031 // Process may have already exited
            catch { /* Process may have already exited */ }
#pragma warning restore CA1031
        }))
        {
            await process.WaitForExitAsync(cancellationToken);
        }
    }
}
