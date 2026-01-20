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
        bool allowEntryReordering = false,
        bool allowBookingCreation = false,
        bool allowBookingViewing = false,
        bool showEmptyStateMessage = true,
        string? emptyStateMessage = null,
        bool showBookingConfirmations = true,
        bool showBookingMetadata = true)
    {
        Trip = trip ?? throw new ArgumentNullException(nameof(trip));
        Timeline = timeline ?? throw new ArgumentNullException(nameof(timeline));
        BookingSelector = bookingSelector ?? (_ => null);
        AllowEntryEditing = allowEntryEditing;
        AllowEntryReordering = allowEntryReordering;
        AllowBookingCreation = allowBookingCreation;
        AllowBookingViewing = allowBookingViewing;
        ShowEmptyStateMessage = showEmptyStateMessage;
        EmptyStateMessage = string.IsNullOrWhiteSpace(emptyStateMessage)
            ? "No itinerary entries yet. Use the quick actions to start planning this trip."
            : emptyStateMessage;
        ShowBookingConfirmations = showBookingConfirmations;
        ShowBookingMetadata = showBookingMetadata;
        
        // Determine if trip is in progress and should hide past days
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        IsTripInProgress = trip.StartDate.HasValue && trip.EndDate.HasValue 
            && today >= trip.StartDate.Value && today <= trip.EndDate.Value;
        CurrentDate = today;
    }

    public Trip Trip { get; }

    public TimelineViewModel Timeline { get; }

    public Func<string, Booking?> BookingSelector { get; }

    public bool AllowEntryEditing { get; }

    public bool AllowEntryReordering { get; }

    public bool AllowBookingCreation { get; }

    public bool AllowBookingViewing { get; }

    public bool ShowEmptyStateMessage { get; }

    public string EmptyStateMessage { get; }

    public bool ShowBookingConfirmations { get; }

    public bool ShowBookingMetadata { get; }

    public bool IsTripInProgress { get; }
    
    public DateOnly CurrentDate { get; }

    public Booking? GetBookingForEntry(string entryId)
        => string.IsNullOrWhiteSpace(entryId) ? null : BookingSelector(entryId);
}
