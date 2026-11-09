namespace Backend;

/// <summary>
/// Orders the 2 * N stops of a pickup-and-delivery problem.
/// Distances come from Dijkstra (exact); the order comes from heuristics (approximate).
/// </summary>
public class RoutePlanner
{
    private const int RandomStarts = 40;
    private const int RuinAndRecreateIterations = 2500;
    private const int Seed = 42;
    private const double Epsilon = 1e-9;

    private readonly IReadOnlyList<Package> packages;
    private readonly string start;
    private readonly List<string> keyLocations = new();
    private readonly Dictionary<string, int> locationIndex = new();
    private readonly double[,] distance;
    private readonly Dictionary<string, string>[] previous;
    private readonly int[] stopLocation;
    private readonly int[] positionBuffer;
    private readonly int startIndex;
    private readonly Random random = new(Seed);

    public RoutePlanner(Graph<string> city, IReadOnlyList<Package> packages, string start)
    {
        if (!city.HasVertex(start))
        {
            throw new DataValidationException($"La posición inicial '{start}' no existe en el mapa.");
        }

        this.packages = packages;
        this.start = start;

        RegisterLocation(start);
        foreach (Package package in packages)
        {
            RegisterLocation(package.Pickup);
            RegisterLocation(package.Delivery);
        }

        startIndex = locationIndex[start];
        int size = keyLocations.Count;
        distance = new double[size, size];
        previous = new Dictionary<string, string>[size];

        // One Dijkstra per key location: after this the graph is no longer needed.
        for (int from = 0; from < size; from++)
        {
            ShortestPathResult result = ShortestPaths.Dijkstra(city, keyLocations[from]);
            previous[from] = result.Previous;
            for (int to = 0; to < size; to++)
            {
                distance[from, to] = result.Distance[keyLocations[to]];
            }
        }

        // Stop ids: package k -> pickup 2k, delivery 2k + 1.
        stopLocation = new int[packages.Count * 2];
        for (int k = 0; k < packages.Count; k++)
        {
            stopLocation[2 * k] = locationIndex[packages[k].Pickup];
            stopLocation[2 * k + 1] = locationIndex[packages[k].Delivery];
        }

        positionBuffer = new int[stopLocation.Length];
        CheckReachability();
    }

    /// <summary>Length of the plain nearest-neighbor route, kept for comparison. Set by Plan().</summary>
    public double GreedyDistance { get; private set; }

    /// <summary>The best route the heuristics find. Deterministic: the random seed is fixed.</summary>
    public List<Stop> Plan()
    {
        List<int> greedy = BuildNearestNeighborRoute();
        GreedyDistance = RouteCost(greedy);

        List<int> best = Improve(greedy);
        return best.Select(ToStop).ToList();
    }

    public double TotalDistance(List<Stop> route)
    {
        double total = 0;
        int current = startIndex;

        foreach (Stop stop in route)
        {
            int next = locationIndex[stop.Location];
            total += distance[current, next];
            current = next;
        }

        return total;
    }

    /// <summary>Each package is picked up once and delivered once, in that order, at its own locations.</summary>
    public bool IsValid(List<Stop> route)
    {
        if (route.Count != packages.Count * 2)
        {
            return false;
        }

        var byId = packages.ToDictionary(package => package.Id);
        var pickedUp = new HashSet<int>();
        var delivered = new HashSet<int>();

        foreach (Stop stop in route)
        {
            if (!byId.TryGetValue(stop.PackageId, out Package package))
            {
                return false;
            }

            if (stop.Kind == StopKind.Pickup)
            {
                if (stop.Location != package.Pickup || !pickedUp.Add(stop.PackageId))
                {
                    return false;
                }
            }
            else if (stop.Location != package.Delivery
                     || !pickedUp.Contains(stop.PackageId)
                     || !delivered.Add(stop.PackageId))
            {
                return false;
            }
        }

        return pickedUp.Count == packages.Count && delivered.Count == packages.Count;
    }

