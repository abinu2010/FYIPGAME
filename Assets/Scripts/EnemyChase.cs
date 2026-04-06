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
    [SerializeField] private float repathInterval = 0.25f;

    [Header("Aggro")]
    [SerializeField] private bool autoAggroOnStart = true;

    private NavMeshAgent agent;
    private bool aggro;
    private float repathTimer;

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

        if (target == null && shooter != null && shooter.TargetPlayer != null)
            target = shooter.TargetPlayer.transform;

        if (agent != null)
        {
            agent.speed = moveSpeed;
            agent.stoppingDistance = stopDistance;
            agent.updateRotation = false;
            agent.isStopped = true;
        }

        aggro = autoAggroOnStart;
    }

    void Update()
    {
        GameSessionManger sm = enemy != null ? enemy.SessionManager : null;
        if (sm != null && !sm.IsRoundActive)
        {
            StopAgent();
            return;
        }

        if (enemy != null && !enemy.IsAlive)
        {
            StopAgent();
            return;
        }

        ResolveTarget();
        if (target == null)
        {
            StopAgent();
            return;
        }

        if (!aggro)
        {
            StopAgent();
            return;
        }

        if (shooter != null && shooter.IsSeeingPlayer)
        {
            StopAgent();
            return;
        }

        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        agent.speed = moveSpeed;
        agent.stoppingDistance = stopDistance;
        agent.updateRotation = false;

        Vector3 flatToTarget = target.position - transform.position;
        flatToTarget.y = 0f;

        float stopDistanceSqr = stopDistance * stopDistance;
        if (flatToTarget.sqrMagnitude <= stopDistanceSqr)
        {
            StopAgent();
            FaceDirection(flatToTarget);
            return;
        }

        repathTimer -= Time.deltaTime;
        if (repathTimer <= 0f)
        {
            agent.SetDestination(target.position);
            repathTimer = repathInterval;
        }

        if (agent.isStopped)
            agent.isStopped = false;

        Vector3 moveDir = agent.desiredVelocity;
        moveDir.y = 0f;
        FaceDirection(moveDir);
    }

    void ResolveTarget()
    {
        if (target != null)
            return;

        if (shooter != null && shooter.TargetPlayer != null)
            target = shooter.TargetPlayer.transform;
    }

    void FaceDirection(Vector3 flatDirection)
    {
        if (flatDirection.sqrMagnitude <= 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(flatDirection.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
    }

    void StopAgent()
    {
        if (agent == null)
            return;

        if (!agent.enabled)
            return;

        if (!agent.isStopped)
            agent.isStopped = true;

        agent.ResetPath();
    }

    public void SetAggro(bool value)
    {
        aggro = value;

        if (!aggro)
            StopAgent();
    }

    public void ResetForSpawn()
    {
        aggro = autoAggroOnStart;
        repathTimer = 0f;
        StopAgent();
    }

    public void ResetAggro()
    {
        aggro = false;
        repathTimer = 0f;
        StopAgent();
    }
}
