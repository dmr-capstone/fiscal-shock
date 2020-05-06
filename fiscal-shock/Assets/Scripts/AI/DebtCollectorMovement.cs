using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using FiscalShock.Graphs;
using FiscalShock.Pathfinding;
using FiscalShock.Procedural;
using System.IO;
using System.Collections;

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

        /// <summary>
        /// Layers the debt collector can climb/jump over.
        /// </summary>
        private LayerMask jumpable;

        /// <summary>
        /// Layers the debt collector tries to avoid.
        /// </summary>
        private LayerMask avoidance;

        private float damageTaken = 0;

        /// <summary>
        /// Modifier on the base stun threshold calculation. Based on floors
        /// visited, so it gets harder to stun him the more you've been around.
        /// </summary>
        private float stunThresholdModifier;

        /// <summary>
        /// Base threshold for stunning the debt collector. Based on how much
        /// debt you owe to any lender.
        /// </summary>
        private float stunThreshold;

        public GameObject stunEffect;

        /// <summary>
        /// Speed modifier. Debt collector is faster when you're more indebted.
        /// </summary>
        private float debtSpeedMod = (float)Mathf.Log10(Mathf.Pow(StateManager.totalDebt, 0.45f));

        [Tooltip("Current forward direction vector. Visible for debugging purposes.")]
        public Vector3 fwd;

        [Header("Climbing/Jumping/Vertical Obstacle Avoidance")]
        [Tooltip("Current vertical speed.")]
        public float verticalSpeed = 0;

        [Tooltip("Gravity modifier. More gravity makes debt collector fall faster.")]
        public float gravity = 20f;

        [Tooltip("Whether the debt collector should jump at the next update.")]
        public bool startJumping = false;

        /// <summary>
        /// Whether the debt collector is currently airborne. Enemies
        /// are typically spawned in the air, and the debt collector
        /// will correct itself if it spawned on solid ground during
        /// the next update anyway.
        /// </summary>
        private bool isJumping = true;

        [Tooltip("Maximum jump height. A shorter jump height results in the debt collector appearing to 'climb' objects. A jump height too short results in the debt collector being unable to climb objects over a certain height.")]
        public float jumpHeight = 2f;

        /// <summary>
        /// Radius of the box used in the box check when the debt collector
        /// tries to scale a wall. Should be equal or very close to the extents
        /// of the character controller.
        /// </summary>
        private float footSize;

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

            // LayerMask.NameToLayer can only be used at runtime.
            jumpable = ((1 << LayerMask.NameToLayer("Obstacle")) | (1 << LayerMask.NameToLayer("Explosive") | (1 << LayerMask.NameToLayer("Decoration"))));
            avoidance = (1 << LayerMask.NameToLayer("Wall") | jumpable);

            // Set stun thresholds.
            stunThresholdModifier = (float)Mathf.Log10(Mathf.Pow(StateManager.totalFloorsVisited, 0.3f)) + StateManager.totalFloorsVisited/4.0f + 1.0f;
            stunThreshold = ((float)Mathf.Pow(StateManager.totalDebt, 1.5f) / 3333.0f + 5) * stunThresholdModifier;

            // Set speed.
            if (float.IsNaN(debtSpeedMod) || float.IsInfinity(debtSpeedMod)) {
                debtSpeedMod = 1;
            }
            float playerSpeed = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerMovement>().speed;
            debtSpeedMod = Mathf.Clamp(debtSpeedMod, 1, Mathf.Ceil(playerSpeed/movementSpeed + 0.5f));
            movementSpeed *= debtSpeedMod;

            // Determine extents of the box check for climbing/jumping
            footSize = controller.bounds.extents.y * 1.1f;

            // DEBUG
            #if UNITY_EDITOR
            Debug.Log("LAST VISITED NODE: " + lastVisitedNode.vector);
            Debug.Log($"DC speed is {movementSpeed} and x{debtSpeedMod}; player speed is {playerSpeed}");
            #endif
        }

        // MAYBE: Adjust so that takes the step distance into account.
        // MAYBE: Need to use both the target direction AND the current foward angle to determine the safe direction!!
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

        private void FixedUpdate() {
            // Can be stunned, but not hurt. He is immortal. Only death can free you of debt.
            // (Or, ya' know, paying off your debt.)
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

            setVerticalMovement();

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
                    verticalSpeed = 0f;
                    isJumping = true;
                    transform.position = new Vector3(nextDestinationNode.x, teleportationHeight, nextDestinationNode.y);

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
                    Vertex teleportTo = lastVisitedNode.cell.neighbors.First(c => c.reachable).site;

                    controller.enabled = false;
                    transform.position = new Vector3(teleportTo.x, teleportationHeight, teleportTo.y);
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

                applyMovement(safeDir);
                return;
            }

            if (path == null) {
                // Because script execution order apparently means nothing now.
                if (hivemind.lastPlayerLocation == null) {
                    return;
                }

                // DEBUG: Remove.
                #if UNITY_EDITOR
                Debug.Log("RECALCULATING PATH.");
                #endif
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
                #if UNITY_EDITOR
                Debug.Log("NEXT DESTINATION: " + nextDestinationNode.vector);
                #endif

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
                    #if UNITY_EDITOR
                    Debug.Log("NEXT DESTINATION: " + nextDestinationNode.vector);
                    #endif

                    nextDestination = new Vector3(nextDestinationNode.x, transform.position.y, nextDestinationNode.y);
                    Vector3 unnormDirection = nextDestination - transform.position;
                    nextFlatDir = new Vector3(unnormDirection.x, 0, unnormDirection.z).normalized;

                    Vector3 safeDir = findSafeDirection(nextFlatDir, transform.forward);

                    // Move in the safe direction.
                    applyMovement(safeDir);
                    recalculationCount++;
                    return;
                }

                // Want to recalculate the path, as already reached our destination.
                path = null;
                recalculationCount = 0;
                return;
            }

            // Find the safe direction and move there.
            Vector3 safeDirection = findSafeDirection(nextFlatDir, transform.forward);
            applyMovement(safeDirection);
            recalculationCount++;
        }

        /// <summary>
        /// Apply velocity changes.
        /// </summary>
        private void applyMovement(Vector3 direction) {
            // Rotate towards the desired direction.
            Quaternion safeDirectionRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, safeDirectionRotation, Time.deltaTime * rotationSpeed);

            fwd = transform.forward * movementSpeed;
            fwd.y = verticalSpeed;
            controller.Move(fwd * Time.deltaTime);
        }

        /// <summary>
        /// Apply velocity changes related to vertical movement.
        /// </summary>
        private void setVerticalMovement() {
            if (startJumping) {
                verticalSpeed = jumpHeight;
                startJumping = false;
                isJumping = true;
                return;
            }

            if (Physics.Raycast(transform.position, -Vector3.up, footSize, (1 << LayerMask.NameToLayer("Ground") | avoidance))) {
                verticalSpeed = 0;
                isJumping = false;
            } else if (isJumping) {
                verticalSpeed -= gravity * Time.deltaTime;
            } else {  // something strange happened and you're clearly not on the ground
                isJumping = true;
            }
        }

        private void OnControllerColliderHit(ControllerColliderHit col) {
            if (stunned) {  // Can stun and touch without game over, to some extent
                return;
            }

            if (col.gameObject.tag == "Missile" || col.gameObject.tag == "Bullet") {
                BulletBehavior bb = col.gameObject.GetComponent<BulletBehavior>();
                if (bb == null) {
                    damageTaken += 1.0f;
                    return;
                }
                damageTaken += bb.damage;
                if (damageTaken >= (stunThreshold * Random.Range(0.85f, 1.15f))) {
                    StartCoroutine(stun(Random.Range(3f, 5f)));
                }
            }

            if (col.gameObject.tag == "Player" && !StateManager.playerDead && StateManager.totalDebt > 0) {
                Debug.Log($"Player was caught by debt collector on floor {StateManager.currentFloor} with {StateManager.totalDebt} debt");
                player.GetComponent<PlayerHealth>().endGameByDebtCollector();
                return;
            }

            // Jump on lateral collisions. Still gets triggered on corners, though
            if (Mathf.Abs(col.normal.y) > 0.5f) {
                return;
            }
            if (Physics.SphereCast(transform.position, controller.bounds.extents.x, transform.forward, out RaycastHit _, 1f, jumpable)) {
                startJumping = true;
            }
        }

        private IEnumerator stun(float duration) {
            stunned = true;
            Material mat = gameObject.GetComponentInChildren<Renderer>().material;
            mat.SetColor("_Color", new Color(0.1f, 0.1f, 0.1f));
            stunEffect.SetActive(true);
            yield return new WaitForSeconds(duration);

            damageTaken = 0;
            stunned = false;
            stunEffect.SetActive(false);
            mat.SetColor("_Color", Color.white);
            yield return null;
        }
    }
}
