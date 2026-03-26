using UnityEngine;

public sealed class PlayerAnimator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;

    [Header("Params")]
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private string shootTrigger = "Shoot";

    [Header("Blend")]
    [SerializeField] private float dampSpeed = 12f;

    private float currentSpeed;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        if (animator == null)
            return;

        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");

        bool hasMoveInput = Mathf.Abs(x) > 0.01f || Mathf.Abs(z) > 0.01f;
        bool isSprinting = hasMoveInput && Input.GetKey(KeyCode.LeftShift);

        float targetSpeed = 0f;

        if (hasMoveInput)
            targetSpeed = isSprinting ? 1f : 0.5f;

        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, dampSpeed * Time.deltaTime);
        animator.SetFloat(speedParam, currentSpeed);

        if (Input.GetButtonDown("Fire1"))
            animator.SetTrigger(shootTrigger);
    }
}