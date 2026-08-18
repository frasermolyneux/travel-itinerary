using System.Reflection;

namespace MX.TravelItinerary.Web;

public static class InfoEndpointExtensions
{
    public static WebApplication MapInfoEndpoint(this WebApplication app)
    {
        app.MapGet("/info", () =>
        {
            var assembly = Assembly.GetExecutingAssembly();
            var informationalVersion = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion ?? "unknown";
            var assemblyVersion = assembly.GetName().Version?.ToString() ?? "unknown";

            // Reconstruct NerdBank.GitVersioning's NuGetPackageVersion so the reported
            // build version matches the CI-produced build_version on every branch:
            // public releases report the clean base version (e.g. 1.0.66) while
            // non-public builds append the "-g<shortCommit>" suffix (e.g. 1.0.66-g0d399e7883).
            var versionParts = informationalVersion.Split('+');
            var baseVersion = versionParts[0];
            var buildVersion = ThisAssembly.IsPublicRelease || versionParts.Length < 2
                ? baseVersion
                : $"{baseVersion}-g{versionParts[1]}";

            return Results.Ok(new
            {
                Version = informationalVersion,
                BuildVersion = buildVersion,
                AssemblyVersion = assemblyVersion
            });
        }).AllowAnonymous();

        return app;
    }
}
