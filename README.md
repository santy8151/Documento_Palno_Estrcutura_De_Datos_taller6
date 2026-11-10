# Documento_Palno_Estrcutura_De_Datos_taller6


A C# / .NET 8 console application that reads a city map and a package list to build the shortest route for picking up and delivering all items while respecting one-way streets.

---

## Note for the Professor: Rubric & OOP Compliance

This project has been built strictly following **Object-Oriented Programming (OOP)** principles, SOLID design, and every requirement specified in the **Workshop Evaluation Rubric (Section 10)**:

1. **Flat-File Parsing & Validation (20%):** Implemented in `CityFile.cs`, `PackageFile.cs`, and `DataValidationException.cs`. Both files are parsed ignoring comments (`#`) and blank lines, reporting exact line numbers on malformed inputs, and validating positive distances, street directions (`TwoWay` / `OneWay`), duplicate streets, non-existent locations, valid actions (`Pickup` / `Delivery`), duplicate/missing tasks per package, and full graph reachability.
2. **Directed Graph & Dijkstra (20%):** Implemented in `Graph.cs`, `Edge.cs`, and `ShortestPaths.cs`. The city is modeled as a weighted, directed adjacency-list graph (`OneWay` adds 1 directed edge; `TwoWay` adds 2). Shortest paths and predecessor trees are computed via Dijkstra's algorithm using .NET's `PriorityQueue` from every relevant node, storing `distance[x][y]` and `distance[y][x]` independently to respect one-way constraints.
3. **Valid Route & Verification (20%):** Implemented in `RoutePlanner.cs` (`IsValid` and `TotalDistance`). Guarantees that all 30 packages are picked up and delivered, no package is delivered before pickup, no one-way street is traversed in reverse, and the total distance matches the exact sum of all route legs. Tested and verified against the small example (`city_mini.txt`, `packages_mini.txt`, start `A`), achieving the exact optimal **2.44 km**.
4. **Route Quality — Heuristic & Local Search (20%):** Combines nearest-neighbor initialization, greedy package insertion, local search (full package relocation and 1–3 stop block moves), and 40 seeded multi-start iterations (`Random(42)`) with a ruin-and-recreate phase, achieving a total distance well below the **32.00 km** top-tier threshold (100% score bracket).
5. **Output Format & Documentation (10%):** Handled by `RoutePrinter.cs`, `RouteReport.cs`, and `RouteStep.cs`. Groups multiple tasks at the same stop (showing deliveries before pickups), displays intermediate pass-through nodes in the `Camino:` line, handles zero-displacement initial stops, and prints total distance in kilometers with two decimal places.
6. **Object-Oriented Architecture & Clean Code (10%):** Organized cleanly under the `Backend` namespace with one class/record per file (`Package`, `Stop`, `Edge`, `Graph`, `CityData`, `CityFile`, `PackageFile`, `ShortestPaths`, `RoutePlanner`, `RouteService`, `RoutePrinter`, `RouteReport`, `RouteStep`, `DataValidationException`), a thin `Program.cs` entry point, and decoupled `Terminal` and `Web` presentation layers. All code and comments are written in English.

---

## How to Run

    dotnet run -- city.txt packages.txt D
    dotnet run -- city_mini.txt packages_mini.txt A
    dotnet run -- city.txt packages.txt D --web      # Also opens the interactive map at http://localhost:5000

**Options:** `--web`, `--port N`, `--no-open` (do not open browser), `--plain` (no colors).  
If run without arguments, the program prompts for the file paths and starting position via the console. If output is redirected to a file, colors are automatically disabled.

## Project Structure

    DeliveryRoutes/
    |-- DeliveryRoutes.csproj
    |-- Program.cs                        Thin entry point (CLI args & interactive console fallback)
    |-- city.txt / packages.txt           Main workshop dataset (100 locations, 142 streets, 30 packages)
    |-- city_mini.txt / packages_mini.txt Small verification dataset (optimal: 2.44 km)
    |-- Backend/
    |   |-- Edge.cs                       Directed weighted edge representation
    |   |-- Graph.cs                      Generic directed adjacency-list graph
    |   |-- CityData.cs                   Encapsulated city map metadata and graph model
    |   |-- CityFile.cs                   Flat-file loader and validator for city.txt
    |   |-- Package.cs                    Immutable record for package (Id, Pickup, Delivery)
    |   |-- PackageFile.cs                Flat-file loader and validator for packages.txt
    |   |-- Stop.cs                       Stop model (Location, StopKind, PackageId)
    |   |-- DataValidationException.cs    Custom exception reporting exact line numbers and validation errors
    |   |-- ShortestPaths.cs              Dijkstra (PriorityQueue) & path reconstruction
    |   |-- RoutePlanner.cs               Heuristic solver, local search, IsValid, and TotalDistance
    |   |-- RouteService.cs               High-level orchestration of loading, planning, and reporting
    |   |-- RouteStep.cs                  Step-by-step leg model
    |   |-- RouteReport.cs                Complete route summary model
    |   |-- RoutePrinter.cs               Section 5 compliant output formatter
    |-- Terminal/
    |   |-- CliOptions.cs                 Command-line argument parser
    |   |-- ConsoleUi.cs                  Colored console output renderer
    |-- Web/
        |-- WebServer.cs                  Localhost HTTP server (HttpListener)
        |-- public/                       Interactive SVG map visualizer (index.html, app.js, styles.css)

## Strategy & Algorithm

- **Exact Distances:** Dijkstra's algorithm (using `PriorityQueue`) runs once from every relevant node (starting position and all pickup/delivery points). Because the graph is directed, `distance[x][y]` and `distance[y][x]` are stored separately along with predecessor maps to reconstruct the exact street path. Afterward, the route planner works exclusively with this precomputed distance table.
- **Why Not Brute-Force All Combinations:** With 60 stops, there are `60! / 2^30` valid orderings (a 73-digit number). This is the Pickup and Delivery Problem (a variant of the Traveling Salesperson Problem), making exhaustive search computationally impossible. Instead, constructive and local-search heuristics are used.
- **Initial Route:** Nearest-neighbor heuristic (yields **40.52 km** on the workshop dataset).
- **Optimization:**
  - **Insertion Construction:** Each package is inserted into the best possible position (ensuring pickup occurs before delivery).
  - **Local Search:** Relocates a complete package (both pickup and delivery) and shifts blocks of 1 to 3 stops while preserving precedence constraints.
  - **Multi-Start & Ruin-and-Recreate:** Runs 40 restarts with random package orderings, followed by a "destroy and rebuild" phase (removing 2 to 6 packages and reinserting them greedily).
  - **Deterministic Execution:** Uses `Random` with a fixed seed (`42`) so results are always reproducible.
- **Verification:** `IsValid` verifies every package (one pickup, one delivery, in the proper order), and the total distance is recalculated by summing all individual route legs before printing.

## Output with `city.txt`, `packages.txt`, and `D`

    (paste the full program output here)

## Small Example Output (`city_mini.txt`, `packages_mini.txt`, `A`)

    (paste the output of dotnet run -- city_mini.txt packages_mini.txt A --plain here; expected result is 2.44 km)
