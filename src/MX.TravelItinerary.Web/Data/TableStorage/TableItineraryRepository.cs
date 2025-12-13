using System.Linq;
using Azure;
using Azure.Data.Tables;
using MX.TravelItinerary.Web.Data.Models;

namespace MX.TravelItinerary.Web.Data.TableStorage;

public sealed class TableItineraryRepository : IItineraryRepository
{
    private readonly ITableContext _tables;
    private const string DateFormat = "yyyy-MM-dd";

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

        var trip = TableEntityMapper.ToTrip(tripEntity.Value!);
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

        var trip = TableEntityMapper.ToTrip(tripEntity.Value!);
        return await BuildTripDetailsAsync(trip, shareLink, cancellationToken);
    }

    public async Task<Trip> CreateTripAsync(string userId, TripMutation mutation, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(mutation);

        var tripId = Guid.NewGuid().ToString("N");
        var entity = new TableEntity(userId, tripId);
        ApplyTripMutation(entity, mutation);

        await _tables.Trips.AddEntityAsync(entity, cancellationToken);
        return TableEntityMapper.ToTrip(entity);
    }

    public async Task<Trip?> UpdateTripAsync(string userId, string tripId, TripMutation mutation, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tripId);
        ArgumentNullException.ThrowIfNull(mutation);

        var existing = await _tables.Trips.GetEntityIfExistsAsync<TableEntity>(userId, tripId, cancellationToken: cancellationToken);
        if (existing.HasValue is false)
        {
            return null;
        }

        var entity = existing.Value;
        if (entity is null)
        {
            return null;
        }
        ApplyTripMutation(entity, mutation);
        await _tables.Trips.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace, cancellationToken);

        return TableEntityMapper.ToTrip(entity);
    }

    public async Task<bool> DeleteTripAsync(string userId, string tripId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tripId);

        try
        {
            await _tables.Trips.DeleteEntityAsync(userId, tripId, cancellationToken: cancellationToken);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
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

    private static void ApplyTripMutation(TableEntity entity, TripMutation mutation)
    {
        entity["Name"] = mutation.Name;
        entity["Slug"] = mutation.Slug;

        if (mutation.StartDate is not null)
        {
            entity["StartDate"] = mutation.StartDate.Value.ToString(DateFormat);
        }
        else
        {
            RemoveIfExists(entity, "StartDate");
        }

        if (mutation.EndDate is not null)
        {
            entity["EndDate"] = mutation.EndDate.Value.ToString(DateFormat);
        }
        else
        {
            RemoveIfExists(entity, "EndDate");
        }

        SetOrRemove(entity, "HomeTimeZone", mutation.HomeTimeZone);
        SetOrRemove(entity, "DefaultCurrency", mutation.DefaultCurrency);
    }

    private static void SetOrRemove(TableEntity entity, string propertyName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            RemoveIfExists(entity, propertyName);
        }
        else
        {
            entity[propertyName] = value;
        }
    }

    private static void RemoveIfExists(TableEntity entity, string propertyName)
    {
        if (entity.ContainsKey(propertyName))
        {
            entity.Remove(propertyName);
        }
    }
}
