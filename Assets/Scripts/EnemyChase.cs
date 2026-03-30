using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyChase : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Enemy enemy;
    [SerializeField] private EnemyShooter shooter;
    [SerializeField] private Transform target;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float stopDistance = 3f;
    [SerializeField] private float turnSpeed = 10f;

    [Header("Aggro")]
    [SerializeField] private bool autoAggroOnStart = true;

    NavMeshAgent agent;
    bool aggro;
    float repathTimer;
    const float repathInterval = 0.25f;
    bool warnedNoTarget;

    public bool Aggro => aggro;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    void Start()
    {
        if (enemy == null)
            enemy = GetComponent<Enemy>();

        if (shooter == null)
            shooter = GetComponent<EnemyShooter>();

        // Try to grab the player from the shooter if not set
        if (target == null && shooter != null && shooter.TargetPlayer != null)
            target = shooter.TargetPlayer.transform;

        agent.speed = moveSpeed;
        agent.stoppingDistance = stopDistance;
        agent.updateRotation = false;
        agent.isStopped = true;

        if (autoAggroOnStart)
        {
            aggro = true;
            Debug.Log("EnemyChase: Auto aggro on " + gameObject.name);
        }

        Debug.Log("EnemyChase: Ready on " + gameObject.name);
    }

    void Update()
    {
        GameSessionManger sm = enemy != null ? enemy.SessionManager : null;
        if (sm != null && !sm.IsRoundActive)
        {
            if (agent != null && !agent.isStopped)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            return;
        }

        if (enemy != null && !enemy.IsAlive)
        {
            if (!agent.isStopped)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            return;
        }

        if (target == null)
        {
            if (shooter != null && shooter.TargetPlayer != null)
                target = shooter.TargetPlayer.transform;

            if (target == null)
                return;
        }
        if (aggro || (shooter != null && shooter.IsSeeingPlayer))
        {
            Vector3 rotTarget = target.position - transform.position;
            Vector3 rotFlat = new Vector3(rotTarget.x, 0f, rotTarget.z);
            if (rotFlat.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(rotFlat);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
            }
        }

        if (!aggro)
        {
            if (!agent.isStopped)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            return;
        }

        if (!agent.isOnNavMesh) return;

        agent.speed = moveSpeed;

        repathTimer -= Time.deltaTime;
        if (repathTimer <= 0f)
        {
            agent.SetDestination(target.position);
            agent.isStopped = false;
            repathTimer = repathInterval;
        }

        if (!agent.pathPending && agent.hasPath)
        {
            if (agent.remainingDistance <= stopDistance)
            {
                if (!agent.isStopped) agent.isStopped = true;
            }
        }

    }
    public void ResetAggro()
    {
        aggro = false;
        warnedNoTarget = false;

        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
    }
}
