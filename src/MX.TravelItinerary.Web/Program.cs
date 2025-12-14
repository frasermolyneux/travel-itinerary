using Azure.Data.Tables;
using Azure.Identity;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using MX.TravelItinerary.Web.Data;
using MX.TravelItinerary.Web.Data.TableStorage;
using MX.TravelItinerary.Web.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));

builder.Services.AddSingleton<TableServiceClient>(sp =>
{
    var storageOptions = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
    if (string.IsNullOrWhiteSpace(storageOptions.TableEndpoint))
    {
        throw new InvalidOperationException("Storage:TableEndpoint must be configured.");
    }

    var credential = new DefaultAzureCredential();
    return new TableServiceClient(new Uri(storageOptions.TableEndpoint), credential);
});

builder.Services.AddSingleton<ITableContext, TableContext>();
builder.Services.AddScoped<IItineraryRepository, TableItineraryRepository>();

builder.Services
    .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(options =>
    {
        builder.Configuration.Bind("AzureAd", options);
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.UsePkce = true;
    });

builder.Services.AddAuthorization();

builder.Services
    .AddRazorPages(options =>
    {
        // Require auth for all Razor Pages by default but keep the public landing and error pages anonymous.
        options.Conventions.AuthorizeFolder("/");
        options.Conventions.AllowAnonymousToPage("/Index");
        options.Conventions.AllowAnonymousToPage("/Error");
        options.Conventions.AllowAnonymousToAreaFolder("MicrosoftIdentity", "/Account");
        options.Conventions.AddPageRoute(
            "/Trips/Details",
            "trips/{tripSlug:regex(^(?!index$)(?!details$)[a-z0-9-]+$)}");
    })
    .AddMicrosoftIdentityUI();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.Use(async (context, next) =>
{
    if (string.Equals(context.Request.Path.Value, "/trips", StringComparison.OrdinalIgnoreCase))
    {
        context.Request.Path = "/Trips/Index";
    }

    await next();
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapRazorPages()
    .WithStaticAssets();
app.MapStaticAssets();

app.Run();
