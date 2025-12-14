using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Microsoft.ApplicationInsights;
using MX.TravelItinerary.Web.Data.Models;

namespace MX.TravelItinerary.Web.Data.TableStorage;

public sealed class TableItineraryRepository : IItineraryRepository
{
    private readonly ITableContext _tables;
    private readonly TelemetryClient _telemetry;
    private const string DateFormat = "yyyy-MM-dd";
    private const int SortOrderIncrement = 10;

    public TableItineraryRepository(ITableContext tables, TelemetryClient telemetry)
    {
        _tables = tables;
        _telemetry = telemetry;
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

    public async Task<TripDetails?> GetTripBySlugAsync(string userId, string slug, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        var rawSlug = slug.Trim();
        var candidates = new List<string> { rawSlug };
        var normalized = rawSlug.ToLowerInvariant();
        if (!string.Equals(rawSlug, normalized, StringComparison.Ordinal))
        {
            candidates.Add(normalized);
        }

        foreach (var candidate in candidates)
        {
            var filter = TableClient.CreateQueryFilter($"PartitionKey eq {userId} and Slug eq {candidate}");

            await foreach (var entity in _tables.Trips.QueryAsync<TableEntity>(
                       filter: filter,
                       maxPerPage: 1,
                       cancellationToken: cancellationToken))
            {
                var trip = TableEntityMapper.ToTrip(entity);
                return await BuildTripDetailsAsync(trip, shareLink: null, cancellationToken);
            }
        }

        return null;
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
            TrackEvent("ShareLinkAccessRejected", properties =>
            {
                properties["ShareCode"] = shareCode;
                properties["Reason"] = "NotFound";
            });
            return null;
        }

        var shareLink = TableEntityMapper.ToShareLink(shareEntity);
        if (string.IsNullOrWhiteSpace(shareLink.OwnerUserId))
        {
            throw new InvalidOperationException("Share link is missing OwnerUserId. Ensure the ShareLinks table stores that column.");
        }

        if (shareLink.ExpiresOn is { } expires && expires < DateTimeOffset.UtcNow)
        {
            TrackEvent("ShareLinkAccessRejected", properties =>
            {
                properties["ShareCode"] = shareCode;
                properties["Reason"] = "Expired";
                properties["ExpiresOnUtc"] = FormatDateTimeOffset(expires);
            });
            return null;
        }

        var tripEntity = await _tables.Trips.GetEntityIfExistsAsync<TableEntity>(shareLink.OwnerUserId, shareLink.TripId, cancellationToken: cancellationToken);
        if (tripEntity.HasValue is false)
        {
            TrackEvent("ShareLinkAccessRejected", properties =>
            {
                properties["ShareCode"] = shareCode;
                properties["Reason"] = "TripMissing";
                properties["TripId"] = shareLink.TripId;
            });
            return null;
        }

        var trip = TableEntityMapper.ToTrip(tripEntity.Value!);
        TrackEvent("ShareLinkAccessGranted", properties =>
        {
            properties["ShareCode"] = shareCode;
            properties["TripId"] = shareLink.TripId;
            properties["OwnerUserId"] = shareLink.OwnerUserId;
            properties["MaskBookings"] = FormatBool(shareLink.MaskBookings);
            properties["IncludeCost"] = FormatBool(shareLink.IncludeCost);
            properties["ExpiresOnUtc"] = FormatDateTimeOffset(shareLink.ExpiresOn);
        });
        return await BuildTripDetailsAsync(trip, shareLink, cancellationToken);
    }

    public async Task<IReadOnlyList<ShareLink>> GetShareLinksAsync(string userId, string tripId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tripId);

        await EnsureTripOwnershipAsync(userId, tripId, cancellationToken);

        var results = new List<ShareLink>();
        await foreach (var entity in _tables.ShareLinks.QueryAsync<TableEntity>(
                   filter: CreatePartitionFilter(tripId),
                   cancellationToken: cancellationToken))
        {
            results.Add(TableEntityMapper.ToShareLink(entity));
        }

