using System.Diagnostics;

using Microsoft.Extensions.Options;

namespace Ruvarr.FFmpeg;

internal sealed class FfmpegService(IOptions<FfmpegOptions> options) : IFfmpegService
{
    public async Task DownloadAsync(Uri uri, string filepath, string title)
    {
        string arguments = new FfmpegArgumentsBuilder()
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
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using Process process = new() { StartInfo = psi };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync()
            .ConfigureAwait(false);
    }
}