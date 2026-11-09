using System.Globalization;
using System.Text;
using Backend;

namespace Terminal;

/// <summary>Colored terminal front end. With colors off it prints the exact plain format of section 5.</summary>
public class ConsoleUi
{
    private const int BoxWidth = 58;
    private const int BarWidth = 24;

    private readonly bool useColor;

    public ConsoleUi(bool useColor)
    {
        this.useColor = useColor;
        Console.OutputEncoding = new UTF8Encoding(false);
    }

    public static void PrintErrors(IReadOnlyList<string> errors)
    {
        Console.Error.WriteLine($"Se encontraron {errors.Count} problema(s) en los datos:");
        foreach (string error in errors)
        {
            Console.Error.WriteLine($"  - {error}");
        }
    }

    public void PrintReport(RouteReport report)
    {
        if (!useColor)
        {
            Console.Write(RoutePrinter.Build(report));
            return;
        }

        PrintBanner();
        Console.WriteLine($"{Dim("Posición inicial:")} {Bold(Cyan(report.Start))}");
        Console.WriteLine(
            $"{Dim("Paquetes:")} {report.PackageCount}  {Dim("Ubicaciones:")} {report.LocationCount}  " +
            $"{Dim("Calles:")} {report.StreetCount} {Dim($"({report.OneWayCount} de un solo sentido)")}");
        Console.WriteLine();

        foreach (RouteStep step in report.Steps)
        {
            PrintStep(step);
        }

        Console.WriteLine();
        Console.WriteLine(Green("Todos los paquetes fueron recogidos y entregados."));
        Console.WriteLine($"{Bold("Recorrido total:")} {Bold(Yellow(RoutePrinter.FormatKilometers(report.TotalMeters) + " km"))}");
        Console.WriteLine();
        PrintComparison(report);
    }

    public void PrintWebAddress(string url)
    {
        Console.WriteLine();
        Console.WriteLine($"{Bold("Mapa interactivo:")} {Cyan(url)}  {Dim("(Ctrl+C para cerrar)")}");
    }

    private void PrintBanner()
    {
        Console.WriteLine(Cyan("╭" + new string('─', BoxWidth) + "╮"));
        Console.WriteLine(Cyan("│") + Bold("  Delivery Routes".PadRight(BoxWidth)) + Cyan("│"));
        Console.WriteLine(Cyan("│") + Dim("  Ruta más corta para recoger y entregar paquetes".PadRight(BoxWidth)) + Cyan("│"));
        Console.WriteLine(Cyan("╰" + new string('─', BoxWidth) + "╯"));
        Console.WriteLine();
    }

    private void PrintStep(RouteStep step)
    {
        string number = Dim($"{step.Order,3}.");
        string actions = RoutePrinter.DescribeActions(step.Actions, Magenta, Green);

        if (step.Travels)
        {
            string meters = Yellow($"{RoutePrinter.FormatMeters(step.Meters)} m");
            Console.WriteLine($"{number} Ir de {Bold(step.From)} a {Bold(step.To)} ({meters}): {actions}.");
            Console.WriteLine($"     {Dim("Camino:")} {string.Join(Dim(" -> "), step.Path.Select(Cyan))}");
        }
        else
        {
            Console.WriteLine($"{number} En {Bold(step.To)}: {actions}.");
        }
    }

    private void PrintComparison(RouteReport report)
    {
        double ratio = report.GreedyMeters > 0 ? Math.Min(1, report.TotalMeters / report.GreedyMeters) : 1;
        string saved = ((1 - ratio) * 100).ToString("F1", CultureInfo.InvariantCulture);

        Console.WriteLine(
            $"{Dim("Vecino más cercano:")} {RoutePrinter.FormatKilometers(report.GreedyMeters),6} km  {Red(Bar(1))}");
        Console.WriteLine(
            $"{Dim("Ruta mejorada:")}      {RoutePrinter.FormatKilometers(report.TotalMeters),6} km  {Green(Bar(ratio))}  {Green($"-{saved} %")}");
        Console.WriteLine(Dim($"Calculada en {report.Elapsed.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture)} s"));
    }

    private static string Bar(double fraction)
    {
        int filled = Math.Clamp((int)Math.Round(fraction * BarWidth), 0, BarWidth);
        return new string('█', filled) + new string('░', BarWidth - filled);
    }

    private string Paint(string text, string code) => useColor ? $"\u001b[{code}m{text}\u001b[0m" : text;

    private string Bold(string text) => Paint(text, "1");

    private string Dim(string text) => Paint(text, "2");

    private string Red(string text) => Paint(text, "31");

    private string Green(string text) => Paint(text, "32");

    private string Yellow(string text) => Paint(text, "33");

    private string Magenta(string text) => Paint(text, "35");

    private string Cyan(string text) => Paint(text, "36");
}
