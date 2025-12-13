using System.Text.Json;
using System.Text.Json.Serialization;

namespace MX.TravelItinerary.Web.Data.TableStorage;

internal static class TableStorageJsonOptions
{
    public static JsonSerializerOptions Metadata { get; } = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };
}
