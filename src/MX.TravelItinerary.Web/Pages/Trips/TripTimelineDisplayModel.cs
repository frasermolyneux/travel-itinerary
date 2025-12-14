using System;
using MX.TravelItinerary.Web.Data.Models;

namespace MX.TravelItinerary.Web.Pages.Trips;

public sealed class TripTimelineDisplayModel
{
    public TripTimelineDisplayModel(
        Trip trip,
        TimelineViewModel timeline,
        Func<string, Booking?> bookingSelector,
        bool allowEntryEditing = false,
        bool allowBookingCreation = false,
        bool allowBookingViewing = false,
        bool showEmptyStateMessage = true,
        string? emptyStateMessage = null)
    {
        Trip = trip ?? throw new ArgumentNullException(nameof(trip));
        Timeline = timeline ?? throw new ArgumentNullException(nameof(timeline));
        BookingSelector = bookingSelector ?? (_ => null);
        AllowEntryEditing = allowEntryEditing;
        AllowBookingCreation = allowBookingCreation;
        AllowBookingViewing = allowBookingViewing;
        ShowEmptyStateMessage = showEmptyStateMessage;
        EmptyStateMessage = string.IsNullOrWhiteSpace(emptyStateMessage)
            ? "No itinerary entries yet. Use the quick actions to start planning this trip."
            : emptyStateMessage;
    }

    public Trip Trip { get; }

    public TimelineViewModel Timeline { get; }

    public Func<string, Booking?> BookingSelector { get; }

    public bool AllowEntryEditing { get; }

    public bool AllowBookingCreation { get; }

    public bool AllowBookingViewing { get; }

    public bool ShowEmptyStateMessage { get; }

    public string EmptyStateMessage { get; }

    public Booking? GetBookingForEntry(string entryId)
        => string.IsNullOrWhiteSpace(entryId) ? null : BookingSelector(entryId);
}
