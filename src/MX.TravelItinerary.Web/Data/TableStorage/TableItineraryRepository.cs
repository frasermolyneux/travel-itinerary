using System;
using System.Collections.Generic;
using System.Linq;
using Azure;
using Azure.Data.Tables;
using MX.TravelItinerary.Web.Data.Models;
using System.Text.Json;

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

    public async Task<ItineraryEntry> CreateItineraryEntryAsync(string userId, string tripId, ItineraryEntryMutation mutation, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tripId);
        ArgumentNullException.ThrowIfNull(mutation);

        await EnsureTripOwnershipAsync(userId, tripId, cancellationToken);

        var entryId = Guid.NewGuid().ToString("N");
        var entity = new TableEntity(tripId, entryId);
        ApplyItineraryEntryMutation(entity, mutation);

        await _tables.ItineraryEntries.AddEntityAsync(entity, cancellationToken);
        return TableEntityMapper.ToItineraryEntry(entity);
    }

    public async Task<ItineraryEntry?> UpdateItineraryEntryAsync(string userId, string tripId, string entryId, ItineraryEntryMutation mutation, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tripId);
        ArgumentException.ThrowIfNullOrWhiteSpace(entryId);
        ArgumentNullException.ThrowIfNull(mutation);

        await EnsureTripOwnershipAsync(userId, tripId, cancellationToken);

        var existing = await _tables.ItineraryEntries.GetEntityIfExistsAsync<TableEntity>(tripId, entryId, cancellationToken: cancellationToken);
        if (existing.HasValue is false)
        {
            return null;
        }

        var entity = existing.Value;
        if (entity is null)
        {
            return null;
        }
        ApplyItineraryEntryMutation(entity, mutation);
        await _tables.ItineraryEntries.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace, cancellationToken);

        return TableEntityMapper.ToItineraryEntry(entity);
    }

    public async Task<bool> DeleteItineraryEntryAsync(string userId, string tripId, string entryId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tripId);
        ArgumentException.ThrowIfNullOrWhiteSpace(entryId);

        await EnsureTripOwnershipAsync(userId, tripId, cancellationToken);

        try
        {
            await _tables.ItineraryEntries.DeleteEntityAsync(tripId, entryId, cancellationToken: cancellationToken);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }

    public async Task<Booking> CreateBookingAsync(string userId, string tripId, BookingMutation mutation, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tripId);
        ArgumentNullException.ThrowIfNull(mutation);

        await EnsureTripOwnershipAsync(userId, tripId, cancellationToken);
        var itemType = await ResolveBookingItemTypeAsync(tripId, mutation, cancellationToken);
        if (await BookingExistsForLinkAsync(tripId, mutation, excludeBookingId: null, cancellationToken))
        {
            throw new InvalidOperationException("The selected item already has a booking linked.");
        }

        mutation = mutation with { ItemType = itemType };
        var bookingId = Guid.NewGuid().ToString("N");
        var entity = new TableEntity(tripId, bookingId);
        ApplyBookingMutation(entity, mutation);

        await _tables.Bookings.AddEntityAsync(entity, cancellationToken);
        return TableEntityMapper.ToBooking(entity);
    }

    public async Task<Booking?> UpdateBookingAsync(string userId, string tripId, string bookingId, BookingMutation mutation, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tripId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bookingId);
        ArgumentNullException.ThrowIfNull(mutation);

        await EnsureTripOwnershipAsync(userId, tripId, cancellationToken);
        var itemType = await ResolveBookingItemTypeAsync(tripId, mutation, cancellationToken);
        if (await BookingExistsForLinkAsync(tripId, mutation, bookingId, cancellationToken))
        {
            throw new InvalidOperationException("The selected item already has a booking linked.");
        }

        var existing = await _tables.Bookings.GetEntityIfExistsAsync<TableEntity>(tripId, bookingId, cancellationToken: cancellationToken);
        if (existing.HasValue is false)
        {
            return null;
        }

        var entity = existing.Value;
        if (entity is null)
        {
            return null;
        }

        mutation = mutation with { ItemType = itemType };
        ApplyBookingMutation(entity, mutation);
        await _tables.Bookings.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace, cancellationToken);

        return TableEntityMapper.ToBooking(entity);
    }

    public async Task<bool> DeleteBookingAsync(string userId, string tripId, string bookingId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tripId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bookingId);

        await EnsureTripOwnershipAsync(userId, tripId, cancellationToken);

        try
        {
            await _tables.Bookings.DeleteEntityAsync(tripId, bookingId, cancellationToken: cancellationToken);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }

    private async Task EnsureTripOwnershipAsync(string userId, string tripId, CancellationToken cancellationToken)
    {
        var tripEntity = await _tables.Trips.GetEntityIfExistsAsync<TableEntity>(userId, tripId, cancellationToken: cancellationToken);
        if (tripEntity.HasValue is false)
        {
            throw new InvalidOperationException($"Trip '{tripId}' is not available for the current user.");
        }
    }

    private async Task<TimelineItemType> ResolveBookingItemTypeAsync(string tripId, BookingMutation mutation, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mutation.EntryId))
        {
            throw new InvalidOperationException("Booking must be linked to a timeline entry.");
        }

        var entry = await _tables.ItineraryEntries.GetEntityIfExistsAsync<TableEntity>(tripId, mutation.EntryId!, cancellationToken: cancellationToken);
        if (entry.HasValue is false)
        {
            throw new InvalidOperationException("The selected timeline entry no longer exists.");
        }

        var itemType = entry.Value!.GetString("ItemType");
        return itemType.ToTimelineItemType();
    }

    private async Task<bool> BookingExistsForLinkAsync(string tripId, BookingMutation mutation, string? excludeBookingId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mutation.EntryId))
        {
            return false;
        }

        var filter = TableClient.CreateQueryFilter($"PartitionKey eq {tripId} and EntryId eq {mutation.EntryId}");

        await foreach (var entity in _tables.Bookings.QueryAsync<TableEntity>(filter: filter, maxPerPage: 5, cancellationToken: cancellationToken))
        {
            if (!string.IsNullOrWhiteSpace(excludeBookingId) && entity.RowKey.Equals(excludeBookingId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static void ApplyItineraryEntryMutation(TableEntity entity, ItineraryEntryMutation mutation)
    {
        var title = string.IsNullOrWhiteSpace(mutation.Title) ? "Untitled entry" : mutation.Title.Trim();
        entity["Title"] = title;

        if (mutation.Date is { } date)
        {
            entity["Date"] = date.ToString(DateFormat);
        }
        else
        {
            RemoveIfExists(entity, "Date");
        }

        if (mutation.EndDate is { } endDate)
        {
            entity["EndDate"] = endDate.ToString(DateFormat);
        }
        else
        {
            RemoveIfExists(entity, "EndDate");
        }

        entity["IsMultiDay"] = mutation.IsMultiDay;
        entity["ItemType"] = mutation.ItemType.ToStorageValue();
        SetOrRemove(entity, "Details", mutation.Details);
        SetOrRemove(entity, "Tags", mutation.Tags);

        SetItineraryLocation(entity, mutation.Location);
        SetMetadata(entity, mutation.Metadata);
    }

    private static void ApplyBookingMutation(TableEntity entity, BookingMutation mutation)
    {
        SetOrRemove(entity, "EntryId", mutation.EntryId);
        entity["ItemType"] = mutation.ItemType.ToStorageValue();
        SetOrRemove(entity, "Vendor", mutation.Vendor);
        SetOrRemove(entity, "Reference", mutation.Reference);
        SetOrRemove(entity, "Cost", mutation.Cost);
        SetOrRemove(entity, "Currency", NormalizeCurrency(mutation.Currency));
        SetOrRemove(entity, "IsRefundable", mutation.IsRefundable);
        SetOrRemove(entity, "IsPaid", mutation.IsPaid);
        SetOrRemove(entity, "CancellationPolicy", mutation.CancellationPolicy);
        SetOrRemove(entity, "ConfirmationDetails", mutation.ConfirmationDetails);
        SetOrRemove(entity, "ConfirmationUrl", mutation.ConfirmationUrl?.ToString());
    }

    private static void SetItineraryLocation(TableEntity entity, LocationInfo? location)
    {
        if (location is null)
        {
            ClearItineraryLocation(entity);
            return;
        }

        SetOrRemove(entity, "LocationName", location.Label);
        SetOrRemove(entity, "LocationUrl", location.Url);
        SetOrRemove(entity, "Latitude", location.Latitude);
        SetOrRemove(entity, "Longitude", location.Longitude);
    }

    private static void ClearItineraryLocation(TableEntity entity)
    {
        RemoveIfExists(entity, "LocationName");
        RemoveIfExists(entity, "LocationUrl");
        RemoveIfExists(entity, "Latitude");
        RemoveIfExists(entity, "Longitude");
    }

    private static void SetMetadata(TableEntity entity, TravelMetadata? metadata)
    {
        if (metadata is null || metadata.HasContent is false)
        {
            RemoveIfExists(entity, "MetadataJson");
            return;
        }

        entity["MetadataJson"] = JsonSerializer.Serialize(metadata, TableStorageJsonOptions.Metadata);
    }

    private static string? NormalizeCurrency(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private async Task<TripDetails> BuildTripDetailsAsync(Trip trip, ShareLink? shareLink, CancellationToken cancellationToken)
    {
        var entriesTask = QueryItineraryEntriesAsync(trip.TripId, cancellationToken);
        var bookingsTask = QueryBookingsAsync(trip.TripId, cancellationToken);

        await Task.WhenAll(entriesTask, bookingsTask);

        var entries = await entriesTask;
        var bookings = await bookingsTask;

        if (shareLink?.IncludeCost == false)
        {
            bookings = bookings
                .Select(booking => booking with { Cost = null, Currency = null, IsPaid = null, ConfirmationUrl = null })
                .ToList();
        }

        if (shareLink?.MaskBookings == true)
        {
            bookings = new List<Booking>();
        }

        return new TripDetails(trip, entries, bookings, shareLink);
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
            entity[propertyName] = value.Trim();
        }
    }

    private static void SetOrRemove(TableEntity entity, string propertyName, DateTimeOffset? value)
    {
        if (value is null)
        {
            RemoveIfExists(entity, propertyName);
        }
        else
        {
            entity[propertyName] = value.Value;
        }
    }

    private static void SetOrRemove(TableEntity entity, string propertyName, decimal? value)
    {
        if (value is null)
        {
            RemoveIfExists(entity, propertyName);
        }
        else
        {
            entity[propertyName] = value.Value;
        }
    }

    private static void SetOrRemove(TableEntity entity, string propertyName, double? value)
    {
        if (value is null)
        {
            RemoveIfExists(entity, propertyName);
        }
        else
        {
            entity[propertyName] = value.Value;
        }
    }

    private static void SetOrRemove(TableEntity entity, string propertyName, bool? value)
    {
        if (value is null)
        {
            RemoveIfExists(entity, propertyName);
        }
        else
        {
            entity[propertyName] = value.Value;
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
