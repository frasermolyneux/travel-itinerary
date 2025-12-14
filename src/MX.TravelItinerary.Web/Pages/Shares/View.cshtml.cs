using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MX.TravelItinerary.Web.Data;
using MX.TravelItinerary.Web.Data.Models;
using MX.TravelItinerary.Web.Pages.Trips;

namespace MX.TravelItinerary.Web.Pages.Shares;

[AllowAnonymous]
public sealed class ViewModel : PageModel
{
    private readonly IItineraryRepository _repository;
    private readonly TelemetryClient _telemetry;

    public ViewModel(IItineraryRepository repository, TelemetryClient telemetry)
    {
        _repository = repository;
        _telemetry = telemetry;
    }

    [BindProperty(SupportsGet = true)]
    public string? TripSlug { get; set; }

    [BindProperty(SupportsGet = true)]
    public string ShareCode { get; set; } = string.Empty;

    public TripDetails? TripDetails { get; private set; }

    public TimelineViewModel Timeline { get; private set; } = TimelineViewModel.Empty;

    public IReadOnlyDictionary<string, Booking> EntryBookings { get; private set; } = new Dictionary<string, Booking>(StringComparer.OrdinalIgnoreCase);

    public string? ErrorMessage { get; private set; }

    public TripTimelineDisplayModel? GetTimelineDisplayModel()
    {
        if (TripDetails is null)
        {
            return null;
        }

        return new TripTimelineDisplayModel(
            TripDetails.Trip,
            Timeline,
            GetBookingForEntry,
            allowEntryEditing: false,
            allowBookingCreation: false,
            allowBookingViewing: true,
            showEmptyStateMessage: true,
            emptyStateMessage: "No shared plans yet.");
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ShareCode))
        {
            ErrorMessage = "This share link is missing a code.";
            TrackShareLinkEvent("ShareLinkAccessRejected", properties =>
            {
                properties["Reason"] = "MissingCode";
            });
            return Page();
        }

        var details = await _repository.GetTripByShareCodeAsync(ShareCode, cancellationToken);
        if (details is null)
        {
            ErrorMessage = "This share link has expired or is no longer available.";
            return Page();
        }

        TripDetails = details;
        Timeline = TimelineViewModel.From(details);
        EntryBookings = BuildBookingLookup(details.Bookings, booking => booking.EntryId);

        var canonicalSlug = string.IsNullOrWhiteSpace(details.Trip.Slug)
            ? details.Trip.TripId
            : details.Trip.Slug;

        if (!string.IsNullOrWhiteSpace(canonicalSlug) && !string.Equals(canonicalSlug, TripSlug, StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToPage(new { tripSlug = canonicalSlug, shareCode = ShareCode });
        }

        TripSlug = canonicalSlug;
        return Page();
    }

    private void TrackShareLinkEvent(string eventName, Action<Dictionary<string, string?>> configure)
    {
        if (string.IsNullOrWhiteSpace(eventName))
        {
            return;
        }

        var properties = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["ShareCode"] = ShareCode
        };

        configure(properties);
        _telemetry.TrackEvent(eventName, properties);
    }

    public Booking? GetBookingForEntry(string entryId)
        => EntryBookings.TryGetValue(entryId, out var booking) ? booking : null;

    private static IReadOnlyDictionary<string, Booking> BuildBookingLookup(IEnumerable<Booking> bookings, Func<Booking, string?> keySelector)
    {
        var lookup = new Dictionary<string, Booking>(StringComparer.OrdinalIgnoreCase);
        foreach (var booking in bookings)
        {
            var key = keySelector(booking);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (!lookup.ContainsKey(key))
            {
                lookup[key] = booking;
            }
        }

        return lookup;
    }
}
