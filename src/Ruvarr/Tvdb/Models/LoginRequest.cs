namespace Ruvarr.Tvdb.Models;

internal sealed record class LoginRequest(string ApiKey, string? Pin);
