using System.Collections;
using UnityEngine;

public class PlayerShootGame : MonoBehaviour
{
    [Header("Core")]
    [SerializeField] private Camera cam;
    [SerializeField] private float range = 100f;
    [SerializeField] private float fireRate = 10f;
    [SerializeField] private int magazineSize = 30;
    [SerializeField] private float reloadDuration = 0.8f;
    [SerializeField] private GameSessionManger sessionManager;
    [SerializeField] private LayerMask shotMask = ~0;

    [Header("Burst Fire")]
    [SerializeField] private bool burstFire = true;
    [SerializeField] private int burstSize = 3;
    [SerializeField] private float burstShotsPerSecond = 12f;
    [SerializeField] private float burstsPerSecond = 2.5f;

    [Header("Gun Recoil Object")]
    [SerializeField] private GunRecoil gunRecoil;

    [Header("View Recoil")]
    [SerializeField] private PlayerLook playerLook;
    [SerializeField] private bool applyViewRecoil = true;
    [SerializeField] private float recoilPitchShot1 = 0.6f;
    [SerializeField] private float recoilPitchShot2 = 1.0f;
    [SerializeField] private float recoilPitchShot3 = 1.35f;
    [SerializeField] private float recoilYawJitter = 0.2f;

    [Header("Visual Effects")]
    [SerializeField] private Transform muzzleTransform;
    [SerializeField] private LineRenderer tracerLine;
    [SerializeField] private float tracerDuration = 0.03f;
    [SerializeField] private GameObject hitImpactPrefab;
    [SerializeField] private float hitImpactLifetime = 3f;

    private float nextActionTime;
    private int bulletsInMag;
    private bool isReloading;
    private bool burstInProgress;
    private int currentBurstShotIndex;
    private Coroutine tracerRoutine;

    void Start()
    {
        ResetForNewRound();
    }

    void Update()
    {
        if (sessionManager != null && !sessionManager.IsRoundActive)
            return;

        if (isReloading)
            return;

        if (burstInProgress)
            return;

        if (Input.GetKeyDown(KeyCode.R))
        {
            StartReload();
            return;
        }

        if (burstFire)
        {
            if (Input.GetButtonDown("Fire1"))
                TryStartBurst();
        }
        else
        {
            if (Input.GetButton("Fire1"))
                TryToFireSingle();
        }
    }

    public void ResetForNewRound()
    {
        StopAllCoroutines();

        nextActionTime = 0f;
        isReloading = false;
        burstInProgress = false;
        bulletsInMag = magazineSize;
        currentBurstShotIndex = 0;
        tracerRoutine = null;

        if (tracerLine != null)
            tracerLine.enabled = false;

        if (gunRecoil != null)
            gunRecoil.ResetInstant();

        if (sessionManager != null)
            sessionManager.UpdateAmmoDisplay(bulletsInMag, magazineSize, false);
    }

    void TryStartBurst()
    {
        if (Time.time < nextActionTime)
            return;

        if (bulletsInMag <= 0)
        {
            StartReload();
            return;
        }

        nextActionTime = Time.time + 1f / Mathf.Max(0.01f, burstsPerSecond);
        StartCoroutine(BurstCoroutine());
    }

    IEnumerator BurstCoroutine()
    {
        burstInProgress = true;
        currentBurstShotIndex = 0;

        if (sessionManager != null)
            sessionManager.BeginBurst();

        int shotsToFire = Mathf.Max(1, burstSize);
        float shotInterval = 1f / Mathf.Max(0.01f, burstShotsPerSecond);

        for (int i = 0; i < shotsToFire; i++)
        {
            if (isReloading)
                break;

            if (bulletsInMag <= 0)
                break;

            FireOneShot();

            if (i < shotsToFire - 1)
                yield return new WaitForSeconds(shotInterval);
        }

        if (sessionManager != null)
            sessionManager.EndBurst();

        burstInProgress = false;
        currentBurstShotIndex = 0;

        if (bulletsInMag <= 0)
            StartReload();
    }

