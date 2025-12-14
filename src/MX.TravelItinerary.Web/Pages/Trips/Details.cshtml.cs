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

    [BindProperty(SupportsGet = true)]
    public string? TripSlug { get; set; }

    public TripDetails? TripDetails { get; private set; }

    public TimelineViewModel Timeline { get; private set; } = TimelineViewModel.Empty;

    [BindProperty]
    public ItineraryEntryForm EntryInput { get; set; } = new();

    [BindProperty]
    public BookingForm BookingInput { get; set; } = new();

    public IReadOnlyList<SelectListItem> EntryTypeOptions { get; } = BuildSelectList<TimelineItemType>(TimelineItemGroupSelector);

    public IReadOnlyList<SelectListItem> BookingInclusionOptions { get; } = BuildStayInclusionOptions();

    [TempData]
    public string? StatusMessage { get; set; }

    public IReadOnlyDictionary<string, Booking> EntryBookings { get; private set; } = new Dictionary<string, Booking>(StringComparer.OrdinalIgnoreCase);

    public TripTimelineDisplayModel? GetTimelineDisplayModel()
    {
        if (TripDetails is null)
        {
            return null;
        }

        return new TripTimelineDisplayModel(
            TripDetails.Trip,
            Timeline,
            GetBookingForEntry,
            allowEntryEditing: true,
            allowEntryReordering: true,
            allowBookingCreation: true,
            allowBookingViewing: true);
    }

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

        return RedirectToTripPage();
    }

    public async Task<IActionResult> OnPostDeleteEntryAsync(string entryId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(entryId))
        {
            return RedirectToTripPage();
        }

        var userId = GetUserId();
        await _repository.DeleteItineraryEntryAsync(userId, TripId, entryId, cancellationToken);
        StatusMessage = "Itinerary entry deleted.";
        return RedirectToTripPage();
    }

    public async Task<IActionResult> OnPostReorderEntriesAsync([FromBody] ReorderEntriesRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.TripId) || string.IsNullOrWhiteSpace(request.Date))
        {
            return BadRequest(new { error = "Invalid reorder payload." });
        }

        TripId = request.TripId;
        if (!await LoadTripAsync(cancellationToken))
        {
            return NotFound();
        }

        if (!DateOnly.TryParse(request.Date, out var day))
        {
            return BadRequest(new { error = "Invalid date." });
        }

        var entryIds = request.EntryIds?.Where(id => !string.IsNullOrWhiteSpace(id)).ToList() ?? new List<string>();
        var userId = GetUserId();
        await _repository.ReorderItineraryEntriesAsync(userId, TripId, day, entryIds, cancellationToken);

        return new JsonResult(new { success = true });
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

        return RedirectToTripPage();
    }

    public async Task<IActionResult> OnPostDeleteBookingAsync(string bookingId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(bookingId))
        {
            return RedirectToTripPage();
        }

        var userId = GetUserId();
        await _repository.DeleteBookingAsync(userId, TripId, bookingId, cancellationToken);
        StatusMessage = "Booking confirmation deleted.";
        return RedirectToTripPage();
    }

    private async Task<bool> LoadTripAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(TripId) && string.IsNullOrWhiteSpace(TripSlug))
        {
            return false;
        }

        var userId = GetUserId();
        TripDetails? details = null;

        if (!string.IsNullOrWhiteSpace(TripId))
        {
            details = await _repository.GetTripAsync(userId, TripId, cancellationToken);
        }

        if (details is null && !string.IsNullOrWhiteSpace(TripSlug))
        {
            details = await _repository.GetTripBySlugAsync(userId, TripSlug, cancellationToken);

            if (details is null && Guid.TryParse(TripSlug, out _))
            {
                details = await _repository.GetTripAsync(userId, TripSlug, cancellationToken);
            }
        }

        if (details is null)
        {
            return false;
        }

        ApplyTripContext(details);
        return true;
    }

    private void ApplyTripContext(TripDetails details)
    {
        TripDetails = details;
        Timeline = TimelineViewModel.From(details);
        TripId = details.Trip.TripId;
        TripSlug = details.Trip.Slug;
        EntryInput.TripId = details.Trip.TripId;
        BookingInput.TripId = details.Trip.TripId;
        EntryBookings = BuildBookingLookup(details.Bookings, booking => booking.EntryId);
        ClearTripIdentifierModelState();
    }

    private void ClearTripIdentifierModelState()
    {
        ModelState.Remove($"{nameof(EntryInput)}.{nameof(ItineraryEntryForm.TripId)}");
        ModelState.Remove($"{nameof(BookingInput)}.{nameof(BookingForm.TripId)}");
        ModelState.Remove(nameof(TripId));
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

    private IActionResult RedirectToTripPage()
    {
        var slug = TripSlug;
        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = TripDetails?.Trip.Slug;
        }

        if (!string.IsNullOrWhiteSpace(slug))
        {
            return RedirectToPage(new { tripSlug = slug });
        }

        return RedirectToPage(new { tripId = TripId });
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

        public FlightMetadataInput FlightMetadata { get; set; } = new();

        public StayMetadataInput StayMetadata { get; set; } = new();

        public ItineraryEntryMutation ToMutation()
            => new(
                Date,
                EndDate,
                IsMultiDay,
                ItemType,
                string.IsNullOrWhiteSpace(Title) ? "Untitled entry" : Title.Trim(),
                string.IsNullOrWhiteSpace(Details) ? null : Details,
                Location: null,
                Tags: null,
                Metadata: BuildMetadata(),
                SortOrder: null);

        private TravelMetadata? BuildMetadata()
        {
            FlightMetadata? flight = ItemType == TimelineItemType.Flight ? FlightMetadata.ToDomain() : null;
            StayMetadata? stay = ItemType is TimelineItemType.Hotel or TimelineItemType.Flat or TimelineItemType.House
                ? StayMetadata.ToDomain()
                : null;

            if (flight is null && stay is null)
            {
                return null;
            }

            return new TravelMetadata(flight, stay);
        }

        public sealed class FlightMetadataInput
        {
            [Display(Name = "Airline / carrier")]
            public string? Airline { get; set; }

            [Display(Name = "Flight number")]
            public string? FlightNumber { get; set; }

            [Display(Name = "Departure airport")]
            public string? DepartureAirport { get; set; }

            [Display(Name = "Departure time")]
            public string? DepartureTime { get; set; }

            [Display(Name = "Arrival airport")]
            public string? ArrivalAirport { get; set; }

            [Display(Name = "Arrival time")]
            public string? ArrivalTime { get; set; }

            public FlightMetadata? ToDomain()
            {
                var metadata = new FlightMetadata(
                    Normalize(Airline),
                    Normalize(FlightNumber),
                    Normalize(DepartureAirport),
                    Normalize(DepartureTime),
                    Normalize(ArrivalAirport),
                    Normalize(ArrivalTime));

                return metadata.HasContent ? metadata : null;
            }
        }

        public sealed class StayMetadataInput
        {
            [Display(Name = "Property name")]
            public string? PropertyName { get; set; }

            [Display(Name = "Property link")]
            [DataType(DataType.Url)]
            [Url]
            public string? PropertyLink { get; set; }

            public StayMetadata? ToDomain()
            {
                var metadata = new StayMetadata(
                    Normalize(PropertyName),
                    Normalize(PropertyLink));

                return metadata.HasContent ? metadata : null;
            }
        }

        private static string? Normalize(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public sealed class BookingForm
    {
        [Required]
        public string TripId { get; set; } = string.Empty;

        public string? BookingId { get; set; }

        public string? EntryId { get; set; }

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

        [Display(Name = "Paid in full")]
        public bool IsPaid { get; set; }

        [StringLength(500)]
        public string? CancellationPolicy { get; set; }

        [Display(Name = "Confirmation details")]
        [DataType(DataType.MultilineText)]
        public string? ConfirmationDetails { get; set; }

        [Display(Name = "Manage booking URL")]
        [DataType(DataType.Url)]
        [Url]
        public string? ConfirmationUrl { get; set; }

        [Display(Name = "Check-in time")]
        [StringLength(100)]
        public string? StayCheckInTime { get; set; }

        [Display(Name = "Check-out time")]
        [StringLength(100)]
        public string? StayCheckOutTime { get; set; }

        [Display(Name = "Room type")]
        [StringLength(150)]
        public string? StayRoomType { get; set; }

        [Display(Name = "Includes")]
        public List<string> StayIncludes { get; set; } = new();

        public BookingMutation ToMutation()
            => new(
                Normalize(EntryId),
                TimelineItemType.Other,
                Normalize(Vendor),
                Normalize(Reference),
                Cost,
                Normalize(Currency)?.ToUpperInvariant(),
                IsRefundable,
                IsPaid,
                Normalize(CancellationPolicy),
                string.IsNullOrWhiteSpace(ConfirmationDetails) ? null : ConfirmationDetails.Trim(),
                ParseUri(ConfirmationUrl),
                BuildMetadata());

        private static string? Normalize(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static Uri? ParseUri(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ? uri : null;
        }

        private BookingMetadata? BuildMetadata()
        {
            var stay = new StayBookingMetadata(
                Normalize(StayCheckInTime),
                Normalize(StayCheckOutTime),
                Normalize(StayRoomType),
                NormalizeIncludes(StayIncludes));

            return stay.HasContent ? new BookingMetadata(stay) : null;
        }

        private static IReadOnlyList<string>? NormalizeIncludes(IEnumerable<string>? values)
        {
            if (values is null)
            {
                return null;
            }

            var normalized = values
                .Select(value => Normalize(value))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => StayInclusionLabels.FirstOrDefault(option => option.Equals(value!, StringComparison.OrdinalIgnoreCase)))
                .Where(value => value is not null)
                .Select(value => value!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return normalized.Count > 0 ? normalized : null;
        }
    }

    public sealed class ReorderEntriesRequest
    {
        public string TripId { get; set; } = string.Empty;

        public string Date { get; set; } = string.Empty;

        public List<string> EntryIds { get; set; } = new();
    }

    private static IReadOnlyDictionary<TimelineItemType, SelectListGroup> TimelineItemGroups { get; } = CreateTimelineItemGroups();

    private static readonly IReadOnlyList<string> StayInclusionLabels = new[]
    {
        "Breakfast",
        "Lunch",
        "Dinner",
        "Lounge Access"
    };

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

    private static IReadOnlyList<SelectListItem> BuildStayInclusionOptions()
        => StayInclusionLabels
            .Select(value => new SelectListItem(value, value))
            .ToList();

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
            [TimelineItemType.PrivateCar] = travel,
            [TimelineItemType.RentalCar] = travel,
            [TimelineItemType.Parking] = travel,
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

}
