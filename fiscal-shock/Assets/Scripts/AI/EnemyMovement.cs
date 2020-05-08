using UnityEngine;
using System.Collections;

namespace FiscalShock.AI {
    public class EnemyMovement : MonoBehaviour {
        [Tooltip("The speed at which the object moes.")]
        public float movementSpeed = 3f;

        [Tooltip("The speed at which the object turns.")]
        public float rotationSpeed = 7f;

        [Tooltip("The absolute minimum distance away from the player.")]
        public float safeRadiusMin = 4f;

        [Tooltip("Creates safe radius in case object ends up too close to player.")]
        public float safeRadiusMax = 5f;

        [Tooltip("How close the player needs to be before being pursued.")]
        public float visionRadius = 35f;

        // PLAYER TRACKING
        public GameObject player;
        private EnemyShoot shootScript;

        /// <summary>
        /// The distance from the player in 2D space (x, z).
        /// </summary>
        public float distanceFromPlayer2D {
            get; private set;
        }

        /// <summary>
        /// The previous position the player was at in 2D space (x,z). Used
        /// to determine if the enemy character needs to recalculate a new destination point.
        /// </summary>
        private Vector3 prevPlayerFlatPos;

        // MOVEMENT

        /// <summary>
        /// Enemy character movement controller. Comes with attached rigidbody.
        /// </summary>
        private CharacterController controller;

        /// <summary>
        /// Whether or not the debt collector has been stunned.
        /// </summary>
        public bool stunned {
            get; set;
        }

        /// <summary>
        /// Used to force enemies to the ground at script startup.
        /// </summary>
        private bool isGrounded = false;

        /// <summary>
        /// Set to true if a collision happens against an object that we're avoiding
        /// or if the enemy character ends up at the desired destination.
        /// </summary>
        private bool recalculateDestination = true;

        /// <summary>
        /// The distance the player must move or the enemy must be
        /// away from the destination to force the enemy to recalculate
        /// the destination point during orbiting movement.
        /// </summary>
        private float destinationRefreshDistance;

        /// <summary>
        /// The point towards which the enemy character is headed.
        /// </summary>
        private Vector3 destination;

        private float gravity = 40f;

        private float safeRadiusAvg;

        // RAYCASTING

        /// <summary>
        /// The 0 degree whisker for raycasting.
        /// </summary>
        private Vector3 forwardWhisker;

        /// <summary>
        /// The -75, 75, and 180 degree whiskers for raycasting.
        /// </summary>
        private Vector3 left75, right75, backward;

        /// <summary>
        /// How long the whiskers should be.
        /// </summary>
        private float whiskerLength = 5f;

        /// <summary>
        /// How often the raycast should happen.
        /// </summary>
        private int whiskerSampleRate = 10;

        /// <summary>
        /// The updates passed since a raycast last happened.
        /// </summary>
        private int whiskerSampleCounter = 0;

        /// <summary>
        /// Layers the debt collector tries to avoid.
        /// </summary>
        private LayerMask avoidance;

        /// <summary>
        /// Layers the debt collector can climb/jump over.
        /// </summary>
        private LayerMask jumpable;

        // REFERENCE SCRIPTS

        /// <summary>
        /// Manages the playing of animations related to movement.
        /// </summary>
        public AnimationManager animationManager;

        /// <summary>
        /// Used to create reaction animation.
        /// </summary>
        private bool reactingToPlayer;

        /// <summary>
        /// Script that keeps track of enemy health.
        /// </summary>
        public EnemyHealth health;

        /// <summary>
        /// Tracks whether or not should be actively moving due to attacking.
        /// </summary>
        public bool isAttacking = false;

