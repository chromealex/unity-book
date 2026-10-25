# BookBenchmarks

Unity 6 (6000.6.0f1) проект, который измеряет все заявления о производительности из книги
и сквозной пример «10 000 юнитов RTS» (шаги 0–4). Только код, ассеты создаются автоматически.

## Открыть

1. Unity Hub → Add → папка `BookBenchmarks`, версия 6000.6.0f1.
2. Для сборки плеера нужны модули **Mac Build Support (Mono)** и **Mac Build Support (IL2CPP)**
   (для IL2CPP еще Xcode Command Line Tools).
3. После импорта в меню появится пункт **Book**.

## Запуск в редакторе

**Book → Run benchmarks in Editor.**

Меню само на время прогона выставляет и потом возвращает:
- Jobs → Burst → Enable Compilation = **On**;
- Jobs → Burst → Safety Checks = **Off**;
- Burst Synchronous Compilation = On (иначе первые кадры шли бы в managed-коде);
- Jobs → Leak Detection = Off, Jobs Debugger = Off.

Если хотите поставить вручную: Jobs > Burst > Safety Checks > Off, Enable Compilation — включен.
Редактор на время прогона «замирает» (всё идет синхронно), прогресс пишется в Console с префиксом `[Book]`.
Не находитесь в Play Mode. Временные GameObject'ы создаются в открытой сцене и удаляются в конце;
сцену после прогона сохранять не нужно.

Результаты: `Results/editor_mono.json` и `Results/editor_mono.md`.

## Плеер (честные цифры)

**Book → Build and run Player (IL2CPP)** и **Book → Build and run Player (Mono)**:
- создает пустую сцену `Assets/Bench/Bench.unity`, если ее нет;
- временно ставит scripting backend для Standalone, Development Build = off,
  архитектуру macOS = Apple silicon (через reflection, если не получилось — проверьте Build Profiles);
- собирает release-плеер в `Builds/<backend>/BookBenchmarks.app` (в release-сборке safety checks нет);
- возвращает прежние настройки;
- на время сборки включает `PlayerSettings.enableFrameTimingStats` и `runInBackground` (нужны для render stats), потом возвращает;
- запускает `BookBenchmarks.app/Contents/MacOS/BookBenchmarks -batchmode -resultsPath ... -logFile ...`
  (без `-nographics`: графическое устройство есть, шаг 4 меряется) в фоновом потоке, ждет завершения
  (таймаут 30 мин), пишет в Console, где лежат результаты.

Результаты: `Results/player_il2cpp.json/.md`, `Results/player_mono.json/.md`, лог плеера `Results/player_<backend>.log`.
Ошибки сборки (например, не установлен модуль IL2CPP) выводятся в Console построчно.

С `-nographics` шаг 4 (полный кадр) помечается SKIPPED.

**Book → Build and run Player (IL2CPP, render stats)** — та же сборка IL2CPP, но плеер запускается
**без** `-batchmode`, с окном: `-renderStats -screen-width 1920 -screen-height 1080 -screen-fullscreen 0`.
В этом режиме плеер не гоняет главы, а только рендерит настоящие кадры (≈ 3 × 150 кадров, меньше минуты),
пишет `Results/player_il2cpp_render.json/.md` (лог `player_il2cpp_render.log`) и закрывается. Таймаут 10 мин.
Окно не трогайте, пока идет замер.

## Что внутри отчета

- `environment`: версия Unity, backend, editor/player, Burst on/off, Burst safety checks, collections checks,
  процессор, память, ОС, графический API, дата.
- `results[]`: `id`, `chapter`, `variant`, `medianNs`, `minNs`, `p90Ns`, `stddevNs`, `bytesPerOp`, `n`, `notes`, `error`.
  Время — **на одну операцию** (для RTS — на кадр). `bytesPerOp = -1` значит «не измерялось».
  Для `ch3.sizeof` в `bytesPerOp` записан размер структуры в байтах.
