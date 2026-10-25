using System;
using System.Collections.Generic;

// Общие типы сквозного примера RTS (главы 2-8). Код из книги лежит в подпапках Step*,
// каждый шаг в своем пространстве имен, имена типов как в книге.
namespace BookBench.Rts {

    // Шаг 1 (дополняет конфиг шага 0 полем WorldSize)
    public static class UnitConfig {
        public const float Speed = 4f;
        public const float SeparationRadius = 1f;
        public const float AttackRadius = 6f;
        public const float AttackCooldown = 1f;
        public const int Damage = 10;
        public const int MaxHp = 100;
        public const float CellSize = AttackRadius;
        public const float WorldSize = 600f;
    }

    public enum UnitState : byte {
        Moving = 0,
        Attacking = 1,
        Dead = 2,
    }

    [Flags]
    public enum UnitFlags : byte {
        None = 0,
        Alive = 1 << 0,
        Selected = 1 << 1,
    }

    /// <summary>
    /// [bench] Подмена UnityEngine.Time для кода из книги. Все шаги симулируются внутри одного
    /// настоящего кадра, поэтому шаг времени фиксированный. Код книги пишет `Time.deltaTime`
    /// и без изменений попадает сюда: тип из объемлющего пространства имен BookBench.Rts
    /// находится раньше, чем `using UnityEngine;`.
    /// </summary>
    public static class Time {
        public static float deltaTime = 1f / 60f;
    }

    /// <summary>
    /// [bench] В Unity Destroy откладывает уничтожение до конца кадра, а в режиме редактирования
    /// вообще запрещен. Шаг 0 вызывает Destroy, мы складываем объекты сюда, а драйвер
    /// уничтожает их в конце каждого симулированного кадра.
    /// </summary>
    public static class RtsDestroyQueue {
        public static readonly List<UnityEngine.Object> Pending = new List<UnityEngine.Object>(1024);

        public static void Enqueue(UnityEngine.Object obj) {
            Pending.Add(obj);
        }
    }
}