        private void Start() {
            if (player == null) {
                player = GameObject.FindGameObjectWithTag("Player");
            }

            controller = GetComponent<CharacterController>();
            shootScript = GetComponent<EnemyShoot>();

            jumpable = ((1 << LayerMask.NameToLayer("Obstacle")) | (1 << LayerMask.NameToLayer("Explosive") | (1 << LayerMask.NameToLayer("Decoration"))));
            avoidance = (1 << LayerMask.NameToLayer("Wall") | jumpable);

            prevPlayerFlatPos = new Vector3(player.transform.position.x, 0, player.transform.position.z);
            safeRadiusAvg = (safeRadiusMax + safeRadiusMin) / 2;
            destinationRefreshDistance = safeRadiusAvg - safeRadiusMin;
        }

        private void FixedUpdate() {
            // Apply gravity after spawning.
            if (!isGrounded) {
                controller.Move(new Vector3(0, -gravity * Time.deltaTime, 0) * movementSpeed * Time.deltaTime);
                return;
            }

            // Play an idling animation when there is no player.
            if ((player == null || (Vector3.Distance(player.transform.position, gameObject.transform.position) > visionRadius) || stunned) && !health.enmityActive) {
                animationManager.playIdleAnimation();
                shootScript.spottedPlayer = false;
                return;
            }

            // Play animation to react to the player if the player is nearby.
            if (!shootScript.spottedPlayer && !health.enmityActive && !reactingToPlayer) {
                StartCoroutine(reactToNearbyPlayer());
            }

            // Don't interrupt other animations to play movement.
            if (!animationManager.animator.isPlaying || animationManager.animator.IsPlaying("idle0")) {
                animationManager.playMoveAnimation();
            }

            if (isAttacking) {
                return;
            }

            Vector3 playerDirection = (player.transform.position - transform.position).normalized;
            Vector3 flatPlayerDirection = new Vector3(playerDirection.x, 0, playerDirection.z);

            // Used for distance calculations.
            Vector3 flatPosition = new Vector3(transform.position.x, 0, transform.position.z);
            Vector3 playerFlatPosition = new Vector3(player.transform.position.x, 0, player.transform.position.z);

            // Calculate the distance from the player.
            distanceFromPlayer2D = Vector3.Distance(playerFlatPosition, flatPosition);
            whiskerSampleCounter++;

            if (distanceFromPlayer2D < visionRadius) {
                if (distanceFromPlayer2D > safeRadiusMax) {
                    Vector3 safeDir = findSafeDirection(flatPlayerDirection);
                    applyMovement(safeDir, playerDirection);
                    return;
                }

                if (distanceFromPlayer2D < safeRadiusMin) {
                    Vector3 safeDir = findSafeDirection(-flatPlayerDirection);
                    applyMovement(safeDir, playerDirection);
                    return;
                }

                if (distanceFromPlayer2D <= safeRadiusMax && distanceFromPlayer2D >= safeRadiusMin) {
                    if (!recalculateDestination && Vector3.Distance(playerFlatPosition, prevPlayerFlatPos) < destinationRefreshDistance) {
                        // The point at which the mook would appear if followed the straight line to the destination. 
                        Vector3 linearTarget = flatPosition + ((destination - flatPosition).normalized * movementSpeed * Time.deltaTime);

                        // Offset from linear position in opposite direction from player, as determined by the average safe radius.
                        float distanceDiff = safeRadiusAvg - Vector3.Distance(playerFlatPosition, linearTarget);

                        // Get next position in orbit around player. Should still be 0 on y at this point.
                        Vector3 targetPosition = getOrbitalCoordinate(linearTarget, playerFlatPosition, distanceDiff);
                        Debug.Log("TARGET POSITON: " + targetPosition);

                        if (Vector3.Distance(targetPosition, destination) < destinationRefreshDistance) {
                            recalculateDestination = true;
                        }

                        targetPosition.y = transform.position.y;

                        Quaternion rotate = Quaternion.LookRotation(playerDirection);
                        transform.rotation = Quaternion.Slerp(transform.rotation, rotate, Time.deltaTime * rotationSpeed);

                        // ARLEADY MULTIPLIED BY MOVEMENT SPEED.
                        controller.Move(linearTarget);
                        return;
                    }

                    // The destination hasn't been reached but the destination coordinate needs to be recalculated.
                    else if (!recalculateDestination) {
                        Vector2 coordinate = getRandomCircularCoordinate();
                        Vector3 coord = new Vector3(coordinate.x, transform.position.y, coordinate.y);
                        destination = (coord * safeRadiusAvg) + playerFlatPosition;
                        prevPlayerFlatPos = playerFlatPosition;
                    }

                    // The destination was reached, requiring a recalculation of the destination coordinate.
                    else {
                        Vector2 coordinate = getRandomCircularCoordinate();
                        Vector3 coord = new Vector3(coordinate.x, transform.position.y, coordinate.y);
                        destination = (coord * safeRadiusAvg) + playerFlatPosition;
                        recalculateDestination = false;
                    }
                }
            }

            Quaternion rotation = Quaternion.LookRotation(playerDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, Time.deltaTime * rotationSpeed);
        }

