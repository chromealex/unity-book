using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Scripting;

namespace BookBench.Rts {

    /// <summary>
    /// Детальные замеры для таблиц «до/после»: время каждой системы внутри шага (rts.n*.split),
    /// масштабирование по числу юнитов (rts.scale.*), по числу потоков (rts.threads.*) и GC за 300 кадров (rts.n1000.gc300).
    /// Существующие замеры (RunAllSteps) не меняются: здесь отдельные прогоны с новыми id.
    /// </summary>
    public static partial class RtsBenchmarks {

        /// <summary>false: пропустить детальные замеры. В плеере: аргумент -rtsNoDetail.</summary>
        public static bool IncludeDetail = true;

        private struct Slot {
            public int index;
            public string name;

            public Slot(int index, string name) {
                this.index = index;
                this.name = name;
            }
        }

        private static readonly Slot[] Step0NaiveSlots = {
            new Slot(RtsSplit.Move, "Move"),
            new Slot(RtsSplit.Combat, "Combat"),
            new Slot(RtsSplit.Labels, "Labels"),
        };

        private static readonly Slot[] Step0FixedSlots = {
            new Slot(RtsSplit.Move, "Move"),
            new Slot(RtsSplit.Combat, "Combat"),
            new Slot(RtsSplit.Cleanup, "Cleanup"),
            new Slot(RtsSplit.Labels, "Labels"),
        };

        private static readonly Slot[] Step1Slots = {
            new Slot(RtsSplit.Grid, "Grid"),
            new Slot(RtsSplit.Move, "Move"),
            new Slot(RtsSplit.Combat, "Combat"),
            new Slot(RtsSplit.Sync, "SyncTransforms"),
        };

        private static readonly Slot[] Step2Slots = {
            new Slot(RtsSplit.Grid, "Grid"),
            new Slot(RtsSplit.Move, "Move"),
            new Slot(RtsSplit.Combat, "Combat"),
            new Slot(RtsSplit.CombatDispatch, "Combat dispatch (workers)"),
            new Slot(RtsSplit.Merge, "MergeDamage"),
            new Slot(RtsSplit.Sync, "SyncTransforms"),
        };

        private static readonly Slot[] Step3ScheduleSlots = {
            new Slot(RtsSplit.JobCellIndex, "schedule CellIndexJob"),
            new Slot(RtsSplit.JobGridBuild, "schedule GridBuildJob"),
            new Slot(RtsSplit.JobMove, "schedule MoveJob"),
            new Slot(RtsSplit.JobCombat, "schedule CombatJob"),
            new Slot(RtsSplit.JobDamageMerge, "schedule DamageMergeJob"),
            new Slot(RtsSplit.JobSyncTransforms, "schedule SyncTransformsJob"),
            new Slot(RtsSplit.UpdateTotal, "Update total (all Schedule + ScheduleBatchedJobs)"),
            new Slot(RtsSplit.CompleteWait, "LateUpdate Complete (main thread waits for the chain)"),
            new Slot(RtsSplit.LateRest, "LateUpdate rest (SetActive + SwapPositions)"),
        };

        private static readonly Slot[] Step3IsolatedSlots = {
            new Slot(RtsSplit.JobCellIndex, "CellIndexJob"),
            new Slot(RtsSplit.JobGridBuild, "GridBuildJob"),
            new Slot(RtsSplit.JobMove, "MoveJob"),
            new Slot(RtsSplit.JobCombat, "CombatJob"),
            new Slot(RtsSplit.JobDamageMerge, "DamageMergeJob"),
            new Slot(RtsSplit.JobSyncTransforms, "SyncTransformsJob"),
            new Slot(RtsSplit.UpdateTotal, "Update total (jobs completed one by one)"),
            new Slot(RtsSplit.LateRest, "LateUpdate rest (SetActive + SwapPositions)"),
        };