        return results
            .OrderByDescending(link => link.CreatedOn)
            .ThenBy(link => link.ShareCode)
            .ToList();
    }

    public async Task<ShareLink> CreateShareLinkAsync(string userId, string tripId, ShareLinkMutation mutation, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tripId);
        ArgumentNullException.ThrowIfNull(mutation);

        await EnsureTripOwnershipAsync(userId, tripId, cancellationToken);

        const int maxAttempts = 5;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var code = GenerateShareCode();
            var entity = new TableEntity(tripId, code)
            {
                ["OwnerUserId"] = userId,
                ["CreatedOn"] = DateTimeOffset.UtcNow,
                ["CreatedBy"] = userId
            };

            ApplyShareLinkMutation(entity, mutation);

            try
            {
                await _tables.ShareLinks.AddEntityAsync(entity, cancellationToken);
                var shareLink = TableEntityMapper.ToShareLink(entity);
                TrackEvent("ShareLinkCreated", properties =>
                {
                    properties["UserId"] = userId;
                    properties["TripId"] = tripId;
                    properties["ShareCode"] = shareLink.ShareCode;
                    properties["MaskBookings"] = FormatBool(shareLink.MaskBookings);
                    properties["IncludeCost"] = FormatBool(shareLink.IncludeCost);
                    properties["ExpiresOnUtc"] = FormatDateTimeOffset(shareLink.ExpiresOn);
                });
                return shareLink;
            }
            catch (RequestFailedException ex) when (ex.Status == 409)
            {
                // Retry with a new share code.
            }
        }

        throw new InvalidOperationException("Unable to create a unique share link. Please try again.");
    }

    public async Task<ShareLink?> UpdateShareLinkAsync(string userId, string tripId, string shareCode, ShareLinkMutation mutation, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tripId);
        ArgumentException.ThrowIfNullOrWhiteSpace(shareCode);
        ArgumentNullException.ThrowIfNull(mutation);

        await EnsureTripOwnershipAsync(userId, tripId, cancellationToken);

        var existing = await _tables.ShareLinks.GetEntityIfExistsAsync<TableEntity>(tripId, shareCode, cancellationToken: cancellationToken);
        if (existing.HasValue is false)
        {
            return null;
        }

        var entity = existing.Value;
        if (entity is null)
        {
            return null;
        }

        ApplyShareLinkMutation(entity, mutation);
        await _tables.ShareLinks.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace, cancellationToken);

        var shareLink = TableEntityMapper.ToShareLink(entity);
        TrackEvent("ShareLinkUpdated", properties =>
        {
            properties["UserId"] = userId;
            properties["TripId"] = tripId;
            properties["ShareCode"] = shareCode;
            properties["MaskBookings"] = FormatBool(shareLink.MaskBookings);
            properties["IncludeCost"] = FormatBool(shareLink.IncludeCost);
            properties["ExpiresOnUtc"] = FormatDateTimeOffset(shareLink.ExpiresOn);
        });

        return shareLink;
    }

    public async Task<bool> DeleteShareLinkAsync(string userId, string tripId, string shareCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tripId);
        ArgumentException.ThrowIfNullOrWhiteSpace(shareCode);

        await EnsureTripOwnershipAsync(userId, tripId, cancellationToken);

        try
        {
            await _tables.ShareLinks.DeleteEntityAsync(tripId, shareCode, cancellationToken: cancellationToken);
            TrackEvent("ShareLinkDeleted", properties =>
            {
                properties["UserId"] = userId;
                properties["TripId"] = tripId;
                properties["ShareCode"] = shareCode;
            });
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }

    public async Task<Trip> CreateTripAsync(string userId, TripMutation mutation, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(mutation);

        var tripId = Guid.NewGuid().ToString("N");
        var entity = new TableEntity(userId, tripId);
        ApplyTripMutation(entity, mutation);

        await _tables.Trips.AddEntityAsync(entity, cancellationToken);
        var trip = TableEntityMapper.ToTrip(entity);
        TrackEvent("TripCreated", properties =>
        {
            properties["UserId"] = userId;
            properties["TripId"] = trip.TripId;
            properties["HasSlug"] = (!string.IsNullOrWhiteSpace(trip.Slug)).ToString().ToLowerInvariant();
        });
        return trip;
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

        var trip = TableEntityMapper.ToTrip(entity);
        TrackEvent("TripUpdated", properties =>
        {
            properties["UserId"] = userId;
            properties["TripId"] = trip.TripId;
        });

        return trip;
    }

    public async Task<bool> DeleteTripAsync(string userId, string tripId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tripId);

        try
        {
            await _tables.Trips.DeleteEntityAsync(userId, tripId, cancellationToken: cancellationToken);
            TrackEvent("TripDeleted", properties =>
            {
                properties["UserId"] = userId;
                properties["TripId"] = tripId;
            });
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
        var entryMutation = mutation;
        var sortOrder = await GetSortOrderForMutationAsync(tripId, existingEntity: null, mutation, cancellationToken);
        if (sortOrder.HasValue)
        {
            entryMutation = entryMutation with { SortOrder = sortOrder };
        }

        ApplyItineraryEntryMutation(entity, entryMutation);

        await _tables.ItineraryEntries.AddEntityAsync(entity, cancellationToken);
        var entry = TableEntityMapper.ToItineraryEntry(entity);
        TrackEvent("ItineraryEntryCreated", properties =>
        {
            properties["UserId"] = userId;
            properties["TripId"] = tripId;
            properties["EntryId"] = entry.EntryId;
            properties["ItemType"] = entry.ItemType.ToString();
            properties["IsMultiDay"] = FormatBool(entry.IsMultiDay);
        });
        return entry;
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
        var entryMutation = mutation;
        var sortOrder = await GetSortOrderForMutationAsync(tripId, entity, mutation, cancellationToken);
        if (sortOrder.HasValue)
        {
            entryMutation = entryMutation with { SortOrder = sortOrder };
        }

        ApplyItineraryEntryMutation(entity, entryMutation);
        await _tables.ItineraryEntries.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace, cancellationToken);

        var entry = TableEntityMapper.ToItineraryEntry(entity);
        TrackEvent("ItineraryEntryUpdated", properties =>
        {
            properties["UserId"] = userId;
            properties["TripId"] = tripId;
            properties["EntryId"] = entry.EntryId;
            properties["ItemType"] = entry.ItemType.ToString();
            properties["IsMultiDay"] = FormatBool(entry.IsMultiDay);
        });

        return entry;
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
            TrackEvent("ItineraryEntryDeleted", properties =>
            {
                properties["UserId"] = userId;
                properties["TripId"] = tripId;
                properties["EntryId"] = entryId;
            });
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }

    public async Task ReorderItineraryEntriesAsync(string userId, string tripId, DateOnly date, IReadOnlyList<string> orderedEntryIds, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tripId);
        ArgumentNullException.ThrowIfNull(orderedEntryIds);

        await EnsureTripOwnershipAsync(userId, tripId, cancellationToken);

        var dateText = date.ToString(DateFormat);
        var filter = TableClient.CreateQueryFilter($"PartitionKey eq {tripId} and Date eq {dateText}");
        var comparer = StringComparer.OrdinalIgnoreCase;
        var entries = new Dictionary<string, TableEntity>(comparer);

        await foreach (var entity in _tables.ItineraryEntries.QueryAsync<TableEntity>(
                   filter: filter,
                   cancellationToken: cancellationToken))
        {
            if (entity.GetBoolean("IsMultiDay", false))
            {
                continue;
            }

            entries[entity.RowKey] = entity;
        }

        if (entries.Count == 0)
        {
            return;
        }

        var normalizedOrder = new List<string>(entries.Count);
        var seen = new HashSet<string>(comparer);

        foreach (var entryId in orderedEntryIds)
        {
            if (string.IsNullOrWhiteSpace(entryId))
            {
                continue;
            }

            var trimmed = entryId.Trim();
            if (entries.ContainsKey(trimmed) && seen.Add(trimmed))
            {
                normalizedOrder.Add(trimmed);
            }
        }

        foreach (var entryId in entries.Keys)
        {
            if (seen.Add(entryId))
            {
                normalizedOrder.Add(entryId);
            }
        }

        var updates = new List<TableEntity>();
        for (var index = 0; index < normalizedOrder.Count; index++)
        {
            var entryId = normalizedOrder[index];
            if (!entries.TryGetValue(entryId, out var entity))
            {
                continue;
            }

            var desiredOrder = (index + 1) * SortOrderIncrement;
            var currentOrder = entity.GetInt32("SortOrder");
            if (currentOrder == desiredOrder)
            {
                continue;
            }

            entity["SortOrder"] = desiredOrder;
            updates.Add(entity);
        }

        foreach (var entity in updates)
        {
            await _tables.ItineraryEntries.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Merge, cancellationToken);
        }

        TrackEvent("TimelineEntriesReordered", properties =>
        {
            properties["UserId"] = userId;
            properties["TripId"] = tripId;
            properties["Date"] = date.ToString(DateFormat, CultureInfo.InvariantCulture);
            properties["EntryCount"] = normalizedOrder.Count.ToString(CultureInfo.InvariantCulture);
            properties["UpdatesApplied"] = updates.Count.ToString(CultureInfo.InvariantCulture);
        });
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
        var booking = TableEntityMapper.ToBooking(entity);
        TrackEvent("BookingCreated", properties =>
        {
            properties["UserId"] = userId;
            properties["TripId"] = tripId;
            properties["BookingId"] = booking.BookingId;
            properties["EntryId"] = booking.EntryId;
            properties["ItemType"] = booking.ItemType.ToString();
        });
        return booking;
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

        var booking = TableEntityMapper.ToBooking(entity);
        TrackEvent("BookingUpdated", properties =>
        {
            properties["UserId"] = userId;
            properties["TripId"] = tripId;
            properties["BookingId"] = booking.BookingId;
            properties["EntryId"] = booking.EntryId;
            properties["ItemType"] = booking.ItemType.ToString();
        });

        return booking;
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
            TrackEvent("BookingDeleted", properties =>
            {
                properties["UserId"] = userId;
                properties["TripId"] = tripId;
                properties["BookingId"] = bookingId;
            });
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

    private async Task<int?> GetSortOrderForMutationAsync(string tripId, TableEntity? existingEntity, ItineraryEntryMutation mutation, CancellationToken cancellationToken)
    {
        if (!IsSingleDayEntry(mutation))
        {
            return null;
        }

        var targetDate = mutation.Date!.Value;

        if (existingEntity is not null)
        {
            var existingDate = existingEntity.GetDateOnly("Date");
            var existingSort = existingEntity.GetInt32("SortOrder");
            var wasMultiDay = existingEntity.GetBoolean("IsMultiDay", false);

            if (!wasMultiDay && existingDate == targetDate)
            {
                return existingSort;
            }
        }

        return await GetNextSortOrderAsync(tripId, targetDate, cancellationToken);
    }

    private static bool IsSingleDayEntry(ItineraryEntryMutation mutation)
        => !mutation.IsMultiDay
           && mutation.Date is not null
           && (mutation.EndDate is null || mutation.EndDate == mutation.Date);

    private async Task<int> GetNextSortOrderAsync(string tripId, DateOnly date, CancellationToken cancellationToken)
    {
        var dateText = date.ToString(DateFormat);
        var filter = TableClient.CreateQueryFilter($"PartitionKey eq {tripId} and Date eq {dateText} and IsMultiDay eq false");
        var maxOrder = 0;
        var count = 0;

        await foreach (var entity in _tables.ItineraryEntries.QueryAsync<TableEntity>(
                   filter: filter,
                   cancellationToken: cancellationToken))
        {
            count++;
            var existingOrder = entity.GetInt32("SortOrder");
            if (existingOrder.HasValue)
            {
                maxOrder = Math.Max(maxOrder, existingOrder.Value);
            }
        }

        var baseline = maxOrder == 0 ? count * SortOrderIncrement : maxOrder;
        return baseline + SortOrderIncrement;
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

        if (mutation.SortOrder.HasValue)
        {
            entity["SortOrder"] = mutation.SortOrder.Value;
        }
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
        var cancellationByDate = mutation.CancellationByDate?.ToString(DateFormat, CultureInfo.InvariantCulture);
        SetOrRemove(entity, "CancellationByDate", cancellationByDate);
        SetOrRemove(entity, "ConfirmationDetails", mutation.ConfirmationDetails);
        SetOrRemove(entity, "ConfirmationUrl", mutation.ConfirmationUrl?.ToString());
        SetBookingMetadata(entity, mutation.Metadata);
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

    private static void SetBookingMetadata(TableEntity entity, BookingMetadata? metadata)
    {
        if (metadata is null || metadata.HasContent is false)
        {
            RemoveIfExists(entity, "BookingMetadataJson");
            return;
        }

        entity["BookingMetadataJson"] = JsonSerializer.Serialize(metadata, TableStorageJsonOptions.Metadata);
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

        if (shareLink?.ShowBookingConfirmations == false)
        {
            bookings = bookings
                .Select(booking => booking with
                {
                    Vendor = null,
                    Reference = null,
                    Cost = null,
                    Currency = null,
                    IsRefundable = null,
                    IsPaid = null,
                    CancellationPolicy = null,
                    CancellationByDate = null,
                    ConfirmationDetails = null,
                    ConfirmationUrl = null
                })
                .ToList();
        }

        if (shareLink?.ShowBookingMetadata == false)
        {
            bookings = bookings
                .Select(booking => booking with { Metadata = null })
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

    private static void ApplyShareLinkMutation(TableEntity entity, ShareLinkMutation mutation)
    {
        SetOrRemove(entity, "ExpiresOn", mutation.ExpiresOn);
        entity["MaskBookings"] = mutation.MaskBookings;
        entity["IncludeCost"] = mutation.IncludeCost;
        entity["ShowBookingConfirmations"] = mutation.ShowBookingConfirmations;
        entity["ShowBookingMetadata"] = mutation.ShowBookingMetadata;
        SetOrRemove(entity, "Notes", mutation.Notes);
    }

    private static string GenerateShareCode()
    {
        Span<char> buffer = stackalloc char[8];
        for (var i = 0; i < buffer.Length; i++)
        {
            var index = RandomNumberGenerator.GetInt32(ShareCodeAlphabet.Length);
            buffer[i] = ShareCodeAlphabet[index];
        }

        return new string(buffer);
    }

    private static void RemoveIfExists(TableEntity entity, string propertyName)
    {
        if (entity.ContainsKey(propertyName))
        {
            entity.Remove(propertyName);
        }
    }

    private static readonly char[] ShareCodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();

    private void TrackEvent(string eventName, Action<Dictionary<string, string?>> configure)
    {
        if (string.IsNullOrWhiteSpace(eventName))
        {
            return;
        }

        var properties = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        configure(properties);
        _telemetry.TrackEvent(eventName, properties);
    }

    private static string FormatBool(bool value) => value ? "true" : "false";

    private static string? FormatDateTimeOffset(DateTimeOffset? value)
        => value?.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
}