- Сначала идет `harness.alloc` — калибровка счетчика аллокаций (0 / new object() / new byte[1000]).
  Счетчик: `GC.GetAllocatedBytesForCurrentThread`, если рантайм его поддерживает, иначе
  `Profiler.GetMonoUsedSizeLong` с выключенным GC. Метод указан в `notes`.

## Дополнительные замеры глав 3, 5, 9

Группы `ch3.extra` (`Benchmarks/Ch03Extra.cs`) и `ch5.extra` (`Benchmarks/Ch05Extra.cs`) идут сразу после `ch3` и `ch5`;
новые варианты `ch9.array2d*` — в `Ch09Math.cs`, после книжных (книжные варианты не менялись). Время — на одну операцию,
что считается операцией — в `variant`/`notes`.

| id | варианты | что проверяет |
|---|---|---|
| `ch9.array2d`, `ch9.array2d.write` | + `int[][] jagged m[y][x]`; `int[,]` / `int[]` / `int[][]` в порядке столбцов (x снаружи, y внутри) | «зубчатый [][] дороже обоих»; обход по столбцам — шаг 4 КБ, где раскладка решает. нс на элемент |
| `ch3.ptrvsclass` | `lifecycle: new AClass()` / `Malloc + Free`; `long-lived AClass` / `A*` (1M NoInlining-вызовов); связный список 100K: ноды-классы / `Node*` (Malloc на ноду), по порядку выделения и с перемешанными связями | «работа с указателями примерно в два раза быстрее, чем ... класс в куче». Управляемый код, GC включен |
| `ch3.ptrvsclass.alloc` | `new AClass()`, `Malloc/Free` | байт управляемой кучи на объект |
| `ch3.diff` | `identical` / `64 runs` / `1% scattered` × `managed byte`, `managed long`, `Burst byte`, `Burst long`, `Burst v256` | разбор «дифф состояния»: ×8 от long, еще ×4 от v256. Стейт 1 МБ, нс на весь дифф. В `notes` — размер диффа и проверка применением (`apply ok`) |
| `ch3.fixedloop` | `arr[i]` до `arr.Length`; `arr[i]` до `count`; `fixed` + `p[i]`; набросок книги (`fixed (int* p = arr)` + итератор) | `fixed`-цикл против `arr[i]`, int[] 1M, управляемый код, нс на элемент |
| `ch5.list.random` | Burst `NativeList` / `UnsafeList` / `int*`, 1M чтений по перемешанным индексам | стоимость доступа при случайных индексах |
| `ch5.list.managed` | без Burst: `NativeList` / `UnsafeList` / `int*`, последовательная сумма 1M | в редакторе индексатор `NativeList` делает проверки безопасности, в плеере их нет |
| `ch5.list.call` | Burst, `[NoInlining] Read(list, i)` × 1M: `NativeList` по значению / по `ref`, `UnsafeList` по значению / по `ref`, `int*` | лишняя косвенность `m_ListData -> Ptr`, которую нельзя вынести из цикла |
| `ch5.bitcase` | `random ~50%` (данные кейса) / `sparse ~3%` / `mixed` × `Burst before`/`after`, `managed before`/`after` | «Разбор из практики: поиск установленных бит в 13 раз быстрее»: «до» — проверка каждого бита, «после» — пропуск 0, `MemCpy` полного слова, `tzcnt`, развертка ×4. 1000 ulong, нс на слово (×1000 = один проход). В `notes` — сверка вывода с наивной версией |

## Сквозной пример RTS

Код шагов лежит в `Assets/Bench/Runtime/Rts/Step*` максимально близко к тексту книги
(правки для стенда помечены `[bench]`, список — в `RTS_FIXES.md`). Все шаги симулируются
внутри одного настоящего кадра с `dt = 1/60` и одинаковым сидом.

| шаг | юниты | кадров (прогрев + замер + аллокации) |
|---|---|---|
| step0 naive | 1 000 / 10 000 | 2+20+2 / 0+2+0 (O(n² log n), очень долго) |
| step0 fixed | 1 000 / 10 000 | 5+60+5 / 1+3+1 (O(n²)) |
| step1, step2 (×2: N-2 и 0 воркеров), step3, step4 | 1 000 / 10 000 | 10+120+10 |

