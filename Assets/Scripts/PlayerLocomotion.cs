using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TM
{
    public class PlayerLocomotion : MonoBehaviour
    {
        PlayerManager playerManager;
        Transform cameraObject;
        InputHandler inputHandler;
        public Vector3 moveDirection;

        [HideInInspector]
        public Transform myTransform;
        [HideInInspector]
        public AnimatorHandler animatorHandler;

        public new Rigidbody rigidbody;
        public GameObject normalCamera;

        [Header("Ground & Air Detection Stats")]
        [SerializeField]
        float groundDetectionRayStartPoint = 0.5f;
        [SerializeField]
        float minimumDistanceNeededToBeginFall = 1f;
        [SerializeField]
        float groundDirectionRayDistance = 0.2f;
        LayerMask ignoreForGroundCheck;
        public float inAirTimer;
        public Vector3 wallCheck = new Vector3(0.2f, -1f, 0.2f);

        [Header("Movement Stats")]
        [SerializeField]
        float movementSpeed = 5;
        [SerializeField]
        float sprintSpeed = 7;
        [SerializeField]
        float rotationSpeed = 10;
        [SerializeField]
        float fallingSpeed = 80;

        void Start()
        {
            playerManager = GetComponent<PlayerManager>();
            rigidbody = GetComponent<Rigidbody>();
            inputHandler = GetComponent<InputHandler>();
            animatorHandler = GetComponentInChildren<AnimatorHandler>();
            cameraObject = Camera.main.transform;
            myTransform = transform;
            animatorHandler.Initialize();


            playerManager.isGrounded = true;
            ignoreForGroundCheck = ~(1 << 8 | 1 << 11);
        }

        #region Movement
        Vector3 normalVector;
        Vector3 targetPosition;

        private void HandleRotation(float delta)
        {
            Vector3 targetDir = Vector3.zero;
            float moveOverride = inputHandler.moveAmount;

            targetDir = cameraObject.forward * inputHandler.vertical;
            targetDir += cameraObject.right * inputHandler.horizontal;

            targetDir.Normalize();
            targetDir.y = 0;

            if(targetDir == Vector3.zero)
                targetDir = myTransform.forward;

            float rs = rotationSpeed;

            Quaternion tr = Quaternion.LookRotation(targetDir);
            Quaternion targetRotation = Quaternion.Slerp(myTransform.rotation, tr, rs * delta);

            myTransform.rotation = targetRotation;

        }

        public void HandleMovement(float delta)
        {

            if(playerManager.isInteracting)
                return;

            if(playerManager.isInAir)
                return;

            moveDirection = cameraObject.forward * inputHandler.vertical;
            moveDirection += cameraObject.right * inputHandler.horizontal;
            moveDirection.Normalize();
            moveDirection.y = 0;

            float speed = movementSpeed;
            if (inputHandler.sprintFlag)
            {
                speed = sprintSpeed;
                playerManager.isSprinting = true;
                moveDirection *= speed;
            }
            else
            {
                moveDirection *= speed;
            }

            Vector3 projectedVelocity = Vector3.ProjectOnPlane(moveDirection, normalVector);
            rigidbody.velocity = projectedVelocity;

            animatorHandler.UpdateAnimatorValues(inputHandler.moveAmount, 0, playerManager.isSprinting);

            if(animatorHandler.canRotate)
            {
                HandleRotation(delta);
            }
        }

        public void HandleRollingAndSprinting(float delta)
        {
            // prevents rolling out of animations, such as interacting with levers, etc.
            if (animatorHandler.anim.GetBool("isInteracting"))
            {
                return;
            }

            if (inputHandler.rollFlag)
            {
                moveDirection = cameraObject.forward * inputHandler.vertical;
                moveDirection += cameraObject.right * inputHandler.horizontal;

                // if you're moving, this will make you roll in the direction of your movement:
                if(inputHandler.moveAmount > 0)
                {
                    animatorHandler.PlayTargetAnimation("RollForward", true);
                    moveDirection.y = 0;
                    Quaternion rollRotation = Quaternion.LookRotation(moveDirection);
                    myTransform.rotation = rollRotation;
                }
                else
                {
                    Vector3 startPosition = myTransform.position;
                    Vector3 backward = -myTransform.forward * 1.35f;
                    animatorHandler.PlayTargetAnimation("BackStep", false);
                    StartCoroutine(MoveOverSeconds(rigidbody, myTransform.position + backward, .15f));
                }
            }
        }

        public void HandleFalling(float delta, Vector3 moveDirection)
        {
            playerManager.isGrounded = false;
            RaycastHit hit;
            Vector3 origin = myTransform.position;
            origin.y += groundDetectionRayStartPoint;

            if (Physics.Raycast(origin, myTransform.forward, out hit, 0.2f))
            {
                moveDirection = Vector3.zero;
            }

            if (playerManager.isInAir)
            {
                rigidbody.AddForce(-Vector3.up * fallingSpeed);
                rigidbody.useGravity = true;

                if(rigidbody.velocity.y > -0.1)
                {
                    playerManager.isOnEdge = SlipChecker();
                }
                else
                {
                    playerManager.isOnEdge = false;
                }
            }

            Vector3 dir = moveDirection;
            dir.Normalize();
            origin = origin + dir * groundDirectionRayDistance;

            targetPosition = myTransform.position;

            Debug.DrawRay(origin, -Vector3.up * minimumDistanceNeededToBeginFall, Color.red, 0.1f, false);
            if (Physics.Raycast(origin, -Vector3.up, out hit, minimumDistanceNeededToBeginFall, ignoreForGroundCheck))
            {
                normalVector = hit.normal;
                Vector3 tp = hit.point;
                playerManager.isGrounded = true;
                targetPosition.y = tp.y;

                if (playerManager.isInAir)
                {
                    if (inAirTimer > 0.5f)
                    {
                        animatorHandler.PlayTargetAnimation("Land", true);
                    }
                    else
                    {
                        animatorHandler.PlayTargetAnimation("Locomotion", false);
                        inAirTimer = 0;
                    }

                    Debug.Log("You were in the air for: " + inAirTimer);
                    playerManager.isInAir = false;
                }
            }
            else
            {
                if(playerManager.isGrounded)
                {
                    playerManager.isGrounded = false;
                }

                if(playerManager.isInAir == false)
                {
                    if(!playerManager.isInteracting && !inputHandler.rollFlag)
                    {
                        animatorHandler.PlayTargetAnimation("Falling", true);
                    }
                    moveDirection = Vector3.zero;
                    Vector3 vel = rigidbody.velocity;
                    vel.Normalize();
                    rigidbody.velocity = vel * (movementSpeed / 2);
                    playerManager.isInAir = true;
                }
            }

            if(playerManager.isGrounded)
            {
                if(playerManager.isInteracting || inputHandler.moveAmount > 0)
                {
                    myTransform.position = Vector3.Lerp(myTransform.position, targetPosition, Time.deltaTime);
                }
                else
                {
                    myTransform.position = targetPosition;
                }
            }
        }

        public bool SlipChecker()
        {
            RaycastHit hit;
            Vector3 raySpawnPosition = myTransform.position + Vector3.up * wallCheck.y;

            Vector3 forward = myTransform.forward * wallCheck.x;
            Vector3 backward = -myTransform.forward * wallCheck.x;
            Vector3 right = myTransform.right * wallCheck.x;
            Vector3 left = -myTransform.right * wallCheck.x;

            float dis = wallCheck.x;

            Debug.DrawRay(raySpawnPosition + backward, forward, Color.blue, 0.1f, false);
            Debug.DrawRay(raySpawnPosition + forward, backward, Color.blue, 0.1f, false);
            Debug.DrawRay(raySpawnPosition + left, right, Color.blue, 0.1f, false);
            Debug.DrawRay(raySpawnPosition + right, left, Color.blue, 0.1f, false);

            if (Physics.Raycast(raySpawnPosition + backward, forward, out hit, wallCheck.x) || Physics.Raycast(raySpawnPosition + forward, backward, out hit, wallCheck.x) || Physics.Raycast(raySpawnPosition + left, right, out hit, wallCheck.x) || Physics.Raycast(raySpawnPosition + right, left, out hit, wallCheck.x))
            {
                SlipMove(hit.normal);
                return true;
            }
            Debug.Log("raycast didn't hit anything");
            playerManager.isInAir = false;
            return false;
        }

        void SlipMove(Vector3 slipDirection)
        {
            rigidbody.MovePosition(rigidbody.position + (slipDirection / 30));
        }

        public IEnumerator MoveOverSpeed (Rigidbody objectToMove, Vector3 end, float speed){
            // speed should be 1 unit per second
            while (objectToMove.transform.position != end)
            {
                objectToMove.transform.position = Vector3.MoveTowards(objectToMove.transform.position, end, speed * Time.deltaTime);
                yield return new WaitForEndOfFrame ();
            }
        }
        public IEnumerator MoveOverSeconds (Rigidbody objectToMove, Vector3 end, float seconds)
        {
            float elapsedTime = 0;
            Vector3 startingPos = objectToMove.transform.position;
            Vector3 nextPos = objectToMove.transform.position;
            while (elapsedTime < seconds)
            {
                nextPos = Vector3.Lerp(startingPos, end, (elapsedTime / seconds));
                Vector3 spd = (nextPos - startingPos).normalized * 13f;
                objectToMove.velocity = spd;
                // objectToMove.transform.position = Vector3.Lerp(startingPos, end, (elapsedTime / seconds));
                elapsedTime += Time.deltaTime;
                yield return new WaitForEndOfFrame();
            }
            // objectToMove.transform.position = end;
        }


        #endregion

    }
}
