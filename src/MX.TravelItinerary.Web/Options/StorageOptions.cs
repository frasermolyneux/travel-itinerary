namespace MX.TravelItinerary.Web.Options;

public sealed class StorageOptions
{
    public required string AccountName { get; set; }

    public required string TableEndpoint { get; set; }

    public required StorageTableNames Tables { get; set; }
}

public sealed class StorageTableNames
{
    public string Trips { get; set; } = "Trips";

    public string ItineraryEntries { get; set; } = "ItineraryEntries";

    public string Bookings { get; set; } = "Bookings";

    public string ShareLinks { get; set; } = "ShareLinks";

    public string TripAccess { get; set; } = "TripAccess";

    public string SavedShareLinks { get; set; } = "SavedShareLinks";
}
