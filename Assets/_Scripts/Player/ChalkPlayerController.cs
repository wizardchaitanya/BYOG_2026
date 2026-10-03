using UnityEngine;

// Side-scroller controller: accel/decel, coyote time, jump buffer,
// fast-fall gravity, wall slide + wall jump. Slower in water.
// Setup: Rigidbody2D (Freeze Rotation Z, Interpolate) + Collider2D with a zero-friction material.
[RequireComponent(typeof(Rigidbody2D))]
public class ChalkPlayerController : MonoBehaviour
{
    [Header("Move")]
    public float maxSpeed = 7f;
    public float groundAccel = 60f;
    public float groundDecel = 70f;
    public float airAccel = 40f;
    public float airDecel = 20f;
    public float waterSpeedMult = 0.5f;
    [Range(0.2f, 1f)] public float minSpeedMult = 0.6f;   // speed multiplier at lowest health

    [Header("Jump")]
    public float jumpForce = 13f;
    [Range(0.2f, 1f)] public float minJumpMult = 0.65f;   // jump multiplier at lowest health
    public float coyoteTime = 0.1f;
    public float jumpBuffer = 0.12f;
    public float fallGravityMult = 2.2f;
    public float maxFallSpeed = 20f;

    [Header("Wall")]
    public float wallSlideSpeed = 2f;
    public Vector2 wallJumpForce = new Vector2(8f, 12f);
    public float wallJumpLock = 0.15f;       // input ignored briefly after wall jump

    [Header("Checks")]
    public LayerMask groundMask;             // Ground + Chalk (so you can stand on strokes)
    public Transform groundCheck;            // small point at feet
    public float groundCheckRadius = 0.15f;
    public Transform wallCheck;              // point at body side, slightly out from collider
    public float wallCheckRadius = 0.12f;

    [Header("Audio")]
    public float footstepInterval = 0.38f;

    Rigidbody2D rb;
    float stepTimer, airVy;
    ChalkPlayer chalk;
    ChalkBodyHealth body;
    float baseGravity;
    float inputX, coyoteCounter, bufferCounter, wallLockCounter;
    bool grounded, onWall, facingRight = true;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        chalk = GetComponent<ChalkPlayer>();
        body = GetComponent<ChalkBodyHealth>();
        // make sure nothing in the Inspector can lock movement
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.simulated = true;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        if (rb.gravityScale <= 0f) { Debug.LogWarning("Gravity Scale was 0, set to 3", this); rb.gravityScale = 3f; }
        if (!groundCheck) Debug.LogError("ChalkPlayerController: GroundCheck is not assigned", this);
        baseGravity = rb.gravityScale;
    }

    void Update()
    {
        if (!GameManager.IsPlaying) { inputX = 0f; bufferCounter = 0f; return; }

        inputX = Input.GetAxisRaw("Horizontal");
        if (Input.GetButtonDown("Jump")) bufferCounter = jumpBuffer;

        bool wasGrounded = grounded;
        grounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundMask);

        // audio: landing + footsteps
        if (!grounded) airVy = rb.velocity.y;
        else if (!wasGrounded && airVy < -4f) AudioManager.Land();

        if (grounded && Mathf.Abs(inputX) > 0.01f && Mathf.Abs(rb.velocity.x) > 0.5f)
        {
            stepTimer -= Time.deltaTime;
            if (stepTimer <= 0f) { AudioManager.Footstep(); stepTimer = footstepInterval / SpeedScale; }
        }
        else stepTimer = 0f;
        float dir = facingRight ? 1f : -1f;
        onWall = !grounded && wallCheck &&
                 Physics2D.OverlapCircle(wallCheck.position, wallCheckRadius, groundMask);

        coyoteCounter = grounded ? coyoteTime : coyoteCounter - Time.deltaTime;
        bufferCounter -= Time.deltaTime;
        wallLockCounter -= Time.deltaTime;

        // flip (wallCheck must be a child so it flips with the scale)
        if (wallLockCounter <= 0f)
        {
            if (inputX > 0.01f && !facingRight) Flip();
            else if (inputX < -0.01f && facingRight) Flip();
        }

        // jump / wall jump
        if (bufferCounter > 0f)
        {
            if (coyoteCounter > 0f) { Jump(); }
            else if (onWall) { WallJump(); }
        }
    }

    void FixedUpdate()
    {
        // horizontal
        float speedMult = (chalk && chalk.InWater) ? waterSpeedMult : 1f;
        float target = wallLockCounter > 0f ? rb.velocity.x : inputX * maxSpeed * speedMult * SpeedScale;
        bool accelerating = Mathf.Abs(inputX) > 0.01f && wallLockCounter <= 0f;
        float rate = grounded ? (accelerating ? groundAccel : groundDecel)
                              : (accelerating ? airAccel : airDecel);
        float vx = Mathf.MoveTowards(rb.velocity.x, target, rate * Time.fixedDeltaTime);

        // vertical: heavier fall, wall slide, terminal velocity
        float vy = rb.velocity.y;
        rb.gravityScale = vy < 0f ? baseGravity * fallGravityMult : baseGravity;

        bool pushingIntoWall = onWall && inputX * (facingRight ? 1f : -1f) > 0.01f;
        if (pushingIntoWall && vy < -wallSlideSpeed) vy = -wallSlideSpeed;
        vy = Mathf.Max(vy, -maxFallSpeed);

        rb.velocity = new Vector2(vx, vy);
    }

    // smaller (lower health) = slower and weaker jump
    float Health01 => body ? body.Health01 : 1f;
    float SpeedScale => Mathf.Lerp(minSpeedMult, 1f, Health01);
    float JumpScale => Mathf.Lerp(minJumpMult, 1f, Health01);

    void Jump()
    {
        rb.velocity = new Vector2(rb.velocity.x, jumpForce * JumpScale);
        AudioManager.Jump();
        bufferCounter = 0f; coyoteCounter = 0f;
    }

    void WallJump()
    {
        float away = facingRight ? -1f : 1f;
        rb.velocity = new Vector2(away * wallJumpForce.x * JumpScale, wallJumpForce.y * JumpScale);
        wallLockCounter = wallJumpLock;
        AudioManager.Jump();
        bufferCounter = 0f;
        Flip();
    }

    void Flip()
    {
        facingRight = !facingRight;
        var s = transform.localScale;
        s.x *= -1f;
        transform.localScale = s;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        if (groundCheck) Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        Gizmos.color = Color.cyan;
        if (wallCheck) Gizmos.DrawWireSphere(wallCheck.position, wallCheckRadius);
    }
}