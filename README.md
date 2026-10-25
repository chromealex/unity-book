# unity-book

Код к книге «Unity: всё, что вы не знали о разработке» (Алекс Силаев).

## Что внутри

| Папка | Глава | Что это |
|---|---|---|
| `Triangulation` | 10, «Триангуляция» | Триангуляция простого многоугольника методом отрезания ушей (ear clipping): выпуклые и вогнутые контуры, любой обход, выбрасывание точек на одной прямой, построение меша. |
| `BitScanner` | 5, перебор битовой маски через `tzcnt` | Индексы установленных битов: скалярная версия и варианты на интринсиках AVX2, SSE2, NEON, постраничная выдача. |
| `ILAnalysis` | 16, кодогенерация | Анализ IL через Mono.Reflection: подсчёт вызовов метода с заходом во вложенные методы и поиск вызовов внутри циклов. Только для редактора. |
| `Benchmarks` | Приложение «Методика замеров» | Unity-проект со всеми замерами из книги и результатами прогона. Открывается отдельно, как самостоятельный проект. |

## Как подключить

Скопируйте нужную папку (кроме `Benchmarks`) в `Assets` своего проекта. `Benchmarks` — отдельный проект: откройте его в Unity Hub через Add. У каждой папки своя сборка (asmdef), тесты лежат в `Tests` и запускаются из Test Runner (EditMode).

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
