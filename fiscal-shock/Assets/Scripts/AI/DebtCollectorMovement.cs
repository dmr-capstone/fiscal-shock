using UnityEngine;
using System.Collections.Generic;
using FiscalShock.Graphs;
using FiscalShock.Pathfinding;
using FiscalShock.Procedural;
using System.IO;

namespace FiscalShock.AI {
    public class DebtCollectorMovement : MonoBehaviour {
        [Tooltip("The speed at which the object moves.")]
        public float movementSpeed = 3f;

        [Tooltip("The speed at which the object turns.")]
        public float rotationSpeed = 7f;

        [Tooltip("The absolute minimum distance away from the player.")]
        public float safeRadiusMin = 4f;

        [Tooltip("Creates safe radius in case object ends up too close to player.")]
        public float safeRadiusMax = 5f;

        [Tooltip("How close the player needs to be before being pursued.")]
        public float visionRadius = 35f;
        public GameObject player;
        public bool stunned { get; set; }
        public float distanceFromPlayer3D { get; private set; }
        public float distanceFromPlayer2D { get; private set; }
        private CharacterController controller;

        // Pathfinding
        public Vertex spawnPoint { get; set; }
        internal Vertex lastVisitedNode { get; set; } = null;
        private Vertex nextDestinationNode = null;
        private Hivemind hivemind;
        private AStar pathfinder;
        private Stack<Vertex> path;
        private Vector3 nextDestination;
        private Vector3 nextFlatDir;
        private int recalculationCount = -1;
        private int recalculationRate = 500;

        // Raycasting
        private Vector3 forwardWhisker;
        private Vector3 left75, right75, left120, right120, left150, right150, backward;
        private float whiskerLength = 5f;
        private int whiskerSampleRate = 10;
        private int whiskerSampleCounter = 0;

        // Used to determine if the debt collector is stuck.
        private int teleportationSaveRate =  300;
        internal int saveCounter = 0;
        private float teleportationHeight;

        // Avoid walls, explosives, and obstacles.
        private LayerMask avoidance = (1 << 12) | (1 << 14) | (1 << 15);

        void Start() {
            if (player == null) {
                player = GameObject.FindGameObjectWithTag("Player");
            }

            controller = GetComponentInParent<CharacterController>();

            GameObject dungeonMaster = GameObject.Find("DungeonSummoner");

            hivemind = dungeonMaster.GetComponent<Hivemind>();
            pathfinder = hivemind.pathfinder;
            lastVisitedNode = spawnPoint;
            teleportationHeight = dungeonMaster.GetComponent<Dungeoneer>().currentDungeonType.wallHeight * 0.8f;

            // DEBUG
            Debug.Log("LAST VISITED NODE: " + lastVisitedNode.vector);
        }

