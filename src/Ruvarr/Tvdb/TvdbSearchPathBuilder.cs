using System.Text;

namespace Ruvarr.Tvdb;

internal sealed class TvdbSearchPathBuilder
{
    private readonly Dictionary<string, string> _queries = [];

    public TvdbSearchPathBuilder WithQuery(string? query)
    {
        AddQuery("query", query);
        return this;
    }

    public TvdbSearchPathBuilder WithType(string? type)
    {
        AddQuery("type", type);
        return this;
    }

    public TvdbSearchPathBuilder WithYear(int? year)
    {
        AddQuery("year", year);
        return this;
    }

    public TvdbSearchPathBuilder WithCompany(string? company)
    {
        AddQuery("company", company);
        return this;
    }

    public TvdbSearchPathBuilder WithCountry(string? country)
    {
        AddQuery("country", country);
        return this;
    }

    public TvdbSearchPathBuilder WithDirectory(string? directory)
    {
        AddQuery("directory", directory);
        return this;
    }

    public TvdbSearchPathBuilder WithLanguage(string? language)
    {
        AddQuery("language", language);
        return this;
    }

    public TvdbSearchPathBuilder WithNetwork(string? network)
    {
        AddQuery("network", network);
        return this;
    }

    public TvdbSearchPathBuilder WithRemoteId(string? remoteId)
    {
        AddQuery("remoteId", remoteId);
        return this;
    }

    public TvdbSearchPathBuilder WithOffset(int? offset)
    {
        AddQuery("offset", offset);
        return this;
    }

    public TvdbSearchPathBuilder WithLimit(int? limit)
    {
        AddQuery("limit", limit);
        return this;
    }

    public string Build()
    {
        string queries = string.Join('&', _queries.Select(x => $"{x.Key}={x.Value}"));
        return $"v4/search?{queries}";
    }

    private void AddQuery(string key, object? value)
    {
        if (value is null)
        {
            return;
        }

        string queries = value.ToString() ?? string.Empty;
        string normalized = queries.Normalize(NormalizationForm.FormC);
        string encoded = Uri.EscapeDataString(normalized);

        _queries.Add(key, encoded);
    }
}