using Azure.Data.Tables;
using MX.TravelItinerary.Web.Data.Models;

namespace MX.TravelItinerary.Web.Data.TableStorage;

internal static class TableEntityMapper
{
    public static Trip ToTrip(TableEntity entity)
        => new(
            TripId: entity.RowKey,
            UserId: entity.PartitionKey,
            Name: entity.GetString("Name") ?? entity.RowKey,
            Slug: entity.GetString("Slug") ?? entity.RowKey,
            StartDate: entity.GetDateOnly("StartDate"),
            EndDate: entity.GetDateOnly("EndDate"),
            HomeTimeZone: entity.GetString("HomeTimeZone"),
            DefaultCurrency: entity.GetString("DefaultCurrency"));

    public static ItineraryEntry ToItineraryEntry(TableEntity entity)
    {
        var locationName = entity.GetString("LocationName");
        var locationUrl = entity.GetString("LocationUrl");
        var latitude = entity.GetDouble("Latitude");
        var longitude = entity.GetDouble("Longitude");

        var location = locationName is null && locationUrl is null && latitude is null && longitude is null
            ? null
            : new LocationInfo(locationName, latitude, longitude, locationUrl);

        return new ItineraryEntry(
            TripId: entity.PartitionKey,
            EntryId: entity.RowKey,
            Date: entity.GetDateOnly("Date"),
            EndDate: entity.GetDateOnly("EndDate"),
            IsMultiDay: entity.GetBoolean("IsMultiDay", defaultValue: false),
            ItemType: entity.GetString("ItemType").ToTimelineItemType(),
            Title: entity.GetString("Title") ?? entity.RowKey,
            Details: entity.GetString("Details"),
            Location: location,
            CostEstimate: entity.GetDecimal("CostEstimate"),
            Currency: entity.GetString("Currency"),
            IsPaid: entity.GetBoolean("IsPaid", defaultValue: false),
            PaymentStatus: entity.GetString("PaymentStatus"),
            Provider: entity.GetString("Provider"),
            Tags: entity.GetString("Tags"));
    }

    public static Booking ToBooking(TableEntity entity)
        => new(
            TripId: entity.PartitionKey,
            BookingId: entity.RowKey,
            EntryId: entity.GetString("EntryId"),
            ItemType: GetBookingItemType(entity),
            Vendor: entity.GetString("Vendor"),
            Reference: entity.GetString("Reference"),
            Cost: entity.GetDecimal("Cost"),
            Currency: entity.GetString("Currency"),
            IsRefundable: entity.GetBoolean("IsRefundable", defaultValue: false),
            CancellationPolicy: entity.GetString("CancellationPolicy"),
            ConfirmationDetailsJson: entity.GetString("ConfirmationDetailsJson"));

    public static ShareLink ToShareLink(TableEntity entity)
    {
        var ownerUserId = entity.GetString("OwnerUserId") ?? entity.GetString("CreatedBy") ?? string.Empty;
        return new ShareLink(
            TripId: entity.PartitionKey,
            ShareCode: entity.RowKey,
            OwnerUserId: ownerUserId,
            CreatedOn: entity.GetDateTimeOffset("CreatedOn"),
            CreatedBy: entity.GetString("CreatedBy"),
            ExpiresOn: entity.GetDateTimeOffset("ExpiresOn"),
            MaskBookings: entity.GetBoolean("MaskBookings", defaultValue: false),
            IncludeCost: entity.GetBoolean("IncludeCost", defaultValue: true),
            Notes: entity.GetString("Notes"));
    }

    private static TimelineItemType GetBookingItemType(TableEntity entity)
        => entity.GetString("ItemType").ToTimelineItemType();
}
