using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace BookBench.Rts {

    /// <summary>
    /// Драйвер сквозного примера RTS. Все шаги симулируются внутри одного настоящего кадра Unity
    /// с фиксированным dt = 1/60 и одинаковым сидом UnityEngine.Random.
    /// </summary>
    public static partial class RtsBenchmarks {

        private const string Chapter = "rts";
        private const int Seed = 12345;

        /// <summary>true: шаг 0 наивный на 10 000 юнитов тоже меряется (очень долго, O(n² log n)).</summary>
        public static bool IncludeNaive10k = true;

        private sealed class Checksum {
            public int alive;
            public long hpSum;
            public uint hpHash = 2166136261u;
            public uint posHash = 2166136261u;
            public int frames;

            public void AddHp(int hp) {
                this.hpHash = (this.hpHash ^ (uint)hp) * 16777619u;
            }

            public void AddPos(float x, float y) {
                this.posHash = (this.posHash ^ (uint)(int)math.round(x * 1000f)) * 16777619u;
                this.posHash = (this.posHash ^ (uint)(int)math.round(y * 1000f)) * 16777619u;
            }

            public bool SameHp(Checksum other) {
                return other != null && this.alive == other.alive && this.hpSum == other.hpSum && this.hpHash == other.hpHash;
            }

            public bool SameAll(Checksum other) {
                return this.SameHp(other) && this.posHash == other.posHash;
            }

            public override string ToString() {
                return "after " + this.frames + " frames: alive=" + this.alive + ", hpSum=" + this.hpSum + ", hpHash=" + this.hpHash.ToString("X8") + ", posHash=" + this.posHash.ToString("X8");
            }
        }

        private sealed class StepRun {
            public Action setup;
            public Action frame;
            public Func<Checksum> checksum;
            public Action teardown;
        }

        public static void Run() {
            var units = new[] { 1000, 10_000 };
            for (int u = 0; u < units.Length; ++u) {
                RunAllSteps(units[u]);
            }
            if (IncludeDetail) {
                RunDetail();
            } else {
                Bench.Log("rts detail (split / scale / threads / gc300) skipped: IncludeDetail = false");
            }
        }

        private static void RunAllSteps(int count) {
            var id = "rts.n" + count;
            var big = count >= 10_000;

            // ---------- шаг 0 ----------
            if (big == false || IncludeNaive10k) {
                var naiveFrames = big ? 2 : 20;
                var naiveWarmup = big ? 0 : 2;
                var naiveAlloc = big ? 0 : 2;
                RunStep(id, "step0 naive (MonoBehaviour.Update + LINQ)", count, naiveWarmup, naiveFrames, naiveAlloc, CreateStep0Naive(count), null, null);
            }
            RunStep(id, "step0 fixed (UnitManager, no alloc, still O(n^2))", count, big ? 1 : 5, big ? 3 : 60, big ? 1 : 5, CreateStep0Fixed(count), null, null);

            // ---------- шаги 1-4: одинаковое число кадров, контрольные суммы сравнимы ----------
            const int warmup = 10;
            const int frames = 120;
            const int allocFrames = 10;
            var s1 = RunStep(id, "step1 data arrays + grid", count, warmup, frames, allocFrames, CreateStep1(count), null, null);
            var s2 = RunStep(id, "step2 threads + double buffer (processorCount-2 workers)", count, warmup, frames, allocFrames, CreateStep2(count, -1), null, null);
            RunStep(id, "step2 threads + double buffer (0 background workers)", count, warmup, frames, allocFrames, CreateStep2(count, 0), s2, "step2 (processorCount-2 workers)");
            var s3 = RunStep(id, "step3 Jobs + Burst (+ IJobParallelForTransform)", count, warmup, frames, allocFrames, CreateStep3(count), s2, "step2 (book: same algorithm, float bits may differ)");
            if (s1 != null && s2 != null) {
                Bench.Log("rts " + count + ": step1 vs step2 same result (book: expected to differ, step1 is order-dependent): " + s1.SameAll(s2));
            }
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) {
                Bench.Info(id, Chapter, "step4 instanced rendering (full frame)", -1, "SKIPPED: no graphics device (-nographics); run in Editor or player without -nographics");
            } else {
                RunStep(id, "step4 instanced rendering (full frame: jobs + cull + Draw)", count, warmup, frames, allocFrames, CreateStep4(count), s3, "step3 (book: same game logic)");
            }
        }

        private static Checksum RunStep(string id, string variant, int count, int warmup, int frames, int allocFrames, StepRun run, Checksum compareTo, string compareName) {
            var sceneRoots = SnapshotRoots();
            Checksum checksum = null;
            try {
                UnityEngine.Random.InitState(Seed);
                Time.deltaTime = 1f / 60f;
                run.setup();

                for (int f = 0; f < warmup; ++f) {
                    run.frame();
                }
                var values = new double[frames];
                for (int f = 0; f < frames; ++f) {
                    var t0 = Stopwatch.GetTimestamp();
                    run.frame();
                    values[f] = Bench.TicksToNs(Stopwatch.GetTimestamp() - t0);
                }
                double bytes = -1;
                var allocMethod = "not measured";
                if (allocFrames > 0) {
                    bytes = Bench.MeasureBytes(run.frame, allocFrames, out allocMethod, true);
                }
                checksum = run.checksum();
                checksum.frames = warmup + frames + allocFrames;
                var notes = "units=" + count + "; ns per frame; bytesPerOp = bytes per frame (" + allocMethod + "); checksum " + checksum;
                if (compareTo != null) {
                    notes += "; same hp as " + compareName + ": " + (checksum.SameHp(compareTo) ? "YES" : "NO") + ", same positions: " + (checksum.SameAll(compareTo) ? "YES" : "NO");
                }
                Bench.FromSamples(id, Chapter, variant, values, bytes, notes);
            } catch (Exception e) {
                Bench.Error(id, Chapter, variant, e);
                checksum = null;
            } finally {
                FinishRun(run, sceneRoots);
            }
            return checksum;
        }

        /// <summary>Уборка после шага: teardown, удаление созданных корней сцены, отложенные Destroy.</summary>
        private static void FinishRun(StepRun run, HashSet<GameObject> sceneRoots) {
            try {
                run.teardown();
            } catch (Exception e) {
                Bench.Log("rts teardown failed: " + e);
            }
            Step0Naive.Unit.All.Clear();
            DestroyNewRoots(sceneRoots);
            FlushDestroyQueue();
            Step0Naive.Unit.All.Clear();
        }

        // ================= helpers =================

        private static HashSet<GameObject> SnapshotRoots() {
            var set = new HashSet<GameObject>();
            var scene = SceneManager.GetActiveScene();
            if (scene.IsValid()) {
                var roots = scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; ++i) {
                    set.Add(roots[i]);
                }
            }
            return set;
        }

        private static void DestroyNewRoots(HashSet<GameObject> before) {
            var scene = SceneManager.GetActiveScene();
            if (scene.IsValid() == false) return;
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; ++i) {
                if (before.Contains(roots[i])) continue;
                PrepareNaiveForDestroy(roots[i]);
                UnityEngine.Object.DestroyImmediate(roots[i]);
            }
        }

        private static void PrepareNaiveForDestroy(GameObject go) {
            if (Application.isPlaying) return; // в плеере DestroyImmediate сам вызовет OnDisable
            var unit = go.GetComponent<Step0Naive.Unit>();
            if (unit != null) unit.OnDisable();
        }

        private static void FlushDestroyQueue() {
            var pending = RtsDestroyQueue.Pending;
            for (int i = 0; i < pending.Count; ++i) {
                var obj = pending[i];
                if (obj == null) continue;
                var go = obj as GameObject;
                if (go != null) PrepareNaiveForDestroy(go);
                UnityEngine.Object.DestroyImmediate(obj);
            }
            pending.Clear();
        }

        private static Mesh cubeMesh;

        private static Mesh CubeMesh() {
            if (cubeMesh == null) cubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            return cubeMesh;
        }

        private static Font labelFont;
        private static bool labelFontLoaded;

        private static Font LabelFont() {
            if (labelFontLoaded) return labelFont;
            labelFontLoaded = true;
            try {
                labelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            } catch (Exception) {
                labelFont = null;
            }
            return labelFont;
        }

        private static GameObject CreateUnitTemplate() {
            var go = new GameObject("RtsUnitTemplate");
            go.AddComponent<MeshFilter>().sharedMesh = CubeMesh();
            go.AddComponent<MeshRenderer>();
            go.transform.position = new Vector3(0f, -1000f, 0f);
            return go;
        }

        private static TextMesh CreateLabel(Transform parent) {
            var labelGo = new GameObject("HpLabel");
            labelGo.transform.SetParent(parent, false);
            labelGo.transform.localPosition = new Vector3(0f, 2f, 0f);
            var meshRenderer = labelGo.AddComponent<MeshRenderer>();
            var label = labelGo.AddComponent<TextMesh>();
            var font = LabelFont();
            if (font != null) {
                label.font = font;
                if (meshRenderer != null) meshRenderer.sharedMaterial = font.material;
            }
            return label;
        }

        // Тот же порядок вызовов Random, что в Start шагов 1-4
        private static void SpawnPoint(int n, out byte team, out Vector3 pos, out Vector3 target) {
            var half = UnitConfig.WorldSize * 0.4f;
            team = (byte)(n & 1);
            var side = team == 0 ? -1f : 1f;
            var p = new Vector2(side * half * UnityEngine.Random.value, UnityEngine.Random.Range(-half, half));
            pos = new Vector3(p.x, 0f, p.y);
            target = new Vector3(-side * half, 0f, p.y);
        }

        // ================= step 0 naive =================

        private static StepRun CreateStep0Naive(int count) {
            var buffer = new List<Step0Naive.Unit>(count);
            var run = new StepRun();
            run.setup = () => {
                Step0Naive.Unit.All.Clear();
                var root = CreateUnitTemplate();
                for (int n = 0; n < count; ++n) {
                    SpawnPoint(n, out var team, out var pos, out var target);
                    var go = UnityEngine.Object.Instantiate(root, pos, Quaternion.identity);
                    var label = CreateLabel(go.transform);
                    var unit = go.AddComponent<Step0Naive.Unit>(); // в плеере OnEnable вызовет Unity
                    unit.team = team;
                    unit.target = target;
                    unit.hpLabel = label;
                    if (Application.isPlaying == false) unit.OnEnable();
                }
                UnityEngine.Object.DestroyImmediate(root);
            };
            run.frame = () => {
                buffer.Clear();
                var all = Step0Naive.Unit.All;
                for (int i = 0; i < all.Count; ++i) {
                    buffer.Add(all[i]);
                }
                for (int i = 0; i < buffer.Count; ++i) {
                    var unit = buffer[i];
                    if (unit != null) unit.Update();
                }
                FlushDestroyQueue();
            };
            run.checksum = () => {
                var c = new Checksum();
                var all = Step0Naive.Unit.All;
                for (int i = 0; i < all.Count; ++i) {
                    var unit = all[i];
                    if (unit == null || unit.hp <= 0) continue;
                    ++c.alive;
                    c.hpSum += unit.hp;
                    c.AddHp(unit.hp);
                    var p = unit.transform.position;
                    c.AddPos(p.x, p.z);
                }
                return c;
            };
            run.teardown = () => {
                buffer.Clear();
            };
            return run;
        }

        // ================= step 0 fixed =================

        private static StepRun CreateStep0Fixed(int count) {
            Step0Fixed.UnitManager manager = null;
            var run = new StepRun();
            run.setup = () => {
                var managerGo = new GameObject("RtsStep0Manager");
                manager = managerGo.AddComponent<Step0Fixed.UnitManager>();
                var root = CreateUnitTemplate();
                for (int n = 0; n < count; ++n) {
                    SpawnPoint(n, out var team, out var pos, out var target);
                    var go = UnityEngine.Object.Instantiate(root, pos, Quaternion.identity);
                    var label = CreateLabel(go.transform);
                    var unit = go.AddComponent<Step0Fixed.Unit>();
                    unit.team = team;
                    unit.target = target;
                    unit.hpLabel = label;
                    manager.Register(unit);
                }
                UnityEngine.Object.DestroyImmediate(root);
            };
            run.frame = () => {
                manager.Update();
                FlushDestroyQueue();
            };
            run.checksum = () => {
                var c = new Checksum();
                for (int i = 0; i < manager.units.Count; ++i) {
                    var unit = manager.units[i];
                    if (unit == null || unit.hp <= 0) continue;
                    ++c.alive;
                    c.hpSum += unit.hp;
                    c.AddHp(unit.hp);
                    var p = unit.transform.position;
                    c.AddPos(p.x, p.z);
                }
                return c;
            };
            run.teardown = () => {
                manager = null;
            };
            return run;
        }

        // ================= step 1 =================

        private static StepRun CreateStep1(int count) {
            Step1.UnitManager manager = null;
            GameObject template = null;
            var run = new StepRun();
            run.setup = () => {
                template = CreateUnitTemplate();
                var go = new GameObject("RtsStep1Manager");
                manager = go.AddComponent<Step1.UnitManager>();
                manager.unitPrefab = template;
                manager.unitCount = count;
                manager.Start();
            };
            run.frame = () => manager.Update();
            run.checksum = () => {
                var c = new Checksum();
                var data = manager.Data;
                for (int i = 0; i < data.count; ++i) {
                    if (data.IsAlive(i) == false) continue;
                    ++c.alive;
                    c.hpSum += data.hp[i];
                    c.AddHp(data.hp[i]);
                    c.AddPos(data.position[i].x, data.position[i].y);
                }
                return c;
            };
            run.teardown = () => {
                manager = null;
            };
            return run;
        }

        // ================= step 2 =================

        private static StepRun CreateStep2(int count, int backgroundThreads) {
            Step2.UnitManager manager = null;
            var run = new StepRun();
            run.setup = () => {
                var template = CreateUnitTemplate();
                var go = new GameObject("RtsStep2Manager");
                manager = go.AddComponent<Step2.UnitManager>();
                manager.unitPrefab = template;
                manager.unitCount = count;
                manager.backgroundThreads = backgroundThreads;
                manager.Start();
            };
            run.frame = () => manager.Update();
            run.checksum = () => {
                var c = new Checksum();
                var data = manager.Data;
                for (int i = 0; i < data.count; ++i) {
                    if (data.IsAlive(i) == false) continue;
                    ++c.alive;
                    c.hpSum += data.hp[i];
                    c.AddHp(data.hp[i]);
                    c.AddPos(data.position[i].x, data.position[i].y);
                }
                return c;
            };
            run.teardown = () => {
                if (manager != null) manager.OnDestroy();
                manager = null;
            };
            return run;
        }

        // ================= step 3 =================

        private static StepRun CreateStep3(int count, bool completeEachJob = false) {
            Step3.UnitManager manager = null;
            var run = new StepRun();
            run.setup = () => {
                var template = CreateUnitTemplate();
                var go = new GameObject("RtsStep3Manager");
                manager = go.AddComponent<Step3.UnitManager>();
                manager.unitPrefab = template;
                manager.unitCount = count;
                manager.completeEachJob = completeEachJob;
                manager.Start();
            };
            run.frame = () => {
                manager.Update();
                manager.LateUpdate();
            };
            run.checksum = () => {
                var c = ChecksumOf(manager.Data);
                MeasureCullOnly(count, manager.Data);
                return c;
            };
            run.teardown = () => {
                if (manager != null) manager.OnDestroy();
                manager = null;
            };
            return run;
        }

        private static Checksum ChecksumOf(Step3.UnitBuffers data) {
            var c = new Checksum();
            for (int i = 0; i < data.count; ++i) {
                if (Step3.AliveMask.IsAlive(data.aliveMask, i) == false) continue;
                ++c.alive;
                c.hpSum += data.hp[i];
                c.AddHp(data.hp[i]);
                // после LateUpdate актуальные позиции лежат в position
                c.AddPos(data.position[i].x, data.position[i].y);
            }
            return c;
        }

        private static Camera CreateCamera() {
            var go = new GameObject("RtsBenchCamera");
            var camera = go.AddComponent<Camera>();
            camera.enabled = false;
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1000f;
            camera.aspect = 16f / 9f;
            go.transform.position = new Vector3(0f, 120f, -120f);
            go.transform.LookAt(Vector3.zero);
            return camera;
        }

        /// <summary>Шаг 4 без GPU: только CullAndBuildInstancesJob на данных шага 3 (работает и с -nographics).</summary>
        private static void MeasureCullOnly(int count, Step3.UnitBuffers data) {
            var camera = CreateCamera();
            var planes = new Plane[6];
            GeometryUtility.CalculateFrustumPlanes(camera, planes);
            var frustum = new NativeArray<float4>(6, Allocator.Persistent);
            var units = new NativeList<Step4.UnitInstance>(count, Allocator.Persistent);
            var bars = new NativeList<Step4.BarInstance>(count, Allocator.Persistent);
            try {
                for (int k = 0; k < 6; ++k) {
                    frustum[k] = new float4(planes[k].normal, planes[k].distance);
                }
                // данные после SwapPositions: актуальные позиции в position
                var r = Bench.Measure("rts.n" + count + ".step4cull", Chapter, "step4 CullAndBuildInstancesJob only (Schedule(n,128).Complete)", () => {
                    units.Clear();
                    bars.Clear();
                    new Step4.CullAndBuildInstancesJob {
                        position = data.position,
                        velocity = data.velocity,
                        hp = data.hp,
                        aliveMask = data.aliveMask,
                        frustumPlanes = frustum,
                        units = units.AsParallelWriter(),
                        bars = bars.AsParallelWriter(),
                        radius = 1f,
                        barHeight = 2.2f,
                    }.Schedule(data.count, 128).Complete();
                }, 5, 51, 1);
                r.notes = "units=" + count + ", visible=" + units.Length + ", bars=" + bars.Length + "; ns per frame";
            } finally {
                frustum.Dispose();
                units.Dispose();
                bars.Dispose();
                UnityEngine.Object.DestroyImmediate(camera.gameObject);
            }
        }

        // ================= step 4 =================

        private static Material LoadMaterial(string resourceName, string shaderName) {
            var asset = Resources.Load<Material>(resourceName);
            if (asset != null) return new Material(asset);
            var shader = Shader.Find(shaderName);
            return shader != null ? new Material(shader) : null;
        }

        private static StepRun CreateStep4(int count) {
            Step4.UnitManager manager = null;
            var drawNs = new List<double>(256);
            Material unitMaterial = null;
            Material barMaterial = null;
            var run = new StepRun();
            run.setup = () => {
                unitMaterial = LoadMaterial("RtsUnitMaterial", "Standard");
                barMaterial = LoadMaterial("RtsBarMaterial", "RTS/HealthBar");
                if (unitMaterial == null || barMaterial == null) throw new Exception("Step 4 materials not found (Resources/RtsUnitMaterial, Resources/RtsBarMaterial). Use Book menu once in Editor to create them.");
                unitMaterial.enableInstancing = true;
                var go = new GameObject("RtsStep4Manager");
                manager = go.AddComponent<Step4.UnitManager>();
                manager.unitMesh = CubeMesh();
                manager.unitMaterial = unitMaterial;
                manager.barMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                manager.barMaterial = barMaterial;
                manager.mainCamera = CreateCamera();
                manager.unitCount = count;
                manager.Start();
            };
            run.frame = () => {
                manager.Update();
                manager.LateUpdate();
                drawNs.Add(manager.LastDrawNs);
            };
            run.checksum = () => {
                var c = ChecksumOf(manager.Data);
                var draws = drawNs.ToArray();
                Bench.FromSamples("rts.n" + count + ".step4draw", Chapter, "step4 Draw() only (RenderMeshInstanced x N + SetData + RenderMeshIndirect)", draws, -1, "units=" + count + ", visible=" + manager.Renderer.VisibleUnits + ", bars=" + manager.Renderer.VisibleBars + "; ns per frame, CPU submission only");
                return c;
            };
            run.teardown = () => {
                if (manager != null) manager.OnDestroy();
                manager = null;
                if (unitMaterial != null) UnityEngine.Object.DestroyImmediate(unitMaterial);
                if (barMaterial != null) UnityEngine.Object.DestroyImmediate(barMaterial);
            };
            return run;
        }
    }
}
