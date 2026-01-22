using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Identity.Web;
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

    public bool IsAlreadySaved { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public TripTimelineDisplayModel? GetTimelineDisplayModel()
    {
        if (TripDetails is null)
        {
            return null;
        }

        var shareLink = TripDetails.ShareLink;

        return new TripTimelineDisplayModel(
            TripDetails.Trip,
            Timeline,
            GetBookingForEntry,
            allowEntryEditing: false,
            allowBookingCreation: false,
            allowBookingViewing: true,
            showEmptyStateMessage: true,
            emptyStateMessage: "No shared plans yet.",
            showBookingConfirmations: shareLink?.ShowBookingConfirmations ?? true,
            showBookingMetadata: shareLink?.ShowBookingMetadata ?? true);
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

        // Check if already saved (for authenticated users)
        if (User.Identity?.IsAuthenticated == true)
        {
            var userId = User.GetObjectId();
            if (!string.IsNullOrWhiteSpace(userId))
            {
                var savedLinks = await _repository.GetSavedShareLinksAsync(userId, cancellationToken);
                IsAlreadySaved = savedLinks.Any(link => 
                    string.Equals(link.ShareCode, ShareCode, StringComparison.OrdinalIgnoreCase));
            }
        }

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

    public async Task<IActionResult> OnPostSaveTripAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ShareCode) || string.IsNullOrWhiteSpace(TripSlug))
        {
            return RedirectToPage();
        }

        // Get trip details to get the name
        var details = await _repository.GetTripByShareCodeAsync(ShareCode, cancellationToken);
        if (details is null)
        {
            ErrorMessage = "This share link has expired or is no longer available.";
            return Page();
        }

        if (User.Identity?.IsAuthenticated == true)
        {
            // Save to remote storage for authenticated users
            var userId = User.GetObjectId();
            if (!string.IsNullOrWhiteSpace(userId))
            {
                await _repository.SaveShareLinkAsync(userId, TripSlug, ShareCode, details.Trip.Name, cancellationToken);
                StatusMessage = "Trip saved! You can now find it on your trips page.";
            }
        }
        else
        {
            // For anonymous users, JavaScript will handle local storage
            StatusMessage = "Trip saved locally! Sign in to save it to your account.";
        }

        return RedirectToPage(new { tripSlug = TripSlug, shareCode = ShareCode });
    }

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
