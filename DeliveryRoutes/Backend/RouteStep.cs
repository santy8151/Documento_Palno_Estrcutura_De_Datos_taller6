namespace Backend;

/// <summary>One numbered stop of the printed route: where the courier goes and what happens there.</summary>
public record RouteStep(
    int Order,
    string From,
    string To,
    double Meters,
    IReadOnlyList<Stop> Actions,
    IReadOnlyList<string> Path)
{
    /// <summary>False for the first stop when the courier already starts at the first task location.</summary>
    public bool Travels => From != To;
}
