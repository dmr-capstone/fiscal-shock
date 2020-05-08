using UnityEngine;
using FiscalShock.Graphs;
using FiscalShock.Procedural;

namespace FiscalShock.Pathfinding {
    public class Hivemind : MonoBehaviour {
        public Vertex lastPlayerLocation { get; set; }
        public AStar pathfinder { get; private set; }

        void Start() {
            Dungeoneer dungeoneer = GameObject.Find("DungeonSummoner").GetComponent<Dungeoneer>();
            Delaunay graph = dungeoneer.navigableDelaunay;

            pathfinder = new AStar(graph);
        }
    }
}
