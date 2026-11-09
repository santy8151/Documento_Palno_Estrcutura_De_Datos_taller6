namespace Backend;

/// <summary>Everything the user interfaces need to show one computed route.</summary>
public record RouteReport(
    string Start,
    int PackageCount,
    int LocationCount,
    int StreetCount,
    int OneWayCount,
    IReadOnlyList<RouteStep> Steps,
    double TotalMeters,
    double GreedyMeters,
    TimeSpan Elapsed);
