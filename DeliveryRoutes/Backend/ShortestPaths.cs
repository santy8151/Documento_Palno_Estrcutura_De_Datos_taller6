namespace Backend;

/// <summary>Result of one Dijkstra run: distances and predecessors from a single source.</summary>
public record ShortestPathResult(Dictionary<string, double> Distance, Dictionary<string, string> Previous);

public static class ShortestPaths
{
    public static ShortestPathResult Dijkstra(Graph<string> graph, string source)
    {
        var distance = graph.Vertices.ToDictionary(vertex => vertex, _ => double.PositiveInfinity);
        var previous = new Dictionary<string, string>();
        var queue = new PriorityQueue<string, double>();

        distance[source] = 0;
        queue.Enqueue(source, 0);

        while (queue.TryDequeue(out string? current, out double priority))
        {
            // The queue has no decrease-key, so outdated entries are skipped here.
            if (priority > distance[current])
            {
                continue;
            }

            foreach (Edge<string> edge in graph.Neighbors(current))
            {
                double candidate = distance[current] + edge.Weight;
                if (candidate < distance[edge.To])
                {
                    distance[edge.To] = candidate;
                    previous[edge.To] = current;
                    queue.Enqueue(edge.To, candidate);
                }
            }
        }

        return new ShortestPathResult(distance, previous);
    }

    /// <summary>Walks the predecessors back from target to source. Empty when target is unreachable.</summary>
    public static List<string> ReconstructPath(IReadOnlyDictionary<string, string> previous, string source, string target)
    {
        var path = new List<string> { target };
        string current = target;

        while (current != source)
        {
            if (!previous.TryGetValue(current, out string? parent))
            {
                return new List<string>();
            }

            path.Add(parent);
            current = parent;
        }

        path.Reverse();
        return path;
    }
}
