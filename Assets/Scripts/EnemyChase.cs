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

        // Make sure we always have a target
        if (target == null)
        {
            if (shooter != null && shooter.TargetPlayer != null)
                target = shooter.TargetPlayer.transform;

            if (target == null)
            {
                if (!warnedNoTarget)
                {
                    Debug.LogWarning("EnemyChase: No target to chase on " + gameObject.name);
                    warnedNoTarget = true;
                }
                return;
            }
        }

        // Vision can also wake them up
        if (!aggro && shooter != null && shooter.IsSeeingPlayer)
        {
            aggro = true;
            Debug.Log("EnemyChase: Aggro from vision on " + gameObject.name);
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

        if (!agent.isOnNavMesh)
        {
            Debug.LogWarning("EnemyChase: Agent not on navmesh " + gameObject.name);
            return;
        }

        agent.speed = moveSpeed;

        // Re-set destination every small interval
        repathTimer -= Time.deltaTime;
        if (repathTimer <= 0f)
        {
            Vector3 dest = target.position;
            bool ok = agent.SetDestination(dest);
            if (!ok)
            {
                Debug.LogWarning("EnemyChase: SetDestination failed on " + gameObject.name);
            }
            else
            {
                Debug.Log("EnemyChase: Chasing, dest " + dest);
            }

            agent.isStopped = false;
            repathTimer = repathInterval;
        }

        // Stop when close enough on the navmesh path
        if (!agent.pathPending && agent.hasPath)
        {
            if (agent.remainingDistance <= stopDistance)
            {
                if (!agent.isStopped)
                {
                    agent.isStopped = true;
                    Debug.Log("EnemyChase: Reached stop distance on " + gameObject.name);
                }
            }
        }

        // Face the player
        Vector3 toTarget = target.position - transform.position;
        Vector3 flat = new Vector3(toTarget.x, 0f, toTarget.z);
        if (flat.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(flat);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
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
