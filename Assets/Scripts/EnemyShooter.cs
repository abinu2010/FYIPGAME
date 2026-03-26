using UnityEngine;

public class EnemyShooter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Enemy enemy;
    [SerializeField] private EnemyAnimator enemyAnimator;
    [SerializeField] private Transform eyePoint;
    [SerializeField] private Transform gunMuzzle;
    [SerializeField] private LineRenderer tracerLine;
    [SerializeField] private PlayerHealth targetPlayer;
    [SerializeField] private LayerMask visionMask = ~0;

    [Header("Stats")]
    [SerializeField] private float detectRange = 30f;
    [SerializeField] private float fieldOfViewDegrees = 120f;
    [SerializeField] private float fireRate = 3f;
    [SerializeField] private int damagePerShot = 5;

    [Header("Tracer")]
    [SerializeField] private float tracerDuration = 0.04f;

    [Header("Debug")]
    [SerializeField] private bool drawDebugRays = true;

    float nextFireTime;
    bool seeingPlayer;
    Collider targetCollider;
    Coroutine tracerRoutine;

    public bool IsSeeingPlayer => seeingPlayer;
    public PlayerHealth TargetPlayer => targetPlayer;

    void Start()
    {
        if (enemy == null)
            enemy = GetComponent<Enemy>();

        if (enemyAnimator == null)
            enemyAnimator = GetComponent<EnemyAnimator>();

        if (eyePoint == null)
            eyePoint = transform;

        if (targetPlayer != null)
            targetCollider = targetPlayer.GetComponent<Collider>();

        if (tracerLine != null)
        {
            tracerLine.enabled = false;
            tracerLine.positionCount = 2;
            tracerLine.useWorldSpace = true;
        }
    }

    void Update()
    {
        GameSessionManger sm = enemy != null ? enemy.SessionManager : null;
        if (sm != null && !sm.IsRoundActive)
        {
            seeingPlayer = false;
            return;
        }

        if (enemy != null && !enemy.IsAlive)
            return;

        if (targetPlayer == null)
            return;

        if (targetPlayer.IsDead)
        {
            seeingPlayer = false;
            return;
        }

        if (TrySeePlayer(out Vector3 dir))
        {
            if (Time.time >= nextFireTime)
                Shoot(dir);
        }
    }

    bool TrySeePlayer(out Vector3 direction)
    {
        direction = Vector3.zero;

        if (targetCollider == null && targetPlayer != null)
            targetCollider = targetPlayer.GetComponent<Collider>();

        Vector3 targetPoint = targetCollider != null
            ? targetCollider.bounds.center
            : targetPlayer.transform.position;

        Vector3 toPlayer = targetPoint - eyePoint.position;
        float distance = toPlayer.magnitude;

        if (drawDebugRays)
            Debug.DrawLine(eyePoint.position, targetPoint, Color.green);

        if (distance > detectRange)
        {
            seeingPlayer = false;
            return false;
        }

        Vector3 dir = toPlayer.normalized;
        float angle = Vector3.Angle(eyePoint.forward, dir);
        if (angle > fieldOfViewDegrees * 0.5f)
        {
            seeingPlayer = false;
            return false;
        }

        if (Physics.Raycast(eyePoint.position, dir, out RaycastHit hit, distance, visionMask))
        {
            if (drawDebugRays)
                Debug.DrawLine(eyePoint.position, hit.point, Color.red);

            PlayerHealth hitHealth = hit.collider.GetComponentInParent<PlayerHealth>();
            if (hitHealth == null)
            {
                seeingPlayer = false;
                return false;
            }

            seeingPlayer = true;
            direction = dir;
            return true;
        }

        if (drawDebugRays)
            Debug.DrawRay(eyePoint.position, dir * detectRange, Color.yellow);

        seeingPlayer = false;
        return false;
    }

    void Shoot(Vector3 direction)
    {
        nextFireTime = Time.time + 1f / fireRate;

        if (enemyAnimator != null)
            enemyAnimator.TriggerShoot();

        Vector3 start = gunMuzzle != null ? gunMuzzle.position : eyePoint.position;

        bool hasHit = Physics.Raycast(start, direction, out RaycastHit hit, detectRange, visionMask);

        Vector3 end = hasHit ? hit.point : start + direction * detectRange;

        if (tracerLine != null)
        {
            if (tracerRoutine != null)
                StopCoroutine(tracerRoutine);

            tracerRoutine = StartCoroutine(ShowTracer(start, end));
        }

        if (hasHit)
        {
            PlayerHealth hitHealth = hit.collider.GetComponentInParent<PlayerHealth>();
            if (hitHealth != null)
                hitHealth.TakeDamage(damagePerShot);
        }

        Vector3 flatDir = new Vector3(direction.x, 0f, direction.z);
        if (flatDir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(flatDir);
    }

    System.Collections.IEnumerator ShowTracer(Vector3 start, Vector3 end)
    {
        tracerLine.enabled = true;
        tracerLine.SetPosition(0, start);
        tracerLine.SetPosition(1, end);
        yield return new WaitForSeconds(tracerDuration);
        tracerLine.enabled = false;
        tracerRoutine = null;
    }

    void OnDrawGizmosSelected()
    {
        Transform eye = eyePoint != null ? eyePoint : transform;

        Vector3 origin = eye.position;
        Vector3 forward = eye.forward;
        Vector3 forwardFlat = new Vector3(forward.x, 0f, forward.z);
        if (forwardFlat.sqrMagnitude < 0.0001f)
            forwardFlat = Vector3.forward;
        forwardFlat.Normalize();

        float halfFov = fieldOfViewDegrees * 0.5f;

        Gizmos.color = new Color(1f, 1f, 0f, 0.25f);

        int segments = 24;
        float step = fieldOfViewDegrees / segments;
        Vector3 previous = Vector3.zero;
        bool hasPrevious = false;

        for (int i = 0; i <= segments; i++)
        {
            float angle = -halfFov + step * i;
            Quaternion rot = Quaternion.AngleAxis(angle, Vector3.up);
            Vector3 dir = rot * forwardFlat;
            Vector3 point = origin + dir * detectRange;

            if (hasPrevious)
                Gizmos.DrawLine(previous, point);

            previous = point;
            hasPrevious = true;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(origin, origin + forwardFlat * detectRange);
    }
}