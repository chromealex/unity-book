using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BookBench.Rts {

    public enum RtsRenderKind {
        GameObjectsNoInstancing = 0,
        GameObjectsInstancing = 1,
        Step4Instanced = 2,
    }

    /// <summary>
    /// [bench] Сцена для замеров рендеринга 10 000 юнитов.
    /// «До» — шаг 3: каждый юнит — GameObject с MeshFilter + MeshRenderer (куб, материал RtsUnitMaterial,
    /// два варианта: GPU instancing у материала выключен / включен).
    /// «После» — шаг 4: RenderMeshInstanced + RenderMeshIndirect (полоски HP).
    /// Кадр симуляции (Update / LateUpdate) вызывает владелец: RtsRenderStatsRunner в плеере или меню в редакторе.
    /// Компоненты менеджеров выключены (enabled = false), чтобы Unity сама не вызвала их Start/Update.
    /// </summary>
    public sealed class RtsRenderScenario : IDisposable {

        private const int Seed = 12345;

        public readonly RtsRenderKind kind;
        public readonly int count;

        private readonly HashSet<GameObject> rootsBefore = new HashSet<GameObject>();
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        private Step3.UnitManager step3;
        private Step4.UnitManager step4;

        public static string Describe(RtsRenderKind kind) {
            switch (kind) {
                case RtsRenderKind.GameObjectsNoInstancing:
                    return "before: step3 GameObjects + MeshRenderer (instancing off)";
                case RtsRenderKind.GameObjectsInstancing:
                    return "before: step3 GameObjects + MeshRenderer (material instancing on)";
                default:
                    return "after: step4 RenderMeshInstanced + RenderMeshIndirect";
            }
        }

        /// <summary>Та же точка обзора, что у камеры шага 4 в RtsBenchmarks. target == null: рендер на экран.</summary>
        public static Camera CreateCamera(RenderTexture target) {
            var go = new GameObject("RtsRenderCamera");
            var camera = go.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1000f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.2f, 0.25f, 0.3f, 1f);
            if (target != null) {
                camera.targetTexture = target;
                camera.enabled = false; // рендер вручную через Camera.Render()
            }
            go.transform.position = new Vector3(0f, 120f, -120f);
            go.transform.LookAt(Vector3.zero);
            return camera;
        }

        public static Light CreateLight() {
            var go = new GameObject("RtsRenderLight");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Hard;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            return light;
        }

        public RtsRenderScenario(RtsRenderKind kind, int count, Camera camera) {
            this.kind = kind;
            this.count = count;
            var scene = SceneManager.GetActiveScene();
            if (scene.IsValid()) {
                var roots = scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; ++i) {
                    this.rootsBefore.Add(roots[i]);
                }
            }

            UnityEngine.Random.InitState(Seed);
            Time.deltaTime = 1f / 60f;

            var unitSource = Resources.Load<Material>("RtsUnitMaterial");
            if (unitSource == null) throw new Exception("Resources/RtsUnitMaterial not found. Use the Book menu once in the Editor to create it.");
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");

            if (kind == RtsRenderKind.Step4Instanced) {
                var barSource = Resources.Load<Material>("RtsBarMaterial");
                if (barSource == null) throw new Exception("Resources/RtsBarMaterial not found. Use the Book menu once in the Editor to create it.");
                var unitMaterial = new Material(unitSource);
                unitMaterial.enableInstancing = true;
                var barMaterial = new Material(barSource);
                this.owned.Add(unitMaterial);
                this.owned.Add(barMaterial);

                var go = new GameObject("RtsRenderStep4");
                this.step4 = go.AddComponent<Step4.UnitManager>();
                this.step4.enabled = false;
                this.step4.unitMesh = cube;
                this.step4.unitMaterial = unitMaterial;
                this.step4.barMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                this.step4.barMaterial = barMaterial;
                this.step4.mainCamera = camera;
                this.step4.unitCount = count;
                this.step4.Start();
            } else {
                var material = new Material(unitSource);
                material.enableInstancing = kind == RtsRenderKind.GameObjectsInstancing;
                this.owned.Add(material);

                var template = new GameObject("RtsRenderUnitTemplate");
                template.AddComponent<MeshFilter>().sharedMesh = cube;
                template.AddComponent<MeshRenderer>().sharedMaterial = material;

                var go = new GameObject("RtsRenderStep3");
                this.step3 = go.AddComponent<Step3.UnitManager>();
                this.step3.enabled = false;
                this.step3.unitPrefab = template;
                this.step3.unitCount = count;
                this.step3.Start();
                UnityEngine.Object.DestroyImmediate(template);
            }
        }

        public void Update() {
            if (this.step3 != null) {
                this.step3.Update();
            } else {
                this.step4.Update();
            }
        }

        public void LateUpdate() {
            if (this.step3 != null) {
                this.step3.LateUpdate();
            } else {
                this.step4.LateUpdate();
            }
        }

        public int AliveUnits() {
            var data = this.step3 != null ? this.step3.Data : this.step4.Data;
            if (data == null) return 0;
            var alive = 0;
            for (int i = 0; i < data.count; ++i) {
                if (Step3.AliveMask.IsAlive(data.aliveMask, i)) ++alive;
            }
            return alive;
        }

        public string Info() {
            var text = "units=" + this.count + ", alive=" + this.AliveUnits();
            if (this.step4 != null && this.step4.Renderer != null) {
                text += ", visible (step4 cull)=" + this.step4.Renderer.VisibleUnits + ", bars=" + this.step4.Renderer.VisibleBars;
            } else {
                text += ", Unity culls the GameObjects itself, no HP bars";
            }
            return text + "; camera (0,120,-120) looking at origin, directional light with hard shadows, Built-in RP";
        }

        public void Dispose() {
            if (this.step3 != null) this.step3.OnDestroy();
            if (this.step4 != null) this.step4.OnDestroy();
            this.step3 = null;
            this.step4 = null;

            var scene = SceneManager.GetActiveScene();
            if (scene.IsValid()) {
                var roots = scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; ++i) {
                    if (this.rootsBefore.Contains(roots[i])) continue;
                    UnityEngine.Object.DestroyImmediate(roots[i]);
                }
            }
            for (int i = 0; i < this.owned.Count; ++i) {
                if (this.owned[i] != null) UnityEngine.Object.DestroyImmediate(this.owned[i]);
            }
            this.owned.Clear();
        }
    }
}
