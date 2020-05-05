using UnityEngine;
using FiscalShock.Graphs;
using FiscalShock.AI;

namespace FiscalShock.Pathfinding {
    public class MovementTrigger : MonoBehaviour {
        public Vertex cellSite { get; set; }
        private Hivemind hivemind { get; set; }

        void Start() {
            hivemind = GameObject.Find("DungeonSummoner").GetComponent<Hivemind>();
        }

        void OnTriggerEnter(Collider col) {
            if (col.gameObject.layer == 11) {
                // Debug.Log($"Player stepped into {gameObject.name}");
                hivemind.lastPlayerLocation = cellSite;
            }

            if (col.gameObject.layer == 16) {
                // Debug.Log($"Debt Collector stepped into {gameObject.name}");
                col.gameObject.GetComponentInChildren<DebtCollectorMovement>().lastVisitedNode = cellSite;
            }
        }
    }
}
