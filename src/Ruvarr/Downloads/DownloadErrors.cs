using Ruvarr.Abstractions;

namespace Ruvarr.Downloads;

public static class DownloadErrors
{
    public const string ItemNotFoundCode = "Downloads.ItemNotFound";
    public const string ItemNotDismissableCode = "Downloads.ItemNotDismissable";
    public const string ItemNotRetryableCode = "Downloads.ItemNotRetryable";

    public static RuvarrError ItemNotFound => new(
        ItemNotFoundCode,
        "Download queue item not found.");

    public static RuvarrError ItemNotDismissable => new(
        ItemNotDismissableCode,
        "Only Failed or Exhausted items can be dismissed.");

    public static RuvarrError ItemNotRetryable => new(
        ItemNotRetryableCode,
        "Only Failed or Exhausted items can be retried.");
}