        private static readonly Slot[] Step4Slots = {
            new Slot(RtsSplit.UpdateTotal, "Update (schedule all jobs incl. CullAndBuildInstancesJob)"),
            new Slot(RtsSplit.CompleteWait, "LateUpdate Complete (main thread waits for the chain)"),
            new Slot(RtsSplit.LateRest, "LateUpdate Draw + SwapPositions"),
        };

        private static void RunDetail() {
            var total = Stopwatch.StartNew();
            var hasGraphics = SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;

            RunSplitAll(1000, hasGraphics);
            RunSplitAll(10_000, hasGraphics);
            RunScale();
            RunThreadsStep2();
            RunThreadsStep3();
            RunGc300();

            if (Application.isEditor == false) {
                Bench.Info("rts.render.n10000", Chapter, "render stats (FrameTimingManager)", -1, "SKIPPED in this run: needs presented frames. Run Book/Build and run Player (IL2CPP, render stats): player with -renderStats, without -batchmode -> player_il2cpp_render.json");
            }
            Bench.Log("rts detail done in " + (total.ElapsedMilliseconds / 1000.0).ToString("0.0") + " s");
        }

        // ================= 1. per-system split =================

        private static void RunSplitAll(int count, bool hasGraphics) {
            var id = "rts.n" + count + ".split";
            var big = count >= 10_000;
            Bench.Log("rts detail: per-system split, " + count + " units ...");

            if (big == false) {
                RunSplit(id, "step0 naive", count, 2, 20, CreateStep0Naive(count), Step0NaiveSlots, "Stopwatch per unit inside Unit.Update (3 timestamps per unit), summed over units");
            }
            RunSplit(id, "step0 fixed", count, big ? 1 : 5, big ? 3 : 60, CreateStep0Fixed(count), Step0FixedSlots, null);
            RunSplit(id, "step1", count, 10, 120, CreateStep1(count), Step1Slots, null);
            var bookThreads = BookBackgroundThreads();
            RunSplit(id, "step2", count, 10, 120, CreateStep2(count, -1), Step2Slots, "workers=" + (bookThreads + 1) + " (processorCount-2 background + main); Combat = Combat dispatch + MergeDamage");
            RunSplit(id, "step3", count, 10, 120, CreateStep3(count, false), Step3ScheduleSlots, "jobs as in the book (one dependency chain); schedule rows = main-thread cost of each Schedule call, the work itself is inside the Complete row");
            RunSplit(id, "step3 isolated", count, 10, 120, CreateStep3(count, true), Step3IsolatedSlots, "dedicated variant: every job Schedule()+Complete() right away = wall time of that job alone (main thread helps the workers while waiting); same results as step3");
            if (hasGraphics) {
                RunSplit(id, "step4", count, 10, 120, CreateStep4(count), Step4Slots, "Move/Combat jobs are the same as step3 (see step3 isolated)");
            } else {
                Bench.Info(id, Chapter, "step4", -1, "SKIPPED: no graphics device (-nographics)");
            }
        }

        private static int BookBackgroundThreads() {
            return Mathf.Max(SystemInfo.processorCount - 2, 1);
        }

