using Backend;

namespace Terminal;

/// <summary>Command-line arguments. Missing positional values are asked for on the console.</summary>
public record CliOptions(
    string CityPath,
    string PackagesPath,
    string Start,
    bool Web,
    int Port,
    bool Plain,
    bool OpenBrowser)
{
    public bool UseColor =>
        !Plain
        && !Console.IsOutputRedirected
        && Environment.GetEnvironmentVariable("NO_COLOR") is null;

    public static CliOptions Parse(string[] args)
    {
        var positional = new List<string>();
        bool web = false;
        bool plain = false;
        bool openBrowser = true;
        int port = 5000;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--web":
                    web = true;
                    break;
                case "--plain":
                    plain = true;
                    break;
                case "--no-open":
                    openBrowser = false;
                    break;
                case "--port":
                    if (i + 1 >= args.Length || !int.TryParse(args[++i], out port) || port is < 1 or > 65535)
                    {
                        throw new DataValidationException("El valor de --port debe ser un número entre 1 y 65535.");
                    }

                    break;
                default:
                    if (args[i].StartsWith("--"))
                    {
                        throw new DataValidationException($"Opción desconocida: {args[i]}");
                    }

                    positional.Add(args[i]);
                    break;
            }
        }

        if (positional.Count > 3)
        {
            throw new DataValidationException("Uso: dotnet run -- city.txt packages.txt START [--web] [--port N] [--plain] [--no-open]");
        }

        string cityPath = positional.Count > 0 ? positional[0] : Ask("Ruta del archivo del mapa (city.txt): ");
        string packagesPath = positional.Count > 1 ? positional[1] : Ask("Ruta del archivo de paquetes (packages.txt): ");
        string start = positional.Count > 2 ? positional[2] : Ask("Posición inicial (por ejemplo D): ");

        if (cityPath.Length == 0 || packagesPath.Length == 0 || start.Length == 0)
        {
            throw new DataValidationException("Faltan datos: se necesitan el mapa, los paquetes y la posición inicial.");
        }

        return new CliOptions(cityPath, packagesPath, start, web, port, plain, openBrowser);
    }

    private static string Ask(string prompt)
    {
        Console.Write(prompt);
        return Console.ReadLine()?.Trim() ?? string.Empty;
    }
}
