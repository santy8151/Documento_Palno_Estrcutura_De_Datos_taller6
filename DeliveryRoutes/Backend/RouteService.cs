using System.Diagnostics;

namespace Backend;

/// <summary>Runs the planner for a starting point and caches the finished report.</summary>
public class RouteService
{
    private readonly CityData city;
    private readonly IReadOnlyList<Package> packages;
    private readonly Dictionary<string, RouteReport> cache = new();
    private readonly object gate = new();

    public RouteService(CityData city, IReadOnlyList<Package> packages)
    {
        this.city = city;
        this.packages = packages;
    }

    public RouteReport Compute(string start)
    {
        lock (gate)
        {
            if (cache.TryGetValue(start, out RouteReport? cached))
            {
                return cached;
            }

            var timer = Stopwatch.StartNew();
            var planner = new RoutePlanner(city.Graph, packages, start);
            List<Stop> route = planner.Plan();

            if (!planner.IsValid(route))
            {
                throw new InvalidOperationException("The planner produced an invalid route.");
            }

            List<RouteStep> steps = planner.BuildSteps(route);
            double total = planner.TotalDistance(route);

            if (Math.Abs(steps.Sum(step => step.Meters) - total) > 1e-6)
            {
                throw new InvalidOperationException("The sum of the legs does not match the total distance.");
            }

            timer.Stop();
            var report = new RouteReport(
                start,
                packages.Count,
                city.Graph.VertexCount,
                city.Streets.Count,
                city.OneWayCount,
                steps,
                total,
                planner.GreedyDistance,
                timer.Elapsed);

            cache[start] = report;
            return report;
        }
    }
}
