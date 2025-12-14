using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MX.TravelItinerary.Web.Infrastructure;

public static class CurrencyCatalog
{
    private static readonly IReadOnlyDictionary<string, string[]> ManualAliases =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["GBP"] = new[]
            {
                "British Pound",
                "British Pounds",
                "Great Britain Pound",
                "Great Britain Pounds",
                "Great British Pound",
                "Great British Pounds",
                "UK Pound",
                "UK Pounds",
                "Pound Sterling"
            },
            ["USD"] = new[]
            {
                "US Dollar",
                "United States Dollar",
                "American Dollar"
            },
            ["EUR"] = new[]
            {
                "Euro",
                "European Euro"
            },
            ["PTS"] = new[]
            {
                "Rewards Points"
            }
        };

    private static readonly IReadOnlyDictionary<string, string[]> ManualKeywords =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["PTS"] = new[]
            {
                "Rewards Points",
                "Reward Points",
                "Loyalty Points",
                "Points Booking",
                "Points Redemption",
                "Loyalty Rewards"
            }
        };

    public static IReadOnlyList<CurrencyOption> All { get; } = Build();

    private static IReadOnlyList<CurrencyOption> Build()
    {
        var cultures = CultureInfo.GetCultures(CultureTypes.SpecificCultures);
        var builders = new Dictionary<string, CurrencyOptionBuilder>(StringComparer.OrdinalIgnoreCase);

        foreach (var culture in cultures)
        {
            RegionInfo? region = null;
            try
            {
                region = new RegionInfo(culture.Name);
            }
            catch (ArgumentException)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(region?.ISOCurrencySymbol))
            {
                continue;
            }

            if (!builders.TryGetValue(region.ISOCurrencySymbol, out var builder))
            {
                builder = new CurrencyOptionBuilder(region.ISOCurrencySymbol);
                builders[region.ISOCurrencySymbol] = builder;
            }

            builder.AddRegion(region);
        }

        foreach (var manual in ManualAliases)
        {
            if (!builders.TryGetValue(manual.Key, out var builder))
            {
                builder = new CurrencyOptionBuilder(manual.Key);
                builders[manual.Key] = builder;
            }

            builder.AddAliases(manual.Value);
        }

        foreach (var manual in ManualKeywords)
        {
            if (!builders.TryGetValue(manual.Key, out var builder))
            {
                builder = new CurrencyOptionBuilder(manual.Key);
                builders[manual.Key] = builder;
            }

            builder.AddKeywords(manual.Value);
        }

        return builders.Values
            .Select(builder => builder.Build())
            .OrderBy(option => option.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public sealed record CurrencyOption(string Code, string DisplayName, IReadOnlyList<string> SearchTerms);

    private sealed class CurrencyOptionBuilder
    {
        private readonly string _code;
        private readonly HashSet<string> _names = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _keywords = new(StringComparer.OrdinalIgnoreCase);

        internal CurrencyOptionBuilder(string code)
        {
            _code = string.IsNullOrWhiteSpace(code)
                ? string.Empty
                : code.Trim().ToUpperInvariant();
        }

        internal void AddRegion(RegionInfo region)
        {
            AddName(region.CurrencyEnglishName);
            AddName(region.CurrencyNativeName);

            AddKeyword(region.CurrencyEnglishName);
            AddKeyword(region.CurrencyNativeName);
            AddKeyword(region.CurrencySymbol);
            AddKeyword(region.ISOCurrencySymbol);
            AddKeyword(region.EnglishName);
            AddKeyword(region.NativeName);
            AddKeyword(region.DisplayName);
            AddKeyword(region.TwoLetterISORegionName);
            AddKeyword(region.ThreeLetterISORegionName);

            if (!string.IsNullOrWhiteSpace(region.CurrencyEnglishName) && !string.IsNullOrWhiteSpace(region.EnglishName))
            {
                AddKeyword($"{region.CurrencyEnglishName} {region.EnglishName}");
            }
        }

        internal void AddAliases(IEnumerable<string> aliases)
        {
            if (aliases is null)
            {
                return;
            }

            foreach (var alias in aliases)
            {
                AddName(alias);
                AddKeyword(alias);
            }
        }

        internal void AddKeywords(IEnumerable<string> keywords)
        {
            if (keywords is null)
            {
                return;
            }

            foreach (var keyword in keywords)
            {
                AddKeyword(keyword);
            }
        }

        private void AddName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            _names.Add(Normalize(value));
        }

        private void AddKeyword(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            _keywords.Add(Normalize(value));
        }

        private static string Normalize(string value)
            => value.Trim();

        internal CurrencyOption Build()
        {
            var primaryName = _names.Count > 0
                ? _names.OrderBy(name => name.Length).ThenBy(name => name, StringComparer.OrdinalIgnoreCase).First()
                : _code;

            var displayName = string.IsNullOrWhiteSpace(primaryName)
                ? _code
                : $"{primaryName} ({_code})";

            var searchTerms = _keywords
                .Concat(_names)
                .Append(_code)
                .Where(term => !string.IsNullOrWhiteSpace(term))
                .Select(term => term.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(term => term, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return new CurrencyOption(_code, displayName, searchTerms);
        }
    }
}