    /// <summary>Shortest street path between two key locations (start or any task location).</summary>
    public List<string> PathBetween(string from, string to)
    {
        return ShortestPaths.ReconstructPath(previous[locationIndex[from]], from, to);
    }

    /// <summary>Groups consecutive stops at the same place into numbered printable steps.</summary>
    public List<RouteStep> BuildSteps(List<Stop> route)
    {
        var steps = new List<RouteStep>();
        string current = start;
        int index = 0;

        while (index < route.Count)
        {
            string location = route[index].Location;
            var group = new List<Stop>();
            while (index < route.Count && route[index].Location == location)
            {
                group.Add(route[index]);
                index++;
            }

            bool travels = location != current;
            double meters = travels ? distance[locationIndex[current], locationIndex[location]] : 0;
            List<string> path = travels ? PathBetween(current, location) : new List<string> { location };

            steps.Add(new RouteStep(steps.Count + 1, current, location, meters, OrderActions(group), path));
            current = location;
        }

        return steps;
    }

    // ---------------------------------------------------------------- setup

    private void RegisterLocation(string location)
    {
        if (!locationIndex.ContainsKey(location))
        {
            locationIndex[location] = keyLocations.Count;
            keyLocations.Add(location);
        }
    }

    private void CheckReachability()
    {
        var taskLocations = stopLocation.Distinct().ToList();
        var errors = new List<string>();

        for (int from = 0; from < keyLocations.Count; from++)
        {
            foreach (int to in taskLocations)
            {
                if (from != to && double.IsPositiveInfinity(distance[from, to]))
                {
                    string origin = from == startIndex ? $"la posición inicial {start}" : $"el punto {keyLocations[from]}";
                    errors.Add($"Desde {origin} no se puede llegar al punto {keyLocations[to]} respetando el sentido de las calles.");
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new DataValidationException(errors.Take(10).ToList());
        }
    }

    // ---------------------------------------------------------------- stop helpers

    private static bool IsPickup(int stop) => stop % 2 == 0;

    private Stop ToStop(int stop)
    {
        Package package = packages[stop / 2];
        return IsPickup(stop)
            ? new Stop(package.Pickup, StopKind.Pickup, package.Id)
            : new Stop(package.Delivery, StopKind.Delivery, package.Id);
    }

    /// <summary>Deliveries first, then pickups, as the output format requires.</summary>
    private static List<Stop> OrderActions(List<Stop> group)
    {
        var pickedUpHere = group.Where(stop => stop.Kind == StopKind.Pickup).Select(stop => stop.PackageId).ToHashSet();

        int Rank(Stop stop)
        {
            if (stop.Kind == StopKind.Pickup)
            {
                return 1;
            }

            // A package picked up and delivered at the same place must keep that order.
            return pickedUpHere.Contains(stop.PackageId) ? 2 : 0;
        }

        return group.OrderBy(Rank).ThenBy(stop => stop.PackageId).ToList();
    }

    // ---------------------------------------------------------------- costs

    /// <summary>Distance of one leg. A negative destination means "end of the route": it costs nothing.</summary>
    private double Leg(int from, int to) => to < 0 ? 0 : distance[from, to];

    private double RouteCost(IReadOnlyList<int> route)
    {
        double total = 0;
        int current = startIndex;

        foreach (int stop in route)
        {
            total += distance[current, stopLocation[stop]];
            current = stopLocation[stop];
        }

        return total;
    }

    // ---------------------------------------------------------------- construction heuristics

    /// <summary>Greedy: always go to the closest valid stop. Fast and valid, but usually far from optimal.</summary>
    private List<int> BuildNearestNeighborRoute()
    {
        int count = stopLocation.Length;
        var done = new bool[count];
        var route = new List<int>(count);
        int current = startIndex;

        for (int step = 0; step < count; step++)
        {
            int bestStop = -1;
            double bestDistance = double.PositiveInfinity;

            for (int stop = 0; stop < count; stop++)
            {
                if (done[stop] || (!IsPickup(stop) && !done[stop - 1]))
                {
                    continue;
                }

                double candidate = distance[current, stopLocation[stop]];
                if (candidate < bestDistance - Epsilon)
                {
                    bestDistance = candidate;
                    bestStop = stop;
                }
            }

            done[bestStop] = true;
            route.Add(bestStop);
            current = stopLocation[bestStop];
        }

        return route;
    }

    private List<int> BuildByInsertion(List<int> packageOrder)
    {
        var route = new List<int>(packageOrder.Count * 2);
        foreach (int package in packageOrder)
        {
            InsertAtBestPosition(route, package);
        }

        return route;
    }

    /// <summary>
    /// Tries every pair of positions (pickup before delivery) and keeps the one that lengthens the route the least.
    /// Each candidate costs O(1) thanks to the precomputed distance table.
    /// </summary>
    private void InsertAtBestPosition(List<int> route, int package)
    {
        int pickup = 2 * package;
        int delivery = pickup + 1;
        int pickupPlace = stopLocation[pickup];
        int deliveryPlace = stopLocation[delivery];
        int count = route.Count;

        double bestDelta = double.PositiveInfinity;
        int bestPickupGap = 0;
        int bestDeliveryGap = 0;

        for (int i = 0; i <= count; i++)
        {
            int before = i == 0 ? startIndex : stopLocation[route[i - 1]];
            int after = i == count ? -1 : stopLocation[route[i]];

            double pickupDelta = Leg(before, pickupPlace) + Leg(pickupPlace, after) - Leg(before, after);

            // Pickup and delivery inserted side by side in the same gap.
            double together = Leg(before, pickupPlace) + distance[pickupPlace, deliveryPlace]
                + Leg(deliveryPlace, after) - Leg(before, after);
            if (together < bestDelta - Epsilon)
            {
                bestDelta = together;
                bestPickupGap = i;
                bestDeliveryGap = i;
            }

            for (int j = i + 1; j <= count; j++)
            {
                int beforeDelivery = stopLocation[route[j - 1]];
                int afterDelivery = j == count ? -1 : stopLocation[route[j]];
                double delta = pickupDelta + Leg(beforeDelivery, deliveryPlace)
                    + Leg(deliveryPlace, afterDelivery) - Leg(beforeDelivery, afterDelivery);

                if (delta < bestDelta - Epsilon)
                {
                    bestDelta = delta;
                    bestPickupGap = i;
                    bestDeliveryGap = j;
                }
            }
        }

        // Insert the delivery first so the pickup gap index stays valid.
        route.Insert(bestDeliveryGap, delivery);
        route.Insert(bestPickupGap, pickup);
    }

    // ---------------------------------------------------------------- improvement

    private List<int> Improve(List<int> greedy)
    {
        var best = new List<int>(greedy);
        LocalSearch(best);
        double bestCost = RouteCost(best);

        // Phase 1: several constructions by insertion with random package orders.
        for (int attempt = 0; attempt < RandomStarts; attempt++)
        {
            List<int> candidate = BuildByInsertion(ShuffledPackages());
            LocalSearch(candidate);
            double cost = RouteCost(candidate);

            if (cost < bestCost - Epsilon)
            {
                best = candidate;
                bestCost = cost;
            }
        }

        // Phase 2: ruin and recreate. Remove a few packages, reinsert them, repair, keep if not worse.
        var current = new List<int>(best);
        double currentCost = bestCost;

        for (int iteration = 0; iteration < RuinAndRecreateIterations; iteration++)
        {
            var candidate = new List<int>(current);
            RuinAndRecreate(candidate);
            LocalSearch(candidate);
            double cost = RouteCost(candidate);

            if (cost <= currentCost + Epsilon)
            {
                current = candidate;
                currentCost = cost;

                if (cost < bestCost - Epsilon)
                {
                    best = new List<int>(candidate);
                    bestCost = cost;
                }
            }
        }

        return best;
    }

    private List<int> ShuffledPackages()
    {
        var order = Enumerable.Range(0, packages.Count).ToList();
        for (int i = order.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }

        return order;
    }

    private void RuinAndRecreate(List<int> route)
    {
        List<int> order = ShuffledPackages();
        int removeCount = Math.Min(packages.Count, random.Next(2, 7));

        for (int i = 0; i < removeCount; i++)
        {
            RemovePackage(route, order[i]);
        }

        // The shuffle already randomized which packages left; reinsert them in a fresh random order.
        List<int> reinsertOrder = order.Take(removeCount).OrderBy(_ => random.Next()).ToList();
        foreach (int package in reinsertOrder)
        {
            InsertAtBestPosition(route, package);
        }
    }

    private static void RemovePackage(List<int> route, int package)
    {
        route.Remove(2 * package);
        route.Remove(2 * package + 1);
    }

    /// <summary>Repeats relocation and segment moves until neither finds an improvement.</summary>
    private void LocalSearch(List<int> route)
    {
        bool improved = true;
        while (improved)
        {
            improved = RelocatePackages(route);
            improved |= MoveSegments(route);
        }
    }

    /// <summary>Takes each package out and puts it back in its best position.</summary>
    private bool RelocatePackages(List<int> route)
    {
        bool anyImprovement = false;

        for (int package = 0; package < packages.Count; package++)
        {
            double before = RouteCost(route);
            var snapshot = new List<int>(route);

            RemovePackage(route, package);
            InsertAtBestPosition(route, package);

            if (RouteCost(route) < before - Epsilon)
            {
                anyImprovement = true;
            }
            else
            {
                route.Clear();
                route.AddRange(snapshot);
            }
        }

        return anyImprovement;
    }

    /// <summary>Or-opt: moves blocks of 1 to 3 consecutive stops to a better place, keeping pickup before delivery.</summary>
    private bool MoveSegments(List<int> route)
    {
        bool anyImprovement = false;

        for (int length = 1; length <= 3; length++)
        {
            for (int first = 0; first + length <= route.Count; first++)
            {
                if (TryMoveSegment(route, first, length))
                {
                    anyImprovement = true;
                }
            }
        }

        return anyImprovement;
    }

    private bool TryMoveSegment(List<int> route, int first, int length)
    {
        List<int> segment = route.GetRange(first, length);
        int firstPlace = stopLocation[segment[0]];
        int lastPlace = stopLocation[segment[^1]];
        int before = first == 0 ? startIndex : stopLocation[route[first - 1]];
        int after = first + length == route.Count ? -1 : stopLocation[route[first + length]];

        double removalGain = Leg(before, firstPlace) + Leg(lastPlace, after) - Leg(before, after);

        var reduced = new List<int>(route);
        reduced.RemoveRange(first, length);
        for (int k = 0; k < reduced.Count; k++)
        {
            positionBuffer[reduced[k]] = k;
        }

        // Gap g means "insert before reduced[g]". The bounds keep every pickup before its delivery.
        int lowGap = 0;
        int highGap = reduced.Count;
        foreach (int stop in segment)
        {
            int partner = IsPickup(stop) ? stop + 1 : stop - 1;
            if (segment.Contains(partner))
            {
                continue;
            }

            if (IsPickup(stop))
            {
                highGap = Math.Min(highGap, positionBuffer[partner]);
            }
            else
            {
                lowGap = Math.Max(lowGap, positionBuffer[partner] + 1);
            }
        }

        double bestDelta = -Epsilon;
        int bestGap = -1;

        for (int gap = lowGap; gap <= highGap; gap++)
        {
            int previousPlace = gap == 0 ? startIndex : stopLocation[reduced[gap - 1]];
            int nextPlace = gap == reduced.Count ? -1 : stopLocation[reduced[gap]];
            double insertionCost = Leg(previousPlace, firstPlace) + Leg(lastPlace, nextPlace) - Leg(previousPlace, nextPlace);
            double delta = insertionCost - removalGain;

            if (delta < bestDelta)
            {
                bestDelta = delta;
                bestGap = gap;
            }
        }

        if (bestGap < 0)
        {
            return false;
        }

        reduced.InsertRange(bestGap, segment);
        route.Clear();
        route.AddRange(reduced);
        return true;
    }
}
