using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace MX.TravelItinerary.Web.Data.Models;

public enum TripSegmentType
{
    [Display(Name = "Travel / transit")]
    Travel = 0,

    [Display(Name = "Lodging / stay")]
    Lodging = 1,

    [Display(Name = "Activity / event")]
    Activity = 2,

    [Display(Name = "Other")]
    Other = 3
}

public enum BookingType
{
    [Display(Name = "Flight")]
    Flight = 0,

    [Display(Name = "Hotel")]
    Hotel = 1,

    [Display(Name = "Transport / transfer")]
    Transport = 2,

    [Display(Name = "Activity / excursion")]
    Activity = 3,

    [Display(Name = "Other")]
    Other = 4
}

public enum ItineraryEntryCategory
{
    [Display(Name = "Activity / excursion")]
    Activity = 0,

    [Display(Name = "Dining / food")]
    Dining = 1,

    [Display(Name = "Transportation")]
    Transportation = 2,

    [Display(Name = "Lodging / stay")]
    Lodging = 3,

    [Display(Name = "Personal note")]
    Note = 4,

    [Display(Name = "Other")]
    Other = 5
}

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
    TripSegmentType SegmentType,
    DateTimeOffset? StartDateTimeUtc,
    DateTimeOffset? EndDateTimeUtc,
    LocationInfo? StartLocation,
    LocationInfo? EndLocation,
    string? Title,
    string? Description);

public sealed record TripSegmentMutation(
    TripSegmentType SegmentType,
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
    ItineraryEntryCategory? Category,
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
    ItineraryEntryCategory? Category,
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
    BookingType BookingType,
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
    BookingType BookingType,
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
            $"[{SegmentType.GetDisplayName()}]",
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
            Category is null ? null : $"[{Category.Value.GetDisplayName()}]"
        };

        var summary = string.Join(" • ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        return string.IsNullOrWhiteSpace(summary) ? EntryId : summary;
    }
}

public static class ModelEnumExtensions
{
    public static string GetDisplayName(this Enum value)
    {
        var member = value.GetType().GetMember(value.ToString()).FirstOrDefault();
        if (member is not null && member.GetCustomAttribute<DisplayAttribute>() is { } display)
        {
            return display.GetName() ?? value.ToString();
        }

        return value.ToString();
    }

    public static TripSegmentType ToSegmentType(this string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && Enum.TryParse<TripSegmentType>(value, true, out var parsed))
        {
            return parsed;
        }

        return TripSegmentType.Other;
    }

    public static BookingType ToBookingType(this string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && Enum.TryParse<BookingType>(value, true, out var parsed))
        {
            return parsed;
        }

        return BookingType.Other;
    }

    public static ItineraryEntryCategory? ToEntryCategory(this string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Enum.TryParse<ItineraryEntryCategory>(value, true, out var parsed) ? parsed : null;
    }

    public static string ToStorageValue(this TripSegmentType segmentType)
        => segmentType.ToString().ToLowerInvariant();

    public static string ToStorageValue(this BookingType bookingType)
        => bookingType.ToString().ToLowerInvariant();

    public static string ToStorageValue(this ItineraryEntryCategory category)
        => category.ToString().ToLowerInvariant();

    public static string ToCssToken(this TripSegmentType segmentType)
        => segmentType.ToStorageValue();
}
