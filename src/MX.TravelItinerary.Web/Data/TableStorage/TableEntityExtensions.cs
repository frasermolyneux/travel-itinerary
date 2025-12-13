using System.Globalization;
using System.Text.Json;
using Azure.Data.Tables;

namespace MX.TravelItinerary.Web.Data.TableStorage;

internal static class TableEntityExtensions
{
    public static string? GetString(this TableEntity entity, string propertyName)
        => entity.TryGetValue(propertyName, out var value) ? value?.ToString() : null;

    public static bool GetBoolean(this TableEntity entity, string propertyName, bool defaultValue = false)
    {
        if (!entity.TryGetValue(propertyName, out var value) || value is null)
        {
            return defaultValue;
        }

        return value switch
        {
            bool b => b,
            string s when bool.TryParse(s, out var parsed) => parsed,
            int i => i != 0,
            long l => l != 0,
            _ => defaultValue
        };
    }

    public static decimal? GetDecimal(this TableEntity entity, string propertyName)
    {
        if (!entity.TryGetValue(propertyName, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            decimal dec => dec,
            double d => (decimal)d,
            float f => (decimal)f,
            int i => i,
            long l => l,
            string s when decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null
        };
    }

    public static DateTimeOffset? GetDateTimeOffset(this TableEntity entity, string propertyName)
    {
        if (!entity.TryGetValue(propertyName, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            DateTimeOffset dto => dto,
            DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)),
            string s when DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed) => parsed,
            _ => null
        };
    }

    public static DateOnly? GetDateOnly(this TableEntity entity, string propertyName)
    {
        if (!entity.TryGetValue(propertyName, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            DateTimeOffset dto => DateOnly.FromDateTime(dto.UtcDateTime),
            DateTime dt => DateOnly.FromDateTime(DateTime.SpecifyKind(dt, DateTimeKind.Utc)),
            string s when DateOnly.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) => parsed,
            _ => null
        };
    }

    public static double? GetDouble(this TableEntity entity, string propertyName)
    {
        if (!entity.TryGetValue(propertyName, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            double d => d,
            float f => f,
            int i => i,
            long l => l,
            decimal dec => (double)dec,
            string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null
        };
    }

    public static T? GetJson<T>(this TableEntity entity, string propertyName)
    {
        var json = entity.GetString(propertyName);
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (JsonException)
        {
            return default;
        }
    }
}
