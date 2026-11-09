using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Backend;

namespace Web;

/// <summary>Tiny localhost server: serves the static page and one JSON endpoint with the route.</summary>
public class WebServer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly Dictionary<string, string> ContentTypes = new()
    {
        [".html"] = "text/html; charset=utf-8",
        [".css"] = "text/css; charset=utf-8",
        [".js"] = "text/javascript; charset=utf-8",
        [".svg"] = "image/svg+xml",
        [".json"] = "application/json; charset=utf-8",
    };

    private readonly CityData city;
    private readonly IReadOnlyList<Package> packages;
    private readonly RouteService service;
    private readonly string defaultStart;
    private readonly string webRoot = Path.Combine(AppContext.BaseDirectory, "Web", "public");

    public WebServer(CityData city, IReadOnlyList<Package> packages, RouteService service, string defaultStart)
    {
        this.city = city;
        this.packages = packages;
        this.service = service;
        this.defaultStart = defaultStart;
    }

    public async Task RunAsync(int port, CancellationToken cancellation)
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Start();
        cancellation.Register(listener.Stop);

        while (!cancellation.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (Exception) when (!listener.IsListening || cancellation.IsCancellationRequested)
            {
                break;
            }

            _ = Task.Run(() => HandleAsync(context));
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        try
        {
            string path = context.Request.Url?.AbsolutePath ?? "/";
            if (path == "/api/route")
            {
                await WriteRouteAsync(context);
            }
            else
            {
                await WriteStaticFileAsync(context, path);
            }
        }
        catch (Exception exception)
        {
            await WriteJsonAsync(context, 500, new { error = exception.Message });
        }
        finally
        {
            context.Response.Close();
        }
    }

    private async Task WriteRouteAsync(HttpListenerContext context)
    {
        string start = context.Request.QueryString["start"] ?? defaultStart;
        if (!city.Graph.HasVertex(start))
        {
            await WriteJsonAsync(context, 400, new { error = $"La posición inicial '{start}' no existe en el mapa." });
            return;
        }

        RouteReport report;
        try
        {
            report = await Task.Run(() => service.Compute(start));
        }
        catch (DataValidationException exception)
        {
            await WriteJsonAsync(context, 400, new { error = string.Join(" ", exception.Errors) });
            return;
        }

        var locations = city.Graph.Vertices
            .OrderBy(location => location.Length)
            .ThenBy(location => location, StringComparer.Ordinal)
            .ToList();

        var payload = new
        {
            report.Start,
            report.PackageCount,
            report.LocationCount,
            report.StreetCount,
            report.OneWayCount,
            report.TotalMeters,
            report.GreedyMeters,
            ElapsedMilliseconds = report.Elapsed.TotalMilliseconds,
            DefaultStart = defaultStart,
            Locations = locations,
            Streets = city.Streets,
            Packages = packages,
            report.Steps,
        };

        await WriteJsonAsync(context, 200, payload);
    }

    private async Task WriteStaticFileAsync(HttpListenerContext context, string requestPath)
    {
        string relative = requestPath == "/" ? "index.html" : requestPath.TrimStart('/');
        string fullPath = Path.GetFullPath(Path.Combine(webRoot, relative));

        // Never serve anything outside the wwwroot folder.
        if (!fullPath.StartsWith(Path.GetFullPath(webRoot), StringComparison.Ordinal) || !File.Exists(fullPath))
        {
            await WriteJsonAsync(context, 404, new { error = "No encontrado." });
            return;
        }

        string extension = Path.GetExtension(fullPath).ToLowerInvariant();
        context.Response.ContentType = ContentTypes.GetValueOrDefault(extension, "application/octet-stream");
        byte[] content = await File.ReadAllBytesAsync(fullPath);
        context.Response.ContentLength64 = content.Length;
        await context.Response.OutputStream.WriteAsync(content);
    }

    private static async Task WriteJsonAsync(HttpListenerContext context, int statusCode, object payload)
    {
        byte[] content = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonOptions));
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.ContentLength64 = content.Length;
        await context.Response.OutputStream.WriteAsync(content);
    }
}
