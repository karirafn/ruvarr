namespace Ruvarr.Infrastructure.Tvdb.Models;

internal sealed record class AuthenticationResponse(AuthenticationData Data, string Status);
