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

    [Header("Shooting State")]
    [SerializeField] private float shootingStateHoldSeconds = 0.2f;
    [SerializeField] private float shootTurnSpeed = 12f;

    [Header("Tracer")]
    [SerializeField] private float tracerDuration = 0.04f;

    [Header("Debug")]
    [SerializeField] private bool drawDebugRays = true;

    private float nextFireTime;
    private float lastSeenTime = -999f;
    private bool seeingPlayer;
    private Collider targetCollider;
    private CharacterController targetController;
    private Coroutine tracerRoutine;

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

        CacheTargetRefs();

        if (tracerLine != null)
        {
            tracerLine.enabled = false;
            tracerLine.positionCount = 2;
            tracerLine.useWorldSpace = true;
        }
    }

    void CacheTargetRefs()
    {
        if (targetPlayer == null)
            return;

        if (targetCollider == null)
            targetCollider = targetPlayer.GetComponentInChildren<Collider>();

        if (targetController == null)
            targetController = targetPlayer.GetComponentInParent<CharacterController>();

        if (targetController == null)
            targetController = targetPlayer.GetComponent<CharacterController>();
    }

    public void ResetShooterState()
    {
        nextFireTime = 0f;
        lastSeenTime = -999f;
        seeingPlayer = false;

        if (tracerRoutine != null)
        {
            StopCoroutine(tracerRoutine);
            tracerRoutine = null;
        }

        if (tracerLine != null)
            tracerLine.enabled = false;

        if (enemyAnimator != null)
            enemyAnimator.SetShooting(false);
    }

    void Update()
    {
        GameSessionManger sm = enemy != null ? enemy.SessionManager : null;
        if (sm != null && !sm.IsRoundActive)
        {
            ResetShooterState();
            return;
        }

        if (enemy != null && !enemy.IsAlive)
        {
            if (enemyAnimator != null)
                enemyAnimator.SetShooting(false);
            return;
        }

        if (targetPlayer == null)
        {
            if (enemyAnimator != null)
                enemyAnimator.SetShooting(false);
            return;
        }

        if (targetPlayer.IsDead)
        {
            seeingPlayer = false;
            if (enemyAnimator != null)
                enemyAnimator.SetShooting(false);
            return;
        }

        CacheTargetRefs();

        if (TrySeePlayer(out Vector3 targetPoint))
        {
            seeingPlayer = true;
            lastSeenTime = Time.time;

            RotateTowards(targetPoint);

            if (enemyAnimator != null)
                enemyAnimator.SetShooting(true);

            if (Time.time >= nextFireTime)
                Shoot(targetPoint);
        }
        else
        {
            seeingPlayer = false;

            bool holdShootState = Time.time - lastSeenTime <= shootingStateHoldSeconds;

            if (enemyAnimator != null)
                enemyAnimator.SetShooting(holdShootState);
        }
    }

    Vector3 GetTargetPoint()
    {
        if (targetCollider != null)
            return targetCollider.bounds.center;

        if (targetController != null)
            return targetController.bounds.center;

        return targetPlayer.transform.position + Vector3.up;
    }

    void RotateTowards(Vector3 targetPoint)
    {
        Vector3 flatDir = targetPoint - transform.position;
        flatDir.y = 0f;

        if (flatDir.sqrMagnitude <= 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(flatDir.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, shootTurnSpeed * Time.deltaTime);
    }

    bool TrySeePlayer(out Vector3 targetPoint)
    {
        targetPoint = Vector3.zero;

        if (targetPlayer == null || eyePoint == null)
            return false;

        targetPoint = GetTargetPoint();

        Vector3 toPlayer = targetPoint - eyePoint.position;
        float distance = toPlayer.magnitude;

        if (drawDebugRays)
            Debug.DrawLine(eyePoint.position, targetPoint, Color.green);

        if (distance > detectRange)
            return false;

        Vector3 dir = toPlayer.normalized;
        float angle = Vector3.Angle(eyePoint.forward, dir);
        if (angle > fieldOfViewDegrees * 0.5f)
            return false;

        if (Physics.Raycast(eyePoint.position, dir, out RaycastHit hit, distance, visionMask, QueryTriggerInteraction.Ignore))
        {
            if (drawDebugRays)
                Debug.DrawLine(eyePoint.position, hit.point, Color.red);

            PlayerHealth hitHealth = hit.collider.GetComponentInParent<PlayerHealth>();
            if (hitHealth == null || hitHealth.transform.root != targetPlayer.transform.root)
                return false;
        }

        if (drawDebugRays)
            Debug.DrawRay(eyePoint.position, dir * detectRange, Color.yellow);

        return true;
    }

    void Shoot(Vector3 targetPoint)
    {
        nextFireTime = Time.time + 1f / Mathf.Max(0.01f, fireRate);

        Vector3 hitRayOrigin = eyePoint != null ? eyePoint.position : transform.position;
        Vector3 hitDir = (targetPoint - hitRayOrigin).normalized;
        Vector3 tracerStart = gunMuzzle != null ? gunMuzzle.position : hitRayOrigin;

        bool hasHit = Physics.Raycast(hitRayOrigin, hitDir, out RaycastHit hit, detectRange, visionMask, QueryTriggerInteraction.Ignore);
        Vector3 tracerEnd = hasHit ? hit.point : hitRayOrigin + hitDir * detectRange;

        if (tracerLine != null)
        {
            if (tracerRoutine != null)
                StopCoroutine(tracerRoutine);
            tracerRoutine = StartCoroutine(ShowTracer(tracerStart, tracerEnd));
        }

        if (!hasHit)
            return;

        PlayerHealth hitHealth = hit.collider.GetComponentInParent<PlayerHealth>();
        if (hitHealth != null && hitHealth.transform.root == targetPlayer.transform.root)
            hitHealth.TakeDamage(damagePerShot);
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