        private void applyMovement(Vector3 direction, Vector3 rotationDirection) {
            Quaternion rotation = Quaternion.LookRotation(rotationDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, Time.deltaTime * rotationSpeed);

            controller.SimpleMove(direction * movementSpeed/* * Time.deltaTime*/);
        }

        /// <summary>
        /// Detect any obtacles and determine the best direction to go towards.
        /// </summary>
        /// <param name="target">The direction against which to raycast.</param>
        /// <returns>A vector representing the safest direction to go towards.</returns>
        private Vector3 findSafeDirection(Vector3 target) {
            forwardWhisker = target;

            // Only check if the raycasts are due for checking.
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
                        // If absolutely no other case works, return the backwards angle.
                        backward = Vector3.Reflect(forwardWhisker * whiskerLength, hit.normal);

                        // Draw the backwards ray.
                        #if UNITY_EDITOR
                        Debug.DrawRay(transform.position, backward * whiskerLength, Color.cyan, 1);
                        #endif

                        return backward;
                    }

                    // Neither left nor right whisker hit.
                    else {
                        return left75;
                    }
                }
            }

            return forwardWhisker;
        }

        /// <summary>
        /// Cause the enemy to react to the player using a mixture of animations and pauses.
        /// </summary>
        /// <returns>Always null.</returns>
        private IEnumerator reactToNearbyPlayer() {
            // Reaction time
            reactingToPlayer = true;
            yield return new WaitForSeconds(1f * Random.Range(1f, 3f));
            shootScript.spottedPlayer = true;
            reactingToPlayer = false;
            yield return null;
        }

        private void OnControllerColliderHit(ControllerColliderHit col) {
            isGrounded = true;

            if (col.gameObject.layer == avoidance) {
                // Reset the destination on the orbit.
                recalculateDestination = true;
            }
        }

        /// <summary>
        /// Obtain a random coordinate on the unit circle using a randomly
        /// chosen angle.
        /// </summary>
        /// <returns>Random coordinate on the (x,z) unit circle.</returns>
        private Vector2 getRandomCircularCoordinate() {
            float angle = Random.Range(0.0f, 2 * Mathf.PI);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        /// <summary>
        /// Obtain the next coordination where the enemy should appear by
        /// using the player position is the origin point and a shifting
        /// of the straight line point to the destination onto a circle around the player.
        /// </summary>
        /// <param name="linearPosition">The original point where the enemy would appear this frame.</param>
        /// <param name="playerPosition">The player position, to be used as the origin of the circle.</param>
        /// <param name="distance">The distance from the linearPosition and the safe radius.</param>
        /// <returns>The point to where the enemy should travel.</returns>
        private Vector3 getOrbitalCoordinate(Vector3 linearPosition, Vector3 playerPosition, float distance) {
            Vector3 result = playerPosition - linearPosition;
            float xValue = result.x / Mathf.Sqrt(Mathf.Pow(result.x, 2) + Mathf.Pow(result.z, 2));
            float zValue = result.z / Mathf.Sqrt(Mathf.Pow(result.x, 2) + Mathf.Pow(result.z, 2));
            result.x = xValue;
            result.z = zValue;
            return linearPosition - (distance * result);
        }
    }
}
