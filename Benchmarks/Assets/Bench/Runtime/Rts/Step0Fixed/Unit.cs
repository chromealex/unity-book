using UnityEngine;

// Шаг 0, исправленная версия (rts_0.md).
namespace BookBench.Rts.Step0Fixed {

    public class Unit : MonoBehaviour {
        public byte team;
        public int hp = UnitConfig.MaxHp;
        public Vector3 target;
        public float cooldown;
        public TextMesh hpLabel;
        public int shownHp = -1;
    }
}
