using System;
using System.Collections.Generic;
using System.Linq;
using MX.TravelItinerary.Web.Data.Models;

namespace MX.TravelItinerary.Web.Pages.Trips;

public static class RouteMapBuilder
{
    public static IReadOnlyList<RouteMapPoint> BuildRoutePoints(TripDetails? details)
    {
        if (details is null || details.Entries.Count == 0)
        {
            return [];
        }

        var orderedEntries = details.Entries
            .OrderBy(entry => entry.Date ?? DateOnly.MaxValue)
            .ThenBy(entry => entry.SortOrder ?? int.MaxValue)
            .ThenBy(entry => entry.Title ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.EntryId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var points = new List<RouteMapPoint>();
        var sequence = 1;

        foreach (var entry in orderedEntries)
        {
            var entryTitle = GetEntryTitle(entry);
            var dateLabel = BuildDateLabel(entry);
            var detailSnippet = BuildDetailsSnippet(entry);
            var segment = entry.Metadata?.Segment;

            if (segment is not null)
            {
                sequence = AppendRoutePoint(points, entry, entryTitle, dateLabel, detailSnippet, segment.DeparturePlaceId, "Departure", sequence);
                sequence = AppendRoutePoint(points, entry, entryTitle, dateLabel, detailSnippet, segment.ArrivalPlaceId, "Arrival", sequence);
                continue;
            }

            sequence = AppendRoutePoint(points, entry, entryTitle, dateLabel, detailSnippet, entry.GooglePlaceId, "Location", sequence);
        }

        return points;
    }

    private static int AppendRoutePoint(
        ICollection<RouteMapPoint> aggregate,
        ItineraryEntry entry,
        string entryTitle,
        string dateLabel,
        string? detailSnippet,
        string? placeId,
        string stopType,
        int sequence)
    {
        if (string.IsNullOrWhiteSpace(placeId))
        {
            return sequence;
        }

        var normalizedPlaceId = placeId.Trim();
        if (normalizedPlaceId.Length == 0)
        {
            return sequence;
        }

        var markerColor = RouteMarkerPalette.GetColor(entry.ItemType);
        var stopLabel = BuildStopLabel(entryTitle, stopType);
        var point = new RouteMapPoint(
            EntryId: entry.EntryId,
            StopId: $"{entry.EntryId}-{stopType.ToLowerInvariant()}-{sequence}",
            StopLabel: stopLabel,
            StopType: stopType,
            EntryTypeLabel: entry.ItemType.GetDisplayName(),
            ItemTypeToken: entry.ItemType.ToCssToken(),
            IconClass: entry.ItemType.GetIconClass(),
            DateLabel: dateLabel,
            Details: detailSnippet,
            PlaceId: normalizedPlaceId,
            Sequence: sequence,
            MarkerColor: markerColor);

        aggregate.Add(point);
        return sequence + 1;
    }

    private static string BuildDateLabel(ItineraryEntry entry)
    {
        if (entry.IsMultiDay && entry.Date is { } start && entry.EndDate is { } end)
        {
            return $"{start:MMM dd} - {end:MMM dd}";
        }

        if (entry.Date is { } single)
        {
            return single.ToString("MMM dd, yyyy");
        }

        return "Date TBD";
    }

    private static string GetEntryTitle(ItineraryEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.Title))
        {
            return entry.Title.Trim();
        }

        return entry.ToString();
    }

    private static string BuildStopLabel(string entryTitle, string stopType)
    {
        if (string.Equals(stopType, "Location", StringComparison.OrdinalIgnoreCase))
        {
            return entryTitle;
        }

        return $"{entryTitle} ({stopType})";
    }

    private static string? BuildDetailsSnippet(ItineraryEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Details))
        {
            return null;
        }

        var trimmed = entry.Details.Trim();
        const int maxLength = 320;
        if (trimmed.Length <= maxLength)
        {
            return trimmed;
        }

        return $"{trimmed[..(maxLength - 1)]}...";
    }
}

public sealed record RouteMapPoint(
    string EntryId,
    string StopId,
    string StopLabel,
    string StopType,
    string EntryTypeLabel,
    string ItemTypeToken,
    string IconClass,
    string? DateLabel,
    string? Details,
    string PlaceId,
    int Sequence,
    string MarkerColor);

internal static class RouteMarkerPalette
{
    private static readonly IReadOnlyDictionary<TimelineItemType, string> Palette = new Dictionary<TimelineItemType, string>
    {
        [TimelineItemType.Flight] = "#0d6efd",
        [TimelineItemType.Train] = "#0d6efd",
        [TimelineItemType.Coach] = "#0d6efd",
        [TimelineItemType.Ferry] = "#0d6efd",
        [TimelineItemType.Taxi] = "#0d6efd",
        [TimelineItemType.PrivateCar] = "#0d6efd",
        [TimelineItemType.RentalCar] = "#0d6efd",
        [TimelineItemType.Parking] = "#0d6efd",
        [TimelineItemType.Hotel] = "#dc3545",
        [TimelineItemType.Flat] = "#dc3545",
        [TimelineItemType.House] = "#dc3545",
        [TimelineItemType.Tour] = "#198754",
        [TimelineItemType.Museum] = "#198754",
        [TimelineItemType.Park] = "#198754",
        [TimelineItemType.Dining] = "#fd7e14",
        [TimelineItemType.Note] = "#6c757d",
        [TimelineItemType.Other] = "#6610f2"
    };

    public static string GetColor(TimelineItemType itemType)
        => Palette.TryGetValue(itemType, out var color) ? color : "#0d6efd";
}
