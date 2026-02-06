using System.Diagnostics;
using System.Net.Http.Json;

using Microsoft.Extensions.Options;

using Ruvarr.Internals.Builders;
using Ruvarr.Internals.Options;
using Ruvarr.Models;

namespace Ruvarr.Internals.Clients;

internal sealed class RuvClient(HttpClient client, IOptions<RuvarrOptions> options) : IRuvClient
{
    public async Task<RuvFeaturedTv?> GetFeaturedTv(CancellationToken cancellationToken = default)
    {
        RuvFeaturedTv? response = await client.GetFromJsonAsync<RuvFeaturedTv>("/api/programs/featured/tv", cancellationToken)
            .ConfigureAwait(false);

        return response;
    }

    public async Task<RuvTvProgram?> GetProgramAsync(int seriesId, CancellationToken cancellationToken = default)
    {
        RuvTvProgram? response = await client.GetFromJsonAsync<RuvTvProgram>($"/api/programs/program/{seriesId}/all", cancellationToken)
            .ConfigureAwait(false);

        return response;
    }

    public async Task DownloadEpisodeAsync(RuvTvProgram program, RuvEpisode episode)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(episode);

        string filename = $"{program.Title} - {episode.Title}.mp4";
        string outputFile = Path.Join(options.Value.Ffmpeg.OutputFolder, filename);
        string arguments = new FfmpegArgumentsBuilder()
            .WithInput(episode.File)
            .WithLogLevel("verbose")
            .WithCodec("copy")
            .WithAudioBitStreamFilter("aac_adtstoasc")
            .WithOutput(outputFile)
            .OverwriteOutputFiles()
            .ShowStats()
            .HideCopyrightBanner()
            .WithMetadata("title", episode.Title)
            .Build();

        ProcessStartInfo psi = new()
        {
            FileName = options.Value.Ffmpeg.ExecutablePath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using Process process = new() { StartInfo = psi };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                Console.WriteLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                Console.WriteLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync()
            .ConfigureAwait(false);
    }
}