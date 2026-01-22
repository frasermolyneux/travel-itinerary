using Azure.Data.Tables;
using Microsoft.Extensions.Options;
using MX.TravelItinerary.Web.Options;

namespace MX.TravelItinerary.Web.Data.TableStorage;

public interface ITableContext
{
    TableClient Trips { get; }
    TableClient ItineraryEntries { get; }
    TableClient Bookings { get; }
    TableClient ShareLinks { get; }
    TableClient TripAccess { get; }
    TableClient SavedShareLinks { get; }
}

internal sealed class TableContext : ITableContext
{
    public TableContext(TableServiceClient tableServiceClient, IOptions<StorageOptions> options)
    {
        var tableNames = options.Value.Tables ?? throw new InvalidOperationException("Storage:Tables is not configured.");

        Trips = tableServiceClient.GetTableClient(tableNames.Trips);
        ItineraryEntries = tableServiceClient.GetTableClient(tableNames.ItineraryEntries);
        Bookings = tableServiceClient.GetTableClient(tableNames.Bookings);
        ShareLinks = tableServiceClient.GetTableClient(tableNames.ShareLinks);
        TripAccess = tableServiceClient.GetTableClient(tableNames.TripAccess);
        SavedShareLinks = tableServiceClient.GetTableClient(tableNames.SavedShareLinks);
    }

    public TableClient Trips { get; }

    public TableClient ItineraryEntries { get; }

    public TableClient Bookings { get; }

    public TableClient ShareLinks { get; }

    public TableClient TripAccess { get; }

    public TableClient SavedShareLinks { get; }
}
