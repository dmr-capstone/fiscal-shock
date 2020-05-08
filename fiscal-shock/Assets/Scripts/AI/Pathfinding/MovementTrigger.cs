using UnityEngine;
using FiscalShock.Graphs;
using FiscalShock.AI;

namespace FiscalShock.Pathfinding {
    public class MovementTrigger : MonoBehaviour {
        public Vertex cellSite { get; set; }
        private Hivemind hivemind { get; set; }
        private DebtCollectorMovement dcMovement;

        void Start() {
            hivemind = GameObject.Find("DungeonSummoner").GetComponent<Hivemind>();
        }

        void OnTriggerEnter(Collider col) {
            if (col.gameObject.layer == 11) {
                // Debug.Log($"Player stepped into {gameObject.name}");
                hivemind.lastPlayerLocation = cellSite;
            }

            if (col.gameObject.layer == 16) {
                if (dcMovement == null) {
                    dcMovement = col.gameObject.GetComponentInChildren<DebtCollectorMovement>();
                }

                // Debug.Log($"Debt Collector stepped into {gameObject.name}");
                dcMovement.lastVisitedNode = cellSite;
                dcMovement.saveCounter = 0;
            }
        }
    }
}
