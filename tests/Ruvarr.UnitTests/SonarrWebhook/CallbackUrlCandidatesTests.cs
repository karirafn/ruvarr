using System.Net;

using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Ruvarr.SonarrWebhook;

using Shouldly;

namespace Ruvarr.UnitTests.SonarrWebhook;

public sealed class CallbackUrlCandidatesTests
{
    private static CallbackUrlCandidates CreateSut(params string[] listeningAddresses)
    {
        ServerAddressesFeature addresses = new();
        foreach (string address in listeningAddresses)
        {
            addresses.Addresses.Add(address);
        }

        FeatureCollection features = new();
        features.Set<IServerAddressesFeature>(addresses);
        IServer server = Substitute.For<IServer>();
        server.Features.Returns(features);

        return new CallbackUrlCandidates(server, NullLogger<CallbackUrlCandidates>.Instance);
    }

    [Fact]
    public void FirstCandidateIsLocalAddressOfSocketTowardSonarrHostOnListeningPort()
    {
        // Arrange
        CallbackUrlCandidates sut = CreateSut("http://[::]:8080");

        // Act
        IReadOnlyList<Uri> candidates = sut.Discover(new Uri("http://127.0.0.1:8989"));

        // Assert
        candidates[0].ShouldBe(new Uri("http://127.0.0.1:8080/api/sonarr/webhook"));
    }

    [Fact]
    public void FallsBackToHostName()
    {
        // Arrange
        CallbackUrlCandidates sut = CreateSut("http://+:8080");

        // Act
        IReadOnlyList<Uri> candidates = sut.Discover(new Uri("http://127.0.0.1:8989"));

        // Assert
        candidates.ShouldContain(new Uri($"http://{Dns.GetHostName()}:8080/api/sonarr/webhook"));
    }

    [Fact]
    public void UsesHttpPort_WhenServerAlsoListensOnHttps()
    {
        // Arrange
        CallbackUrlCandidates sut = CreateSut("https://[::]:8443", "http://*:5000");

        // Act
        IReadOnlyList<Uri> candidates = sut.Discover(new Uri("http://127.0.0.1:8989"));

        // Assert
        candidates.ShouldNotBeEmpty();
        candidates.ShouldAllBe(c => c.Port == 5000 && c.Scheme == "http");
    }

    [Fact]
    public void ReturnsNoCandidates_WhenServerHasNoHttpAddress()
    {
        // Arrange
        CallbackUrlCandidates sut = CreateSut("https://[::]:8443");

        // Act
        IReadOnlyList<Uri> candidates = sut.Discover(new Uri("http://127.0.0.1:8989"));

        // Assert
        candidates.ShouldBeEmpty();
    }

    [Fact]
    public void SkipsLocalAddress_WhenSonarrHostCannotBeResolved()
    {
        // Arrange
        CallbackUrlCandidates sut = CreateSut("http://[::]:8080");

        // Act
        IReadOnlyList<Uri> candidates = sut.Discover(new Uri("http://sonarr.invalid:8989"));

        // Assert
        candidates.ShouldBe([new Uri($"http://{Dns.GetHostName()}:8080/api/sonarr/webhook")]);
    }
}
