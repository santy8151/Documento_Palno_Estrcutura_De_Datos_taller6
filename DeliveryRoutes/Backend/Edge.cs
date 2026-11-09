namespace Backend;

/// <summary>A directed arc stored in an adjacency list.</summary>
public readonly record struct Edge<T>(T To, double Weight);
