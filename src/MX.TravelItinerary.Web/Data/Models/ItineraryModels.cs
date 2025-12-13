using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace MX.TravelItinerary.Web.Data.Models;

public enum TimelineItemType
{
    [Display(Name = "Travel / transit")]
    Travel = 0,

    [Display(Name = "Lodging / stay")]
    Lodging = 1,

    [Display(Name = "Activity / excursion")]
    Activity = 2,

    [Display(Name = "Dining / food")]
    Dining = 3,

    [Display(Name = "Transportation / transfer")]
    Transportation = 4,

    [Display(Name = "Personal note")]
    Note = 5,

    [Display(Name = "Other")]
    Other = 6
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
    DateOnly? EndDate,
    bool IsMultiDay,
    TimelineItemType ItemType,
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
    DateOnly? EndDate,
    bool IsMultiDay,
    TimelineItemType ItemType,
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
    IReadOnlyList<ItineraryEntry> Entries,
    IReadOnlyList<Booking> Bookings,
    ShareLink? ShareLink);

public sealed partial record ItineraryEntry
{
    public override string ToString()
    {
        var dateText = Date is null
            ? null
            : IsMultiDay && EndDate is { } endDate && endDate >= Date
                ? $"{Date.Value.ToString("MMM d", CultureInfo.InvariantCulture)} → {endDate.ToString("MMM d", CultureInfo.InvariantCulture)}"
                : Date.Value.ToString("MMM d", CultureInfo.InvariantCulture);

        var parts = new List<string?>
        {
            dateText,
            string.IsNullOrWhiteSpace(Title) ? null : Title,
            $"[{ItemType.GetDisplayName()}]"
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

    public static BookingType ToBookingType(this string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && Enum.TryParse<BookingType>(value, true, out var parsed))
        {
            return parsed;
        }

        return BookingType.Other;
    }

    public static TimelineItemType ToTimelineItemType(this string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && Enum.TryParse<TimelineItemType>(value, true, out var parsed))
        {
            return parsed;
        }

        return TimelineItemType.Other;
    }

    public static string ToStorageValue(this BookingType bookingType)
        => bookingType.ToString().ToLowerInvariant();

    public static string ToStorageValue(this TimelineItemType itemType)
        => itemType.ToString().ToLowerInvariant();

    public static string ToCssToken(this TimelineItemType itemType)
        => itemType.ToStorageValue();
}