В `notes` у каждого шага — контрольная сумма (живые, сумма HP, хэши HP и позиций) и сравнение:
- step2 с 0 воркеров против step2 с N-2 воркерами — книга обещает одинаковый результат;
- step3 против step2 — «алгоритм не меняется»: HP должны совпасть, биты float-позиций могут разойтись;
- step4 против step3 — «логика не менялась»: должно совпасть полностью.

Шаг 4: всегда измеряется `CullAndBuildInstancesJob` отдельно (`rts.n*.step4cull`).
Полный кадр с `Draw()` (`RenderMeshInstanced` + `RenderMeshIndirect`) — только при наличии графического
устройства (редактор и плеер из меню: `-batchmode` без `-nographics`); с `-nographics` он помечен SKIPPED. Материалы для шага 4
создаются меню в `Assets/Bench/Resources`, шейдер полосок из книги — `Assets/Bench/Shaders/HealthBar.shader`.

Чтобы пропустить наивный шаг 0 на 10 000 юнитов в плеере: аргумент `-rtsNoNaive10k`
(в редакторе — `RtsBenchmarks.IncludeNaive10k = false`). Чтобы пропустить все детальные замеры ниже:
`-rtsNoDetail` (в редакторе — `RtsBenchmarks.IncludeDetail = false`).

## RTS: детальные замеры для таблиц «до/после»

Идут после основных замеров RTS отдельными прогонами (основные `rts.n1000` / `rts.n10000` / `*.step4cull` /
`*.step4draw` не меняются). Время каждой системы — `Stopwatch` вокруг вызова прямо в коде шага
(`RtsSplit`, строки `// [bench]`), сумма за кадр; в отчете — медиана по кадрам (`medianNs`, `p90Ns`, ...).
В каждом прогоне есть строка `<префикс> frame (same run)` — полный кадр того же прогона, чтобы видеть долю систем.

| id | варианты | что меряет |
|---|---|---|
| `rts.n1000.split`, `rts.n10000.split` | `step0 naive Move/Combat/Labels` (только 1 000), `step0 fixed Move/Combat/Cleanup/Labels`, `step1 Grid/Move/Combat/SyncTransforms`, `step2 Grid/Move/Combat/Combat dispatch (workers)/MergeDamage/SyncTransforms` | время каждой системы за кадр. step0 naive: 3 отметки времени на юнита внутри `Unit.Update`, суммируются. step2 `Combat` = `Combat dispatch` + `MergeDamage`; воркеров как в книге (processorCount−2 + главный) |
| то же | `step3 schedule CellIndexJob` … `schedule SyncTransformsJob`, `step3 Update total`, `step3 LateUpdate Complete`, `step3 LateUpdate rest` | шаг 3 как в книге (одна цепочка): стоимость каждого `Schedule` на главном потоке, вся работа — в строке `Complete` (ожидание цепочки) |
| то же | `step3 isolated CellIndexJob` / `GridBuildJob` / `MoveJob` / `CombatJob` / `DamageMergeJob` / `SyncTransformsJob` | отдельный вариант: каждая задача `Schedule()+Complete()` сразу — время стены одной задачи (главный поток помогает воркерам). Кадр в сумме медленнее книжного: нет перекрытия задач |
| то же | `step4 Update`, `step4 LateUpdate Complete`, `step4 LateUpdate Draw + SwapPositions` | шаг 4: планирование, ожидание, подача отрисовки (только при наличии графического устройства) |
| `rts.scale.step1` | `n=2500 / n=5000 / n=10000` × `frame`, `Grid`, `Move`, `Combat`, `SyncTransforms` | масштабирование шага 1 по числу юнитов |
| `rts.scale.step0fixed` | `n=2500` × `frame`, `Move`, `Combat`, `Cleanup`, `Labels` | шаг 0 fixed на 2 500 (O(n²), 2+20 кадров); 1 000 и 10 000 — в `*.split` |
| `rts.threads.step2` | `workers=1/2/4/8`, `workers=N (book: processorCount-2 background)` × те же системы, что в split | 10 000 юнитов. workers = `WorkerPool.WorkerCount` = фоновые потоки + главный (главный поток — worker 0), т.е. `backgroundThreads = workers − 1` |
| `rts.threads.step3` | `JobWorkerCount=1/2/4/8 frame`, `JobWorkerCount=N (as found) frame` | 10 000 юнитов, только время кадра. `JobsUtility.JobWorkerCount` ставится перед прогоном и возвращается после; в `notes` — прочитанное обратно значение. Главный поток тоже выполняет задачи, пока ждет `Complete` |
| `rts.n1000.gc300` | `step0 naive ...`, `step0 fixed ...` | 300 кадров с **включенным** GC (`GarbageCollector.GCMode = Enabled`): время кадра с паузами GC (смотрите `p90Ns`), `bytesPerOp` — байт на кадр (тот же метод, что везде, еще 5 кадров), в `notes` — `GC.CollectionCount(0)` |
| то же | `... : GC.CollectionCount(0) delta` | **в колонке `bytesPerOp` — число сборок** gen0 за 300 кадров (как `ch3.sizeof` хранит размер) |

