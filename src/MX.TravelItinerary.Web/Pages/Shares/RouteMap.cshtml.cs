using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using MX.TravelItinerary.Web.Data;
using MX.TravelItinerary.Web.Data.Models;
using MX.TravelItinerary.Web.Options;
using MX.TravelItinerary.Web.Pages.Trips;

namespace MX.TravelItinerary.Web.Pages.Shares;

[AllowAnonymous]
public sealed class ShareRouteMapModel : PageModel
{
    private static readonly JsonSerializerOptions RouteSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IItineraryRepository _repository;

    public ShareRouteMapModel(IItineraryRepository repository, IOptions<GoogleMapsOptions> googleMapsOptions)
    {
        _repository = repository;
        GoogleMapsApiKey = googleMapsOptions?.Value.ApiKey?.Trim();
    }

    [BindProperty(SupportsGet = true)]
    public string? TripSlug { get; set; }

    [BindProperty(SupportsGet = true)]
    public string ShareCode { get; set; } = string.Empty;

    public TripDetails? TripDetails { get; private set; }

    public IReadOnlyList<RouteMapPoint> RoutePoints { get; private set; } = [];

    public string? ErrorMessage { get; private set; }

    public string? GoogleMapsApiKey { get; }

    public bool IsGoogleMapsConfigured => !string.IsNullOrWhiteSpace(GoogleMapsApiKey);

    public bool HasRoutePoints => RoutePoints.Count > 0;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ShareCode))
        {
            ErrorMessage = "This share link is missing a code.";
            return Page();
        }

        var details = await _repository.GetTripByShareCodeAsync(ShareCode, cancellationToken);
        if (details is null)
        {
            ErrorMessage = "This share link has expired or is no longer available.";
            return Page();
        }

        TripDetails = details;
        RoutePoints = RouteMapBuilder.BuildRoutePoints(details);

        var canonicalSlug = string.IsNullOrWhiteSpace(details.Trip.Slug)
            ? details.Trip.TripId
            : details.Trip.Slug;

        if (!string.IsNullOrWhiteSpace(canonicalSlug) && !string.Equals(canonicalSlug, TripSlug, StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToPage(new { tripSlug = canonicalSlug, shareCode = ShareCode });
        }

        TripSlug = canonicalSlug;
        return Page();
    }

    public string GetSerializedRoutePoints()
        => JsonSerializer.Serialize(RoutePoints, RouteSerializerOptions);
}
