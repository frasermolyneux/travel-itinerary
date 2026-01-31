using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Web;
using MX.TravelItinerary.Web.Data;
using MX.TravelItinerary.Web.Data.Models;

namespace MX.TravelItinerary.Web.Pages.Trips;

public sealed class CostSummaryModel : PageModel
{
    private readonly IItineraryRepository _repository;
    private readonly ILogger<CostSummaryModel> _logger;

    public CostSummaryModel(IItineraryRepository repository, ILogger<CostSummaryModel> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string TripId { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? TripSlug { get; set; }

    public TripDetails? TripDetails { get; private set; }

    public IReadOnlyList<CurrencyCostSummary> CurrencySummaries { get; private set; } = [];

    public decimal OverallTotal => CurrencySummaries.Sum(group => group.Total);

    public decimal OverallPaid => CurrencySummaries.Sum(group => group.PaidTotal);

    public decimal OverallUnpaid => CurrencySummaries.Sum(group => group.UnpaidTotal);

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await LoadTripAsync(cancellationToken))
        {
            return NotFound();
        }

        BuildCurrencySummaries();
        return Page();
    }

    private void BuildCurrencySummaries()
    {
        if (TripDetails is null)
        {
            CurrencySummaries = [];
            return;
        }

        var entries = TripDetails.Entries.ToDictionary(entry => entry.EntryId, StringComparer.OrdinalIgnoreCase);
        var defaultCurrency = TripDetails.Trip.DefaultCurrency?.Trim();
        var bookingRows = new List<BookingCostRow>();

        foreach (var booking in TripDetails.Bookings)
        {
            if (string.IsNullOrWhiteSpace(booking.EntryId))
            {
                continue;
            }

            if (!entries.TryGetValue(booking.EntryId, out var entry))
            {
                continue;
            }

            bookingRows.Add(BookingCostRow.From(entry, booking, defaultCurrency));
        }

        if (bookingRows.Count == 0)
        {
            CurrencySummaries = [];
            return;
        }

        CurrencySummaries = bookingRows
            .GroupBy(row => row.CurrencyKey)
            .Select(group =>
            {
                var orderedRows = group
                    .OrderBy(row => row.EntryDateSortKey, StringComparer.Ordinal)
                    .ThenBy(row => row.EntryTitle, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var total = orderedRows.Sum(row => row.Cost ?? 0m);
                var paid = orderedRows.Where(row => row.IsPaid is true).Sum(row => row.Cost ?? 0m);
                var unpaid = orderedRows.Where(row => row.IsPaid is not true).Sum(row => row.Cost ?? 0m);

                return new CurrencyCostSummary(
                    CurrencyKey: group.Key,
                    CurrencyDisplay: orderedRows[0].CurrencyDisplay,
                    Total: total,
                    PaidTotal: paid,
                    UnpaidTotal: unpaid,
                    Bookings: orderedRows);
            })
            .OrderBy(summary => summary.IsUnspecified ? 1 : 0)
            .ThenBy(summary => summary.CurrencyDisplay, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<bool> LoadTripAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(TripId) && string.IsNullOrWhiteSpace(TripSlug))
        {
            return false;
        }

        var userId = GetUserId();
        var userEmail = GetUserEmail();
        TripDetails? details = null;

        if (!string.IsNullOrWhiteSpace(TripId))
        {
            details = await _repository.GetTripAsync(userId, userEmail, TripId, cancellationToken);
        }

        if (details is null && !string.IsNullOrWhiteSpace(TripSlug))
        {
            details = await _repository.GetTripBySlugAsync(userId, userEmail, TripSlug, cancellationToken);
            if (details is null && Guid.TryParse(TripSlug, out _))
            {
                details = await _repository.GetTripAsync(userId, userEmail, TripSlug, cancellationToken);
            }
        }

        if (details is null)
        {
            return false;
        }

        TripDetails = details;
        TripId = details.Trip.TripId;
        TripSlug = details.Trip.Slug;
        return true;
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

    private string? GetUserEmail()
    {
        var email = User.FindFirstValue("preferred_username") ?? User.FindFirstValue(ClaimTypes.Email);
        return string.IsNullOrWhiteSpace(email) ? null : email;
    }

    public sealed record CurrencyCostSummary(
        string CurrencyKey,
        string CurrencyDisplay,
        decimal Total,
        decimal PaidTotal,
        decimal UnpaidTotal,
        IReadOnlyList<BookingCostRow> Bookings)
    {
        public bool IsUnspecified => string.Equals(CurrencyKey, BookingCostRow.UnspecifiedCurrency, StringComparison.OrdinalIgnoreCase);
    }

    public sealed record BookingCostRow(
        string EntryId,
        string EntryTitle,
        TimelineItemType EntryType,
        string EntryDateLabel,
        string EntryDateSortKey,
        string BookingLabel,
        string? BookingReference,
        decimal? Cost,
        bool? IsPaid,
        string CurrencyKey,
        string CurrencyDisplay)
    {
        public const string UnspecifiedCurrency = "UNSPECIFIED";

        public static BookingCostRow From(ItineraryEntry entry, Booking booking, string? defaultCurrency)
        {
            var entryTitle = string.IsNullOrWhiteSpace(entry.Title) ? entry.ToString() : entry.Title;
            var bookingLabel = string.IsNullOrWhiteSpace(booking.Vendor)
                ? booking.ItemType.GetDisplayName()
                : booking.Vendor;
            var currencyKey = NormalizeCurrency(booking.Currency, defaultCurrency);
            var currencyDisplay = currencyKey == UnspecifiedCurrency ? "Unspecified" : currencyKey;

            return new BookingCostRow(
                entry.EntryId,
                entryTitle,
                entry.ItemType,
                BuildDateLabel(entry),
                BuildDateSortKey(entry),
                bookingLabel,
                booking.Reference,
                booking.Cost,
                booking.IsPaid,
                currencyKey,
                currencyDisplay);
        }

        private static string BuildDateLabel(ItineraryEntry entry)
        {
            if (entry.IsMultiDay && entry.Date is { } start && entry.EndDate is { } end)
            {
                return $"{start:MMM dd} – {end:MMM dd}";
            }

            if (entry.Date is { } date)
            {
                return date.ToString("MMM dd, yyyy");
            }

            return "Date TBD";
        }

        private static string BuildDateSortKey(ItineraryEntry entry)
        {
            if (entry.Date is { } start)
            {
                return start.ToString("yyyy-MM-dd");
            }

            return "9999-12-31";
        }

        private static string NormalizeCurrency(string? bookingCurrency, string? defaultCurrency)
        {
            var source = !string.IsNullOrWhiteSpace(bookingCurrency)
                ? bookingCurrency
                : defaultCurrency;

            if (string.IsNullOrWhiteSpace(source))
            {
                return UnspecifiedCurrency;
            }

            return source.Trim().ToUpperInvariant();
        }
    }
}
