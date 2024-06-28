using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public Vector3 Gravity = new Vector3(0, -9.81f, 0);
    public float MovementSpeed;
    public float swimmingSpeed;
    public float swimmingVerticalSpeed;
    public bool swimMaxHeight;
    public float MovementAcceleration = 0.02f;
    public float RotationSpeed;
    public float jumpHeight;
    public float inAirSpeed;
    public Vector3 lookDirection;
    [Header("Edge Grab Settings")]
    public Vector3 EdgeRayOrigin;
    public Vector3 EdgeRayDirection;
    public float EdgeRayDistance;
    public bool ClimbableEdge;
    [Header("Raycast Tuning/Slope Settings")]
    public float MaxSlopeAngle = 45f;
    public float SlideSpeed = 5f;
    private float slopeAngle;
    public float distanceToGround;
    public Vector3 RaycastOriginOffset;
    public LayerMask groundLayer;
    public GameObject RaycastOrigin;
    public float distanceToGroundForLanding;
    public bool IsSwimming;

    [Header("Readouts")]

    public Vector3 velocity;
    private bool isGrounded;

    private CharacterController cc;
    public Animator anim;
    private GameObject Player;
    private Camera playerCam;
    private InputAction inputMove;
    private InputAction inputJump;
    private InputAction inputCrouch;
    private InputAction inputAttack;
    private InputAction inputInteract;
    private InputAction inputItem;
    private InputAction inputRun;
    private bool isRunning = false;

    void Start()
    {
        DontDestroyOnLoad(this.gameObject);
        anim = transform.GetComponentInChildren<Animator>();
        cc = transform.GetComponentInChildren<CharacterController>();

        inputMove = InputSystem.actions.FindAction("Move");
        inputAttack = InputSystem.actions.FindAction("Attack");
        inputAttack.performed += OnAttack;
        inputJump = InputSystem.actions.FindAction("Jump");
        inputJump.performed += OnJump;
        inputCrouch = InputSystem.actions.FindAction("Crouch");
        inputCrouch.performed += OnCrouch;
        inputInteract = InputSystem.actions.FindAction("Interact");
        inputInteract.performed += OnInteract;
        inputItem = InputSystem.actions.FindAction("Item");
        inputItem.performed += OnItem;
        inputRun = InputSystem.actions.FindAction("Run");
        inputRun.performed += OnRun;
        Player = cc.transform.gameObject;
        playerCam = transform.GetComponentInChildren<Camera>();

        RaycastOrigin = new GameObject("RaycastOrigin");
        RaycastOrigin.transform.parent = transform.GetChild(0).transform;
        RaycastOrigin.transform.localPosition = RaycastOriginOffset;
    }

    private void OnCrouch(InputAction.CallbackContext obj)
    {
        //throw new System.NotImplementedException();
    }

    public void ProcessMovement()
    {
       
        
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = 0f;
            anim.SetBool("IsFalling", false);
            anim.SetFloat("FallDistance", 0);
        }
        
        if (!inputMove.IsPressed())
        {
            if (MovementSpeed > 1f)
            {
                MovementSpeed = Mathf.Lerp(MovementSpeed, 0f, MovementAcceleration);
            }
            else
            {
                MovementSpeed = Mathf.Lerp(MovementSpeed, 0f, MovementAcceleration * 3);
            }

            return;
        }

        var value = inputMove.ReadValue<Vector2>();
        lookDirection = playerCam.transform.right * value.x + playerCam.transform.forward * value.y;
        lookDirection.y = 0; // Ensure the movement is only on the XZ plane

        if (lookDirection.sqrMagnitude > 0.01f) // Check to avoid small rotations when not moving
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection * -1);
            Player.transform.rotation = Quaternion.Slerp(Player.transform.rotation, targetRotation, Time.deltaTime * RotationSpeed);
        }
        if (isRunning)
        {
            // Running
            MovementSpeed = Mathf.Lerp(MovementSpeed, 10f, MovementAcceleration);
        }
        else
        {
            // Walking
            MovementSpeed = Mathf.Lerp(MovementSpeed, 1f, MovementAcceleration * 2);
        }
        //Falling, in air movement
        if (anim.GetBool("IsFalling") & value != Vector2.zero)
        {
            cc.Move(lookDirection * Time.deltaTime * MovementSpeed * inAirSpeed);
        }


        // Apply gravity
        velocity.y += Gravity.y * Time.deltaTime;
        cc.Move(velocity * Time.deltaTime);
        


    }
    public void Swimming()
    {
        int SwimUpDown = 0;
        if (inputJump.IsPressed())
        {
            SwimUpDown = 1;
        }
        if (inputCrouch.IsPressed())
        {
            SwimUpDown = -1;
        }
        var PreUpDown = (lookDirection * Time.deltaTime * MovementSpeed * swimmingSpeed);
        if (swimMaxHeight)
        {
            SwimUpDown = Mathf.Clamp(SwimUpDown, -1, 0);
        }
        cc.Move( new Vector3(PreUpDown.x, SwimUpDown * swimmingVerticalSpeed, PreUpDown.z));
        Debug.LogWarning(new Vector3(PreUpDown.x, SwimUpDown, PreUpDown.z));
    }
    public void CheckEdgeGrab()
    {
        RaycastHit hit;
        Debug.DrawRay(RaycastOrigin.transform.position + EdgeRayOrigin, EdgeRayDirection * EdgeRayDistance, Color.magenta);
        Debug.DrawRay(RaycastOrigin.transform.position + EdgeRayOrigin, RaycastOrigin.transform.forward * -1 * EdgeRayDistance, Color.magenta);
        if (Physics.Raycast(RaycastOrigin.transform.position + EdgeRayOrigin, EdgeRayDirection, out hit, EdgeRayDistance, groundLayer))
        {
            ClimbableEdge = true;
        }
        else
        {
            ClimbableEdge = false;
        }
    }
    public void CheckSlope()
    {

        RaycastOrigin.transform.localPosition = RaycastOriginOffset;
        RaycastHit hit;
        if (Physics.Raycast(RaycastOrigin.transform.position, Vector3.down, out hit, Mathf.Infinity, groundLayer))
        {
            Debug.DrawRay(RaycastOrigin.transform.position, Vector3.down * hit.distance, Color.yellow);
            slopeAngle = Vector3.Angle(Vector3.up, hit.normal);
            distanceToGround = hit.distance - RaycastOriginOffset.y;
            if(distanceToGround > 2)
            {
                anim.SetBool("IsFalling", true);
                
            }
            if(distanceToGround > anim.GetFloat("FallDistance"))
            {
                anim.SetFloat("FallDistance", distanceToGround);
            }

            if (slopeAngle > MaxSlopeAngle)
            {
                // Apply sliding logic
                Vector3 slideDirection = new Vector3(hit.normal.x, -hit.normal.y, hit.normal.z);
                cc.Move(slideDirection * SlideSpeed * Time.deltaTime);
            }
        }
    }
    

    public void OnInteract(InputAction.CallbackContext obj)
    {
        // Player interaction logic here
    }
    public void OnRun(InputAction.CallbackContext obj)
    {
        isRunning = !isRunning;   
    }
    public void OnJump(InputAction.CallbackContext obj)
    {
        if (IsSwimming)
        {
            return;
        }
        anim.SetTrigger("Jump");
        
    }

    public void Jump()
    {
        
        velocity.y = Mathf.Sqrt(jumpHeight * -2f * Gravity.y);
        cc.Move(velocity * Time.deltaTime);
        isGrounded = false;
        Debug.Log("Jumped with physics");
    }

    public void OnAttack(InputAction.CallbackContext obj)
    {
        // Player attack logic here
    }

    public void OnItem(InputAction.CallbackContext obj)
    {
        // Player item use logic here
    }

    public void CheckGrounded()
    {
        isGrounded = cc.isGrounded; 
        anim.SetBool("IsGrounded", cc.isGrounded);
        if(!cc.isGrounded & anim.GetBool("IsFalling") & distanceToGround < distanceToGroundForLanding)
        {
            anim.SetBool("IsGrounded", true);
        }
    }
    public void FixedUpdate()
    {
        CheckGrounded();
        CheckSlope();
        CheckEdgeGrab();

        ProcessMovement();
        if(IsSwimming)
        {
            Swimming();
        }
        //Apply animator components
        anim.SetFloat("MovementSpeed", MovementSpeed);



    }
}
