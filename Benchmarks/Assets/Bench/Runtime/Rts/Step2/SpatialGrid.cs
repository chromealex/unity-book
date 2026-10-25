using System;
using UnityEngine;

// Шаг 2: SpatialGrid из шага 1 без изменений, только работает с UnitData шага 2.
namespace BookBench.Rts.Step2 {

    public sealed class SpatialGrid {

        public readonly int width;
        public readonly int height;
        public readonly float cellSize;
        public readonly Vector2 origin;

        // cellStart[c]..cellStart[c + 1] — диапазон ячейки c в cellItems
        public readonly int[] cellStart;
        public readonly int[] cellItems;

        private readonly int[] unitCell;
        private readonly int[] cursor;

        public SpatialGrid(float worldSize, float cellSize, int capacity) {
            this.cellSize = cellSize;
            this.width = (int)Math.Ceiling(worldSize / cellSize);
            this.height = this.width;
            this.origin = new Vector2(-worldSize * 0.5f, -worldSize * 0.5f);
            this.cellStart = new int[this.width * this.height + 1];
            this.cellItems = new int[capacity];
            this.unitCell = new int[capacity];
            this.cursor = new int[this.width * this.height];
        }

        public int CellX(float x) => Math.Clamp((int)((x - this.origin.x) / this.cellSize), 0, this.width - 1);
        public int CellY(float y) => Math.Clamp((int)((y - this.origin.y) / this.cellSize), 0, this.height - 1);

        public void Build(UnitData data) {
            var cells = this.width * this.height;
            Array.Clear(this.cellStart, 0, this.cellStart.Length);

            for (int i = 0; i < data.count; ++i) {
                if (data.IsAlive(i) == false) continue;
                var p = data.position[i];
                var c = this.CellY(p.y) * this.width + this.CellX(p.x);
                this.unitCell[i] = c;
                ++this.cellStart[c + 1];
            }

            for (int c = 1; c <= cells; ++c) {
                this.cellStart[c] += this.cellStart[c - 1];
            }

            Array.Copy(this.cellStart, this.cursor, cells);
            for (int i = 0; i < data.count; ++i) {
                if (data.IsAlive(i) == false) continue;
                this.cellItems[this.cursor[this.unitCell[i]]++] = i;
            }
        }
    }
}
