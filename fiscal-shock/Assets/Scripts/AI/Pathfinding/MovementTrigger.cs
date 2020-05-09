using UnityEngine;
using FiscalShock.Graphs;
using FiscalShock.AI;

namespace FiscalShock.Pathfinding {
    /// <summary>
    /// Class describing a trigger zone associated with a cell.
    /// </summary>
    public class MovementTrigger : MonoBehaviour {

        /// <summary>
        /// The cell vertex with which this trigger is associated.
        /// </summary>
        public Vertex cellSite { get; set; }

        /// <summary>
        /// Used to set the last visited location of the player.
        /// </summary>
        private Hivemind hivemind;

        /// <summary>
        /// Used to set the last visited location of the debt collector.
        /// </summary>
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
                if (dcMovement.recentlyVisitedNodes.Contains(cellSite)) {
                    dcMovement.saveCounter++;
                    return;
                }
                if (dcMovement.recentlyVisitedNodes.Count >= 3) {
                    dcMovement.recentlyVisitedNodes.RemoveAt(0);
                }
                dcMovement.recentlyVisitedNodes.Add(cellSite);
                dcMovement.saveCounter = 0;
            }
        }
    }
}
