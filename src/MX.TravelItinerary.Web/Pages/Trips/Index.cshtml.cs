using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Identity.Web;
using MX.TravelItinerary.Web.Data;
using MX.TravelItinerary.Web.Data.Models;

namespace MX.TravelItinerary.Web.Pages.Trips;

public sealed class IndexModel : PageModel
{
    private readonly IItineraryRepository _repository;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(IItineraryRepository repository, ILogger<IndexModel> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public IReadOnlyList<Trip> Trips { get; private set; } = Array.Empty<Trip>();

    public IReadOnlyList<SavedShareLink> SavedShareLinks { get; private set; } = Array.Empty<SavedShareLink>();

    [BindProperty]
    public TripForm Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public bool IsEditing => !string.IsNullOrWhiteSpace(Input.TripId);

    public async Task<IActionResult> OnGetAsync(string? edit, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        Trips = await _repository.GetTripsForUserAsync(userId, GetUserEmail(), cancellationToken);
        SavedShareLinks = await _repository.GetSavedShareLinksAsync(userId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(edit))
        {
            var trip = Trips.FirstOrDefault(t => t.TripId == edit);
            if (trip is not null)
            {
                Input = TripForm.FromTrip(trip);
            }
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        await ValidateDatesAsync();
        if (!ModelState.IsValid)
        {
            Trips = await _repository.GetTripsForUserAsync(userId, GetUserEmail(), cancellationToken);
            return Page();
        }

        var mutation = Input.ToMutation();

        if (string.IsNullOrWhiteSpace(Input.TripId))
        {
            await _repository.CreateTripAsync(userId, mutation, cancellationToken);
            StatusMessage = "Trip created.";
        }
        else
        {
            var updated = await _repository.UpdateTripAsync(userId, GetUserEmail(), Input.TripId, mutation, cancellationToken);
            if (updated is null)
            {
                ModelState.AddModelError(string.Empty, "Unable to find the selected trip.");
                Trips = await _repository.GetTripsForUserAsync(userId, GetUserEmail(), cancellationToken);
                return Page();
            }

            StatusMessage = "Trip updated.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(string tripId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tripId))
        {
            return RedirectToPage();
        }

        var userId = GetUserId();
        await _repository.DeleteTripAsync(userId, GetUserEmail(), tripId, cancellationToken);
        StatusMessage = "Trip deleted.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRemoveSavedLinkAsync(string savedLinkId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(savedLinkId))
        {
            return RedirectToPage();
        }

        var userId = GetUserId();
        await _repository.DeleteSavedShareLinkAsync(userId, savedLinkId, cancellationToken);
        StatusMessage = "Saved trip removed.";
        return RedirectToPage();
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

    private Task ValidateDatesAsync()
    {
        if (Input.StartDate is not null && Input.EndDate is not null && Input.EndDate < Input.StartDate)
        {
            ModelState.AddModelError("Input.EndDate", "End date cannot be earlier than the start date.");
        }

        return Task.CompletedTask;
    }

    public sealed class TripForm
    {
        private static readonly Regex SlugRegex = new("[^a-z0-9-]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public string? TripId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Slug { get; set; }

        [DataType(DataType.Date)]
        public DateOnly? StartDate { get; set; }

        [DataType(DataType.Date)]
        public DateOnly? EndDate { get; set; }

        [StringLength(100)]
        public string? HomeTimeZone { get; set; }

        [StringLength(3, MinimumLength = 3)]
        public string? DefaultCurrency { get; set; }

        public TripMutation ToMutation()
        {
            var slugSource = string.IsNullOrWhiteSpace(Slug) ? Name : Slug!;
            var slug = GenerateSlug(slugSource);
            return new TripMutation(Name.Trim(), slug, StartDate, EndDate, Normalize(HomeTimeZone), NormalizeCurrency(DefaultCurrency));
        }

        public static TripForm FromTrip(Trip trip) => new()
        {
            TripId = trip.TripId,
            Name = trip.Name,
            Slug = trip.Slug,
            StartDate = trip.StartDate,
            EndDate = trip.EndDate,
            HomeTimeZone = trip.HomeTimeZone,
            DefaultCurrency = trip.DefaultCurrency
        };

        private static string? Normalize(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string? NormalizeCurrency(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

        private static string GenerateSlug(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Guid.NewGuid().ToString("N");
            }

            var slug = SlugRegex.Replace(value.Trim().ToLowerInvariant(), "-");
            slug = Regex.Replace(slug, "-+", "-").Trim('-');

            return string.IsNullOrEmpty(slug) ? Guid.NewGuid().ToString("N") : slug;
        }
    }
}
