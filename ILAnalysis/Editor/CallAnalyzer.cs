using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityBook.ILAnalysis.MonoReflection;

namespace UnityBook.ILAnalysis {

    /// <summary>
    /// Метод с этим атрибутом анализатор не открывает: вызовы внутри него не считаются.
    /// Так отрезают служебный код, в который заходить незачем.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor)]
    public sealed class CallAnalyzerIgnoreAttribute : Attribute { }

    /// <summary>
    /// Результат анализа: сколько раз вызывается целевой метод и сколько из этих вызовов стоит внутри циклов.
    /// </summary>
    public struct CallCount {

        public int total;
        public int insideLoop;

        public override string ToString() => $"total: {this.total}, inside loop: {this.insideLoop}";

    }

    /// <summary>
    /// Анализ IL: считает вызовы заданного метода в теле метода и во всех методах, которые он вызывает.
    /// Вызов внутри цикла определяется по обратным переходам: инструкция br* с адресом перехода
    /// меньше своего — это конец итерации, всё между адресом перехода и ею — тело цикла.
    /// </summary>
    public static class CallAnalyzer {

        /// <summary>
        /// Считает вызовы target в root с заходом во вызываемые методы.
        /// В вызываемые методы заходим, только если они из той же сборки, что и root:
        /// иначе анализ уйдёт в стандартную библиотеку и движок.
        /// </summary>
        public static CallCount Count(MethodBase root, MethodBase target) {
            var result = new CallCount();
            var path = new HashSet<MethodBase>();
            Visit(root, target, root.Module.Assembly, false, path, ref result);
            return result;
        }

        private static void Visit(MethodBase method, MethodBase target, Assembly assembly, bool callerInsideLoop, HashSet<MethodBase> path, ref CallCount result) {
            // Защита от рекурсии: метод уже есть в текущей цепочке вызовов — второй раз в него не заходим.
            // Множество хранит именно цепочку, а не все посещённые методы: если вспомогательный метод
            // вызывается из двух мест, его вызовы честно посчитаются дважды.
            if (path.Add(method) == false) {
                return;
            }
            if (method.GetMethodBody() == null) {
                // абстрактные, extern и методы интерфейсов: тела нет, читать нечего
                path.Remove(method);
                return;
            }

            var instructions = method.GetInstructions();
            var loops = FindLoops(instructions);
            for (int i = 0; i < instructions.Count; ++i) {
                var instruction = instructions[i];
                if (instruction.Operand is MethodBase called) {
                    var insideLoop = callerInsideLoop == true || IsInsideLoop(instruction.Offset, loops) == true;
                    if (called == target) {
                        ++result.total;
                        if (insideLoop == true) {
                            ++result.insideLoop;
                        }
                    } else if (called.Module.Assembly == assembly && called.IsDefined(typeof(CallAnalyzerIgnoreAttribute), false) == false) {
                        Visit(called, target, assembly, insideLoop, path, ref result);
                    }
                }
            }

            path.Remove(method);
        }

        /// <summary>
        /// Диапазоны тел циклов [начало, конец] по смещениям IL.
        /// Любой if, for, foreach, while в IL — это переход на адрес. Адрес больше текущего — прыжок вперёд,
        /// открытие условия. Адрес меньше или равен текущему — прыжок назад, конец итерации цикла.
        /// </summary>
        public static List<(int start, int end)> FindLoops(IList<Instruction> instructions) {
            var loops = new List<(int start, int end)>();
            for (int i = 0; i < instructions.Count; ++i) {
                var instruction = instructions[i];
                var operandType = instruction.OpCode.OperandType;
                if (operandType != OperandType.InlineBrTarget && operandType != OperandType.ShortInlineBrTarget) {
                    continue;
                }
                // MethodBodyReader уже заменил смещение перехода на саму инструкцию
                if (instruction.Operand is Instruction jumpTarget && jumpTarget.Offset <= instruction.Offset) {
                    loops.Add((jumpTarget.Offset, instruction.Offset));
                }
            }
            return loops;
        }

        private static bool IsInsideLoop(int offset, List<(int start, int end)> loops) {
            for (int i = 0; i < loops.Count; ++i) {
                if (offset >= loops[i].start && offset <= loops[i].end) {
                    return true;
                }
            }
            return false;
        }

    }

}
