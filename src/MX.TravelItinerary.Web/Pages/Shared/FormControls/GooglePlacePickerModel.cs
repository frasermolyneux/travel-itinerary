namespace MX.TravelItinerary.Web.Pages.Shared.FormControls;

public sealed record GooglePlacePickerModel(
    string FieldId,
    string FieldName,
    string Label,
    string? Value,
    bool PickerEnabled = true);
