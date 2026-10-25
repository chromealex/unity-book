using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Jobs;

namespace BookBench.Rts {

    /// <summary>
    /// [bench] Посистемные замеры внутри кода шагов. Код шага вызывает Begin/End вокруг своих фаз
    /// (строки помечены `// [bench]`). Пока Enabled == false, Begin возвращает 0, а End ничего не делает,
    /// так что существующие замеры кадра не меняются (одна проверка статического bool на фазу).
    /// Драйвер перед каждым кадром вызывает Clear, после кадра читает Ticks.
    /// </summary>
    public static class RtsSplit {

        // Общие фазы (шаги 0-2)
        public const int Grid = 0;
        public const int Move = 1;
        public const int Combat = 2;
        public const int Sync = 3;
        public const int Cleanup = 4;
        public const int Labels = 5;
        public const int CombatDispatch = 6; // шаг 2: Dispatch(combatJob) без MergeDamage
        public const int Merge = 7;          // шаг 2: MergeDamage

        // Шаг 3/4: по задачам. Режим "schedule" — время Schedule на главном потоке,
        // режим "complete each" — Schedule + Complete каждой задачи по отдельности.
        public const int JobCellIndex = 8;
        public const int JobGridBuild = 9;
        public const int JobMove = 10;
        public const int JobCombat = 11;
        public const int JobDamageMerge = 12;
        public const int JobSyncTransforms = 13; // шаг 3: SyncTransformsJob, шаг 4: CullAndBuildInstancesJob
        public const int UpdateTotal = 14;       // весь Update (все Schedule + ScheduleBatchedJobs)
        public const int CompleteWait = 15;      // LateUpdate: frameHandle.Complete()
        public const int LateRest = 16;          // LateUpdate после Complete (SetActive, SwapPositions / Draw)

        public const int SlotCount = 17;

        public static bool Enabled;
        public static readonly long[] Ticks = new long[SlotCount];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long Begin() {
            return Enabled ? Stopwatch.GetTimestamp() : 0L;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void End(int slot, long t0) {
            if (Enabled) Ticks[slot] += Stopwatch.GetTimestamp() - t0;
        }

        /// <summary>Шаг 3: после Schedule задачи. completeEach = true дожидается задачи сразу (отдельный вариант замера).</summary>
        public static JobHandle Job(int slot, long t0, JobHandle handle, bool completeEach) {
            if (completeEach) handle.Complete();
            End(slot, t0);
            return handle;
        }

        public static void Clear() {
            Array.Clear(Ticks, 0, SlotCount);
        }
    }
}
