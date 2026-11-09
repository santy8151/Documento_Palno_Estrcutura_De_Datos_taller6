using System.Diagnostics;
using Backend;
using Terminal;
using Web;

try
{
    CliOptions options = CliOptions.Parse(args);
    var ui = new ConsoleUi(options.UseColor);

    CityData city = CityFile.Load(options.CityPath);
    List<Package> packages = PackageFile.Load(options.PackagesPath, city.Graph);

    var service = new RouteService(city, packages);
    RouteReport report = service.Compute(options.Start);
    ui.PrintReport(report);

    if (options.Web)
    {
        string url = $"http://localhost:{options.Port}/";
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        var server = new WebServer(city, packages, service, options.Start);
        Task running = server.RunAsync(options.Port, cancellation.Token);
        ui.PrintWebAddress(url);

        if (options.OpenBrowser)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception)
            {
                // The address is already printed; opening the browser is only a convenience.
            }
        }

        await running;
    }

    return 0;
}
catch (DataValidationException exception)
{
    ConsoleUi.PrintErrors(exception.Errors);
    return 1;
}
catch (System.Net.HttpListenerException exception)
{
    Console.Error.WriteLine($"No se pudo abrir el servidor local (¿el puerto está ocupado? prueba --port 5001): {exception.Message}");
    return 1;
}
