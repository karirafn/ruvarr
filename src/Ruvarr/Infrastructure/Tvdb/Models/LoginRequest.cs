namespace Ruvarr.Infrastructure.Tvdb.Models;

internal sealed record class LoginRequest(string ApiKey, string? Pin);