        // TODO: Adjust so that takes the step distance into account.
        // TODO: Need to use both the target direction AND the current foward angle to determine the safe direction!!
        private Vector3 findSafeDirection(Vector3 target, Vector3 currentForward) {
            forwardWhisker = target;

            if (whiskerSampleCounter >= whiskerSampleRate) {
                whiskerSampleCounter = 0;

                // Draw the forward whisker.
                #if UNITY_EDITOR
                Debug.DrawRay(transform.position, forwardWhisker * whiskerLength, Color.blue, 2);
                #endif

                // Create right 75 degrees and left 75 degrees whiskers.
                left75 = Quaternion.Euler(0, -75, 0) * forwardWhisker;
                right75 = Quaternion.Euler(0, 75, 0) * forwardWhisker;

                RaycastHit hit;
                // Check the forward whisker.
                if (Physics.Raycast(transform.position, forwardWhisker, out hit, whiskerLength, avoidance)) {
                    // Debug.Log($"Forward whisker hit {hit.collider.gameObject.name}");

                    // Draw the left and right whiskers.
                    #if UNITY_EDITOR
                    Debug.DrawRay(transform.position, right75 * whiskerLength, Color.red, 1);
                    Debug.DrawRay(transform.position, left75 * whiskerLength, Color.green, 1);
                    #endif

                    // Find out if the 75 degree left and right whiskers hits something.
                    bool hitLeft, hitRight;
                    hitLeft = Physics.Raycast(transform.position, left75, whiskerLength, avoidance);
                    hitRight = Physics.Raycast(transform.position, right75, whiskerLength, avoidance);

                    // Determine which whiskers were hit;
                    if (hitLeft && !hitRight) {
                        return right75;
                    }

                    else if (!hitLeft && hitRight) {
                        return left75;
                    }

                    // Empty left and right to reuse for the next set of whiskers.
                    else if (hitLeft && hitRight) {
                        hitLeft = false;
                        hitRight = false;
                    }

                    // Neither left nor right whisker hit.
                    // TODO: Check the necessity of this case?
                    else {
                        return left75;
                    }

                    // If didn't return in one of the previous cases, good sign that need to check next whiskers.
                    // Calculate 120 degree left and right whiskers.
                    left120 = Quaternion.Euler(0, -120, 0) * forwardWhisker;
                    right120 = Quaternion.Euler(0, 120, 0) * forwardWhisker;

                    // Draw the whiskers.
                    #if UNITY_EDITOR
                    Debug.DrawRay(transform.position, right120 * whiskerLength, Color.gray, 1);
                    Debug.DrawRay(transform.position, left120 * whiskerLength, Color.yellow, 1);
                    #endif

                    // Find out if the 120 degree left and right whiskers hit something.
                    hitLeft = Physics.Raycast(transform.position, left120, whiskerLength, avoidance);
                    hitRight = Physics.Raycast(transform.position, right120, whiskerLength, avoidance);

                    // Determine which whiskers were hit;
                    if (hitLeft && !hitRight) {
                        return right120;
                    }

                    else if (!hitLeft && hitRight) {
                        return left120;
                    }

                    // Empty left and right to reuse for the next set of whiskers.
                    else if (hitLeft && hitRight) {
                        hitLeft = false;
                        hitRight = false;
                    }

                    // Neither left nor right whisker hit.
                    // TODO: Check the necessity of this case?
                    else {
                        return left120;
                    }

                    // If didn't return in one of the previous cases, good sign that need to check next whiskers.
                    // Calculate 150 degree left and right whiskers.
                    left150 = Quaternion.Euler(0, -150, 0) * forwardWhisker;
                    right150 = Quaternion.Euler(0, 150, 0) * forwardWhisker;

                    // Draw the whiskers.
                    #if UNITY_EDITOR
                    Debug.DrawRay(transform.position, right150 * whiskerLength, Color.magenta, 1);
                    Debug.DrawRay(transform.position, left150 * whiskerLength, Color.white, 1);
                    #endif

                    // Find out if the 120 degree left and right whiskers hit something.
                    hitLeft = Physics.Raycast(transform.position, left150, whiskerLength, avoidance);
                    hitRight = Physics.Raycast(transform.position, right150, whiskerLength, avoidance);

                    // Determine which whiskers were hit;
                    if (hitLeft && !hitRight) {
                        return right150;
                    }

                    else if (!hitLeft && hitRight) {
                        return left150;
                    }

                    // Empty left and right to reuse for the next set of whiskers.
                    else if (hitLeft && hitRight) {
                        hitLeft = false;
                        hitRight = false;
                    }

                    // Neither left nor right whisker hit.
                    // TODO: Check the necessity of this case?
                    else {
                        return left150;
                    }

                    // If absolutely no other case works, return the backwards angle.
                    backward = Vector3.Reflect(forwardWhisker * whiskerLength, hit.normal);
                    #if UNITY_EDITOR
                    Debug.DrawRay(transform.position, backward * whiskerLength, Color.cyan, 1);
                    #endif
                    return backward;
                }
            }

            return forwardWhisker;
        }

