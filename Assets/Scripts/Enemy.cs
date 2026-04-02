using UnityEngine;
using UnityEngine.AI;

public class Enemy : MonoBehaviour
{
    public enum HitArea
    {
        Body,
        Head
    }

    [Header("Stats")]
    [SerializeField] private int baseHealth = 100;
    [SerializeField] private int bodyshotDamage = 10;
    [SerializeField] private int headshotDamage = 50;

    [Header("Links")]
    [SerializeField] private Transform recoilCenter;
    [SerializeField] private GameSessionManger sessionManager;

    [Header("Respawn")]
    [SerializeField] private float respawnDelaySeconds = 2f;
    [SerializeField] private float deathAnimationSeconds = 1.6f;
    [SerializeField] private Transform spawnPoint;

    private int currentHealth;
    private bool isAlive = true;

    private Collider[] colliders;
    private Renderer[] renderers;
    private NavMeshAgent agent;
    private EnemyChase chase;
    private EnemyShooter shooter;
    private EnemyAnimator enemyAnimator;

    private Vector3 spawnPosition;
    private Quaternion spawnRotation;

    private bool lastHitHeadshot;
    private float lastSpawnTime;
    private Coroutine respawnRoutine;

    public bool IsAlive => isAlive;
    public float LastSpawnTime => lastSpawnTime;
    public GameSessionManger SessionManager => sessionManager;
    public Transform RecoilCenter => recoilCenter;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        chase = GetComponent<EnemyChase>();
        shooter = GetComponent<EnemyShooter>();
        enemyAnimator = GetComponent<EnemyAnimator>();
    }

    void Start()
    {
        colliders = GetComponentsInChildren<Collider>(true);
        renderers = GetComponentsInChildren<Renderer>(true);

        spawnPosition = transform.position;
        spawnRotation = transform.rotation;

        SpawnNew();
    }

    public void ForceResetForRoundStart()
    {
        if (respawnRoutine != null)
        {
            StopCoroutine(respawnRoutine);
            respawnRoutine = null;
        }

        SpawnNew();
    }

    public void SpawnNew()
    {
        if (respawnRoutine != null)
        {
            StopCoroutine(respawnRoutine);
            respawnRoutine = null;
        }

        Vector3 pos = spawnPoint != null ? spawnPoint.position : spawnPosition;
        Quaternion rot = spawnPoint != null ? spawnPoint.rotation : spawnRotation;

        if (agent != null)
        {
            if (agent.enabled)
            {
                agent.isStopped = true;
                agent.ResetPath();
                agent.enabled = false;
            }
        }

        transform.position = pos;
        transform.rotation = rot;

        currentHealth = baseHealth;
        isAlive = true;
        lastHitHeadshot = false;
        lastSpawnTime = Time.time;

        SetRenderersEnabled(true);
        SetHitCollidersEnabled(true);

        if (agent != null)
        {
            agent.enabled = true;
            agent.isStopped = false;
        }

        if (chase != null)
            chase.enabled = true;

        if (shooter != null)
        {
            shooter.enabled = true;
            shooter.ResetShooterState();
        }

        if (enemyAnimator != null)
            enemyAnimator.PlaySpawn();

        if (sessionManager != null && sessionManager.IsRoundActive)
            sessionManager.NotifyEnemySpawned(this);
    }

    void SetHitCollidersEnabled(bool value)
    {
        if (colliders == null)
            return;

        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = value;
    }

    void SetRenderersEnabled(bool value)
    {
        if (renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
            renderers[i].enabled = value;
    }

    public void TakeHit(HitArea area)
    {
        if (!isAlive)
            return;

        bool headshot = area == HitArea.Head;
        int damage = headshot ? headshotDamage : bodyshotDamage;

        lastHitHeadshot = headshot;
        currentHealth -= damage;

        if (currentHealth <= 0)
            Die();
    }

    void Die()
    {
        if (!isAlive)
            return;

        isAlive = false;

        SetHitCollidersEnabled(false);

        if (agent != null)
        {
            if (agent.enabled)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            agent.enabled = false;
        }

        if (chase != null)
            chase.enabled = false;

        if (shooter != null)
        {
            shooter.ResetShooterState();
            shooter.enabled = false;
        }

        if (enemyAnimator != null)
            enemyAnimator.PlayDeath();

        if (sessionManager != null)
            sessionManager.RegisterEnemyKill(this, lastHitHeadshot);

        respawnRoutine = StartCoroutine(RespawnRoutine());
    }

    System.Collections.IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(deathAnimationSeconds);
        SetRenderersEnabled(false);
        yield return new WaitForSeconds(Mathf.Max(0f, respawnDelaySeconds - deathAnimationSeconds));
        respawnRoutine = null;
        SpawnNew();
    }
}
