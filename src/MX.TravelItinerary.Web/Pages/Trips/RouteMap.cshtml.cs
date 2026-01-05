using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using MX.TravelItinerary.Web.Data;
using MX.TravelItinerary.Web.Data.Models;
using MX.TravelItinerary.Web.Options;

namespace MX.TravelItinerary.Web.Pages.Trips;

public sealed class RouteMapModel : PageModel
{
    private static readonly JsonSerializerOptions RouteSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IItineraryRepository _repository;
    private readonly ILogger<RouteMapModel> _logger;

    public RouteMapModel(IItineraryRepository repository, ILogger<RouteMapModel> logger, IOptions<GoogleMapsOptions> googleMapsOptions)
    {
        _repository = repository;
        _logger = logger;
        GoogleMapsApiKey = googleMapsOptions?.Value.ApiKey?.Trim();
    }

    [BindProperty(SupportsGet = true)]
    public string TripId { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? TripSlug { get; set; }

    public TripDetails? TripDetails { get; private set; }

    public IReadOnlyList<RouteMapPoint> RoutePoints { get; private set; } = Array.Empty<RouteMapPoint>();

    public string? GoogleMapsApiKey { get; }

    public bool IsGoogleMapsConfigured => !string.IsNullOrWhiteSpace(GoogleMapsApiKey);

    public bool HasRoutePoints => RoutePoints.Count > 0;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await LoadTripAsync(cancellationToken))
        {
            return NotFound();
        }

        RoutePoints = RouteMapBuilder.BuildRoutePoints(TripDetails);
        return Page();
    }

    public string GetSerializedRoutePoints()
        => JsonSerializer.Serialize(RoutePoints, RouteSerializerOptions);

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
}
