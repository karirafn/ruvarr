namespace Ruvarr.Abstractions;

internal static class ApiClientErrors
{
    public const string NotFoundCode = "ApiClient.NotFound";
    public const string RequestFailedCode = "ApiClient.RequestFailed";

    public static readonly RuvarrError NotFound = new(NotFoundCode, "The requested resource was not found.");
    public static readonly RuvarrError RequestFailed = new(RequestFailedCode, "The API request failed.");
}
