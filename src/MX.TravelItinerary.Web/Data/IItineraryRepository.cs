using System;
using MX.TravelItinerary.Web.Data.Models;

namespace MX.TravelItinerary.Web.Data;

public interface IItineraryRepository
{
    Task<IReadOnlyList<Trip>> GetTripsForUserAsync(string userId, string? userEmail, CancellationToken cancellationToken = default);

    Task<TripDetails?> GetTripAsync(string userId, string? userEmail, string tripId, CancellationToken cancellationToken = default);

    Task<TripDetails?> GetTripBySlugAsync(string userId, string? userEmail, string slug, CancellationToken cancellationToken = default);

    Task<TripDetails?> GetTripByShareCodeAsync(string shareCode, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TripAccess>> GetTripAccessListAsync(string userId, string tripId, CancellationToken cancellationToken = default);

    Task<TripAccess> GrantTripAccessAsync(string userId, string tripId, TripAccessMutation mutation, CancellationToken cancellationToken = default);

    Task<TripAccess?> UpdateTripAccessAsync(string userId, string tripId, string accessId, TripAccessMutation mutation, CancellationToken cancellationToken = default);

    Task<bool> RevokeTripAccessAsync(string userId, string tripId, string accessId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShareLink>> GetShareLinksAsync(string userId, string tripId, CancellationToken cancellationToken = default);

    Task<ShareLink> CreateShareLinkAsync(string userId, string tripId, ShareLinkMutation mutation, CancellationToken cancellationToken = default, string? shareCode = null);

    Task<ShareLink?> UpdateShareLinkAsync(string userId, string tripId, string shareCode, ShareLinkMutation mutation, CancellationToken cancellationToken = default);

    Task<bool> DeleteShareLinkAsync(string userId, string tripId, string shareCode, CancellationToken cancellationToken = default);

    Task<Trip> CreateTripAsync(string userId, TripMutation mutation, CancellationToken cancellationToken = default);

    Task<Trip?> UpdateTripAsync(string userId, string? userEmail, string tripId, TripMutation mutation, CancellationToken cancellationToken = default);

    Task<bool> DeleteTripAsync(string userId, string? userEmail, string tripId, CancellationToken cancellationToken = default);

    Task<ItineraryEntry> CreateItineraryEntryAsync(string userId, string? userEmail, string tripId, ItineraryEntryMutation mutation, CancellationToken cancellationToken = default);

    Task<ItineraryEntry?> UpdateItineraryEntryAsync(string userId, string? userEmail, string tripId, string entryId, ItineraryEntryMutation mutation, CancellationToken cancellationToken = default);

    Task<bool> DeleteItineraryEntryAsync(string userId, string? userEmail, string tripId, string entryId, CancellationToken cancellationToken = default);

    Task ReorderItineraryEntriesAsync(string userId, string? userEmail, string tripId, DateOnly date, IReadOnlyList<string> orderedEntryIds, CancellationToken cancellationToken = default);

    Task<Booking> CreateBookingAsync(string userId, string? userEmail, string tripId, BookingMutation mutation, CancellationToken cancellationToken = default);

    Task<Booking?> UpdateBookingAsync(string userId, string? userEmail, string tripId, string bookingId, BookingMutation mutation, CancellationToken cancellationToken = default);

    Task<bool> DeleteBookingAsync(string userId, string? userEmail, string tripId, string bookingId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SavedShareLink>> GetSavedShareLinksAsync(string userId, CancellationToken cancellationToken = default);

    Task<SavedShareLink> SaveShareLinkAsync(string userId, string tripSlug, string shareCode, string tripName, CancellationToken cancellationToken = default);

    Task<bool> DeleteSavedShareLinkAsync(string userId, string savedLinkId, CancellationToken cancellationToken = default);
}
