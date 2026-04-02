using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] float walkSpeed = 5f;
    [SerializeField] float sprintSpeed = 8f;
    [SerializeField] float gravity = -24f;
    [SerializeField] float jumpHeight = 1.2f;

    [SerializeField] Transform groundCheck;
    [SerializeField] float groundCheckRadius = 0.25f;
    [SerializeField] LayerMask groundMask = ~0;

    CharacterController controller;
    Vector3 velocity;
    bool isGrounded;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        isGrounded = groundCheck != null
            ? Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundMask, QueryTriggerInteraction.Ignore)
            : controller.isGrounded;

        if (isGrounded && velocity.y < 0f)
            velocity.y = -2f;

        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");

        Vector3 move = transform.right * x + transform.forward * z;
        if (move.sqrMagnitude > 1f)
            move.Normalize();

        float speed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : walkSpeed;
        controller.Move(move * speed * Time.deltaTime);

        if (Input.GetButtonDown("Jump") && isGrounded)
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    public void ResetMovementState()
    {
        velocity = Vector3.zero;
    }
}
