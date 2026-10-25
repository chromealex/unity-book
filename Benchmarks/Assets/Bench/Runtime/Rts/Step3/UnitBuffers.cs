using System;
using Unity.Collections;
using Unity.Mathematics;

// Шаг 3 (rts_3.md), без изменений.
namespace BookBench.Rts.Step3 {

    public sealed class UnitBuffers : IDisposable {

        public readonly int capacity;
        public int count;

        public NativeArray<float2> position;
        public NativeArray<float2> nextPosition;
        public NativeArray<float2> velocity;
        public NativeArray<float2> target;
        public NativeArray<float> cooldown;
        public NativeArray<int> hp;
        public NativeArray<int> targetEnemy;
        public NativeArray<byte> team;
        public NativeArray<UnitState> state;
        public NativeArray<UnitFlags> flags;
        public NativeArray<ulong> aliveMask;

        // Сетка и служебные буферы
        public NativeArray<int> unitCell;
        public NativeArray<int> cellStart;
        public NativeArray<int> cellItems;
        public NativeArray<int> cellCursor;
        public NativeQueue<int> hits;
        public NativeList<int> died;

        public UnitBuffers(int capacity, int cellCount) {
            this.capacity = capacity;
            this.position = new NativeArray<float2>(capacity, Allocator.Persistent);
            this.nextPosition = new NativeArray<float2>(capacity, Allocator.Persistent);
            this.velocity = new NativeArray<float2>(capacity, Allocator.Persistent);
            this.target = new NativeArray<float2>(capacity, Allocator.Persistent);
            this.cooldown = new NativeArray<float>(capacity, Allocator.Persistent);
            this.hp = new NativeArray<int>(capacity, Allocator.Persistent);
            this.targetEnemy = new NativeArray<int>(capacity, Allocator.Persistent);
            this.team = new NativeArray<byte>(capacity, Allocator.Persistent);
            this.state = new NativeArray<UnitState>(capacity, Allocator.Persistent);
            this.flags = new NativeArray<UnitFlags>(capacity, Allocator.Persistent);
            this.aliveMask = new NativeArray<ulong>((capacity + 63) >> 6, Allocator.Persistent);
            this.unitCell = new NativeArray<int>(capacity, Allocator.Persistent);
            this.cellStart = new NativeArray<int>(cellCount + 1, Allocator.Persistent);
            this.cellItems = new NativeArray<int>(capacity, Allocator.Persistent);
            this.cellCursor = new NativeArray<int>(cellCount, Allocator.Persistent);
            this.hits = new NativeQueue<int>(Allocator.Persistent);
            this.died = new NativeList<int>(256, Allocator.Persistent);
        }

        public int Add(float2 position, float2 target, byte team) {
            var i = this.count++;
            this.position[i] = position;
            this.nextPosition[i] = position;
            this.velocity[i] = float2.zero;
            this.target[i] = target;
            this.cooldown[i] = 0f;
            this.hp[i] = UnitConfig.MaxHp;
            this.targetEnemy[i] = -1;
            this.team[i] = team;
            this.state[i] = UnitState.Moving;
            this.flags[i] = UnitFlags.Alive;
            this.aliveMask[i >> 6] |= 1UL << (i & 63);
            return i;
        }

        public void SwapPositions() {
            (this.position, this.nextPosition) = (this.nextPosition, this.position);
        }

        public void Dispose() {
            this.position.Dispose();
            this.nextPosition.Dispose();
            this.velocity.Dispose();
            this.target.Dispose();
            this.cooldown.Dispose();
            this.hp.Dispose();
            this.targetEnemy.Dispose();
            this.team.Dispose();
            this.state.Dispose();
            this.flags.Dispose();
            this.aliveMask.Dispose();
            this.unitCell.Dispose();
            this.cellStart.Dispose();
            this.cellItems.Dispose();
            this.cellCursor.Dispose();
            this.hits.Dispose();
            this.died.Dispose();
        }
    }
}
