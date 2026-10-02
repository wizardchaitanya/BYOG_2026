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

    [Header("Jump")]
    public float jumpForce = 13f;
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

    Rigidbody2D rb;
    ChalkPlayer chalk;
    float baseGravity;
    float inputX, coyoteCounter, bufferCounter, wallLockCounter;
    bool grounded, onWall, facingRight = true;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        chalk = GetComponent<ChalkPlayer>();
        baseGravity = rb.gravityScale;
    }

    void Update()
    {
        inputX = Input.GetAxisRaw("Horizontal");
        if (Input.GetButtonDown("Jump")) bufferCounter = jumpBuffer;

        grounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundMask);
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
        float target = wallLockCounter > 0f ? rb.velocity.x : inputX * maxSpeed * speedMult;
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

    void Jump()
    {
        rb.velocity = new Vector2(rb.velocity.x, jumpForce);
        bufferCounter = 0f; coyoteCounter = 0f;
    }

    void WallJump()
    {
        float away = facingRight ? -1f : 1f;
        rb.velocity = new Vector2(away * wallJumpForce.x, wallJumpForce.y);
        wallLockCounter = wallJumpLock;
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