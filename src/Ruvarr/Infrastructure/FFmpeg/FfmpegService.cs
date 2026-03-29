using System.Diagnostics;

namespace Ruvarr.Infrastructure.FFmpeg;

internal sealed class FfmpegService(IConfiguration configuration) : IFfmpegService
{
    private readonly string _ffmpegPath = configuration.GetValue("Ruvarr:FfmpegPath", "ffmpeg")!;

    public async Task DownloadAsync(Uri uri, string filepath, string title, IProgress<FfmpegProgressData>? progress = null)
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

        await process.WaitForExitAsync();
    }
}
