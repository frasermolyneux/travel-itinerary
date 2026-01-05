using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Web;
using MX.TravelItinerary.Web.Data;
using MX.TravelItinerary.Web.Data.Models;

namespace MX.TravelItinerary.Web.Pages.Trips;

public sealed class AccessModel : PageModel
{
    private readonly IItineraryRepository _repository;
    private readonly ILogger<AccessModel> _logger;

    public AccessModel(IItineraryRepository repository, ILogger<AccessModel> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string TripId { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? TripSlug { get; set; }

    public TripDetails? TripDetails { get; private set; }

    public IReadOnlyList<TripAccess> AccessList { get; private set; } = Array.Empty<TripAccess>();

    [BindProperty]
    public AccessForm Input { get; set; } = new();

    public IReadOnlyList<SelectListItem> PermissionOptions { get; } = BuildPermissionOptions();

    [TempData]
    public string? StatusMessage { get; set; }

    public bool IsEditing => !string.IsNullOrWhiteSpace(Input.AccessId);

    public async Task<IActionResult> OnGetAsync(string? edit, CancellationToken cancellationToken)
    {
        if (!await LoadTripAsync(cancellationToken))
        {
            return NotFound();
        }

        await LoadAccessListAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(edit))
        {
            var access = AccessList.FirstOrDefault(x => x.AccessId.Equals(edit, StringComparison.OrdinalIgnoreCase));
            if (access is not null)
            {
                Input = AccessForm.FromAccess(access);
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
            await LoadAccessListAsync(cancellationToken);
            return Page();
        }

        var userId = GetUserId();
        var mutation = Input.ToMutation();

        if (string.IsNullOrWhiteSpace(Input.AccessId))
        {
            var created = await _repository.GrantTripAccessAsync(userId, TripId, mutation, cancellationToken);
            StatusMessage = "Access added.";
            return RedirectToPage(new { tripId = TripId, tripSlug = TripSlug, edit = created.AccessId });
        }
        else
        {
            var updated = await _repository.UpdateTripAccessAsync(userId, TripId, Input.AccessId, mutation, cancellationToken);
            if (updated is null)
            {
                ModelState.AddModelError(string.Empty, "Unable to find that person anymore.");
                await LoadAccessListAsync(cancellationToken);
                return Page();
            }

            StatusMessage = "Access updated.";
            return RedirectToPage(new { tripId = TripId, tripSlug = TripSlug, edit = updated.AccessId });
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(string accessId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(accessId))
        {
            return RedirectToPage(new { tripId = TripId, tripSlug = TripSlug });
        }

        if (!await LoadTripAsync(cancellationToken))
        {
            return NotFound();
        }

        var userId = GetUserId();
        await _repository.RevokeTripAccessAsync(userId, TripId, accessId, cancellationToken);
        StatusMessage = "Access removed.";
        return RedirectToPage(new { tripId = TripId, tripSlug = TripSlug });
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

    private async Task LoadAccessListAsync(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        AccessList = await _repository.GetTripAccessListAsync(userId, TripId, cancellationToken);
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

    private static IReadOnlyList<SelectListItem> BuildPermissionOptions()
    {
        return new List<SelectListItem>
        {
            new("Full control", TripPermission.FullControl.ToString()),
            new("Read only", TripPermission.ReadOnly.ToString())
        };
    }

    public sealed class AccessForm
    {
        public string TripId { get; set; } = string.Empty;

        public string? AccessId { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "Email address")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Permission")]
        [Required]
        public TripPermission Permission { get; set; } = TripPermission.FullControl;

        public TripAccessMutation ToMutation()
        {
            return new TripAccessMutation(
                Normalize(Email) ?? string.Empty,
                Permission);
        }

        public static AccessForm FromAccess(TripAccess access)
            => new()
            {
                TripId = access.TripId,
                AccessId = access.AccessId,
                Email = access.Email,
                Permission = access.Permission
            };

        private static string? Normalize(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
