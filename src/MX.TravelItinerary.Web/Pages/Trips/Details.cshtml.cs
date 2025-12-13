using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Identity.Web;
using MX.TravelItinerary.Web.Data;
using MX.TravelItinerary.Web.Data.Models;

namespace MX.TravelItinerary.Web.Pages.Trips;

public sealed class DetailsModel : PageModel
{
    private readonly IItineraryRepository _repository;
    private readonly ILogger<DetailsModel> _logger;

    public DetailsModel(IItineraryRepository repository, ILogger<DetailsModel> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string TripId { get; set; } = string.Empty;

    public TripDetails? TripDetails { get; private set; }

    public TimelineViewModel Timeline { get; private set; } = TimelineViewModel.Empty;

    [BindProperty]
    public TripSegmentForm SegmentInput { get; set; } = new();

    [BindProperty]
    public ItineraryEntryForm EntryInput { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await LoadTripAsync(cancellationToken))
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSaveSegmentAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(TripId) && !string.IsNullOrWhiteSpace(SegmentInput.TripId))
        {
            TripId = SegmentInput.TripId;
        }

        ModelState.Clear();
        TryValidateModel(SegmentInput, nameof(SegmentInput));
        ValidateSegmentInput();

        if (!ModelState.IsValid)
        {
            if (!await LoadTripAsync(cancellationToken))
            {
                return NotFound();
            }

            return Page();
        }

        var userId = GetUserId();
        var mutation = SegmentInput.ToMutation();

        if (string.IsNullOrWhiteSpace(SegmentInput.SegmentId))
        {
            await _repository.CreateTripSegmentAsync(userId, TripId, mutation, cancellationToken);
            StatusMessage = "Segment added.";
        }
        else
        {
            var updated = await _repository.UpdateTripSegmentAsync(userId, TripId, SegmentInput.SegmentId, mutation, cancellationToken);
            if (updated is null)
            {
                ModelState.AddModelError(string.Empty, "Unable to find the selected segment.");
                if (!await LoadTripAsync(cancellationToken))
                {
                    return NotFound();
                }

                return Page();
            }

            StatusMessage = "Segment updated.";
        }

