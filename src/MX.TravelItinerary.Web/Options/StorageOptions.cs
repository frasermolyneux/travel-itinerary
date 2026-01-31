namespace MX.TravelItinerary.Web.Options;

public sealed class StorageOptions
{
    public required string AccountName { get; set; }

    public required string TableEndpoint { get; set; }

    public required StorageTableNames Tables { get; set; }
}

public sealed class StorageTableNames
{
    public required string Trips { get; set; }

    public required string ItineraryEntries { get; set; }

    public required string Bookings { get; set; }

    public required string ShareLinks { get; set; }

    public required string TripAccess { get; set; }

    public required string SavedShareLinks { get; set; }
}
