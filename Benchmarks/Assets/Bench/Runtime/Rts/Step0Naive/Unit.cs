using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Шаг 0, наивная версия (rts_0.md). Изменения для стенда помечены [bench].
namespace BookBench.Rts.Step0Naive {

    public class Unit : MonoBehaviour {

        public static readonly List<Unit> All = new List<Unit>();

        public byte team;
        public int hp = UnitConfig.MaxHp;
        public Vector3 target;
        public TextMesh hpLabel;
        private float cooldown;

        // [bench] public: в режиме редактирования Unity не вызывает OnEnable/OnDisable/Update, их вызывает драйвер
        public void OnEnable() {
            All.Add(this);
        }

        public void OnDisable() {
            All.Remove(this); // O(n) поиск по списку
        }

        public void Update() {
            var tMove = RtsSplit.Begin(); // [bench]
            var pos = this.transform.position;

            // Расталкивание: новый List и замыкание на каждого юнита каждый кадр
            var neighbours = All
                .Where(u => u != this && Vector3.Distance(u.transform.position, pos) < UnitConfig.SeparationRadius)
                .ToList();
            var separation = Vector3.zero;
            foreach (var n in neighbours) {
                separation += (pos - n.transform.position).normalized;
            }

            var dir = (this.target - pos).normalized + separation;
            this.transform.position = pos + dir.normalized * UnitConfig.Speed * Time.deltaTime;
            RtsSplit.End(RtsSplit.Move, tMove); // [bench]
            var tCombat = RtsSplit.Begin(); // [bench]

            // Ближайший враг: сортировка всех живых чужих юнитов
            var enemy = All
                .Where(u => u.team != this.team && u.hp > 0)
                .OrderBy(u => Vector3.Distance(u.transform.position, pos))
                .FirstOrDefault();

            this.cooldown -= Time.deltaTime;
            if (enemy != null && Vector3.Distance(enemy.transform.position, pos) < UnitConfig.AttackRadius && this.cooldown <= 0f) {
                enemy.TakeDamage(UnitConfig.Damage);
                this.cooldown = UnitConfig.AttackCooldown;
            }
            RtsSplit.End(RtsSplit.Combat, tCombat); // [bench]

            var tLabels = RtsSplit.Begin(); // [bench]
            this.hpLabel.text = "HP: " + this.hp; // новая строка каждый кадр
            RtsSplit.End(RtsSplit.Labels, tLabels); // [bench]
        }

        public void TakeDamage(int damage) {
            this.hp -= damage;
            if (this.hp <= 0) Destroy(this.gameObject);
        }

        // [bench] отложенный Destroy, см. RtsDestroyQueue
        private static new void Destroy(UnityEngine.Object obj) {
            RtsDestroyQueue.Enqueue(obj);
        }
    }
}
