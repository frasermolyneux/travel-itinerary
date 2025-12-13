using MX.TravelItinerary.Web.Data.Models;

namespace MX.TravelItinerary.Web.Data;

public interface IItineraryRepository
{
    Task<IReadOnlyList<Trip>> GetTripsForUserAsync(string userId, CancellationToken cancellationToken = default);

    Task<TripDetails?> GetTripAsync(string userId, string tripId, CancellationToken cancellationToken = default);

    Task<TripDetails?> GetTripByShareCodeAsync(string shareCode, CancellationToken cancellationToken = default);

    Task<Trip> CreateTripAsync(string userId, TripMutation mutation, CancellationToken cancellationToken = default);

    Task<Trip?> UpdateTripAsync(string userId, string tripId, TripMutation mutation, CancellationToken cancellationToken = default);

    Task<bool> DeleteTripAsync(string userId, string tripId, CancellationToken cancellationToken = default);
}
