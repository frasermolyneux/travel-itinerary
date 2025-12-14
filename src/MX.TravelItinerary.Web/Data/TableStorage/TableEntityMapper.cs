using System;
using System.Text.Json;
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
        var metadata = GetMetadata(entity);

        return new ItineraryEntry(
            TripId: entity.PartitionKey,
            EntryId: entity.RowKey,
            Date: entity.GetDateOnly("Date"),
            EndDate: entity.GetDateOnly("EndDate"),
            IsMultiDay: entity.GetBoolean("IsMultiDay", defaultValue: false),
            ItemType: entity.GetString("ItemType").ToTimelineItemType(),
            Title: entity.GetString("Title") ?? entity.RowKey,
            Details: entity.GetString("Details"),
            GooglePlaceId: entity.GetString("GooglePlaceId"),
            Tags: entity.GetString("Tags"),
            Metadata: metadata,
            SortOrder: entity.GetInt32("SortOrder"));
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
            IsPaid: entity.GetBoolean("IsPaid", defaultValue: false),
            CancellationPolicy: entity.GetString("CancellationPolicy"),
            CancellationByDate: entity.GetDateOnly("CancellationByDate"),
            ConfirmationDetails: entity.GetString("ConfirmationDetails"),
            ConfirmationUrl: TryGetUri(entity.GetString("ConfirmationUrl")),
            Metadata: GetBookingMetadata(entity));

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
            ShowBookingConfirmations: entity.GetBoolean("ShowBookingConfirmations", defaultValue: true),
            ShowBookingMetadata: entity.GetBoolean("ShowBookingMetadata", defaultValue: true),
            Notes: entity.GetString("Notes"));
    }

    private static TimelineItemType GetBookingItemType(TableEntity entity)
        => entity.GetString("ItemType").ToTimelineItemType();

    private static TravelMetadata? GetMetadata(TableEntity entity)
    {
        var json = entity.GetString("MetadataJson");
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TravelMetadata>(json, TableStorageJsonOptions.Metadata);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static BookingMetadata? GetBookingMetadata(TableEntity entity)
    {
        var json = entity.GetString("BookingMetadataJson");
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<BookingMetadata>(json, TableStorageJsonOptions.Metadata);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Uri? TryGetUri(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ? uri : null;
    }
}
