using System.Globalization;

using Ruvarr.Abstractions;

namespace Ruvarr.Programs.Domain;

/// <summary>
/// The TVDB season(s) a user assigns to a RÚV program matched to a TVDB series: a non-empty set of regular
/// seasons, kept distinct and ascending.
/// </summary>
internal sealed class ManualSeasons : IEquatable<ManualSeasons>
{
    private const char StorageSeparator = ',';

    private ManualSeasons(IReadOnlyList<int> numbers)
    {
        Numbers = numbers;
    }

    public IReadOnlyList<int> Numbers { get; }

    public static Result<ManualSeasons> Create(IEnumerable<int> seasons)
    {
        ArgumentNullException.ThrowIfNull(seasons);

        List<int> numbers = [.. seasons.Distinct().Order()];

        if (numbers.Count == 0)
        {
            return ProgramErrors.ManualSeasonsEmpty;
        }

        if (numbers[0] <= 0)
        {
            return ProgramErrors.ManualSeasonNotInSeries;
        }

        return new ManualSeasons(numbers);
    }

    public bool Contains(int season) => Numbers.Contains(season);

    public string ToStorage() => string.Join(StorageSeparator, Numbers.Select(x => x.ToString(CultureInfo.InvariantCulture)));

    public static ManualSeasons FromStorage(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return Create(value.Split(StorageSeparator).Select(x => int.Parse(x, CultureInfo.InvariantCulture))).FromResult();
    }

    public bool Equals(ManualSeasons? other) => other is not null && Numbers.SequenceEqual(other.Numbers);

    public override bool Equals(object? obj) => Equals(obj as ManualSeasons);

    public override int GetHashCode()
    {
        HashCode hash = new();
        foreach (int number in Numbers)
        {
            hash.Add(number);
        }

        return hash.ToHashCode();
    }

    public override string ToString() => string.Join(", ", Numbers);
}
