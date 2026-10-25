# ILAnalysis

Код к главе 16, раздел про кодогенерацию: рабочий пример анализа IL, на котором построен кодген ME.BECS.
Полная реализация — в репозитории [ME.BECS](https://github.com/chromealex/ME.BECS).

`CallAnalyzer.Count(root, target)` читает IL метода `root`, считает вызовы `target` и рекурсивно заходит во все вызываемые методы
из той же сборки. Вызов внутри цикла определяется по обратным переходам: инструкция `br*` с адресом перехода меньше своего —
конец итерации, всё между адресом перехода и ней — тело цикла.

- Рекурсия не зацикливает анализ: метод, который уже есть в текущей цепочке вызовов, второй раз не открывается.
- `[CallAnalyzerIgnore]` на методе запрещает в него заходить.
- IL читается лёгкой библиотекой Mono.Reflection (папка `Editor/MonoReflection`, лицензия MIT, автор Jb Evain; заголовки лицензии сохранены в каждом файле, полный текст — в `Editor/MonoReflection/LICENSE`).

Только для редактора: сборка `UnityBook.ILAnalysis` подключается на платформе Editor. Тесты (EditMode): прямой вызов, вызов в `for` и `while`,
вызов через вложенный метод, вложенный вызов в цикле, условие без цикла, рекурсия, игнорируемый метод.

```csharp
var root = typeof(MyJob).GetMethod("Execute");
var target = typeof(MyApi).GetMethod("CreateEntity");
var count = CallAnalyzer.Count(root, target);
Debug.Log(count); // total: 3, inside loop: 1
```
