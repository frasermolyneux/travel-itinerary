using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Identity.Web;
using MX.TravelItinerary.Web.Data;
using MX.TravelItinerary.Web.Data.Models;

namespace MX.TravelItinerary.Web.Pages;

public class IndexModel : PageModel
{
    private readonly IItineraryRepository _repository;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(IItineraryRepository repository, ILogger<IndexModel> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public IReadOnlyList<TripWithDetails> CurrentAndUpcomingTrips { get; private set; } = [];
    public IReadOnlyList<TripWithDetails> PastTrips { get; private set; } = [];
    public IReadOnlyList<TodayActivity> TodayActivities { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return;
        }

        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        var userEmail = GetUserEmail();
        var trips = await _repository.GetTripsForUserAsync(userId, userEmail, cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var tripsWithDetails = new List<TripWithDetails>();

        foreach (var trip in trips)
        {
            var tripDetails = await _repository.GetTripAsync(userId, userEmail, trip.TripId, cancellationToken);
            if (tripDetails is not null)
            {
                tripsWithDetails.Add(new TripWithDetails(trip, tripDetails.Entries, tripDetails.Bookings));
            }
        }

        // Separate trips into current/upcoming and past
        var currentAndUpcoming = new List<TripWithDetails>();
        var past = new List<TripWithDetails>();

        foreach (var tripWithDetails in tripsWithDetails)
        {
            var trip = tripWithDetails.Trip;
            var isExpired = trip.EndDate.HasValue && trip.EndDate.Value < today;

            if (isExpired)
            {
                past.Add(tripWithDetails);
            }
            else
            {
                currentAndUpcoming.Add(tripWithDetails);
            }
        }

        // Sort current/upcoming trips by start date (nulls last)
        CurrentAndUpcomingTrips = currentAndUpcoming
            .OrderBy(t => t.Trip.StartDate ?? DateOnly.MaxValue)
            .ToList();

        // Sort past trips by end date descending (most recent first)
        PastTrips = past
            .OrderByDescending(t => t.Trip.EndDate ?? DateOnly.MinValue)
            .ToList();

        // Extract today's activities from trips currently in progress
        var todayActivities = new List<TodayActivity>();
        foreach (var tripWithDetails in CurrentAndUpcomingTrips)
        {
            var trip = tripWithDetails.Trip;
            var isInProgress = trip.StartDate <= today && (trip.EndDate == null || trip.EndDate >= today);

            if (isInProgress)
            {
                // Find entries for today
                var todayEntries = tripWithDetails.Entries
                    .Where(e => e.Date == today || (e.IsMultiDay && e.Date <= today && (e.EndDate == null || e.EndDate >= today)))
                    .OrderBy(e => e.SortOrder ?? 0)
                    .ToList();

                // Find bookings for entries happening today
                var todayBookings = tripWithDetails.Bookings
                    .Where(b => b.EntryId != null && todayEntries.Any(e => e.EntryId == b.EntryId))
                    .ToList();

                if (todayEntries.Count > 0 || todayBookings.Count > 0)
                {
                    todayActivities.Add(new TodayActivity(trip, todayEntries, todayBookings));
                }
            }
        }

        TodayActivities = todayActivities;
    }

    private string? GetUserId()
    {
        try
        {
            return User.GetObjectId();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve user object ID from claims");
            return null;
        }
    }

    private string? GetUserEmail()
    {
        var email = User.FindFirstValue("preferred_username") ?? User.FindFirstValue(ClaimTypes.Email);
        return string.IsNullOrWhiteSpace(email) ? null : email;
    }

    public sealed record TripWithDetails(
        Trip Trip,
        IReadOnlyList<ItineraryEntry> Entries,
        IReadOnlyList<Booking> Bookings);

    public sealed record TodayActivity(
        Trip Trip,
        IReadOnlyList<ItineraryEntry> Entries,
        IReadOnlyList<Booking> Bookings);
}
