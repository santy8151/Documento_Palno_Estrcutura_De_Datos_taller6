namespace Backend;

public enum StopKind
{
    Pickup,
    Delivery,
}

/// <summary>One task of the route: a place, what is done there, and for which package.</summary>
public readonly record struct Stop(string Location, StopKind Kind, int PackageId);
