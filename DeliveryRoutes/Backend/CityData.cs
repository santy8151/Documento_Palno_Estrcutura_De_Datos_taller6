namespace Backend;

/// <summary>One street exactly as it appears in the city file.</summary>
public readonly record struct Street(string From, string To, double Meters, bool OneWay);

/// <summary>The loaded map: the directed graph plus the original list of streets.</summary>
public record CityData(Graph<string> Graph, IReadOnlyList<Street> Streets)
{
    public int OneWayCount => Streets.Count(street => street.OneWay);
}
