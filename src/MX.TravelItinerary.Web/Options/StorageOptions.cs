namespace MX.TravelItinerary.Web.Options;

public sealed class StorageOptions
{
    public string AccountName { get; set; } = string.Empty;

    public string TableEndpoint { get; set; } = string.Empty;

    public StorageTableNames Tables { get; set; } = new();
}

public sealed class StorageTableNames
{
    public string Trips { get; set; } = "Trips";

    public string ItineraryEntries { get; set; } = "ItineraryEntries";

    public string Bookings { get; set; } = "Bookings";

    public string ShareLinks { get; set; } = "ShareLinks";

    public string TripAccess { get; set; } = "TripAccess";
}
