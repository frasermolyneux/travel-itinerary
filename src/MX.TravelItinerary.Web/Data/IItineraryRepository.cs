using MX.TravelItinerary.Web.Data.Models;

namespace MX.TravelItinerary.Web.Data;

public interface IItineraryRepository
{
    Task<IReadOnlyList<Trip>> GetTripsForUserAsync(string userId, CancellationToken cancellationToken = default);

    Task<TripDetails?> GetTripAsync(string userId, string tripId, CancellationToken cancellationToken = default);

    Task<TripDetails?> GetTripByShareCodeAsync(string shareCode, CancellationToken cancellationToken = default);
}
