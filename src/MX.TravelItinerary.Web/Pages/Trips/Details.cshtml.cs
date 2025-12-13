using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
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
    public ItineraryEntryForm EntryInput { get; set; } = new();

    [BindProperty]
    public BookingForm BookingInput { get; set; } = new();

    public IReadOnlyList<SelectListItem> BookingTypeOptions { get; } = BuildSelectList<BookingType>();

    public IReadOnlyList<SelectListItem> EntryTypeOptions { get; } = BuildSelectList<TimelineItemType>(TimelineItemGroupSelector);

    [TempData]
    public string? StatusMessage { get; set; }

    public IReadOnlyDictionary<string, Booking> EntryBookings { get; private set; } = new Dictionary<string, Booking>(StringComparer.OrdinalIgnoreCase);

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await LoadTripAsync(cancellationToken))
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSaveEntryAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(TripId) && !string.IsNullOrWhiteSpace(EntryInput.TripId))
        {
            TripId = EntryInput.TripId;
        }

        if (!await LoadTripAsync(cancellationToken))
        {
            return NotFound();
        }

        ModelState.Clear();
        TryValidateModel(EntryInput, nameof(EntryInput));
        ValidateEntryInput();
        if (!ModelState.IsValid)
        {
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

    public async Task<IActionResult> OnPostSaveBookingAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(TripId) && !string.IsNullOrWhiteSpace(BookingInput.TripId))
        {
            TripId = BookingInput.TripId;
        }

        ModelState.Clear();
        TryValidateModel(BookingInput, nameof(BookingInput));
        ValidateBookingInput();

        if (!ModelState.IsValid)
        {
            if (!await LoadTripAsync(cancellationToken))
            {
                return NotFound();
            }

            return Page();
        }

        var userId = GetUserId();
        var mutation = BookingInput.ToMutation();

        try
        {
            if (string.IsNullOrWhiteSpace(BookingInput.BookingId))
            {
                await _repository.CreateBookingAsync(userId, TripId, mutation, cancellationToken);
                StatusMessage = "Booking confirmation added.";
            }
            else
            {
                var updated = await _repository.UpdateBookingAsync(userId, TripId, BookingInput.BookingId, mutation, cancellationToken);
                if (updated is null)
                {
                    ModelState.AddModelError(string.Empty, "Unable to find the selected booking.");
                    if (!await LoadTripAsync(cancellationToken))
                    {
                        return NotFound();
                    }

                    return Page();
                }

                StatusMessage = "Booking confirmation updated.";
            }
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            if (!await LoadTripAsync(cancellationToken))
            {
                return NotFound();
            }

            return Page();
        }

        return RedirectToPage(new { tripId = TripId });
    }

    public async Task<IActionResult> OnPostDeleteBookingAsync(string bookingId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(bookingId))
        {
            return RedirectToPage(new { tripId = TripId });
        }

        var userId = GetUserId();
        await _repository.DeleteBookingAsync(userId, TripId, bookingId, cancellationToken);
        StatusMessage = "Booking confirmation deleted.";
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
        EntryInput.TripId = details.Trip.TripId;
        BookingInput.TripId = details.Trip.TripId;
        EntryBookings = BuildBookingLookup(details.Bookings, booking => booking.EntryId);
        return true;
    }

    private void ValidateEntryInput()
    {
        if (EntryInput.IsMultiDay)
        {
            if (EntryInput.Date is null)
            {
                ModelState.AddModelError("EntryInput.Date", "Select a start date for a multi-day entry.");
            }

            if (EntryInput.EndDate is null)
            {
                ModelState.AddModelError("EntryInput.EndDate", "Select an end date for a multi-day entry.");
            }
            else if (EntryInput.Date is { } start && EntryInput.EndDate < start)
            {
                ModelState.AddModelError("EntryInput.EndDate", "End date cannot be earlier than the start date.");
            }
        }
        else if (EntryInput.EndDate is not null)
        {
            ModelState.AddModelError("EntryInput.EndDate", "Clear the end date for a single-day entry.");
        }

        var trip = TripDetails?.Trip;
        if (trip is null)
        {
            return;
        }

        if (trip.StartDate is { } tripStart)
        {
            if (EntryInput.Date is { } entryStart && entryStart < tripStart)
            {
                ModelState.AddModelError("EntryInput.Date", $"Date must be on or after {tripStart:MMM dd, yyyy}.");
            }

            if (EntryInput.EndDate is { } entryEnd && entryEnd < tripStart)
            {
                ModelState.AddModelError("EntryInput.EndDate", $"End date must be on or after {tripStart:MMM dd, yyyy}.");
            }
        }

        if (trip.EndDate is { } tripEnd)
        {
            if (EntryInput.Date is { } entryStart && entryStart > tripEnd)
            {
                ModelState.AddModelError("EntryInput.Date", $"Date must be on or before {tripEnd:MMM dd, yyyy}.");
            }

            if (EntryInput.EndDate is { } entryEnd && entryEnd > tripEnd)
            {
                ModelState.AddModelError("EntryInput.EndDate", $"End date must be on or before {tripEnd:MMM dd, yyyy}.");
            }
        }
    }

    private void ValidateBookingInput()
    {
        if (string.IsNullOrWhiteSpace(BookingInput.EntryId))
        {
            ModelState.AddModelError("BookingInput.EntryId", "Link the booking to a timeline entry.");
        }
    }

    private static IReadOnlyDictionary<string, Booking> BuildBookingLookup(IEnumerable<Booking> bookings, Func<Booking, string?> keySelector)
    {
        var lookup = new Dictionary<string, Booking>(StringComparer.OrdinalIgnoreCase);
        foreach (var booking in bookings)
        {
            var key = keySelector(booking);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (!lookup.ContainsKey(key))
            {
                lookup[key] = booking;
            }
        }

        return lookup;
    }

    public Booking? GetBookingForEntry(string entryId)
        => EntryBookings.TryGetValue(entryId, out var booking) ? booking : null;

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

        [DataType(DataType.Date)]
        [Display(Name = "End date")]
        public DateOnly? EndDate { get; set; }

        [Display(Name = "Multi-day entry")]
        public bool IsMultiDay { get; set; }

        [Display(Name = "Type")]
        public TimelineItemType ItemType { get; set; } = TimelineItemType.Tour;

        [DataType(DataType.MultilineText)]
        public string? Details { get; set; }

        public ItineraryEntryMutation ToMutation()
            => new(
                Date,
                EndDate,
                IsMultiDay,
                ItemType,
                string.IsNullOrWhiteSpace(Title) ? "Untitled entry" : Title.Trim(),
                string.IsNullOrWhiteSpace(Details) ? null : Details,
                Location: null,
                CostEstimate: null,
                Currency: null,
                IsPaid: null,
                PaymentStatus: null,
                Provider: null,
                Tags: null);
    }

    public sealed class BookingForm
    {
        [Required]
        public string TripId { get; set; } = string.Empty;

        public string? BookingId { get; set; }

        public string? EntryId { get; set; }

        [Display(Name = "Booking type")]
        public BookingType BookingType { get; set; } = BookingType.Other;

        [StringLength(200)]
        public string? Vendor { get; set; }

        [StringLength(100)]
        [Display(Name = "Confirmation number")]
        public string? Reference { get; set; }

        [DataType(DataType.Currency)]
        public decimal? Cost { get; set; }

        [StringLength(3)]
        [Display(Name = "Currency (ISO)")]
        public string? Currency { get; set; }

        [Display(Name = "Refundable booking")]
        public bool IsRefundable { get; set; }

        [StringLength(500)]
        public string? CancellationPolicy { get; set; }

        [Display(Name = "Confirmation details")]
        [DataType(DataType.MultilineText)]
        public string? ConfirmationDetails { get; set; }

        public BookingMutation ToMutation()
            => new(
                Normalize(EntryId),
                BookingType,
                Normalize(Vendor),
                Normalize(Reference),
                Cost,
                Normalize(Currency)?.ToUpperInvariant(),
                IsRefundable,
                Normalize(CancellationPolicy),
                string.IsNullOrWhiteSpace(ConfirmationDetails) ? null : ConfirmationDetails.Trim());

        private static string? Normalize(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static IReadOnlyDictionary<TimelineItemType, SelectListGroup> TimelineItemGroups { get; } = CreateTimelineItemGroups();

    private static SelectListGroup? TimelineItemGroupSelector(TimelineItemType type)
        => TimelineItemGroups.TryGetValue(type, out var group) ? group : null;

    private static IReadOnlyList<SelectListItem> BuildSelectList<TEnum>(Func<TEnum, SelectListGroup?>? groupSelector = null) where TEnum : struct, Enum
        => Enum.GetValues<TEnum>()
            .Select(value =>
            {
                var item = new SelectListItem(value.GetDisplayName(), value.ToString());
                var group = groupSelector?.Invoke(value);
                if (group is not null)
                {
                    item.Group = group;
                }

                return item;
            })
            .ToList();

    private static IReadOnlyList<SelectListItem> BuildNullableSelectList<TEnum>(string placeholder, Func<TEnum, SelectListGroup?>? groupSelector = null) where TEnum : struct, Enum
    {
        var items = new List<SelectListItem>
        {
            new(placeholder, string.Empty)
        };

        items.AddRange(BuildSelectList(groupSelector));
        return items;
    }

    private static IReadOnlyDictionary<TimelineItemType, SelectListGroup> CreateTimelineItemGroups()
    {
        var travel = new SelectListGroup { Name = "Travel" };
        var stay = new SelectListGroup { Name = "Stays" };
        var activity = new SelectListGroup { Name = "Activities" };
        var dining = new SelectListGroup { Name = "Dining" };
        var notes = new SelectListGroup { Name = "Notes" };

        return new Dictionary<TimelineItemType, SelectListGroup>
        {
            [TimelineItemType.Flight] = travel,
            [TimelineItemType.Train] = travel,
            [TimelineItemType.Coach] = travel,
            [TimelineItemType.Ferry] = travel,
            [TimelineItemType.Taxi] = travel,
            [TimelineItemType.Hotel] = stay,
            [TimelineItemType.Flat] = stay,
            [TimelineItemType.House] = stay,
            [TimelineItemType.Tour] = activity,
            [TimelineItemType.Museum] = activity,
            [TimelineItemType.Park] = activity,
            [TimelineItemType.Dining] = dining,
            [TimelineItemType.Note] = notes,
            [TimelineItemType.Other] = notes
        };
    }

    public sealed class TimelineViewModel
    {
        public TimelineViewModel(
            IReadOnlyList<TimelineDay> days,
            IReadOnlyList<TimelineSpanBlock> spans)
        {
            Days = days;
            Spans = spans;
        }

        public IReadOnlyList<TimelineDay> Days { get; }

        public IReadOnlyList<TimelineSpanBlock> Spans { get; }

        public static TimelineViewModel Empty { get; } = new(Array.Empty<TimelineDay>(), Array.Empty<TimelineSpanBlock>());

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
                        .OrderBy(entry => (int)entry.ItemType)
                        .ThenBy(entry => entry.Title)
                        .ToList();

                    return new TimelineDay(index + 1, date, entries);
                })
                .ToList();

            var spans = BuildSpanBlocks(multiDayEntries, dates, dayLookup);

            return new TimelineViewModel(days, spans);
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
}
