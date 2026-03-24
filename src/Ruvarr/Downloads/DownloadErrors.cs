using Ruvarr.Abstractions;

namespace Ruvarr.Downloads;

public static class DownloadErrors
{
    public const string ItemNotFoundCode = "Downloads.ItemNotFound";
    public const string ItemNotDeletableCode = "Downloads.ItemNotDeletable";

    public static RuvarrError ItemNotFound => new(
        ItemNotFoundCode,
        "Download queue item not found.");

    public static RuvarrError ItemNotDeletable => new(
        ItemNotDeletableCode,
        "Only pending or failed items can be deleted.");
}
