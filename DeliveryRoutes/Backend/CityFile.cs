using System.Globalization;

namespace Backend;

/// <summary>Loads and validates city.txt.</summary>
public static class CityFile
{
    public static CityData Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new DataValidationException($"No se encontró el archivo del mapa: {path}");
        }

        var errors = new List<string>();
        var streets = new List<Street>();
        var firstSeenAt = new Dictionary<(string, string), int>();
        string[] lines = File.ReadAllLines(path);

        for (int index = 0; index < lines.Length; index++)
        {
            int lineNumber = index + 1;
            string line = lines[index].Trim();

            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            string[] fields = line.Split(',', StringSplitOptions.TrimEntries);
            if (fields.Length != 4 || fields.Any(field => field.Length == 0))
            {
                errors.Add($"Línea {lineNumber}: se esperaba 'from,to,distance_in_meters,direction' y se encontró '{line}'.");
                continue;
            }

            string from = fields[0];
            string to = fields[1];
            bool lineIsValid = true;

            if (!double.TryParse(fields[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double meters)
                || !double.IsFinite(meters)
                || meters <= 0)
            {
                errors.Add($"Línea {lineNumber}: la distancia '{fields[2]}' no es un número positivo.");
                lineIsValid = false;
            }

            bool isTwoWay = fields[3].Equals("TwoWay", StringComparison.OrdinalIgnoreCase);
            bool isOneWay = fields[3].Equals("OneWay", StringComparison.OrdinalIgnoreCase);
            if (!isTwoWay && !isOneWay)
            {
                errors.Add($"Línea {lineNumber}: el sentido '{fields[3]}' debe ser TwoWay u OneWay.");
                lineIsValid = false;
            }

            if (from == to)
            {
                errors.Add($"Línea {lineNumber}: la calle '{from},{to}' une una ubicación consigo misma.");
                lineIsValid = false;
            }

            if (!lineIsValid)
            {
                continue;
            }

            // A,B and B,A are the same street, so the key ignores the order of the endpoints.
            var key = string.CompareOrdinal(from, to) <= 0 ? (from, to) : (to, from);
            if (firstSeenAt.TryGetValue(key, out int firstLine))
            {
                errors.Add($"Línea {lineNumber}: la calle {from},{to} ya apareció en la línea {firstLine}.");
                continue;
            }

            firstSeenAt[key] = lineNumber;
            streets.Add(new Street(from, to, meters, isOneWay));
        }

        if (streets.Count == 0 && errors.Count == 0)
        {
            errors.Add("El archivo del mapa no tiene calles.");
        }

        if (errors.Count > 0)
        {
            throw new DataValidationException(errors);
        }

        var graph = new Graph<string>(directed: true);
        foreach (Street street in streets)
        {
            graph.AddEdge(street.From, street.To, street.Meters);
            if (!street.OneWay)
            {
                graph.AddEdge(street.To, street.From, street.Meters);
            }
        }

        return new CityData(graph, streets);
    }
}