        void FixedUpdate() {
            // Can be stunned, but not hurt. He is immortal. Only death can free you of debt.
            // (Or, ya' know, paying off your debt.)
            // TODO: implement ability to stun debt collector.
            if (player == null || stunned) {
                return;
            }

            Vector3 playerDirection = (player.transform.position - transform.position).normalized;
            Vector3 flatPlayerDirection = new Vector3(playerDirection.x, 0, playerDirection.z);
            Vector2 flatPosition = new Vector2(transform.position.x, transform.position.z);
            Vector2 playerFlatPosition = new Vector2(player.transform.position.x, player.transform.position.z);

            distanceFromPlayer3D = Vector3.Distance(player.transform.position, transform.position);
            // Need 2D distance - will only consider how far away enemy is from player on x,z plane.
            distanceFromPlayer2D = Vector2.Distance(playerFlatPosition, flatPosition);

            // Increase the raycast sample rate counter.
            whiskerSampleCounter++;
            saveCounter++;

            // DC has been in the same cell for too long
            if (saveCounter >= teleportationSaveRate) {
                if (path != null && path.Count > 0) {
                    if (nextDestinationNode == null) {
                        // Grab the next destination from the path.
                        nextDestinationNode = path.Pop();
                    }

                    // Turn off the character controller.
                    controller.enabled = false;

                    // Teleport to the nextDestinationNode (at 80% of max height).
                    transform.parent.position = new Vector3(nextDestinationNode.x, teleportationHeight, nextDestinationNode.y);

                    // Turn on the character controller again.
                    controller.enabled = true;
                    lastVisitedNode = nextDestinationNode;

                    if (path.Count > 0) {
                        nextDestinationNode = path.Pop();
                    }

                    else {
                        path = null;
                        recalculationCount = 0;
                    }
                }

                else {
                    // Spawn point is default. Should technically never be used.
                    Vertex teleportTo = spawnPoint;

                    foreach (Cell c in lastVisitedNode.cell.neighbors) {
                        if (c.reachable) {
                            teleportTo = c.site;
                            break;
                        }
                    }

                    controller.enabled = false;
                    transform.parent.position = new Vector3(teleportTo.x, teleportationHeight, teleportTo.y);
                    controller.enabled = true;
                    lastVisitedNode = teleportTo;

                    if (path != null) {
                        path = null;
                        recalculationCount = 0;
                    }
                }

                saveCounter = 0;
                return;
            }

            // Straight line pursuit. Want to catch player, so no retreat.
            // TODO: Determine if need a retreat? 
            if (distanceFromPlayer2D < visionRadius) {
                // Unlikely that the path will be valid if player gets away.
                if (path != null) {
                    path = null;
                    recalculationCount = 0;
                }

                // Obtain the "safe" direction to go.
                Vector3 safeDir = findSafeDirection(flatPlayerDirection, transform.forward);

                #if UNITY_EDITOR
                Debug.DrawRay(transform.position, safeDir * whiskerLength, Color.black, 1);
                #endif

                // Quaternion rotationToPlayer = Quaternion.LookRotation(playerDirection);
                Quaternion safeDirRotation = Quaternion.LookRotation(safeDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, safeDirRotation, Time.deltaTime * rotationSpeed);

                controller.SimpleMove(transform.forward * movementSpeed);
                return;
            }

            if (path == null) {
                // Because script execution order apparently means nothing now.
                if (hivemind.lastPlayerLocation == null) {
                    return;
                }

                // DEBUG: Remove.
                Debug.Log("RECALCULATING PATH.");
                path = pathfinder.findPath(lastVisitedNode, hivemind.lastPlayerLocation);

                // DEBUG: Move into debug code or remove.
                // StreamWriter writer = new StreamWriter("/home/ybautista/Desktop/UnityOutput/new_path.txt");
                // Vertex[] pathNodes = path.ToArray();

                // foreach (Vertex node in pathNodes) {
                //     writer.Write(node.vector + "\n");
                // }

                // writer.Close();

                // WARNING: In the correct conditions, which are very very few, this code
                // could lead into an infinite loop where the path always comes out
                // null and this function returns infinitely.
                if (path.Count == 0) {
                    path = null;
                    return;
                }

                // Assuming the path contains something, remove the first node off
                // the path.
                nextDestinationNode = path.Pop();

                // DEBUG: Remove or set debugging code.
                Debug.Log("NEXT DESTINATION: " + nextDestinationNode.vector);

                // T: Vector2 -> Vector3
                nextDestination = new Vector3(nextDestinationNode.x, transform.position.y, nextDestinationNode.y);
                Vector3 unnormDirection = nextDestination - transform.position;
                nextFlatDir = new Vector3(unnormDirection.x, 0, unnormDirection.z).normalized;

                return;
            }

            if (recalculationCount >= recalculationRate) {
                path = null;
                recalculationCount = 0;
            }

            if (lastVisitedNode.Equals(nextDestinationNode)) {
                if (path.Count > 0) {
                    nextDestinationNode = path.Pop();

                    // DEBUG: Remove or set debugging code.
                    Debug.Log("NEXT DESTINATION: " + nextDestinationNode.vector);

                    nextDestination = new Vector3(nextDestinationNode.x, transform.position.y, nextDestinationNode.y);
                    Vector3 unnormDirection = nextDestination - transform.position;
                    nextFlatDir = new Vector3(unnormDirection.x, 0, unnormDirection.z).normalized;

                    Vector3 safeDir = findSafeDirection(nextFlatDir, transform.forward);

                    // Rotate towards the safe direction.
                    Quaternion safeDirRotation = Quaternion.LookRotation(safeDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, safeDirRotation, Time.deltaTime * rotationSpeed);

                    // Move in the safe direction, now the object's forward vector.
                    controller.SimpleMove(transform.forward * movementSpeed);
                    recalculationCount++;
                    return;
                }

                // Want to recalculate the path, as already reached our destination.
                path = null;
                recalculationCount = 0;
                return;
            }

            // Find the safe direction.
            Vector3 safeDirection = findSafeDirection(nextFlatDir, transform.forward);

            // Rotate towards the safe direction.
            Quaternion safeDirectionRotation = Quaternion.LookRotation(safeDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, safeDirectionRotation, Time.deltaTime * rotationSpeed);

            controller.SimpleMove(transform.forward * movementSpeed);
            recalculationCount++;
        }
    }
}
