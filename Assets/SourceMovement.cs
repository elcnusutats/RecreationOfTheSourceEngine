using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class SourceMovement : MonoBehaviour
{
    [Header("mouse settings")]
    public Transform playerCamera;
    public float mouseSensitivity = 0.05f;
    
    [Header("camera settings")]
    public float tiltAngle = 4f;
    public float wallRunTiltAngle = 15f;   
    public float tiltSpeed = 10f;
    public float jumpKickAmount = 3f;
    public float landingPunchMultiplier = 0.4f;
    public float punchRecoverySpeed = 15f;
    
    private float cameraPitch = 0f;
    private float currentTilt = 0f;
    private float currentPunchX = 0f;
    private float defaultCameraY;

    [Header("physics")]
    public float gravity = 25f;       
    public float friction = 8f;       
    public float jumpForce = 10f;     
    public float pushPower = 5f;      

    [Header("speeds")]
    public float moveSpeed = 12f;        
    public float runAcceleration = 100f; 
    public float runDeacceleration = 15f;
    public float jumpBoost = 4f;         
    public float airAcceleration = 150f; 
    public float airCap = 25f;           

    [Header("sliding")]
    public float slideBoost = 15f;
    public float slideFriction = 1f;      
    private bool isSliding = false;
    private float defaultHeight = 2f;
    private float slideHeight = 1f;

    [Header("wallrun")]
    public float wallRunGravity = 2f;     
    public float wallJumpKickForce = 15f; 
    public LayerMask wallLayer;           
    private bool isWallRunning = false;
    private bool wallLeft = false;
    private bool wallRight = false;
    private Vector3 wallNormal;

    [Header("grapple thing")]
    public float grappleRange = 100f;
    public float grapplePullSpeed = 40f;
    private bool isGrappling = false;
    private Vector3 grapplePoint;
    private LineRenderer grappleRope;

    
    private CharacterController controller;
    private Vector3 playerVelocity = Vector3.zero;
    private bool wishJump = false;
    private bool wasGrounded = false;
    private bool jumpedThisFrame = false;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        defaultCameraY = playerCamera.localPosition.y;

        
        grappleRope = gameObject.AddComponent<LineRenderer>();
        grappleRope.startWidth = 0.05f;
        grappleRope.endWidth = 0.05f;
        grappleRope.material = new Material(Shader.Find("Sprites/Default"));
        grappleRope.startColor = Color.red;
        grappleRope.endColor = Color.white;
        grappleRope.enabled = false;
        
        
        if (wallLayer == 0) wallLayer = ~LayerMask.GetMask("Ignore Raycast");
    }

    void Update()
    {
        HandleInputs();
        HandleCameraPolish();
        HandleMouseLook();
        DrawGrappleRope();
    }

    void FixedUpdate()
    {
        jumpedThisFrame = false;
        CheckLanding();
        CheckWallRun();

        if (controller.isGrounded)
        {
            if (isSliding) SlideMove();
            else GroundMove();
        }
        else
        {
            AirMove();
            if (isWallRunning) ApplyWallRunPhysics();
            if (isGrappling) ApplyGrapplePhysics();
        }

        ApplyGravity();
        controller.Move(playerVelocity * Time.fixedDeltaTime);
        wasGrounded = controller.isGrounded;
    }

    
    private void HandleInputs()
    {
        if (Keyboard.current == null || Mouse.current == null) return;

        
        if (Keyboard.current.spaceKey.isPressed) wishJump = true;

        
        bool holdingSlide = Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.cKey.isPressed;
        if (holdingSlide && !isSliding && controller.isGrounded)
        {
            StartSlide();
        }
        else if (!holdingSlide && isSliding)
        {
            StopSlide();
        }

        
        if (Mouse.current.rightButton.wasPressedThisFrame && !isGrappling)
        {
            StartGrapple();
        }
        else if (Mouse.current.rightButton.wasReleasedThisFrame)
        {
            StopGrapple();
        }
    }

    
    private void StartSlide()
    {
        isSliding = true;
        controller.height = slideHeight; 
        
       
        Vector3 currentDir = new Vector3(playerVelocity.x, 0, playerVelocity.z).normalized;
        if (currentDir.magnitude > 0)
        {
            playerVelocity.x += currentDir.x * slideBoost;
            playerVelocity.z += currentDir.z * slideBoost;
        }
        
        currentPunchX += 5f; 
    }

    private void StopSlide()
    {
        isSliding = false;
        controller.height = defaultHeight; 
    }

    private void SlideMove()
    {
        ApplyFriction(slideFriction); 
        
        if (wishJump)
        {
            playerVelocity.y = jumpForce;
            jumpedThisFrame = true;
            wishJump = false;
            StopSlide();
            return;
        }
    }

    
    private void StartGrapple()
    {
        if (Physics.Raycast(playerCamera.position, playerCamera.forward, out RaycastHit hit, grappleRange, wallLayer))
        {
            isGrappling = true;
            grapplePoint = hit.point;
            grappleRope.enabled = true;
        }
    }

    private void StopGrapple()
    {
        isGrappling = false;
        grappleRope.enabled = false;
    }

    private void ApplyGrapplePhysics()
    {
        Vector3 pullDirection = (grapplePoint - transform.position).normalized;
        
        playerVelocity += pullDirection * grapplePullSpeed * Time.fixedDeltaTime;
    }

    private void DrawGrappleRope()
    {
        if (!isGrappling) return;
        
        grappleRope.SetPosition(0, playerCamera.position + (transform.right * 0.5f) - (transform.up * 0.5f));
        grappleRope.SetPosition(1, grapplePoint);
    }

    
    private void CheckWallRun()
    {
        wallLeft = Physics.Raycast(transform.position, -transform.right, out RaycastHit leftHit, 1.2f, wallLayer);
        wallRight = Physics.Raycast(transform.position, transform.right, out RaycastHit rightHit, 1.2f, wallLayer);

        if ((wallLeft || wallRight) && !controller.isGrounded && playerVelocity.y < 0) 
        {
            isWallRunning = true;
            wallNormal = wallLeft ? leftHit.normal : rightHit.normal;
        }
        else
        {
            isWallRunning = false;
        }
    }

    private void ApplyWallRunPhysics()
    {
        
        playerVelocity.y = Mathf.Max(playerVelocity.y, -wallRunGravity);

        
        if (wishJump)
        {
            playerVelocity.y = jumpForce * 1.2f; 
            playerVelocity += wallNormal * wallJumpKickForce; 
            jumpedThisFrame = true;
            wishJump = false;
            isWallRunning = false;
        }
    }

    
    private void GroundMove()
    {
        ApplyFriction(1f);
        Vector3 wishDir = GetWishDirection();

        if (wishJump)
        {
            playerVelocity.y = jumpForce;
            playerVelocity.x += wishDir.x * jumpBoost;
            playerVelocity.z += wishDir.z * jumpBoost;
            currentPunchX -= jumpKickAmount; 
            jumpedThisFrame = true;
            wishJump = false;
            return;
        }

        Accelerate(wishDir, moveSpeed, runAcceleration);
    }

    private void AirMove()
    {
        Vector3 wishDir = GetWishDirection();
        Accelerate(wishDir, airCap, airAcceleration);
    }

    private void ApplyGravity()
    {
        if (controller.isGrounded && !jumpedThisFrame) 
        {
            playerVelocity.y = -2f; 
        } 
        else if (!isWallRunning) 
        {
            playerVelocity.y -= gravity * Time.fixedDeltaTime;
        }
    }

    private void ApplyFriction(float multiplier)
    {
        Vector3 vec = playerVelocity;
        vec.y = 0f; 
        float speed = vec.magnitude;
        float drop = 0f;

        if (controller.isGrounded)
        {
            float control = speed < runDeacceleration ? runDeacceleration : speed;
            drop = control * friction * multiplier * Time.fixedDeltaTime;
        }

        float newSpeed = speed - drop;
        if (newSpeed < 0) newSpeed = 0;
        if (speed > 0) newSpeed /= speed;

        playerVelocity.x *= newSpeed;
        playerVelocity.z *= newSpeed;
    }

    private void Accelerate(Vector3 wishDir, float wishSpeed, float accel)
    {
        float currentSpeed = Vector3.Dot(playerVelocity, wishDir);
        float addSpeed = wishSpeed - currentSpeed;

        if (addSpeed <= 0) return;

        float accelSpeed = accel * Time.fixedDeltaTime * wishSpeed;
        if (accelSpeed > addSpeed) accelSpeed = addSpeed;

        playerVelocity.x += accelSpeed * wishDir.x;
        playerVelocity.z += accelSpeed * wishDir.z;
    }

    private Vector3 GetWishDirection()
    {
        if (Keyboard.current == null) return Vector3.zero;

        float horizontal = (Keyboard.current.dKey.isPressed ? 1f : 0f) - (Keyboard.current.aKey.isPressed ? 1f : 0f);
        float vertical = (Keyboard.current.wKey.isPressed ? 1f : 0f) - (Keyboard.current.sKey.isPressed ? 1f : 0f);

        Vector3 wishDir = transform.right * horizontal + transform.forward * vertical;
        if (wishDir.magnitude > 1) wishDir.Normalize();

        return wishDir;
    }

    
    private void HandleCameraPolish()
    {
        currentPunchX = Mathf.Lerp(currentPunchX, 0f, Time.deltaTime * punchRecoverySpeed);

        float targetTilt = 0f;
        if (isWallRunning)
        {
            
            targetTilt = wallLeft ? -wallRunTiltAngle : wallRunTiltAngle;
        }
        else if (!controller.isGrounded && Keyboard.current != null)
        {
            
            float horizontal = (Keyboard.current.dKey.isPressed ? 1f : 0f) - (Keyboard.current.aKey.isPressed ? 1f : 0f);
            targetTilt = -horizontal * tiltAngle; 
        }
        
        currentTilt = Mathf.Lerp(currentTilt, targetTilt, Time.deltaTime * tiltSpeed);

        
        float targetHeight = isSliding ? defaultCameraY - 0.5f : defaultCameraY;
        playerCamera.localPosition = Vector3.Lerp(playerCamera.localPosition, new Vector3(0, targetHeight, 0), Time.deltaTime * 10f);
    }

    private void HandleMouseLook()
    {
        if (Mouse.current == null) return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        float mouseX = mouseDelta.x * mouseSensitivity;
        float mouseY = mouseDelta.y * mouseSensitivity;

        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(cameraPitch, -90f, 90f);
        
        playerCamera.localRotation = Quaternion.Euler(cameraPitch + currentPunchX, 0f, currentTilt);
        transform.Rotate(Vector3.up * mouseX);
    }

    private void CheckLanding()
    {
        if (!wasGrounded && controller.isGrounded)
        {
            float fallSpeed = Mathf.Abs(playerVelocity.y);
            if (fallSpeed > 2f) 
            {
                currentPunchX += fallSpeed * landingPunchMultiplier;
            }
        }
    }
}