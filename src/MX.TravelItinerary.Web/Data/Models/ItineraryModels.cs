using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace MX.TravelItinerary.Web.Data.Models;

public enum TimelineItemType
{
    [Display(Name = "Flight")]
    Flight = 0,

    [Display(Name = "Train")]
    Train = 1,

    [Display(Name = "Coach")]
    Coach = 2,

    [Display(Name = "Ferry")]
    Ferry = 3,

    [Display(Name = "Taxi")]
    Taxi = 4,

    [Display(Name = "Private Car")]
    PrivateCar = 5,

    [Display(Name = "Rental Car")]
    RentalCar = 6,

    [Display(Name = "Parking")]
    Parking = 7,

    [Display(Name = "Hotel")]
    Hotel = 8,

    [Display(Name = "Flat")]
    Flat = 9,

    [Display(Name = "House")]
    House = 10,

    [Display(Name = "Tour")]
    Tour = 11,

    [Display(Name = "Museum")]
    Museum = 12,

    [Display(Name = "Park")]
    Park = 13,

    [Display(Name = "Dining")]
    Dining = 14,

    [Display(Name = "Note")]
    Note = 15,

    [Display(Name = "Other")]
    Other = 16
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

public sealed partial record TravelMetadata(
    FlightMetadata? Flight,
    StayMetadata? Stay);

public sealed partial record FlightMetadata(
    string? Airline,
    string? FlightNumber,
    string? DepartureAirport,
    string? DepartureTime,
    string? ArrivalAirport,
    string? ArrivalTime);

public sealed partial record StayMetadata(
    string? PropertyName,
    string? PropertyLink);

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
    string? Tags,
    TravelMetadata? Metadata,
    int? SortOrder);

public sealed record ItineraryEntryMutation(
    DateOnly? Date,
    DateOnly? EndDate,
    bool IsMultiDay,
    TimelineItemType ItemType,
    string Title,
    string? Details,
    LocationInfo? Location,
    string? Tags,
    TravelMetadata? Metadata,
    int? SortOrder);

public sealed record Booking(
    string TripId,
    string BookingId,
    string? EntryId,
    TimelineItemType ItemType,
    string? Vendor,
    string? Reference,
    decimal? Cost,
    string? Currency,
    bool? IsRefundable,
    bool? IsPaid,
    string? CancellationPolicy,
    DateOnly? CancellationByDate,
    string? ConfirmationDetails,
    Uri? ConfirmationUrl,
    BookingMetadata? Metadata);

public sealed record BookingMutation(
    string? EntryId,
    TimelineItemType ItemType,
    string? Vendor,
    string? Reference,
    decimal? Cost,
    string? Currency,
    bool? IsRefundable,
    bool? IsPaid,
    string? CancellationPolicy,
    DateOnly? CancellationByDate,
    string? ConfirmationDetails,
    Uri? ConfirmationUrl,
    BookingMetadata? Metadata);

public sealed partial record BookingMetadata(
    StayBookingMetadata? Stay);

public sealed partial record StayBookingMetadata(
    string? CheckInTime,
    string? CheckOutTime,
    string? RoomType,
    IReadOnlyList<string>? Includes);

public sealed record ShareLink(
    string TripId,
    string ShareCode,
    string OwnerUserId,
    DateTimeOffset? CreatedOn,
    string? CreatedBy,
    DateTimeOffset? ExpiresOn,
    bool MaskBookings,
    bool IncludeCost,
    bool ShowBookingConfirmations,
    bool ShowBookingMetadata,
    string? Notes);

public sealed record ShareLinkMutation(
    DateTimeOffset? ExpiresOn,
    bool MaskBookings,
    bool IncludeCost,
    bool ShowBookingConfirmations,
    bool ShowBookingMetadata,
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
        string? dateText = null;
        if (IsMultiDay && Date is { } start)
        {
            if (EndDate is { } end && end >= start)
            {
                dateText = $"{start.ToString("MMM d", CultureInfo.InvariantCulture)} → {end.ToString("MMM d", CultureInfo.InvariantCulture)}";
            }
            else
            {
                dateText = start.ToString("MMM d", CultureInfo.InvariantCulture);
            }
        }

        var parts = new List<string?>
        {
            dateText,
            string.IsNullOrWhiteSpace(Title) ? null : Title
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

    public static TimelineItemType ToTimelineItemType(this string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return TimelineItemType.Other;
        }

        if (Enum.TryParse<TimelineItemType>(value, true, out var parsed))
        {
            return parsed;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "travel" => TimelineItemType.Flight,
            "lodging" => TimelineItemType.Hotel,
            "activity" => TimelineItemType.Tour,
            "dining" => TimelineItemType.Dining,
            "transportation" => TimelineItemType.Taxi,
            _ => TimelineItemType.Other
        };
    }

    public static string ToStorageValue(this TimelineItemType itemType)
        => itemType.ToString().ToLowerInvariant();

    public static string ToCssToken(this TimelineItemType itemType)
        => itemType.ToStorageValue();

    public static string GetIconClass(this TimelineItemType itemType)
        => itemType switch
        {
            TimelineItemType.Flight => "bi-airplane-engines",
            TimelineItemType.Train => "bi-train-front",
            TimelineItemType.Coach => "bi-bus-front",
            TimelineItemType.Ferry => "bi-tsunami",
            TimelineItemType.Taxi => "bi-taxi-front",
            TimelineItemType.PrivateCar => "bi-car-front",
            TimelineItemType.RentalCar => "bi-steering-wheel",
            TimelineItemType.Parking => "bi-p-circle",
            TimelineItemType.Hotel => "bi-buildings",
            TimelineItemType.Flat => "bi-building",
            TimelineItemType.House => "bi-house-heart",
            TimelineItemType.Tour => "bi-map",
            TimelineItemType.Museum => "bi-bank",
            TimelineItemType.Park => "bi-tree",
            TimelineItemType.Dining => "bi-egg-fried",
            TimelineItemType.Note => "bi-journal-text",
            _ => "bi-stars"
        };
}

public sealed partial record TravelMetadata
{
    public bool HasFlightDetails => Flight?.HasContent == true;

    public bool HasStayDetails => Stay?.HasContent == true;

    public bool HasContent => HasFlightDetails || HasStayDetails;
}

public sealed partial record FlightMetadata
{
    public bool HasContent => !string.IsNullOrWhiteSpace(Airline)
        || !string.IsNullOrWhiteSpace(FlightNumber)
        || !string.IsNullOrWhiteSpace(DepartureAirport)
        || !string.IsNullOrWhiteSpace(DepartureTime)
        || !string.IsNullOrWhiteSpace(ArrivalAirport)
        || !string.IsNullOrWhiteSpace(ArrivalTime);
}

public sealed partial record StayMetadata
{
    public bool HasContent => !string.IsNullOrWhiteSpace(PropertyName)
    || !string.IsNullOrWhiteSpace(PropertyLink);
}

public sealed partial record BookingMetadata
{
    public bool HasContent => Stay?.HasContent == true;
}

public sealed partial record StayBookingMetadata
{
    public bool HasContent => !string.IsNullOrWhiteSpace(CheckInTime)
        || !string.IsNullOrWhiteSpace(CheckOutTime)
    || !string.IsNullOrWhiteSpace(RoomType)
    || (Includes?.Count > 0);
}