        return RedirectToPage(new { tripId = TripId });
    }

    public async Task<IActionResult> OnPostDeleteSegmentAsync(string segmentId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(segmentId))
        {
            return RedirectToPage(new { tripId = TripId });
        }

        var userId = GetUserId();
        await _repository.DeleteTripSegmentAsync(userId, TripId, segmentId, cancellationToken);
        StatusMessage = "Segment deleted.";
        return RedirectToPage(new { tripId = TripId });
    }

    public async Task<IActionResult> OnPostSaveEntryAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(TripId) && !string.IsNullOrWhiteSpace(EntryInput.TripId))
        {
            TripId = EntryInput.TripId;
        }

        ModelState.Clear();
        TryValidateModel(EntryInput, nameof(EntryInput));
        if (!ModelState.IsValid)
        {
            if (!await LoadTripAsync(cancellationToken))
            {
                return NotFound();
            }

            return Page();
        }

        var userId = GetUserId();
        var mutation = EntryInput.ToMutation();

        if (string.IsNullOrWhiteSpace(EntryInput.EntryId))
        {
            await _repository.CreateItineraryEntryAsync(userId, TripId, mutation, cancellationToken);
            StatusMessage = "Itinerary entry added.";
        }
        else
        {
            var updated = await _repository.UpdateItineraryEntryAsync(userId, TripId, EntryInput.EntryId, mutation, cancellationToken);
            if (updated is null)
            {
                ModelState.AddModelError(string.Empty, "Unable to find the selected entry.");
                if (!await LoadTripAsync(cancellationToken))
                {
                    return NotFound();
                }

                return Page();
            }

            StatusMessage = "Itinerary entry updated.";
        }

        return RedirectToPage(new { tripId = TripId });
    }

    public async Task<IActionResult> OnPostDeleteEntryAsync(string entryId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(entryId))
        {
            return RedirectToPage(new { tripId = TripId });
        }

        var userId = GetUserId();
        await _repository.DeleteItineraryEntryAsync(userId, TripId, entryId, cancellationToken);
        StatusMessage = "Itinerary entry deleted.";
        return RedirectToPage(new { tripId = TripId });
    }

    private async Task<bool> LoadTripAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(TripId))
        {
            return false;
        }

        var userId = GetUserId();
        var details = await _repository.GetTripAsync(userId, TripId, cancellationToken);
        if (details is null)
        {
            return false;
        }

        TripDetails = details;
        Timeline = TimelineViewModel.From(details);
        SegmentInput.TripId = details.Trip.TripId;
        EntryInput.TripId = details.Trip.TripId;
        return true;
    }

    private void ValidateSegmentInput()
    {
        if (SegmentInput.StartDateTimeUtc.HasValue && SegmentInput.EndDateTimeUtc.HasValue &&
            SegmentInput.EndDateTimeUtc < SegmentInput.StartDateTimeUtc)
        {
            ModelState.AddModelError("SegmentInput.EndDateTimeUtc", "Segment end cannot be earlier than the start.");
        }
    }

    private string GetUserId()
    {
        var userId = User.GetObjectId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogWarning("Authenticated user missing object id claim.");
            throw new InvalidOperationException("User identifier is unavailable.");
        }

        return userId;
    }


    public sealed class TripSegmentForm
    {
        [Required]
        public string TripId { get; set; } = string.Empty;

        public string? SegmentId { get; set; }

        [Required]
        [Display(Name = "Segment type")]
        public string SegmentType { get; set; } = "travel";

        [StringLength(200)]
        public string? Title { get; set; }

        [DataType(DataType.DateTime)]
        [Display(Name = "Start (local time)")]
        public DateTime? StartDateTimeUtc { get; set; }

        [DataType(DataType.DateTime)]
        [Display(Name = "End (local time)")]
        public DateTime? EndDateTimeUtc { get; set; }

        [DataType(DataType.MultilineText)]
        public string? Description { get; set; }

        public TripSegmentMutation ToMutation()
            => new(
                string.IsNullOrWhiteSpace(SegmentType) ? "travel" : SegmentType.Trim(),
                ToUtc(StartDateTimeUtc),
                ToUtc(EndDateTimeUtc),
                StartLocation: null,
                EndLocation: null,
                Title: string.IsNullOrWhiteSpace(Title) ? null : Title,
                Description: string.IsNullOrWhiteSpace(Description) ? null : Description);

        private static DateTimeOffset? ToUtc(DateTime? value)
        {
            if (value is null)
            {
                return null;
            }

            var local = DateTime.SpecifyKind(value.Value, DateTimeKind.Local);
            return new DateTimeOffset(local).ToUniversalTime();
        }
    }

    public sealed class ItineraryEntryForm
    {
        [Required]
        public string TripId { get; set; } = string.Empty;

        public string? EntryId { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        public DateOnly? Date { get; set; }

        [StringLength(100)]
        public string? Category { get; set; }

        [DataType(DataType.MultilineText)]
        public string? Details { get; set; }

        public ItineraryEntryMutation ToMutation()
            => new(
                Date,
                Normalize(Category),
                string.IsNullOrWhiteSpace(Title) ? "Untitled entry" : Title.Trim(),
                string.IsNullOrWhiteSpace(Details) ? null : Details,
                Location: null,
                CostEstimate: null,
                Currency: null,
                IsPaid: null,
                PaymentStatus: null,
                Provider: null,
                Tags: null);

        private static string? Normalize(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public sealed class TimelineViewModel
    {
        public TimelineViewModel(
            IReadOnlyList<TimelineDay> days,
            IReadOnlyList<TimelineSegmentBlock> segments,
            IReadOnlyList<TripSegment> unscheduledSegments)
        {
            Days = days;
            Segments = segments;
            UnscheduledSegments = unscheduledSegments;
        }

        public IReadOnlyList<TimelineDay> Days { get; }

        public IReadOnlyList<TimelineSegmentBlock> Segments { get; }

        public IReadOnlyList<TripSegment> UnscheduledSegments { get; }

        public static TimelineViewModel Empty { get; } = new(Array.Empty<TimelineDay>(), Array.Empty<TimelineSegmentBlock>(), Array.Empty<TripSegment>());

        public static TimelineViewModel From(TripDetails details)
        {
            var dates = BuildTimelineDates(details);
            var dayLookup = dates.Select((date, index) => new { date, index }).ToDictionary(item => item.date, item => item.index);

            var days = dates
                .Select((date, index) =>
                {
                    var entries = details.Entries
                        .Where(entry => entry.Date == date)
                        .OrderBy(entry => entry.Category)
                        .ThenBy(entry => entry.Title)
                        .ToList();

                    return new TimelineDay(index + 1, date, entries);
                })
                .ToList();

            var segments = new List<TimelineSegmentBlock>();
            var unscheduledSegments = new List<TripSegment>();
            if (dates.Count > 0)
            {
                var firstDate = dates.First();
                var lastDate = dates.Last();

                foreach (var segment in details.Segments)
                {
                    if (segment.StartDateTimeUtc is null && segment.EndDateTimeUtc is null)
                    {
                        unscheduledSegments.Add(segment);
                        continue;
                    }

                    var startDate = segment.StartDateTimeUtc is { } start
                        ? DateOnly.FromDateTime(start.UtcDateTime)
                        : firstDate;

                    var endDate = segment.EndDateTimeUtc is { } end
                        ? DateOnly.FromDateTime(end.UtcDateTime)
                        : startDate;

                    if (endDate < startDate)
                    {
                        (startDate, endDate) = (endDate, startDate);
                    }

                    startDate = startDate < firstDate ? firstDate : startDate;
                    endDate = endDate > lastDate ? lastDate : endDate;

                    var startIndex = dayLookup[startDate];
                    var endIndex = dayLookup[endDate];
                    segments.Add(new TimelineSegmentBlock(segment, startIndex + 1, endIndex + 2, startDate, endDate));
                }
            }

            return new TimelineViewModel(days, segments, unscheduledSegments);
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

            candidates.AddRange(details.Entries.Where(entry => entry.Date is not null).Select(entry => entry.Date!.Value));

            foreach (var segment in details.Segments)
            {
                if (segment.StartDateTimeUtc is { } segStart)
                {
                    candidates.Add(DateOnly.FromDateTime(segStart.UtcDateTime));
                }

                if (segment.EndDateTimeUtc is { } segEnd)
                {
                    candidates.Add(DateOnly.FromDateTime(segEnd.UtcDateTime));
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

        public sealed record TimelineDay(int RowLine, DateOnly Date, IReadOnlyList<ItineraryEntry> Entries);

        public sealed record TimelineSegmentBlock(TripSegment Segment, int RowStart, int RowEnd, DateOnly StartDate, DateOnly EndDate);
    }
}