        /// <summary>
        /// Как RunStep, но вместе со временем кадра записывает время каждой системы (RtsSplit) в каждом кадре.
        /// slots == null: только время кадра, RtsSplit выключен.
        /// </summary>
        private static void RunSplit(string id, string prefix, int count, int warmup, int frames, StepRun run, Slot[] slots, string extraNotes) {
            var sceneRoots = SnapshotRoots();
            var timed = slots != null && slots.Length > 0;
            try {
                UnityEngine.Random.InitState(Seed);
                Time.deltaTime = 1f / 60f;
                run.setup();

                RtsSplit.Clear();
                RtsSplit.Enabled = timed;
                for (int f = 0; f < warmup; ++f) {
                    run.frame();
                }

                var frameNs = new double[frames];
                var slotNs = new double[timed ? slots.Length : 0][];
                for (int s = 0; s < slotNs.Length; ++s) {
                    slotNs[s] = new double[frames];
                }
                for (int f = 0; f < frames; ++f) {
                    RtsSplit.Clear();
                    var t0 = Stopwatch.GetTimestamp();
                    run.frame();
                    frameNs[f] = Bench.TicksToNs(Stopwatch.GetTimestamp() - t0);
                    for (int s = 0; s < slotNs.Length; ++s) {
                        slotNs[s][f] = Bench.TicksToNs(RtsSplit.Ticks[slots[s].index]);
                    }
                }
                RtsSplit.Enabled = false;

                var notes = "units=" + count + "; ns per frame, median over " + frames + " frames (+" + warmup + " warmup)";
                if (timed) notes += "; system time = Stopwatch around the call inside the step code (RtsSplit)";
                if (string.IsNullOrEmpty(extraNotes) == false) notes += "; " + extraNotes;

                Bench.FromSamples(id, Chapter, prefix + (timed ? " frame (same run)" : " frame"), frameNs, -1, notes);
                for (int s = 0; s < slotNs.Length; ++s) {
                    Bench.FromSamples(id, Chapter, prefix + " " + slots[s].name, slotNs[s], -1, notes);
                }
            } catch (Exception e) {
                Bench.Error(id, Chapter, prefix, e);
            } finally {
                RtsSplit.Enabled = false;
                RtsSplit.Clear();
                FinishRun(run, sceneRoots);
            }
        }

        // ================= 2. unit-count scaling =================

        private static void RunScale() {
            Bench.Log("rts detail: unit-count scaling ...");
            var counts = new[] { 2500, 5000, 10_000 };
            for (int i = 0; i < counts.Length; ++i) {
                var n = counts[i];
                RunSplit("rts.scale.step1", "n=" + n, n, 10, 120, CreateStep1(n), Step1Slots, null);
            }
            RunSplit("rts.scale.step0fixed", "n=2500", 2500, 2, 20, CreateStep0Fixed(2500), Step0FixedSlots, "O(n^2): only 2 500 units (1 000 and 10 000 are in rts.n*.split)");
        }

        // ================= 3. step2 thread scaling =================

        private static void RunThreadsStep2() {
            const int count = 10_000;
            const string id = "rts.threads.step2";
            Bench.Log("rts detail: step2 thread scaling ...");
            var workers = new[] { 1, 2, 4, 8 };
            for (int i = 0; i < workers.Length; ++i) {
                var w = workers[i];
                RunSplit(id, "workers=" + w, count, 10, 120, CreateStep2(count, w - 1), Step2Slots, "workers = WorkerPool.WorkerCount = " + (w - 1) + " background threads + main thread");
            }
            var book = BookBackgroundThreads();
            RunSplit(id, "workers=" + (book + 1) + " (book: processorCount-2 background)", count, 10, 120, CreateStep2(count, -1), Step2Slots, "workers = " + book + " background threads (processorCount=" + SystemInfo.processorCount + ") + main thread");
        }

        // ================= 4. step3 JobWorkerCount scaling =================

        private static void RunThreadsStep3() {
            const int count = 10_000;
            const string id = "rts.threads.step3";
            Bench.Log("rts detail: step3 JobWorkerCount scaling ...");
            int original;
            int maximum;
            try {
                original = JobsUtility.JobWorkerCount;
                maximum = JobsUtility.JobWorkerMaximumCount;
            } catch (Exception e) {
                Bench.Error(id, Chapter, "JobsUtility.JobWorkerCount", e);
                return;
            }
            var workers = new[] { 1, 2, 4, 8 };
            try {
                for (int i = 0; i < workers.Length; ++i) {
                    var w = workers[i];
                    if (w > maximum) {
                        Bench.Info(id, Chapter, "JobWorkerCount=" + w, -1, "SKIPPED: JobWorkerMaximumCount=" + maximum);
                        continue;
                    }
                    try {
                        JobsUtility.JobWorkerCount = w;
                    } catch (Exception e) {
                        Bench.Error(id, Chapter, "JobWorkerCount=" + w, e);
                        continue;
                    }
                    var actual = JobsUtility.JobWorkerCount;
                    RunSplit(id, "JobWorkerCount=" + w, count, 10, 120, CreateStep3(count, false), null, "JobWorkerCount set to " + w + ", read back " + actual + " (maximum " + maximum + ", default " + original + "); the main thread also runs jobs while it waits in Complete; frame time only");
                }
                JobsUtility.JobWorkerCount = original;
                RunSplit(id, "JobWorkerCount=" + original + " (as found)", count, 10, 120, CreateStep3(count, false), null, "JobWorkerCount left as found: " + original + " (maximum " + maximum + "); frame time only");
            } finally {
                try {
                    JobsUtility.JobWorkerCount = original;
                } catch (Exception e) {
                    Bench.Log("could not restore JobWorkerCount=" + original + ": " + e.Message);
                }
            }
        }

