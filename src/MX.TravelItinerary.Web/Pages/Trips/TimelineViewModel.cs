using System;
using System.Collections.Generic;
using System.Linq;
using MX.TravelItinerary.Web.Data.Models;

namespace MX.TravelItinerary.Web.Pages.Trips;

public sealed class TimelineViewModel
{
    public TimelineViewModel(
        IReadOnlyList<TimelineDay> days,
        IReadOnlyList<TimelineSpanBlock> spans,
        int maxSegmentLaneCount)
    {
        Days = days;
        Spans = spans;
        MaxSegmentLaneCount = Math.Max(1, maxSegmentLaneCount);
    }

    public IReadOnlyList<TimelineDay> Days { get; }

    public IReadOnlyList<TimelineSpanBlock> Spans { get; }

    public int MaxSegmentLaneCount { get; }

    public static TimelineViewModel Empty { get; } = new(Array.Empty<TimelineDay>(), Array.Empty<TimelineSpanBlock>(), 1);

    public static TimelineViewModel From(TripDetails details)
    {
        var dates = BuildTimelineDates(details);
        var dayLookup = dates
            .Select((date, index) => new { date, index })
            .ToDictionary(item => item.date, item => item.index);

        var multiDayEntries = details.Entries
            .Where(entry => entry.IsMultiDay && entry.Date is not null && entry.EndDate is not null)
            .ToList();

        var singleDayEntries = details.Entries
            .Where(entry => !entry.IsMultiDay || entry.EndDate is null || entry.EndDate == entry.Date)
            .ToList();

        var days = dates
            .Select((date, index) =>
            {
                var entries = singleDayEntries
                    .Where(entry => entry.Date == date)
                    .OrderBy(entry => entry.SortOrder ?? int.MaxValue)
                    .ThenBy(entry => (int)entry.ItemType)
                    .ThenBy(entry => entry.Title)
                    .ToList();

                return new TimelineDay(index + 1, date, entries);
            })
            .ToList();

        var spans = BuildSpanBlocks(multiDayEntries, dates, dayLookup);
        var maxSegmentLanes = spans.Count == 0 ? 1 : spans.Max(span => span.LaneCount);

        return new TimelineViewModel(days, spans, maxSegmentLanes);
    }

    private static IReadOnlyList<DateOnly> BuildTimelineDates(TripDetails details)
    {
        var candidates = new List<DateOnly>();
        if (details.Trip.StartDate is { } start)
        {
            candidates.Add(start);
        }

        if (details.Trip.EndDate is { } end)
        {
            candidates.Add(end);
        }

        foreach (var entry in details.Entries)
        {
            if (entry.Date is { } entryDate)
            {
                candidates.Add(entryDate);
            }

            if (entry.EndDate is { } entryEnd)
            {
                candidates.Add(entryEnd);
            }
        }

        if (candidates.Count == 0)
        {
            candidates.Add(DateOnly.FromDateTime(DateTime.UtcNow));
        }

        var minDate = candidates.Min();
        var maxDate = candidates.Max();

        if (maxDate < minDate)
        {
            maxDate = minDate;
        }

        var totalDays = maxDate.DayNumber - minDate.DayNumber + 1;
        var dates = new List<DateOnly>(totalDays);
        for (var i = 0; i < totalDays; i++)
        {
            dates.Add(minDate.AddDays(i));
        }

        return dates;
    }

    private static IReadOnlyList<TimelineSpanBlock> BuildSpanBlocks(
        IReadOnlyList<ItineraryEntry> entries,
        IReadOnlyList<DateOnly> dates,
        IReadOnlyDictionary<DateOnly, int> dayLookup)
    {
        if (entries.Count == 0 || dates.Count == 0)
        {
            return Array.Empty<TimelineSpanBlock>();
        }

        var firstDate = dates.First();
        var lastDate = dates.Last();
        var spans = new List<TimelineSpanBlock>();

        foreach (var entry in entries)
        {
            var startDate = entry.Date!.Value;
            var endDate = entry.EndDate!.Value;

            if (endDate < startDate)
            {
                (startDate, endDate) = (endDate, startDate);
            }

            startDate = startDate < firstDate ? firstDate : startDate;
            endDate = endDate > lastDate ? lastDate : endDate;

            var startIndex = dayLookup[startDate];
            var endIndex = dayLookup[endDate];
            spans.Add(new TimelineSpanBlock(entry, startIndex + 1, endIndex + 2, startDate, endDate));
        }

        return AssignSpanLanes(spans);
    }

    public sealed record TimelineDay(int RowLine, DateOnly Date, IReadOnlyList<ItineraryEntry> Entries);

    private static IReadOnlyList<TimelineSpanBlock> AssignSpanLanes(IReadOnlyList<TimelineSpanBlock> spans)
    {
        var ordered = spans
            .OrderBy(span => span.RowStart)
            .ThenByDescending(span => span.RowEnd - span.RowStart)
            .ToList();

        var active = new List<(int lane, TimelineSpanBlock block)>();
        var laidOut = new List<TimelineSpanBlock>(ordered.Count);

        foreach (var span in ordered)
        {
            active.RemoveAll(item => item.block.RowEnd <= span.RowStart);

            var lane = 0;
            while (active.Any(item => item.lane == lane))
            {
                lane++;
            }

            var updated = span with { LaneIndex = lane };
            active.Add((lane, updated));
            laidOut.Add(updated);
        }

        var finalized = new List<TimelineSpanBlock>(laidOut.Count);
        foreach (var block in laidOut)
        {
            var laneCount = laidOut
                .Where(other => Overlaps(block, other))
                .Select(other => other.LaneIndex)
                .DefaultIfEmpty(block.LaneIndex)
                .Max() + 1;

            finalized.Add(block with { LaneCount = laneCount });
        }

        return finalized
            .OrderBy(block => block.RowStart)
            .ThenBy(block => block.LaneIndex)
            .ToList();
    }

    private static bool Overlaps(TimelineSpanBlock a, TimelineSpanBlock b)
        => a.RowStart < b.RowEnd && b.RowStart < a.RowEnd;

    public sealed record TimelineSpanBlock(
        ItineraryEntry Entry,
        int RowStart,
        int RowEnd,
        DateOnly StartDate,
        DateOnly EndDate,
        int LaneIndex = 0,
        int LaneCount = 1);
}
