using MX.TravelItinerary.Web.Data.Models;

namespace MX.TravelItinerary.Web.Data;

public interface IItineraryRepository
{
    Task<IReadOnlyList<Trip>> GetTripsForUserAsync(string userId, CancellationToken cancellationToken = default);

    Task<TripDetails?> GetTripAsync(string userId, string tripId, CancellationToken cancellationToken = default);

    Task<TripDetails?> GetTripBySlugAsync(string userId, string slug, CancellationToken cancellationToken = default);

    Task<TripDetails?> GetTripByShareCodeAsync(string shareCode, CancellationToken cancellationToken = default);

    Task<Trip> CreateTripAsync(string userId, TripMutation mutation, CancellationToken cancellationToken = default);

    Task<Trip?> UpdateTripAsync(string userId, string tripId, TripMutation mutation, CancellationToken cancellationToken = default);

    Task<bool> DeleteTripAsync(string userId, string tripId, CancellationToken cancellationToken = default);

    Task<ItineraryEntry> CreateItineraryEntryAsync(string userId, string tripId, ItineraryEntryMutation mutation, CancellationToken cancellationToken = default);

    Task<ItineraryEntry?> UpdateItineraryEntryAsync(string userId, string tripId, string entryId, ItineraryEntryMutation mutation, CancellationToken cancellationToken = default);

    Task<bool> DeleteItineraryEntryAsync(string userId, string tripId, string entryId, CancellationToken cancellationToken = default);

    Task<Booking> CreateBookingAsync(string userId, string tripId, BookingMutation mutation, CancellationToken cancellationToken = default);

    Task<Booking?> UpdateBookingAsync(string userId, string tripId, string bookingId, BookingMutation mutation, CancellationToken cancellationToken = default);

    Task<bool> DeleteBookingAsync(string userId, string tripId, string bookingId, CancellationToken cancellationToken = default);
}
