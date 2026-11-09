namespace Backend;

/// <summary>Weighted graph backed by an adjacency list.</summary>
public class Graph<T> where T : notnull
{
    private readonly Dictionary<T, List<Edge<T>>> adjacency = new();

    public Graph(bool directed = false)
    {
        Directed = directed;
    }

    public bool Directed { get; }

    public int VertexCount => adjacency.Count;

    public IEnumerable<T> Vertices => adjacency.Keys;

    public bool HasVertex(T vertex) => adjacency.ContainsKey(vertex);

    public void AddVertex(T vertex)
    {
        if (!adjacency.ContainsKey(vertex))
        {
            adjacency[vertex] = new List<Edge<T>>();
        }
    }

    /// <summary>Adds an arc from -> to. In an undirected graph the reverse arc is added too.</summary>
    public void AddEdge(T from, T to, double weight)
    {
        AddVertex(from);
        AddVertex(to);
        adjacency[from].Add(new Edge<T>(to, weight));

        if (!Directed)
        {
            adjacency[to].Add(new Edge<T>(from, weight));
        }
    }

    public IReadOnlyList<Edge<T>> Neighbors(T vertex) => adjacency[vertex];
}
