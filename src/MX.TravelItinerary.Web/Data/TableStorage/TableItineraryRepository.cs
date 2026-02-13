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

    private sealed record TripAccessContext(Trip Trip, TripPermission Permission, TableEntity? AccessEntity);

    public TableItineraryRepository(ITableContext tables, TelemetryClient telemetry)
    {
        _tables = tables;
        _telemetry = telemetry;
    }

    public async Task<IReadOnlyList<Trip>> GetTripsForUserAsync(string userId, string? userEmail, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var normalizedEmail = NormalizeEmail(userEmail);
        var trips = new Dictionary<string, Trip>(StringComparer.OrdinalIgnoreCase);

        await foreach (var entity in _tables.Trips.QueryAsync<TableEntity>(
                   filter: CreatePartitionFilter(userId),
                   cancellationToken: cancellationToken))
        {
            var trip = TableEntityMapper.ToTrip(entity);
            trips[trip.TripId] = trip;
        }

        var accessFilter = CreateAccessFilterForUser(userId, normalizedEmail);
        await foreach (var entity in _tables.TripAccess.QueryAsync<TableEntity>(
                   filter: accessFilter,
                   cancellationToken: cancellationToken))
        {
            var tripId = entity.PartitionKey;
            if (trips.ContainsKey(tripId))
            {
                continue;
            }

            var ownerUserId = entity.GetString("OwnerUserId");
            if (string.IsNullOrWhiteSpace(ownerUserId))
            {
                continue;
            }

            var tripEntity = await _tables.Trips.GetEntityIfExistsAsync<TableEntity>(ownerUserId, tripId, cancellationToken: cancellationToken);
            if (tripEntity.HasValue && tripEntity.Value != null)
            {
                trips[tripId] = TableEntityMapper.ToTrip(tripEntity.Value);
            }
        }

        return trips.Values
            .OrderBy(trip => trip.StartDate ?? DateOnly.FromDateTime(DateTime.UtcNow))
            .ThenBy(trip => trip.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<TripDetails?> GetTripAsync(string userId, string? userEmail, string tripId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tripId);

        var context = await GetTripContextAsync(userId, userEmail, tripId, cancellationToken);
        if (context is null)
        {
            return null;
        }

        return await BuildTripDetailsAsync(context.Trip, shareLink: null, context.Permission, cancellationToken);
    }

    public async Task<TripDetails?> GetTripBySlugAsync(string userId, string? userEmail, string slug, CancellationToken cancellationToken = default)
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

        var ownedTrip = await FindTripBySlugForUserAsync(userId, candidates, cancellationToken);
        if (ownedTrip is not null)
        {
            return await BuildTripDetailsAsync(ownedTrip, shareLink: null, TripPermission.Owner, cancellationToken);
        }

        var accessFilter = CreateAccessFilterForUser(userId, NormalizeEmail(userEmail));
        await foreach (var accessEntity in _tables.TripAccess.QueryAsync<TableEntity>(
                   filter: accessFilter,
                   cancellationToken: cancellationToken))
        {
            var tripId = accessEntity.PartitionKey;
            var ownerUserId = accessEntity.GetString("OwnerUserId");
            if (string.IsNullOrWhiteSpace(ownerUserId))
            {
                continue;
            }

            var tripCandidate = await FindTripBySlugForUserAsync(ownerUserId, candidates, cancellationToken, tripId);
            if (tripCandidate is null)
            {
                continue;
            }

            var permission = TripPermissionExtensions.FromStorage(accessEntity.GetString("Permission"));
            return await BuildTripDetailsAsync(tripCandidate, shareLink: null, permission, cancellationToken);
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
        return await BuildTripDetailsAsync(trip, shareLink, TripPermission.ReadOnly, cancellationToken);
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

    public async Task<IReadOnlyList<TripAccess>> GetTripAccessListAsync(string userId, string tripId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tripId);

        await EnsureTripOwnershipAsync(userId, tripId, cancellationToken);

        var results = new List<TripAccess>();
        await foreach (var entity in _tables.TripAccess.QueryAsync<TableEntity>(
                   filter: CreatePartitionFilter(tripId),
                   cancellationToken: cancellationToken))
        {
            results.Add(TableEntityMapper.ToTripAccess(entity));
        }

        return results
            .OrderBy(access => access.Permission == TripPermission.FullControl ? 0 : 1)
            .ThenBy(access => access.Email, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<TripAccess> GrantTripAccessAsync(string userId, string tripId, TripAccessMutation mutation, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tripId);
        ArgumentNullException.ThrowIfNull(mutation);

        await EnsureTripOwnershipAsync(userId, tripId, cancellationToken);

        var normalizedEmail = NormalizeEmail(mutation.Email);
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            throw new InvalidOperationException("Enter a valid email address.");
        }

        var accessId = normalizedEmail;
        var existing = await _tables.TripAccess.GetEntityIfExistsAsync<TableEntity>(tripId, accessId, cancellationToken: cancellationToken);
        var entity = (existing.HasValue && existing.Value != null) ? existing.Value : new TableEntity(tripId, accessId)
        {
            ["OwnerUserId"] = userId,
            ["InvitedByUserId"] = userId,
            ["InvitedOn"] = DateTimeOffset.UtcNow
        };

        entity["Email"] = mutation.Email.Trim();
        entity["NormalizedEmail"] = normalizedEmail;
        entity["Permission"] = mutation.Permission.ToStorageValue();

        await _tables.TripAccess.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);

        var access = TableEntityMapper.ToTripAccess(entity);
        TrackEvent("TripAccessGranted", properties =>
        {
            properties["UserId"] = userId;
            properties["TripId"] = tripId;
            properties["AccessId"] = access.AccessId;
            properties["Permission"] = access.Permission.ToString();
        });

        return access;
    }

    public async Task<TripAccess?> UpdateTripAccessAsync(string userId, string tripId, string accessId, TripAccessMutation mutation, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tripId);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessId);
        ArgumentNullException.ThrowIfNull(mutation);

        await EnsureTripOwnershipAsync(userId, tripId, cancellationToken);

        var normalizedEmail = NormalizeEmail(mutation.Email);
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            throw new InvalidOperationException("Enter a valid email address.");
        }

        var existing = await _tables.TripAccess.GetEntityIfExistsAsync<TableEntity>(tripId, accessId, cancellationToken: cancellationToken);
        if (existing.HasValue is false || existing.Value is null)
        {
            return null;
        }

        var entity = existing.Value;
        var targetRowKey = normalizedEmail;

        entity["Email"] = mutation.Email.Trim();
        entity["NormalizedEmail"] = normalizedEmail;
        entity["Permission"] = mutation.Permission.ToStorageValue();

        if (!string.Equals(entity.RowKey, targetRowKey, StringComparison.OrdinalIgnoreCase))
        {
            var newEntity = new TableEntity(tripId, targetRowKey)
            {
                ["OwnerUserId"] = entity.GetString("OwnerUserId") ?? userId,
                ["InvitedByUserId"] = entity.GetString("InvitedByUserId") ?? userId,
                ["InvitedOn"] = entity.GetDateTimeOffset("InvitedOn") ?? DateTimeOffset.UtcNow,
                ["Email"] = entity["Email"],
                ["NormalizedEmail"] = entity["NormalizedEmail"],
                ["Permission"] = entity["Permission"],
                ["UserId"] = entity.GetString("UserId") ?? string.Empty
            };

            await _tables.TripAccess.UpsertEntityAsync(newEntity, TableUpdateMode.Replace, cancellationToken);
            await _tables.TripAccess.DeleteEntityAsync(tripId, accessId, cancellationToken: cancellationToken);
            entity = newEntity;
        }
        else
        {
            await _tables.TripAccess.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace, cancellationToken);
        }

        var access = TableEntityMapper.ToTripAccess(entity);
        TrackEvent("TripAccessUpdated", properties =>
        {
            properties["UserId"] = userId;
            properties["TripId"] = tripId;
            properties["AccessId"] = access.AccessId;
            properties["Permission"] = access.Permission.ToString();
        });

        return access;
    }

    public async Task<bool> RevokeTripAccessAsync(string userId, string tripId, string accessId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tripId);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessId);

        await EnsureTripOwnershipAsync(userId, tripId, cancellationToken);

        try
        {
            await _tables.TripAccess.DeleteEntityAsync(tripId, accessId, cancellationToken: cancellationToken);
            TrackEvent("TripAccessRevoked", properties =>
            {
                properties["UserId"] = userId;
                properties["TripId"] = tripId;
                properties["AccessId"] = accessId;
            });
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }

    public async Task<ShareLink> CreateShareLinkAsync(string userId, string tripId, ShareLinkMutation mutation, CancellationToken cancellationToken = default, string? shareCode = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tripId);
        ArgumentNullException.ThrowIfNull(mutation);

        await EnsureTripOwnershipAsync(userId, tripId, cancellationToken);

        var normalizedShareCode = NormalizeShareCode(shareCode);
        if (!string.IsNullOrWhiteSpace(normalizedShareCode))
        {
            ValidateShareCode(normalizedShareCode);
            await EnsureShareCodeIsAvailableAsync(normalizedShareCode, cancellationToken);
            return await CreateShareLinkEntityAsync(userId, tripId, normalizedShareCode, mutation, cancellationToken);
        }

        const int maxAttempts = 5;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var generatedCode = GenerateShareCode();
            if (await ShareCodeExistsAsync(generatedCode, cancellationToken))
            {
                continue;
            }

            try
            {
                return await CreateShareLinkEntityAsync(userId, tripId, generatedCode, mutation, cancellationToken);
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

    public async Task<Trip?> UpdateTripAsync(string userId, string? userEmail, string tripId, TripMutation mutation, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tripId);
        ArgumentNullException.ThrowIfNull(mutation);

        var context = await EnsureTripAccessAsync(userId, userEmail, tripId, requireWrite: true, cancellationToken: cancellationToken);
        var ownerUserId = context.Trip.UserId;

        var existing = await _tables.Trips.GetEntityIfExistsAsync<TableEntity>(ownerUserId, tripId, cancellationToken: cancellationToken);
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

    public async Task<bool> DeleteTripAsync(string userId, string? userEmail, string tripId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tripId);

        await EnsureTripOwnershipAsync(userId, tripId, cancellationToken);

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

    public async Task<ItineraryEntry> CreateItineraryEntryAsync(string userId, string? userEmail, string tripId, ItineraryEntryMutation mutation, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tripId);
        ArgumentNullException.ThrowIfNull(mutation);

        await EnsureTripAccessAsync(userId, userEmail, tripId, requireWrite: true, cancellationToken: cancellationToken);

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

    public async Task<ItineraryEntry?> UpdateItineraryEntryAsync(string userId, string? userEmail, string tripId, string entryId, ItineraryEntryMutation mutation, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tripId);
        ArgumentException.ThrowIfNullOrWhiteSpace(entryId);
        ArgumentNullException.ThrowIfNull(mutation);

        await EnsureTripAccessAsync(userId, userEmail, tripId, requireWrite: true, cancellationToken: cancellationToken);

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

    public async Task<bool> DeleteItineraryEntryAsync(string userId, string? userEmail, string tripId, string entryId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tripId);
        ArgumentException.ThrowIfNullOrWhiteSpace(entryId);

        await EnsureTripAccessAsync(userId, userEmail, tripId, requireWrite: true, cancellationToken: cancellationToken);

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

    public async Task ReorderItineraryEntriesAsync(string userId, string? userEmail, string tripId, DateOnly date, IReadOnlyList<string> orderedEntryIds, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tripId);
        ArgumentNullException.ThrowIfNull(orderedEntryIds);

        await EnsureTripAccessAsync(userId, userEmail, tripId, requireWrite: true, cancellationToken: cancellationToken);

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

    public async Task<Booking> CreateBookingAsync(string userId, string? userEmail, string tripId, BookingMutation mutation, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tripId);
        ArgumentNullException.ThrowIfNull(mutation);

        await EnsureTripAccessAsync(userId, userEmail, tripId, requireWrite: true, cancellationToken: cancellationToken);
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

    public async Task<Booking?> UpdateBookingAsync(string userId, string? userEmail, string tripId, string bookingId, BookingMutation mutation, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tripId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bookingId);
        ArgumentNullException.ThrowIfNull(mutation);

        await EnsureTripAccessAsync(userId, userEmail, tripId, requireWrite: true, cancellationToken: cancellationToken);
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

    public async Task<bool> DeleteBookingAsync(string userId, string? userEmail, string tripId, string bookingId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tripId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bookingId);

        await EnsureTripAccessAsync(userId, userEmail, tripId, requireWrite: true, cancellationToken: cancellationToken);

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
        SetOrRemove(entity, "GooglePlaceId", mutation.GooglePlaceId);
        SetOrRemove(entity, "Tags", mutation.Tags);

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

    private async Task<TripDetails> BuildTripDetailsAsync(Trip trip, ShareLink? shareLink, TripPermission permission, CancellationToken cancellationToken)
    {
        var entriesTask = QueryItineraryEntriesAsync(trip.TripId, cancellationToken);
        var bookingsTask = QueryBookingsAsync(trip.TripId, cancellationToken);

        await Task.WhenAll(entriesTask, bookingsTask);

        var entries = await entriesTask;
        var bookings = await bookingsTask;

        if (shareLink?.IncludeCost is false)
        {
            bookings = bookings
                .Select(booking => booking with { Cost = null, Currency = null, IsPaid = null, ConfirmationUrl = null })
                .ToList();
        }

        if (shareLink?.ShowBookingConfirmations is false)
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

        if (shareLink?.ShowBookingMetadata is false)
        {
            bookings = bookings
                .Select(booking => booking with { Metadata = null })
                .ToList();
        }

        if (shareLink?.MaskBookings is true)
        {
            bookings = new List<Booking>();
        }

        return new TripDetails(trip, entries, bookings, shareLink, permission);
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

    private async Task<TripAccessContext?> GetTripContextAsync(string userId, string? userEmail, string tripId, CancellationToken cancellationToken)
    {
        var ownedTrip = await _tables.Trips.GetEntityIfExistsAsync<TableEntity>(userId, tripId, cancellationToken: cancellationToken);
        if (ownedTrip.HasValue && ownedTrip.Value != null)
        {
            return new TripAccessContext(TableEntityMapper.ToTrip(ownedTrip.Value), TripPermission.Owner, null);
        }

        var accessEntity = await FindAccessEntityAsync(tripId, userId, userEmail, cancellationToken);
        if (accessEntity is null)
        {
            return null;
        }

        var ownerUserId = accessEntity.GetString("OwnerUserId");
        if (string.IsNullOrWhiteSpace(ownerUserId))
        {
            return null;
        }

        var tripEntity = await _tables.Trips.GetEntityIfExistsAsync<TableEntity>(ownerUserId, tripId, cancellationToken: cancellationToken);
        if (tripEntity.HasValue is false || tripEntity.Value is null)
        {
            return null;
        }

        await MaybeStampUserIdOnAccessAsync(accessEntity, userId, cancellationToken);
        var permission = TripPermissionExtensions.FromStorage(accessEntity.GetString("Permission"));
        return new TripAccessContext(TableEntityMapper.ToTrip(tripEntity.Value), permission, accessEntity);
    }

    private async Task<TableEntity?> FindAccessEntityAsync(string tripId, string userId, string? userEmail, CancellationToken cancellationToken)
    {
        var userFilter = TableClient.CreateQueryFilter($"PartitionKey eq {tripId} and UserId eq {userId}");
        await foreach (var entity in _tables.TripAccess.QueryAsync<TableEntity>(
                   filter: userFilter,
                   maxPerPage: 1,
                   cancellationToken: cancellationToken))
        {
            return entity;
        }

        var normalizedEmail = NormalizeEmail(userEmail);
        if (!string.IsNullOrWhiteSpace(normalizedEmail))
        {
            var emailFilter = TableClient.CreateQueryFilter($"PartitionKey eq {tripId} and NormalizedEmail eq {normalizedEmail}");
            await foreach (var entity in _tables.TripAccess.QueryAsync<TableEntity>(
                       filter: emailFilter,
                       maxPerPage: 1,
                       cancellationToken: cancellationToken))
            {
                return entity;
            }
        }

        return null;
    }

    private async Task MaybeStampUserIdOnAccessAsync(TableEntity accessEntity, string userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        var existingUserId = accessEntity.GetString("UserId");
        if (!string.IsNullOrWhiteSpace(existingUserId))
        {
            return;
        }

        accessEntity["UserId"] = userId;
        try
        {
            await _tables.TripAccess.UpdateEntityAsync(accessEntity, accessEntity.ETag, TableUpdateMode.Merge, cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // The access entry disappeared between lookup and update; ignore.
        }
    }

    private async Task<TripAccessContext> EnsureTripAccessAsync(string userId, string? userEmail, string tripId, bool requireWrite, CancellationToken cancellationToken)
    {
        var context = await GetTripContextAsync(userId, userEmail, tripId, cancellationToken);
        if (context is null)
        {
            throw new InvalidOperationException($"Trip '{tripId}' is not available for the current user.");
        }

        if (requireWrite && !context.Permission.HasWriteAccess())
        {
            throw new InvalidOperationException("You only have read-only access to this trip.");
        }

        return context;
    }

    private async Task EnsureTripOwnershipAsync(string userId, string tripId, CancellationToken cancellationToken)
    {
        var tripEntity = await _tables.Trips.GetEntityIfExistsAsync<TableEntity>(userId, tripId, cancellationToken: cancellationToken);
        if (tripEntity.HasValue is false)
        {
            throw new InvalidOperationException("Only the itinerary owner can perform this action.");
        }
    }

    private async Task<Trip?> FindTripBySlugForUserAsync(string ownerUserId, IReadOnlyList<string> slugCandidates, CancellationToken cancellationToken, string? specificTripId = null)
    {
        foreach (var candidate in slugCandidates)
        {
            string filter = string.IsNullOrWhiteSpace(specificTripId)
                ? TableClient.CreateQueryFilter($"PartitionKey eq {ownerUserId} and Slug eq {candidate}")
                : TableClient.CreateQueryFilter($"PartitionKey eq {ownerUserId} and RowKey eq {specificTripId} and Slug eq {candidate}");

            await foreach (var entity in _tables.Trips.QueryAsync<TableEntity>(
                       filter: filter,
                       maxPerPage: 1,
                       cancellationToken: cancellationToken))
            {
                return TableEntityMapper.ToTrip(entity);
            }
        }

        return null;
    }

    private static string CreateAccessFilterForUser(string userId, string? normalizedEmail)
    {
        var userFilter = TableClient.CreateQueryFilter($"UserId eq {userId}");
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return userFilter;
        }

        var emailFilter = TableClient.CreateQueryFilter($"NormalizedEmail eq {normalizedEmail}");
        return $"{userFilter} or {emailFilter}";
    }

    private static string? NormalizeEmail(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    private static void ApplyShareLinkMutation(TableEntity entity, ShareLinkMutation mutation)
    {
        SetOrRemove(entity, "ExpiresOn", mutation.ExpiresOn);
        entity["MaskBookings"] = mutation.MaskBookings;
        entity["IncludeCost"] = mutation.IncludeCost;
        entity["ShowBookingConfirmations"] = mutation.ShowBookingConfirmations;
        entity["ShowBookingMetadata"] = mutation.ShowBookingMetadata;
        SetOrRemove(entity, "Notes", mutation.Notes);
    }

    private async Task<ShareLink> CreateShareLinkEntityAsync(string userId, string tripId, string shareCode, ShareLinkMutation mutation, CancellationToken cancellationToken)
    {
        var entity = new TableEntity(tripId, shareCode)
        {
            ["OwnerUserId"] = userId,
            ["CreatedOn"] = DateTimeOffset.UtcNow,
            ["CreatedBy"] = userId
        };

        ApplyShareLinkMutation(entity, mutation);

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

    private async Task EnsureShareCodeIsAvailableAsync(string shareCode, CancellationToken cancellationToken)
    {
        if (await ShareCodeExistsAsync(shareCode, cancellationToken))
        {
            throw new InvalidOperationException("That share code is already in use. Choose another one.");
        }
    }

    private async Task<bool> ShareCodeExistsAsync(string shareCode, CancellationToken cancellationToken)
    {
        await foreach (var _ in _tables.ShareLinks.QueryAsync<TableEntity>(
                   filter: CreateRowFilter(shareCode),
                   maxPerPage: 1,
                   cancellationToken: cancellationToken))
        {
            return true;
        }

        return false;
    }

    private static string? NormalizeShareCode(string? shareCode)
    {
        if (string.IsNullOrWhiteSpace(shareCode))
        {
            return null;
        }

        return shareCode.Trim().ToUpperInvariant();
    }

    private static void ValidateShareCode(string shareCode)
    {
        if (shareCode.Length is < 4 or > 32)
        {
            throw new InvalidOperationException("Share code must be between 4 and 32 characters.");
        }

        foreach (var character in shareCode)
        {
            if (!ShareCodeAlphabetSet.Contains(character))
            {
                throw new InvalidOperationException("Share code can only use letters A-Z and digits 2-9.");
            }
        }
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

    private static readonly char[] ShareCodeAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ23456789".ToCharArray();

    private static readonly HashSet<char> ShareCodeAlphabetSet = new(ShareCodeAlphabet);

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

    public async Task<IReadOnlyList<SavedShareLink>> GetSavedShareLinksAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var savedLinks = new List<SavedShareLink>();

        await foreach (var entity in _tables.SavedShareLinks.QueryAsync<TableEntity>(
                   filter: CreatePartitionFilter(userId),
                   cancellationToken: cancellationToken))
        {
            savedLinks.Add(TableEntityMapper.ToSavedShareLink(entity));
        }

        return savedLinks
            .OrderByDescending(link => link.SavedOn)
            .ToList();
    }

    public async Task<SavedShareLink> SaveShareLinkAsync(string userId, string tripSlug, string shareCode, string tripName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tripSlug);
        ArgumentException.ThrowIfNullOrWhiteSpace(shareCode);

        // Check if already saved to avoid duplicates
        var existingLinks = await GetSavedShareLinksAsync(userId, cancellationToken);
        var existing = existingLinks.FirstOrDefault(link =>
            string.Equals(link.ShareCode, shareCode, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(link.TripSlug, tripSlug, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            return existing;
        }

        var savedLinkId = Guid.NewGuid().ToString("N");
        var savedOn = DateTimeOffset.UtcNow;

        var entity = new TableEntity(userId, savedLinkId)
        {
            ["TripSlug"] = tripSlug,
            ["ShareCode"] = shareCode,
            ["TripName"] = tripName ?? string.Empty,
            ["SavedOn"] = savedOn
        };

        await _tables.SavedShareLinks.UpsertEntityAsync(entity, cancellationToken: cancellationToken);

        TrackEvent("SavedShareLinkCreated", properties =>
        {
            properties["UserId"] = userId;
            properties["TripSlug"] = tripSlug;
            properties["ShareCode"] = shareCode;
        });

        return new SavedShareLink(savedLinkId, userId, tripSlug, shareCode, tripName ?? string.Empty, savedOn);
    }

    public async Task<bool> DeleteSavedShareLinkAsync(string userId, string savedLinkId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(savedLinkId);

        try
        {
            await _tables.SavedShareLinks.DeleteEntityAsync(userId, savedLinkId, cancellationToken: cancellationToken);

            TrackEvent("SavedShareLinkDeleted", properties =>
            {
                properties["UserId"] = userId;
                properties["SavedLinkId"] = savedLinkId;
            });

            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }
}
