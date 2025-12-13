using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MX.TravelItinerary.Web.Data.Models;

public sealed record Trip(
    string TripId,
    string UserId,
    string Name,
    string Slug,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? HomeTimeZone,
    string? DefaultCurrency);

public sealed record TripMutation(
    string Name,
    string Slug,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? HomeTimeZone,
    string? DefaultCurrency);

public sealed partial record TripSegment(
    string TripId,
    string SegmentId,
    string SegmentType,
    DateTimeOffset? StartDateTimeUtc,
    DateTimeOffset? EndDateTimeUtc,
    LocationInfo? StartLocation,
    LocationInfo? EndLocation,
    string? Title,
    string? Description);

public sealed record TripSegmentMutation(
    string SegmentType,
    DateTimeOffset? StartDateTimeUtc,
    DateTimeOffset? EndDateTimeUtc,
    LocationInfo? StartLocation,
    LocationInfo? EndLocation,
    string? Title,
    string? Description);

public sealed record LocationInfo(
    string? Label,
    double? Latitude,
    double? Longitude,
    string? Url = null,
    string? Notes = null);

public sealed partial record ItineraryEntry(
    string TripId,
    string EntryId,
    DateOnly? Date,
    string? Category,
    string Title,
    string? Details,
    LocationInfo? Location,
    decimal? CostEstimate,
    string? Currency,
    bool? IsPaid,
    string? PaymentStatus,
    string? Provider,
    string? Tags);

public sealed record ItineraryEntryMutation(
    DateOnly? Date,
    string? Category,
    string Title,
    string? Details,
    LocationInfo? Location,
    decimal? CostEstimate,
    string? Currency,
    bool? IsPaid,
    string? PaymentStatus,
    string? Provider,
    string? Tags);

public sealed record Booking(
    string TripId,
    string BookingId,
    string? EntryId,
    string? SegmentId,
    string? BookingType,
    string? Vendor,
    string? Reference,
    decimal? Cost,
    string? Currency,
    bool? IsRefundable,
    string? CancellationPolicy,
    string? ConfirmationDetailsJson);

public sealed record BookingMutation(
    string? EntryId,
    string? SegmentId,
    string? BookingType,
    string? Vendor,
    string? Reference,
    decimal? Cost,
    string? Currency,
    bool? IsRefundable,
    string? CancellationPolicy,
    string? ConfirmationDetailsJson);

public sealed record ShareLink(
    string TripId,
    string ShareCode,
    string OwnerUserId,
    DateTimeOffset? CreatedOn,
    string? CreatedBy,
    DateTimeOffset? ExpiresOn,
    bool MaskBookings,
    bool IncludeCost,
    string? Notes);

public sealed record TripDetails(
    Trip Trip,
    IReadOnlyList<TripSegment> Segments,
    IReadOnlyList<ItineraryEntry> Entries,
    IReadOnlyList<Booking> Bookings,
    ShareLink? ShareLink);

public sealed partial record TripSegment
{
    public override string ToString()
    {
        var parts = new List<string?>
        {
            string.IsNullOrWhiteSpace(Title) ? null : Title,
            string.IsNullOrWhiteSpace(SegmentType) ? null : $"[{SegmentType}]",
            FormatWindow(StartDateTimeUtc, EndDateTimeUtc)
        };

        var summary = string.Join(" • ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        return string.IsNullOrWhiteSpace(summary) ? SegmentId : summary;
    }

    private static string? FormatWindow(DateTimeOffset? start, DateTimeOffset? end)
    {
        if (start is null && end is null)
        {
            return null;
        }

        var startText = start?.ToString("MMM d HH:mm 'UTC'", CultureInfo.InvariantCulture);
        var endText = end?.ToString("MMM d HH:mm 'UTC'", CultureInfo.InvariantCulture);

        return end is null ? startText : $"{startText ?? "?"} → {endText}";
    }
}

public sealed partial record ItineraryEntry
{
    public override string ToString()
    {
        var parts = new List<string?>
        {
            Date?.ToString("MMM d", CultureInfo.InvariantCulture),
            string.IsNullOrWhiteSpace(Title) ? null : Title,
            string.IsNullOrWhiteSpace(Category) ? null : $"[{Category}]"
        };

        var summary = string.Join(" • ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        return string.IsNullOrWhiteSpace(summary) ? EntryId : summary;
    }
}
