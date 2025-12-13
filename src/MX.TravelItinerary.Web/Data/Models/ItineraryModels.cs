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

public sealed record TripSegment(
    string TripId,
    string SegmentId,
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

public sealed record ItineraryEntry(
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

public sealed record Booking(
    string TripId,
    string BookingId,
    string? EntryId,
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
