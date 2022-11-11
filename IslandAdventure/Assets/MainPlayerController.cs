using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainPlayerController : MonoBehaviour
{
    public enum GeneralPlayerMovementState
    {
        OnFoot,
        Swinging,
        Swimming
    }


    float colliderHeight;
    CharacterController characterController;

    Camera currentPlayerCamera;
    Rigidbody player_rb;

    public float playerCreepSpeed = 1f;
    public float playerWalkSpeed = 3f;
    public float playerSprintSpeed = 6f;

    public float player45DegreeSpeed = 3f;
    public float player60DegreeSpeed = 2f;
    public float player75DegreeSpeed = 1f;


    [SerializeField] float speed;


    public float turnSmoothTime = 0.1f;
    float turnSmoothVelocity;

    public float gravity = 9.8f;

    public Vector3 momentum;

    public Vector3 directionOfSlope;
    public Vector3 directionOfWall;
    Vector3 normalOfWall;

    public float angleOfSlope = 0;

    public ParticleSystem sprintEffect;

    [SerializeField] float downCast = 0.5f;
    [SerializeField] float forwardCast = 0.3f;

    GeneralPlayerMovementState playerMovementState = GeneralPlayerMovementState.OnFoot;

    //Flags for different states
    [SerializeField] bool isJumping = false;
    [SerializeField] bool isHoldingLedge = false;
    [SerializeField] bool isStomping = false;
    [SerializeField] bool isClimbing = false;

    //Flags for different restrictions
    [SerializeField] bool cantJump = false;
    [SerializeField] bool cantGrabLedge = false;
    [SerializeField] bool cantStomp = false;
    [SerializeField] bool cantClimb = false;
    [SerializeField] bool cantSprint = false;

    [SerializeField] string previousTag = "";
    [SerializeField] string frontTag = "";
    [SerializeField] string middleTag = "";

    public Animator playerCharacterAnimator;

    // Start is called before the first frame update
    void Start()
    {
        characterController = GetComponent<CharacterController>();
        colliderHeight = characterController.height;
        downCast = colliderHeight / 2;

        currentPlayerCamera = Camera.main;
        player_rb = GetComponent<Rigidbody>();

        directionOfSlope = new Vector3();

        speed = playerWalkSpeed;
    }

    // Update is called once per frame
    void Update()
    {
        if (isClimbing)
            return;

        playerCharacterAnimator.SetBool("Hanging", isHoldingLedge);

        if (isStomping)
            return;

        bool crouch = Input.GetButton("Fire1");
        bool sprint = Input.GetButton("Fire3");
        bool stomp = Input.GetButton("Jump");

        if (cantSprint)
            sprint = false;

        speed = sprint ? playerSprintSpeed : playerWalkSpeed;

        var horz = Input.GetAxis("Horizontal");
        var vert = Input.GetAxis("Vertical");

        Vector3 controlDirection = new Vector3(horz, 0f, vert).normalized;

        float targetAngle = Mathf.Atan2(controlDirection.x, controlDirection.z) * Mathf.Rad2Deg + currentPlayerCamera.transform.eulerAngles.y;
        float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);

        RaycastHit hit;

        downCast = colliderHeight / 2 + 0.5f;

        //Slope detection, ledge below detection
        if (Physics.Raycast(transform.position, -transform.up, out hit, downCast))
        {
            angleOfSlope = Vector3.Angle(transform.up, hit.normal);
            if (angleOfSlope > 10)
            {
                //By not normalizing we can probably use this vector for slope "strength"
                directionOfSlope = new Vector3(hit.normal.x, 0f, hit.normal.z).normalized;
                momentum = new Vector3(characterController.velocity.x, 0f, characterController.velocity.z);
            }
            middleTag = hit.transform.tag;
        }
        else
        {
            middleTag = "";
        }
        //Open air detection
        if (Physics.Raycast(transform.position + new Vector3(0, 0, 0.1f), -transform.up, out hit, downCast))
        {

            frontTag = hit.transform.tag;
        }
        else
        {
            frontTag = "";
        }

        if (Physics.Raycast(transform.position + new Vector3(), transform.forward, out hit, forwardCast))
        {
            normalOfWall = hit.normal.normalized;
            directionOfWall = Quaternion.AngleAxis(-90, transform.up) * hit.normal.normalized;
        }
        else
        {
            //No wall! Manage it!
            directionOfWall = new Vector3();
        }

        //Need to add more checks to prevent the player from reattaching after grabbing and make sure the player can control when they attach
        if (!isHoldingLedge)
        {
            if (Physics.Raycast(transform.position + new Vector3(0f, 0.7f, 0f), transform.forward, out hit,/*space needed to lift up*/1f))
            {
                //Whoops, hit a wall
                //This is where we can do the run up wall function call

            }
            else
            {
                //If there wasn't a wall, (directionOfWall = new Vector3();), don't attempt to attach. (Vector3.Dot(controlDirection, directionOfWall) < 0) makes sure the player is trying to move into the wall
                if (directionOfWall.magnitude > 0f && !crouch && (Vector3.Dot(controlDirection, directionOfWall) < 0) && !cantGrabLedge)
                {
                    Debug.Log(Vector3.Dot(controlDirection, directionOfWall));

                    //Adjust player to wall
                    StartCoroutine(HoldRoutine());
                    return;
                }
            }
        }


        if ((middleTag == frontTag) && !isHoldingLedge && !isJumping)
        {
            if (previousTag == "Solid" && middleTag == "" && !cantJump)
            {
                Jump();
            }
            previousTag = middleTag;
        }


        if (isJumping)
            return;

        if (isHoldingLedge)
        {
            //Might need to move the cant variable to inside TryToClimbUp()
            if (controlDirection.z > 0.1f && !cantClimb)
            {
                //Pick up
                isClimbing = true;
                TryToClimbUp();
            }
            else if (controlDirection.z < -0.1f)
            {
                //Drop down
                isClimbing = true;
                DropDown();
            }

            //TODO: Rotate to face the wall
            float hangTargetAngle = Mathf.Atan2(-normalOfWall.x, -normalOfWall.z) * Mathf.Rad2Deg;
            //float hangAngle = Mathf.SmoothDampAngle(transform.eulerAngles.y, hangTargetAngle, ref turnSmoothVelocity, turnSmoothTime);

            transform.rotation = Quaternion.Euler(0f, hangTargetAngle, 0f);

            //Move along the wall
            characterController.Move(directionOfWall * controlDirection.x * speed * Time.deltaTime);


            //I don't know if I can add a race condition here to check for ledges that can't be entered. Might have to change the check for moving along the wall or switch to preset ledge system
            if (Physics.Raycast(transform.position + new Vector3(), transform.forward, out hit, forwardCast))
            {
                //This checks to make sure the wall doesn't sharply jump upward. If so, don't let the player move since that means there is not immediate ledge
                if (Physics.Raycast(transform.position + new Vector3(0f, 0.7f, 0f), transform.forward, out hit,/*space needed to lift up*/1f))
                {
                    characterController.Move(directionOfWall * -controlDirection.x * speed * Time.deltaTime);
                }
            }
            else
            {
                //No wall! Manage it!
                characterController.Move(directionOfWall * -controlDirection.x * speed * Time.deltaTime);
            }


        }
        else
        {
            if (crouch)
            {
                characterController.height = colliderHeight / 2;
                transform.rotation = Quaternion.Euler(0f, angle, 0f);

                if (angleOfSlope > 25)
                {
                    characterController.Move(directionOfSlope * (playerSprintSpeed - 1) * Time.deltaTime);
                }
            }
            else if (controlDirection.magnitude > 0.1f)
            {
                characterController.height = colliderHeight;

                transform.rotation = Quaternion.Euler(0f, angle, 0f);

                if (sprint)
                {
                    if (!sprintEffect.isPlaying)
                        sprintEffect.Play();
                }
                else
                {
                    if (sprintEffect.isPlaying)
                        sprintEffect.Stop();
                }

                Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;

                if (angleOfSlope > 74 && sprint)
                {
                    speed = player75DegreeSpeed;
                }
                else if (angleOfSlope > 59 && sprint)
                {
                    speed = player60DegreeSpeed;
                }
                else if (angleOfSlope > 44 && sprint)
                {
                    speed = player45DegreeSpeed;
                }
                else if (angleOfSlope > 44) //If player isn't sprinting on any large angle slope, slide
                {
                    speed = 0;
                }

                characterController.Move(moveDir * speed * Time.deltaTime);

            }

            //Allow the player to stomp on 44 degree or less slopes
            if (angleOfSlope < 45)
            {
                if (stomp)
                {
                    isStomping = true;
                    playerCharacterAnimator.SetTrigger("Stomp");
                }
            }
            else
            {
                if (!sprint)
                    characterController.Move(directionOfSlope * (playerSprintSpeed - 1) * Time.deltaTime);
            }


            characterController.Move(-transform.up * gravity * Time.deltaTime);

            /*momentum *= 0.9f * Time.deltaTime;
            if(momentum.magnitude <= 0.02f)
            {
                momentum = new Vector3();
            }*/
        }
    }

    public void StompDone()
    {
        isStomping = false;
    }

    public void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position,-transform.up*downCast);
        Gizmos.DrawRay(transform.position + new Vector3(0, 0, 0.1f), -transform.up * downCast);

        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position + new Vector3(), transform.forward * forwardCast);
        Gizmos.DrawRay(transform.position + new Vector3(0f, 0.5f, 0f), transform.forward * forwardCast);
    }

    public void Jump()
    {
        if (!isJumping)
        {
            isJumping = true;
            StartCoroutine(JumpRoutine(transform.forward, speed));
        }
    }

    void TryToClimbUp()
    {
        //Check a certain height above
        //If clear, move up
        //If a ledge is in range, go to it
        RaycastHit hit;
        if (Physics.Raycast(transform.position + new Vector3(0f,0.7f,0f), transform.forward, out hit,/*space needed to lift up*/1f))
        {
            if(hit.transform.tag == "Solid")
            {
                //Do a rejection or jump and fall
                isClimbing = false;
            }
            else
            {
                ClimbUp();
            }
        }
        else
        {
            ClimbUp();
        }
    }

    void ClimbUp()
    {
        StartCoroutine(ClimbRoutine());
    }

    void DropDown()
    {
        StartCoroutine(DropRoutine());
    }

    IEnumerator DropRoutine()
    {
        yield return new WaitForSeconds(0.3f);
        //Detatch from wall and drop
        isHoldingLedge = false;
        isClimbing = false;
    }

    IEnumerator ClimbRoutine()
    {
        characterController.Move(transform.up * 0.7f);
        characterController.Move(transform.forward * 0.5f);

        yield return new WaitForSeconds(0.3f);

        isHoldingLedge = false;
        isClimbing = false;
    }

    IEnumerator HoldRoutine()
    {
        isJumping = false;
        isClimbing = true;
        isHoldingLedge = true;

        float hangTargetAngle = Mathf.Atan2(-normalOfWall.x, -normalOfWall.z) * Mathf.Rad2Deg + currentPlayerCamera.transform.eulerAngles.y;
        //float hangAngle = Mathf.SmoothDampAngle(transform.eulerAngles.y, hangTargetAngle, ref turnSmoothVelocity, turnSmoothTime);

        transform.rotation = Quaternion.Euler(0f, hangTargetAngle, 0f);


        yield return new WaitForSeconds(0.5f);
        
        isClimbing = false;
    }

    IEnumerator JumpRoutine(Vector3 jumpDirection, float jumpSpeed)
    {
        float distance = 0;

        if (jumpSpeed > 5f)
        {
            distance = 2f;
        }
        else if(jumpSpeed > 2f)
        {
            distance = 1f;
        }
        else //if(jumpSpeed > 1f)
        {
            isJumping = false;
        }

        float distLoop = 0f;
        float heightLoop = 0f;
        float jumpLoopSpeed = 0.03f;
        float jumpHeightSpeed = 0.01f;

        //Use this to make it a smooth move
        while (isJumping)
        {
            characterController.Move(jumpDirection * jumpLoopSpeed + new Vector3(0, jumpHeightSpeed, 0));

            if(distLoop < distance)
            {
                distLoop += jumpLoopSpeed;
            }
            else
            {
                isJumping = false;
            }

            if(distLoop < distance/2)
            {
                heightLoop += jumpHeightSpeed;
                
            }
            else
            {
                heightLoop -= jumpHeightSpeed;
                //jumpHeightSpeed = -jumpHeightSpeed;
            }

            yield return new WaitForEndOfFrame();
        }

        /*characterController.Move(jumpDirection * distance/2);
        characterController.Move(transform.up * 0.5f);

        yield return new WaitForSeconds(0.2f);

        characterController.Move(jumpDirection * distance/2);
        characterController.Move(-transform.up * 0.3f);

        yield return new WaitForSeconds(0.2f);
        //}*/

        isJumping = false;
    }
}
