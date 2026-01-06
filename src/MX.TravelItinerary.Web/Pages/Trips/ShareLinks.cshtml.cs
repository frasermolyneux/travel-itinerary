using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Identity.Web;
using MX.TravelItinerary.Web.Data;
using MX.TravelItinerary.Web.Data.Models;

namespace MX.TravelItinerary.Web.Pages.Trips;

public sealed class ShareLinksModel : PageModel
{
    private readonly IItineraryRepository _repository;
    private readonly ILogger<ShareLinksModel> _logger;

    public ShareLinksModel(IItineraryRepository repository, ILogger<ShareLinksModel> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string TripId { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? TripSlug { get; set; }

    public TripDetails? TripDetails { get; private set; }

    public IReadOnlyList<ShareLink> ShareLinks { get; private set; } = Array.Empty<ShareLink>();

    [BindProperty]
    public ShareLinkForm Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public bool IsEditing => !string.IsNullOrWhiteSpace(Input.ShareCode);

    public async Task<IActionResult> OnGetAsync(string? edit, CancellationToken cancellationToken)
    {
        if (!await LoadTripAsync(cancellationToken))
        {
            return NotFound();
        }

        await LoadShareLinksAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(edit))
        {
            var link = ShareLinks.FirstOrDefault(link => link.ShareCode.Equals(edit, StringComparison.OrdinalIgnoreCase));
            if (link is not null)
            {
                Input = ShareLinkForm.FromShareLink(link);
            }
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        if (!await LoadTripAsync(cancellationToken))
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            await LoadShareLinksAsync(cancellationToken);
            return Page();
        }

        var userId = GetUserId();
        var mutation = Input.ToMutation();
        var customShareCode = string.IsNullOrWhiteSpace(Input.ShareCode)
            ? NormalizeShareCode(Input.CustomShareCode)
            : null;
        Input.CustomShareCode = customShareCode;

        try
        {
            if (string.IsNullOrWhiteSpace(Input.ShareCode))
            {
                var created = await _repository.CreateShareLinkAsync(userId, TripId, mutation, cancellationToken, customShareCode);
                StatusMessage = "Share link created.";
                return RedirectToPage(new { tripId = TripId, tripSlug = TripSlug, edit = created.ShareCode });
            }

            var updated = await _repository.UpdateShareLinkAsync(userId, TripId, Input.ShareCode, mutation, cancellationToken);
            if (updated is null)
            {
                ModelState.AddModelError(string.Empty, "Unable to find the selected share link.");
                await LoadShareLinksAsync(cancellationToken);
                return Page();
            }

            StatusMessage = "Share link updated.";
            return RedirectToPage(new { tripId = TripId, tripSlug = TripSlug, edit = updated.ShareCode });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.IsNullOrWhiteSpace(Input.ShareCode) ? nameof(Input.CustomShareCode) : string.Empty, ex.Message);
            await LoadShareLinksAsync(cancellationToken);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(string shareCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(shareCode))
        {
            return RedirectToPage(new { tripId = TripId, tripSlug = TripSlug });
        }

        if (!await LoadTripAsync(cancellationToken))
        {
            return NotFound();
        }

        var userId = GetUserId();
        await _repository.DeleteShareLinkAsync(userId, TripId, shareCode, cancellationToken);
        StatusMessage = "Share link deleted.";
        return RedirectToPage(new { tripId = TripId, tripSlug = TripSlug });
    }

    public string BuildShareUrl(ShareLink link)
    {
        var slug = TripDetails?.Trip.Slug;
        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = TripDetails?.Trip.TripId;
        }

        return Url.Page(
            "/Shares/View",
            pageHandler: null,
            values: new { tripSlug = slug, shareCode = link.ShareCode },
            protocol: Request.Scheme) ?? string.Empty;
    }

    private static string? NormalizeShareCode(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

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

        if (details is null || details.CurrentUserPermission != TripPermission.Owner)
        {
            return false;
        }

        TripDetails = details;
        TripId = details.Trip.TripId;
        TripSlug = details.Trip.Slug;
        Input.TripId = details.Trip.TripId;
        return true;
    }

    private async Task LoadShareLinksAsync(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        ShareLinks = await _repository.GetShareLinksAsync(userId, TripId, cancellationToken);
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

    public sealed class ShareLinkForm
    {
        public string TripId { get; set; } = string.Empty;

        public string? ShareCode { get; set; }

        [Display(Name = "Custom share code")]
        [RegularExpression("^(|[A-HJ-NP-Za-hj-np-z2-9]{4,32})$", ErrorMessage = "Use 4-32 characters from A-H, J-N, P-Z and digits 2-9 or leave blank to auto-generate.")]
        public string? CustomShareCode { get; set; }

        [Display(Name = "Expires on")]
        [DataType(DataType.DateTime)]
        public DateTime? ExpiresOn { get; set; }

        [Display(Name = "Hide bookings")]
        public bool MaskBookings { get; set; }

        [Display(Name = "Show booking cost")]
        public bool IncludeCost { get; set; } = true;

        [Display(Name = "Show booking confirmations")]
        public bool ShowBookingConfirmations { get; set; } = true;

        [Display(Name = "Show booking metadata")]
        public bool ShowBookingMetadata { get; set; } = true;

        [StringLength(500)]
        public string? Notes { get; set; }

        public ShareLinkMutation ToMutation()
        {
            DateTimeOffset? expires = null;
            if (ExpiresOn is { } dateTime)
            {
                var specified = DateTime.SpecifyKind(dateTime, DateTimeKind.Local);
                expires = new DateTimeOffset(specified).ToUniversalTime();
            }

            return new ShareLinkMutation(
                expires,
                MaskBookings,
                IncludeCost,
                ShowBookingConfirmations,
                ShowBookingMetadata,
                Normalize(Notes));
        }

        public static ShareLinkForm FromShareLink(ShareLink link)
            => new()
            {
                TripId = link.TripId,
                ShareCode = link.ShareCode,
                CustomShareCode = null,
                ExpiresOn = link.ExpiresOn?.LocalDateTime,
                MaskBookings = link.MaskBookings,
                IncludeCost = link.IncludeCost,
                ShowBookingConfirmations = link.ShowBookingConfirmations,
                ShowBookingMetadata = link.ShowBookingMetadata,
                Notes = link.Notes
            };

        private static string? Normalize(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
