using System;
using Unity.Mathematics;
using UnityEngine;

// Шаг 2 (rts_2.md): UnitData из шага 1 + второй буфер позиций.
// Книга показывает только изменения ("остальные поля как в шаге 1"), здесь класс собран целиком.
namespace BookBench.Rts.Step2 {

    public sealed class UnitData {

        public readonly int capacity;
        public int count;

        // Горячие: читаются всеми фазами каждый кадр
        public Vector2[] position;
        public Vector2[] nextPosition;
        public readonly Vector2[] velocity;
        // Теплые: читаются и пишутся владельцем
        public readonly Vector2[] target;
        public readonly float[] cooldown;
        public readonly int[] hp;
        public readonly int[] targetEnemy;
        // Маленькие
        public readonly byte[] team;
        public readonly UnitState[] state;
        public readonly UnitFlags[] flags;
        // Один бит на юнит
        public readonly ulong[] aliveMask;

        public UnitData(int capacity) {
            this.capacity = capacity;
            this.position = new Vector2[capacity];
            this.nextPosition = new Vector2[capacity];
            this.velocity = new Vector2[capacity];
            this.target = new Vector2[capacity];
            this.cooldown = new float[capacity];
            this.hp = new int[capacity];
            this.targetEnemy = new int[capacity];
            this.team = new byte[capacity];
            this.state = new UnitState[capacity];
            this.flags = new UnitFlags[capacity];
            this.aliveMask = new ulong[(capacity + 63) >> 6];
        }

        public int Add(Vector2 position, Vector2 target, byte team) {
            var i = this.count++;
            this.position[i] = position;
            this.nextPosition[i] = position;
            this.velocity[i] = Vector2.zero;
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
            var tmp = this.position;
            this.position = this.nextPosition;
            this.nextPosition = tmp;
        }

        public bool IsAlive(int i) => (this.aliveMask[i >> 6] & (1UL << (i & 63))) != 0;

        public void Kill(int i) {
            this.aliveMask[i >> 6] &= ~(1UL << (i & 63));
            this.flags[i] &= ~UnitFlags.Alive;
            this.state[i] = UnitState.Dead;
        }

        public int AliveCount() {
            var result = 0;
            for (int w = 0; w < this.aliveMask.Length; ++w) {
                result += math.countbits(this.aliveMask[w]);
            }
            return result;
        }
    }
}
