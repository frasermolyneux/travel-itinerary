using System.Linq;
using Azure;
using Azure.Data.Tables;
using MX.TravelItinerary.Web.Data.Models;

namespace MX.TravelItinerary.Web.Data.TableStorage;

public sealed class TableItineraryRepository : IItineraryRepository
{
    private readonly ITableContext _tables;

    public TableItineraryRepository(ITableContext tables)
    {
        _tables = tables;
    }

    public async Task<IReadOnlyList<Trip>> GetTripsForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var trips = new List<Trip>();
        await foreach (var entity in _tables.Trips.QueryAsync<TableEntity>(
                   filter: CreatePartitionFilter(userId),
                   cancellationToken: cancellationToken))
        {
            trips.Add(TableEntityMapper.ToTrip(entity));
        }

        return trips;
    }

    public async Task<TripDetails?> GetTripAsync(string userId, string tripId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tripId);

        var tripEntity = await _tables.Trips.GetEntityIfExistsAsync<TableEntity>(userId, tripId, cancellationToken: cancellationToken);
        if (tripEntity.HasValue is false)
        {
            return null;
        }

        var trip = TableEntityMapper.ToTrip(tripEntity.Value);
        return await BuildTripDetailsAsync(trip, shareLink: null, cancellationToken);
    }

    public async Task<TripDetails?> GetTripByShareCodeAsync(string shareCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shareCode);

        TableEntity? shareEntity = null;
        await foreach (var entity in _tables.ShareLinks.QueryAsync<TableEntity>(
                   filter: CreateRowFilter(shareCode),
                   maxPerPage: 1,
                   cancellationToken: cancellationToken))
        {
            shareEntity = entity;
            break;
        }

        if (shareEntity is null)
        {
            return null;
        }

        var shareLink = TableEntityMapper.ToShareLink(shareEntity);
        if (string.IsNullOrWhiteSpace(shareLink.OwnerUserId))
        {
            throw new InvalidOperationException("Share link is missing OwnerUserId. Ensure the ShareLinks table stores that column.");
        }

        if (shareLink.ExpiresOn is { } expires && expires < DateTimeOffset.UtcNow)
        {
            return null;
        }

        var tripEntity = await _tables.Trips.GetEntityIfExistsAsync<TableEntity>(shareLink.OwnerUserId, shareLink.TripId, cancellationToken: cancellationToken);
        if (tripEntity.HasValue is false)
        {
            return null;
        }

        var trip = TableEntityMapper.ToTrip(tripEntity.Value);
        return await BuildTripDetailsAsync(trip, shareLink, cancellationToken);
    }

    private async Task<TripDetails> BuildTripDetailsAsync(Trip trip, ShareLink? shareLink, CancellationToken cancellationToken)
    {
        var segmentsTask = QueryTripSegmentsAsync(trip.TripId, cancellationToken);
        var entriesTask = QueryItineraryEntriesAsync(trip.TripId, cancellationToken);
        var bookingsTask = QueryBookingsAsync(trip.TripId, cancellationToken);

        await Task.WhenAll(segmentsTask, entriesTask, bookingsTask);

        var segments = await segmentsTask;
        var entries = await entriesTask;
        var bookings = await bookingsTask;

        if (shareLink?.IncludeCost == false)
        {
            entries = entries.Select(entry => entry with { CostEstimate = null, Currency = null }).ToList();
            bookings = bookings.Select(booking => booking with { Cost = null, Currency = null }).ToList();
        }

        if (shareLink?.MaskBookings == true)
        {
            bookings = new List<Booking>();
        }

        return new TripDetails(trip, segments, entries, bookings, shareLink);
    }

    private async Task<IReadOnlyList<TripSegment>> QueryTripSegmentsAsync(string tripId, CancellationToken cancellationToken)
    {
        var segments = new List<TripSegment>();
        await foreach (var entity in _tables.TripSegments.QueryAsync<TableEntity>(
                   filter: CreatePartitionFilter(tripId),
                   cancellationToken: cancellationToken))
        {
            segments.Add(TableEntityMapper.ToTripSegment(entity));
        }

        return segments;
    }

    private async Task<List<ItineraryEntry>> QueryItineraryEntriesAsync(string tripId, CancellationToken cancellationToken)
    {
        var entries = new List<ItineraryEntry>();
        await foreach (var entity in _tables.ItineraryEntries.QueryAsync<TableEntity>(
                   filter: CreatePartitionFilter(tripId),
                   cancellationToken: cancellationToken))
        {
            entries.Add(TableEntityMapper.ToItineraryEntry(entity));
        }

        return entries;
    }

    private async Task<List<Booking>> QueryBookingsAsync(string tripId, CancellationToken cancellationToken)
    {
        var bookings = new List<Booking>();
        await foreach (var entity in _tables.Bookings.QueryAsync<TableEntity>(
                   filter: CreatePartitionFilter(tripId),
                   cancellationToken: cancellationToken))
        {
            bookings.Add(TableEntityMapper.ToBooking(entity));
        }

        return bookings;
    }

    private static string CreatePartitionFilter(string partitionKey)
        => TableClient.CreateQueryFilter($"PartitionKey eq {partitionKey}");

    private static string CreateRowFilter(string rowKey)
        => TableClient.CreateQueryFilter($"RowKey eq {rowKey}");
}
