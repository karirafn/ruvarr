using System.Diagnostics;

using Microsoft.Extensions.Options;

namespace Ruvarr.Infrastructure.FFmpeg;

internal sealed class FfmpegService(IOptions<FfmpegOptions> options) : IFfmpegService
{
    public async Task DownloadAsync(Uri uri, string filepath, string title)
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
            FileName = options.Value.ExecutablePath,
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

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();
    }
}