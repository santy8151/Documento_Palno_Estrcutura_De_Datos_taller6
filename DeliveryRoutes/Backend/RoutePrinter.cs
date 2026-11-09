using System.Globalization;
using System.Text;

namespace Backend;

/// <summary>Builds the plain-text output of section 5 and the text pieces the other front ends reuse.</summary>
public static class RoutePrinter
{
    public static string FormatMeters(double meters) => meters.ToString("0.##", CultureInfo.InvariantCulture);

    public static string FormatKilometers(double meters) => (meters / 1000).ToString("F2", CultureInfo.InvariantCulture);

    /// <summary>"1", "1 y 2", "1, 2 y 3".</summary>
    public static string JoinNumbers(IReadOnlyList<int> numbers)
    {
        if (numbers.Count <= 1)
        {
            return string.Join("", numbers);
        }

        string head = string.Join(", ", numbers.Take(numbers.Count - 1));
        return $"{head} y {numbers[^1]}";
    }

    /// <summary>"entregar paquetes 14 y 15 y recoger paquete 27". Optional styles let the terminal add colors.</summary>
    public static string DescribeActions(
        IReadOnlyList<Stop> actions,
        Func<string, string>? styleDelivery = null,
        Func<string, string>? stylePickup = null)
    {
        var deliveries = actions.Where(stop => stop.Kind == StopKind.Delivery).Select(stop => stop.PackageId).ToList();
        var pickups = actions.Where(stop => stop.Kind == StopKind.Pickup).Select(stop => stop.PackageId).ToList();
        var parts = new List<string>();

        if (deliveries.Count > 0)
        {
            string text = $"entregar {Noun(deliveries.Count)} {JoinNumbers(deliveries)}";
            parts.Add(styleDelivery is null ? text : styleDelivery(text));
        }

        if (pickups.Count > 0)
        {
            string text = $"recoger {Noun(pickups.Count)} {JoinNumbers(pickups)}";
            parts.Add(stylePickup is null ? text : stylePickup(text));
        }

        return string.Join(" y ", parts);
    }

    public static string Build(RouteReport report)
    {
        var text = new StringBuilder();
        text.AppendLine($"Posición inicial: {report.Start}");
        text.AppendLine(
            $"Paquetes: {report.PackageCount}  Ubicaciones: {report.LocationCount}  " +
            $"Calles: {report.StreetCount} ({report.OneWayCount} de un solo sentido)");
        text.AppendLine();

        foreach (RouteStep step in report.Steps)
        {
            string actions = DescribeActions(step.Actions);
            if (step.Travels)
            {
                text.AppendLine($"{step.Order}. Ir de {step.From} a {step.To} ({FormatMeters(step.Meters)} m): {actions}.");
                text.AppendLine($"   Camino: {string.Join(" -> ", step.Path)}");
            }
            else
            {
                text.AppendLine($"{step.Order}. En {step.To}: {actions}.");
            }
        }

        text.AppendLine();
        text.AppendLine("Todos los paquetes fueron recogidos y entregados.");
        text.AppendLine($"Recorrido total: {FormatKilometers(report.TotalMeters)} km");
        return text.ToString();
    }

    private static string Noun(int count) => count == 1 ? "paquete" : "paquetes";
}
