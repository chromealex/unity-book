using System;
using System.Diagnostics;

namespace BookBench {

    public static class BenchSuite {

        private struct Group {
            public string name;
            public Action run;
        }

        /// <summary>Запускает все группы. Каждая группа в своем try/catch: падение одной не останавливает остальные.</summary>
        public static BenchReport RunAll(Action<string> log, bool includeTransforms = true, bool includeRts = true) {
            Action<string> write = log ?? (s => { });
            Bench.Log = s => write("[Book] " + s);
            Bench.Results.Clear();

            var report = new BenchReport {
                environment = BenchEnvironment.Capture(),
            };
            Bench.Log("start: Unity " + report.environment.unityVersion + ", " + report.environment.scriptingBackend + ", editor=" + report.environment.isEditor + ", burst=" + report.environment.burstEnabled + ", burstSafety=" + report.environment.burstSafetyChecks);

            var groups = new System.Collections.Generic.List<Group> {
                new Group { name = "harness", run = RunCalibration },
                new Group { name = "ch1", run = Ch01CSharp.Run },
                new Group { name = "ch2", run = Ch02Memory.Run },
                new Group { name = "ch3", run = Ch03Unsafe.Run },
                new Group { name = "ch3.extra", run = Ch03Extra.Run },
                new Group { name = "ch4", run = Ch04Threads.Run },
                new Group { name = "ch5", run = Ch05Jobs.Run },
                new Group { name = "ch5.extra", run = Ch05Extra.Run },
                new Group { name = "ch9", run = Ch09Math.Run },
                new Group { name = "ch10", run = Ch10Algorithms.Run },
            };
            if (includeTransforms) groups.Add(new Group { name = "ch5.transforms", run = Ch05Jobs.RunTransforms });
            if (includeRts) groups.Add(new Group { name = "rts", run = Rts.RtsBenchmarks.Run });

            var total = Stopwatch.StartNew();
            for (int i = 0; i < groups.Count; ++i) {
                var group = groups[i];
                var sw = Stopwatch.StartNew();
                Bench.Log("group " + group.name + " ...");
                try {
                    group.run();
                } catch (Exception e) {
                    Bench.Error(group.name, group.name, "group failed", e);
                }
                GC.Collect();
                Bench.Log("group " + group.name + " done in " + (sw.ElapsedMilliseconds / 1000.0).ToString("0.0") + " s");
            }
            Bench.Log("all groups done in " + (total.ElapsedMilliseconds / 1000.0).ToString("0.0") + " s");

            report.results.AddRange(Bench.Results);
            return report;
        }

        private static void RunCalibration() {
            Bench.MeasureAlloc("harness.alloc", "harness", "nothing (expect 0)", () => Bench.SinkInt++, 10_000);
            Bench.MeasureAlloc("harness.alloc", "harness", "new object() (expect 16-24)", () => Bench.SinkObject = new object(), 10_000);
            Bench.MeasureAlloc("harness.alloc", "harness", "new byte[1000] (expect ~1024-1040)", () => Bench.SinkObject = new byte[1000], 10_000);
            Bench.SinkObject = null;
            Bench.Measure("harness.empty", "harness", "empty action (timer overhead)", () => { }, 5, 101, 1);
        }
    }
}
