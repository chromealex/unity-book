# unity-book

Код к книге «Unity: всё, что вы не знали о разработке» (Алекс Силаев).

## Что внутри

| Папка | Глава | Что это |
|---|---|---|
| `Triangulation` | 10, «Триангуляция» | Триангуляция простого многоугольника методом отрезания ушей (ear clipping): выпуклые и вогнутые контуры, любой обход, выбрасывание точек на одной прямой, построение меша. |

## Как подключить

Скопируйте нужную папку в `Assets` своего проекта. У каждой папки своя сборка (asmdef), тесты лежат в `Tests` и запускаются из Test Runner (EditMode).

```csharp
var points = new List<Vector2> { new Vector2(0f, 0f), new Vector2(2f, 0f), new Vector2(2f, 1f), new Vector2(1f, 1f), new Vector2(1f, 2f), new Vector2(0f, 2f) };
var triangles = Triangulator.Triangulate(points); // индексы, по три на треугольник
var mesh = Triangulator.BuildMesh(points);        // готовый меш в плоскости XY
```

## Другие проекты

- [ME.BECS](https://github.com/chromealex/ME.BECS)
- [chromealex.com](https://chromealex.com)

## Лицензия

MIT, см. [LICENSE](LICENSE).
