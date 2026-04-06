using UnityEngine;
using UnityEngine.AI;

public class EnemyAnimator : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Enemy enemy;

    [Header("Params")]
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private string shootingBool = "IsShooting";
    [SerializeField] private string deadBool = "Dead";

    [Header("Blend")]
    [SerializeField] private float speedDamp = 10f;

    private float currentSpeed;
    private bool isShooting;

    void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (enemy == null)
            enemy = GetComponent<Enemy>();

        if (animator != null)
            animator.applyRootMotion = false;
    }

    void Update()
    {
        if (animator == null)
            return;

        bool dead = enemy != null && !enemy.IsAlive;
        animator.SetBool(deadBool, dead);

        if (dead)
        {
            isShooting = false;
            currentSpeed = 0f;
            animator.SetBool(shootingBool, false);
            animator.SetFloat(speedParam, 0f);
            return;
        }

        float targetSpeed = 0f;

        if (agent != null && agent.enabled && agent.isOnNavMesh && !agent.isStopped)
            targetSpeed = agent.velocity.magnitude / Mathf.Max(0.01f, agent.speed);

        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, speedDamp * Time.deltaTime);

        animator.SetFloat(speedParam, currentSpeed);
        animator.SetBool(shootingBool, isShooting);
    }

    public void SetShooting(bool value)
    {
        if (enemy != null && !enemy.IsAlive)
            value = false;

        isShooting = value;

        if (animator == null)
            return;

        animator.SetBool(shootingBool, isShooting);
    }

    public void PlaySpawn()
    {
        if (animator == null)
            return;

        animator.Rebind();
        animator.Update(0f);

        isShooting = false;
        currentSpeed = 0f;

        animator.SetBool(deadBool, false);
        animator.SetBool(shootingBool, false);
        animator.SetFloat(speedParam, 0f);
    }

    public void PlayDeath()
    {
        if (animator == null)
            return;

        isShooting = false;
        currentSpeed = 0f;

        animator.SetBool(shootingBool, false);
        animator.SetFloat(speedParam, 0f);
        animator.SetBool(deadBool, true);
    }
}
