# Delivery Routes

Programa de consola en C# / .NET 8 que lee un mapa de ciudad y una lista de paquetes y arma la ruta más corta
para recoger y entregar todo, respetando las calles de un solo sentido.

## Cómo ejecutarlo

```
dotnet run -- city.txt packages.txt D
dotnet run -- city_mini.txt packages_mini.txt A
dotnet run -- city.txt packages.txt D --web      (además abre el mapa en http://localhost:5000)
```

Opciones: `--web`, `--port N`, `--no-open` (no abrir el navegador), `--plain` (sin colores).
Sin argumentos, el programa pide los archivos y la posición inicial por consola.
Si la salida se redirige a un archivo, se imprime sin colores automáticamente.

## Estructura

```
DeliveryRoutes/
|-- Program.cs            entrada delgada
|-- Backend/              grafo, Dijkstra, archivos planos, planificador, formato de salida
|-- Terminal/             interfaz de consola con colores
|-- Web/                  servidor localhost (HttpListener) y página en Web/public
```

## Estrategia (explicación)

1. **Distancias exactas.** Se ejecuta Dijkstra (con `PriorityQueue`) una vez desde cada punto que importa
   (posición inicial y puntos de recogida/entrega). Como el grafo es dirigido, `distance[x][y]` y `distance[y][x]`
   se guardan por separado. Después el planificador trabaja solo con esa tabla.
2. **Por qué no se prueban todas las combinaciones.** Con 60 paradas hay 60!/2^30 órdenes válidos (73 cifras).
   Es el problema de recogida y entrega (una variante del agente viajero), así que se usan heurísticas.
3. **Ruta inicial:** vecino más cercano (40.52 km con los datos del taller).
4. **Mejora:**
   - construcción por inserción: cada paquete se mete en la mejor posición (recogida antes que entrega);
   - búsqueda local: reubicar un paquete completo y mover bloques de 1 a 3 paradas;
   - 40 reinicios con órdenes aleatorios y luego "destruir y reconstruir" (quitar de 2 a 6 paquetes y volver a insertarlos);
   - `Random` con semilla fija (42), así el resultado siempre es el mismo.
5. **Verificación:** `IsValid` comprueba cada paquete (una recogida, una entrega, en orden) y el total se recalcula
   sumando los tramos antes de imprimir.

## Salida con city.txt, packages.txt y D

(pegar aquí la salida completa del programa)

## Salida del ejemplo pequeño

(pegar aquí la salida de `dotnet run -- city_mini.txt packages_mini.txt A --plain`; debe dar 2.44 km)