        // ================= 5. GC over 300 frames =================

        private static void RunGc300() {
            const int count = 1000;
            const int frames = 300;
            const string id = "rts.n1000.gc300";
            Bench.Log("rts detail: GC over " + frames + " frames, " + count + " units (step0 naive is slow: ~1 min) ...");
            RunGc(id, "step0 naive (MonoBehaviour.Update + LINQ)", count, frames, CreateStep0Naive(count));
            RunGc(id, "step0 fixed (UnitManager, no alloc, still O(n^2))", count, frames, CreateStep0Fixed(count));
        }

        private static void RunGc(string id, string variant, int count, int frames, StepRun run) {
            var sceneRoots = SnapshotRoots();
            var modeChanged = false;
            var previousMode = GarbageCollector.Mode.Enabled;
            try {
                UnityEngine.Random.InitState(Seed);
                Time.deltaTime = 1f / 60f;
                run.setup();
                run.frame();
                run.frame();

                string modeNote;
                try {
                    previousMode = GarbageCollector.GCMode;
                    GarbageCollector.GCMode = GarbageCollector.Mode.Enabled;
                    modeChanged = true;
                    modeNote = "GCMode=" + GarbageCollector.GCMode + " (was " + previousMode + "), incremental=" + GarbageCollector.isIncremental;
                } catch (Exception e) {
                    modeNote = "GCMode could not be set (" + e.Message + "), incremental=" + GarbageCollector.isIncremental;
                }

                GC.Collect();
                GC.WaitForPendingFinalizers();
                var collectionsBefore = GC.CollectionCount(0);
                var values = new double[frames];
                for (int f = 0; f < frames; ++f) {
                    var t0 = Stopwatch.GetTimestamp();
                    run.frame();
                    values[f] = Bench.TicksToNs(Stopwatch.GetTimestamp() - t0);
                    if ((f + 1) % 100 == 0) Bench.Log("rts gc300 " + variant + ": frame " + (f + 1) + "/" + frames);
                }
                var collections = GC.CollectionCount(0) - collectionsBefore;

                if (modeChanged) {
                    GarbageCollector.GCMode = previousMode;
                    modeChanged = false;
                }

                var bytes = Bench.MeasureBytes(run.frame, 5, out var allocMethod, true);
                var notes = "units=" + count + "; " + frames + " frames with GC enabled, ns per frame (GC pauses included: see p90); GC.CollectionCount(0) delta=" + collections + " (MaxGeneration=" + GC.MaxGeneration + "); bytesPerOp = bytes per frame over 5 more frames (" + allocMethod + "); " + modeNote;
                Bench.FromSamples(id, Chapter, variant, values, bytes, notes);
                Bench.Info(id, Chapter, variant + ": GC.CollectionCount(0) delta", collections, "bytesPerOp column = number of gen0 collections during " + frames + " frames (not bytes); " + modeNote);
            } catch (Exception e) {
                Bench.Error(id, Chapter, variant, e);
            } finally {
                if (modeChanged) {
                    try {
                        GarbageCollector.GCMode = previousMode;
                    } catch (Exception) {
                        // режим не менялся
                    }
                }
                FinishRun(run, sceneRoots);
            }
        }
    }
}