### Рендеринг «до/после» (10 000 юнитов)

«До» — шаг 3: юниты — GameObject'ы с `MeshRenderer` (куб, `RtsUnitMaterial`), два варианта: GPU instancing у
материала выключен и включен (Built-in RP сам батчит инстансингом одинаковые меши с таким материалом).
«После» — шаг 4: `RenderMeshInstanced` + полоски HP через `RenderMeshIndirect`. Камера та же, что у шага 4
(`(0,120,-120)`, смотрит в центр), направленный свет с жесткими тенями, симуляция идет каждый кадр.

| id | где | что меряет |
|---|---|---|
| `rts.render.n10000` | плеер render stats | на каждый вариант: `cpuMainThreadFrameTime`, `cpuRenderThreadFrameTime`, `gpuFrameTime`, `cpuFrameTime` из `FrameTimingManager` (мс → нс), `frame dt`, `simulation Update+LateUpdate` (Stopwatch). 30 кадров прогрева, 120 сэмплов, vSync выключен. Тайминг, который платформа не отдала (все нули), — SKIPPED. На Metal `gpuFrameTime` может быть больше кадра (TBDR) |
| `rts.render.n10000.counters` | плеер render stats | `Batches Count`, `SetPass Calls Count`, `Draw Calls Count` через `ProfilerRecorder` (доступны в release-плеере); **в `bytesPerOp` — медиана счетчика за кадр** |
| `rts.render.n10000.editor` | редактор | `Camera.Render() CPU` (синхронный рендер в RenderTexture 1920×1080) и `UnityStats.drawCalls/setPassCalls/dynamicBatches/staticBatches/instancedBatches/instancedBatchedDrawCalls/triangles` после рендера (**в `bytesPerOp` — медиана**). В `notes` — первый и последний сэмпл: если растут, счетчик копится за кадр редактора и не годится. `UnityStats.batches` не публичный — SKIPPED-строка, batches смотрите в плеере |

В обычном batchmode-прогоне плеера `rts.render.n10000` — одна SKIPPED-строка со ссылкой на render stats.

## Время

Ориентировочно на M-серии: главы 1–10 — 1.5–3 мин, RTS — 2–4 мин (из них 1–2 мин наивный шаг 0 на 10k),
детальные замеры RTS — еще 1.5–2.5 мин (из них 0.5–1.5 мин наивный шаг 0 в `gc300`, ~15 с step0 fixed на 10k в split).
Итого полный прогон плеера ≈ 5–9.5 мин; `-rtsNoNaive10k` экономит 1–2 мин.
Render stats — меньше минуты плюс сборка. Сборка IL2CPP в первый раз — еще 3–8 мин.
