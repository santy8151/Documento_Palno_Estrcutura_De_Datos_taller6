using System.Globalization;

namespace Backend;

/// <summary>Loads packages.txt, validates it and groups the tasks by package.</summary>
public static class PackageFile
{
    public static List<Package> Load(string path, Graph<string> city)
    {
        if (!File.Exists(path))
        {
            throw new DataValidationException($"No se encontró el archivo de paquetes: {path}");
        }

        var errors = new List<string>();
        var pickups = new Dictionary<int, (string Location, int Line)>();
        var deliveries = new Dictionary<int, (string Location, int Line)>();
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
            if (fields.Length != 3 || fields.Any(field => field.Length == 0))
            {
                errors.Add($"Línea {lineNumber}: se esperaba 'location,action,package' y se encontró '{line}'.");
                continue;
            }

            string location = fields[0];
            bool lineIsValid = true;

            if (!city.HasVertex(location))
            {
                errors.Add($"Línea {lineNumber}: la ubicación '{location}' no existe en el mapa.");
                lineIsValid = false;
            }

            bool isPickup = fields[1].Equals("Pickup", StringComparison.OrdinalIgnoreCase);
            bool isDelivery = fields[1].Equals("Delivery", StringComparison.OrdinalIgnoreCase);
            if (!isPickup && !isDelivery)
            {
                errors.Add($"Línea {lineNumber}: la acción '{fields[1]}' debe ser Pickup o Delivery.");
                lineIsValid = false;
            }

            if (!int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int packageId) || packageId <= 0)
            {
                errors.Add($"Línea {lineNumber}: el número de paquete '{fields[2]}' debe ser un entero mayor que 0.");
                lineIsValid = false;
            }

            if (!lineIsValid)
            {
                continue;
            }

            var target = isPickup ? pickups : deliveries;
            if (target.TryGetValue(packageId, out var first))
            {
                string task = isPickup ? "recogida" : "entrega";
                errors.Add($"Línea {lineNumber}: el paquete {packageId} ya tiene una {task} en la línea {first.Line}.");
            }
            else
            {
                target[packageId] = (location, lineNumber);
            }
        }

        foreach (int packageId in pickups.Keys.Union(deliveries.Keys).OrderBy(id => id))
        {
            if (!pickups.ContainsKey(packageId))
            {
                errors.Add($"Al paquete {packageId} le falta la recogida.");
            }

            if (!deliveries.ContainsKey(packageId))
            {
                errors.Add($"Al paquete {packageId} le falta la entrega.");
            }
        }

        if (pickups.Count == 0 && deliveries.Count == 0 && errors.Count == 0)
        {
            errors.Add("El archivo de paquetes no tiene tareas.");
        }

        if (errors.Count > 0)
        {
            throw new DataValidationException(errors);
        }

        return pickups.Keys
            .OrderBy(id => id)
            .Select(id => new Package(id, pickups[id].Location, deliveries[id].Location))
            .ToList();
    }
}