    void TryToFireSingle()
    {
        if (Time.time < nextActionTime)
            return;

        if (bulletsInMag <= 0)
        {
            StartReload();
            return;
        }

        nextActionTime = Time.time + 1f / Mathf.Max(0.01f, fireRate);
        currentBurstShotIndex = 0;
        FireOneShot();

        if (bulletsInMag <= 0)
            StartReload();
    }

    void FireOneShot()
    {
        bulletsInMag--;

        if (sessionManager != null)
        {
            sessionManager.RegisterShot();
            sessionManager.UpdateAmmoDisplay(bulletsInMag, magazineSize, bulletsInMag <= 0);
        }

        FireRay();

        if (gunRecoil != null)
            gunRecoil.ApplyRecoil();

        if (applyViewRecoil && playerLook != null)
        {
            float pitch = GetPitchForCurrentShot();
            float yaw = recoilYawJitter > 0f ? Random.Range(-recoilYawJitter, recoilYawJitter) : 0f;
            playerLook.AddRecoil(pitch, yaw);
        }

        currentBurstShotIndex++;
    }

    float GetPitchForCurrentShot()
    {
        if (!burstFire)
            return recoilPitchShot1;

        if (currentBurstShotIndex <= 0)
            return recoilPitchShot1;

        if (currentBurstShotIndex == 1)
            return recoilPitchShot2;

        return recoilPitchShot3;
    }

    void FireRay()
    {
        if (cam == null)
            return;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        bool hitSomething = Physics.Raycast(ray, out RaycastHit hit, range, shotMask, QueryTriggerInteraction.Ignore);

        Vector3 tracerStart = muzzleTransform != null ? muzzleTransform.position : ray.origin;
        Vector3 tracerEnd = hitSomething ? hit.point : ray.origin + ray.direction * range;

        if (tracerLine != null)
        {
            if (tracerRoutine != null)
                StopCoroutine(tracerRoutine);
            tracerRoutine = StartCoroutine(ShowTracer(tracerStart, tracerEnd));
        }

        EnemyHitbox hb = hitSomething ? hit.collider.GetComponent<EnemyHitbox>() : null;
        Enemy shotEnemy = hb != null ? hb.Owner : null;

        if (sessionManager != null && (burstFire || burstInProgress))
            sessionManager.RegisterBurstShotPoint(shotEnemy, tracerEnd);

        if (!hitSomething)
            return;

        if (hitImpactPrefab != null)
        {
            Quaternion rot = Quaternion.LookRotation(hit.normal);
            GameObject impact = Instantiate(hitImpactPrefab, hit.point, rot);
            Destroy(impact, hitImpactLifetime);
        }

        if (hb == null || hb.Owner == null)
            return;

        bool isHead = hb.HitArea == Enemy.HitArea.Head;
        Vector3 aimCenter = hb.Owner.RecoilCenter != null ? hb.Owner.RecoilCenter.position : hit.collider.bounds.center;

        if (sessionManager != null)
            sessionManager.RegisterEnemyHitDetailed(hb.Owner, isHead, hit.point, aimCenter);

        hb.Owner.TakeHit(hb.HitArea);
    }

    void StartReload()
    {
        if (isReloading)
            return;

        if (bulletsInMag == magazineSize)
            return;

        if (sessionManager != null)
            sessionManager.RegisterReloadStarted();

        isReloading = true;
        StartCoroutine(ReloadCoroutine());
    }

    IEnumerator ReloadCoroutine()
    {
        yield return new WaitForSeconds(reloadDuration);

        bulletsInMag = magazineSize;
        isReloading = false;
        currentBurstShotIndex = 0;

        if (sessionManager != null)
        {
            sessionManager.RegisterReloadFinished();
            sessionManager.UpdateAmmoDisplay(bulletsInMag, magazineSize, false);
        }
    }

    IEnumerator ShowTracer(Vector3 start, Vector3 end)
    {
        if (tracerLine == null)
            yield break;

        tracerLine.positionCount = 2;
        tracerLine.SetPosition(0, start);
        tracerLine.SetPosition(1, end);
        tracerLine.enabled = true;

        yield return new WaitForSeconds(tracerDuration);

        tracerLine.enabled = false;
        tracerRoutine = null;
    }
}
