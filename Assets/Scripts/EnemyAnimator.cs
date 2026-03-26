using UnityEngine;
using UnityEngine.AI;

public class EnemyAnimator : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Enemy enemy;

    [Header("Params")]
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private string shootTrigger = "Shoot";
    [SerializeField] private string deadBool = "Dead";

    void Awake()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (enemy == null)
            enemy = GetComponent<Enemy>();
    }

    void Update()
    {
        if (animator == null)
            return;

        float speed = 0f;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
            speed = agent.velocity.magnitude;

        animator.SetFloat(speedParam, speed);

        bool dead = enemy != null && !enemy.IsAlive;
        animator.SetBool(deadBool, dead);
    }

    public void TriggerShoot()
    {
        if (animator == null)
            return;

        if (enemy != null && !enemy.IsAlive)
            return;

        animator.SetTrigger(shootTrigger);
    }

    public void PlaySpawn()
    {
        if (animator == null)
            return;

        animator.Rebind();
        animator.Update(0f);
        animator.SetBool(deadBool, false);
    }

    public void PlayDeath()
    {
        if (animator == null)
            return;

        animator.SetBool(deadBool, true);
    }
}