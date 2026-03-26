using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TMPro;
using UnityEngine;

public class GameSessionManger : MonoBehaviour
{
    [Header("Experiment")]
    [SerializeField] private int maxRounds = 3;

    [Header("Round")]
    [SerializeField] private float roundDurationSeconds = 180f;

    [Header("Player")]
    [SerializeField] private PlayerShootGame playerShoot;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerLook playerLook;
    [SerializeField] private PlayerHealth playerHealth;

    [Header("UI")]
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text accuracyText;
    [SerializeField] private TMP_Text killsText;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private TMP_Text ammoText;
    [SerializeField] private TMP_Text summaryText;
    [SerializeField] private GameObject startButton;
    [SerializeField] private TMP_InputField playerIdInput;

    [Header("Logging")]
    [SerializeField] private bool writeCsv = true;
    [SerializeField] private string killLogFileName = "game_kill_log.csv";
    [SerializeField] private string roundSummaryFileName = "game_round_summary.csv";

    private bool roundActive;
    private float timeRemaining;
    private float roundStartTime;

    private int shotsFired;
    private int hits;
    private int headshots;
    private int kills;

    private int roundIndex;

    private string lockedPlayerId = "Player";
    private bool identityLocked;

    private int reloadCount;
    private float reloadStartTime;
    private float reloadTimeSum;
    private bool reloadActive;

    private int recoilSamples;
    private float recoilErrorSum;

    private bool burstActive;
    private Enemy burstEnemy;
    private readonly List<Vector3> burstHitPoints = new List<Vector3>(8);

    private float lastKillTime;
    private bool awaitingSwitch;
    private float switchTimeSum;
    private int switchTimeCount;

    private class Engagement
    {
        public int engagementIndex;
        public float spawnTime;
        public float firstHitTime = -1f;
    }

    private int engagementCounter;
    private readonly Dictionary<Enemy, Engagement> engagements = new Dictionary<Enemy, Engagement>();

    private float sumFirstHitLatency;
    private int countFirstHitLatency;

    private float sumTtkFromFirstHit;
    private int countTtkFromFirstHit;

    private string killLogPath;
    private string roundSummaryPath;

    public bool IsRoundActive => roundActive;

    void Start()
    {
        ResetRoundState();
        PrepareCsvFiles();
    }

    void Update()
    {
        if (!roundActive)
            return;

        timeRemaining -= Time.deltaTime;
        if (timeRemaining <= 0f)
        {
            timeRemaining = 0f;
            EndRound(false);
            return;
        }

        UpdateTimerUI();
    }

    public void StartRound()
    {
        if (roundActive)
            return;

        if (roundIndex >= maxRounds)
            return;

        LockIdentity();

        roundIndex++;

        roundActive = true;
        timeRemaining = roundDurationSeconds;
        roundStartTime = Time.time;

        shotsFired = 0;
        hits = 0;
        headshots = 0;
        kills = 0;

        reloadCount = 0;
        reloadStartTime = 0f;
        reloadTimeSum = 0f;
        reloadActive = false;

        recoilSamples = 0;
        recoilErrorSum = 0f;

        burstActive = false;
        burstEnemy = null;
        burstHitPoints.Clear();

        lastKillTime = 0f;
        awaitingSwitch = false;
        switchTimeSum = 0f;
        switchTimeCount = 0;

        engagementCounter = 0;
        engagements.Clear();

        sumFirstHitLatency = 0f;
        countFirstHitLatency = 0;

        sumTtkFromFirstHit = 0f;
        countTtkFromFirstHit = 0;

        Enemy[] existingEnemies = Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        for (int i = 0; i < existingEnemies.Length; i++)
        {
            if (existingEnemies[i] != null && existingEnemies[i].IsAlive)
                NotifyEnemySpawned(existingEnemies[i]);
        }

        if (playerHealth != null)
            playerHealth.ResetHealth();

        if (playerShoot != null)
            playerShoot.enabled = true;

        if (playerMovement != null)
            playerMovement.enabled = true;

        if (playerLook != null)
            playerLook.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (startButton != null)
            startButton.SetActive(false);

        if (summaryText != null)
            summaryText.text = "";

        UpdateTimerUI();
        UpdateAccuracyUI();
        UpdateKillsUI();
        UpdateHealthUI();
    }

    public void EndRound(bool playerDied)
    {
        if (!roundActive)
            return;

        if (burstActive)
            EndBurstInternal();

        roundActive = false;

        if (playerShoot != null)
            playerShoot.enabled = false;

        if (playerMovement != null)
            playerMovement.enabled = false;

        if (playerLook != null)
            playerLook.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (startButton != null)
            startButton.SetActive(true);

        float accuracyPercent = shotsFired > 0 ? (hits / (float)shotsFired) * 100f : 0f;
        float headshotPercent = hits > 0 ? (headshots / (float)hits) * 100f : 0f;

        float avgFirstHitLatency = countFirstHitLatency > 0 ? sumFirstHitLatency / countFirstHitLatency : 0f;
        float avgTtkFromFirstHit = countTtkFromFirstHit > 0 ? sumTtkFromFirstHit / countTtkFromFirstHit : 0f;

        float avgRecoilError = recoilSamples > 0 ? recoilErrorSum / recoilSamples : 0f;
        float avgSwitchTime = switchTimeCount > 0 ? switchTimeSum / switchTimeCount : 0f;

        float elapsed = Mathf.Max(0.01f, Time.time - roundStartTime);
        float hitsPerMinute = hits > 0 ? (hits / elapsed) * 60f : 0f;

        float shotsPerKill = kills > 0 ? (shotsFired / (float)kills) : 0f;

        float avgReloadTime = reloadCount > 0 ? reloadTimeSum / reloadCount : 0f;

        string diedText = playerDied ? "Player died" : "Time up";
        if (summaryText != null)
        {
            summaryText.text =
                diedText +
                "\nPlayer: " + lockedPlayerId +
                "\nRound: " + roundIndex + " / " + maxRounds +
                "\nAcc: " + accuracyPercent.ToString("F1") + "%  HS: " + headshotPercent.ToString("F1") + "%" +
                "\nKills: " + kills + "  Shots/Kill: " + shotsPerKill.ToString("F2") +
                "\nFirstHit: " + avgFirstHitLatency.ToString("F3") + "s  TTK(after hit): " + avgTtkFromFirstHit.ToString("F3") + "s" +
                "\nRecoilErr: " + avgRecoilError.ToString("F3") + "  Switch: " + avgSwitchTime.ToString("F3") +
                "\nReloads: " + reloadCount + "  AvgReload: " + avgReloadTime.ToString("F3") +
                "\nHPM: " + hitsPerMinute.ToString("F1");
        }

        WriteRoundSummaryCsv(playerDied, accuracyPercent, headshotPercent, avgFirstHitLatency, avgTtkFromFirstHit, shotsPerKill, avgRecoilError, avgSwitchTime, reloadCount, avgReloadTime, hitsPerMinute);

        if (roundIndex >= maxRounds && startButton != null)
            startButton.SetActive(false);

        UpdateTimerUI();
        UpdateAccuracyUI();
        UpdateKillsUI();
        UpdateHealthUI();
    }

    public void OnPlayerDied()
    {
        EndRound(true);
    }

    public void BeginBurst()
    {
        if (!roundActive)
            return;

        burstActive = true;
        burstEnemy = null;
        burstHitPoints.Clear();
    }

    public void RegisterBurstHitPoint(Enemy enemy, Vector3 hitPoint)
    {
        if (!roundActive)
            return;

        if (!burstActive)
            return;

        if (enemy == null)
            return;

        if (burstEnemy == null)
            burstEnemy = enemy;
        else if (burstEnemy != enemy)
            return;

        burstHitPoints.Add(hitPoint);
    }

    public void EndBurst()
    {
        if (!roundActive)
            return;

        if (!burstActive)
            return;

        EndBurstInternal();
    }

    private void EndBurstInternal()
    {
        burstActive = false;

        int count = burstHitPoints.Count;
        if (count >= 2)
        {
            Vector3 center = Vector3.zero;
            for (int i = 0; i < count; i++)
                center += burstHitPoints[i];
            center /= count;

            float sumDist = 0f;
            for (int i = 0; i < count; i++)
                sumDist += Vector3.Distance(center, burstHitPoints[i]);

            float meanDist = sumDist / count;

            recoilSamples++;
            recoilErrorSum += meanDist;
        }

        burstHitPoints.Clear();
        burstEnemy = null;
    }

    public void RegisterReloadStarted()
    {
        if (!roundActive)
            return;

        if (reloadActive)
            return;

        reloadActive = true;
        reloadStartTime = Time.time;
        reloadCount++;
    }

    public void RegisterReloadFinished()
    {
        if (!roundActive)
            return;

        if (!reloadActive)
            return;

        reloadActive = false;
        float d = Mathf.Max(0f, Time.time - reloadStartTime);
        reloadTimeSum += d;
    }

    public void RegisterShot()
    {
        if (!roundActive)
            return;

        shotsFired++;
        UpdateAccuracyUI();
    }

    public void RegisterEnemyHitDetailed(Enemy enemy, bool headshotHit, Vector3 hitPoint, Vector3 aimCenter)
    {
        if (!roundActive || enemy == null)
            return;

        hits++;
        if (headshotHit)
            headshots++;

        if (awaitingSwitch && lastKillTime > 0f)
        {
            float st = Mathf.Max(0f, Time.time - lastKillTime);
            switchTimeSum += st;
            switchTimeCount++;
            awaitingSwitch = false;
        }

        if (!engagements.TryGetValue(enemy, out Engagement e))
        {
            e = new Engagement();
            engagements.Add(enemy, e);
            engagementCounter++;
            e.engagementIndex = engagementCounter;
            e.spawnTime = Time.time;
            e.firstHitTime = -1f;
        }

        if (e.firstHitTime < 0f)
            e.firstHitTime = Time.time;

        UpdateAccuracyUI();
    }

    public void UpdateAmmoDisplay(int current, int max, bool showReloadHint)
    {
        if (ammoText == null)
            return;

        if (max <= 0)
        {
            ammoText.text = "Ammo: -";
            return;
        }

        if (showReloadHint && current == 0)
            ammoText.text = "Ammo: " + current + "/" + max + "  Press R to reload";
        else
            ammoText.text = "Ammo: " + current + "/" + max;
    }

    public void NotifyEnemySpawned(Enemy enemy)
    {
        if (!roundActive || enemy == null)
            return;

        if (!engagements.TryGetValue(enemy, out Engagement e))
        {
            e = new Engagement();
            engagements.Add(enemy, e);
        }

        engagementCounter++;
        e.engagementIndex = engagementCounter;
        e.spawnTime = Time.time;
        e.firstHitTime = -1f;
    }

    public void RegisterEnemyKill(Enemy enemy, bool headshotKill)
    {
        if (!roundActive || enemy == null)
            return;

        kills++;
        UpdateKillsUI();

        if (!engagements.TryGetValue(enemy, out Engagement e))
        {
            e = new Engagement();
            engagements.Add(enemy, e);
            engagementCounter++;
            e.engagementIndex = engagementCounter;
            e.spawnTime = Time.time;
            e.firstHitTime = -1f;
        }

        float killTime = Time.time;

        float firstHitLatency = e.firstHitTime >= 0f ? e.firstHitTime - e.spawnTime : -1f;
        float ttkFromFirstHit = e.firstHitTime >= 0f ? killTime - e.firstHitTime : -1f;

        if (firstHitLatency >= 0f)
        {
            sumFirstHitLatency += firstHitLatency;
            countFirstHitLatency++;
        }

        if (ttkFromFirstHit >= 0f)
        {
            sumTtkFromFirstHit += ttkFromFirstHit;
            countTtkFromFirstHit++;
        }

        WriteKillLogCsv(enemy, e, firstHitLatency, ttkFromFirstHit, headshotKill);

        lastKillTime = killTime;
        awaitingSwitch = true;
    }

    public void UpdateHealthUI()
    {
        if (healthText == null || playerHealth == null)
            return;

        healthText.text = "HP: " + playerHealth.CurrentHealth + "/" + playerHealth.MaxHealth;
    }

    private void UpdateTimerUI()
    {
        if (timerText == null)
            return;

        int seconds = Mathf.CeilToInt(timeRemaining);
        timerText.text = "Time: " + seconds + "s";
    }

    private void UpdateAccuracyUI()
    {
        if (accuracyText == null)
            return;

        float accuracyPercent = shotsFired > 0 ? (hits / (float)shotsFired) * 100f : 0f;
        float headshotPercent = hits > 0 ? (headshots / (float)hits) * 100f : 0f;

        accuracyText.text =
            "Acc: " + accuracyPercent.ToString("F1") + "%  Hits: " + hits + "/" + shotsFired +
            "\nHS: " + headshotPercent.ToString("F1") + "% (" + headshots + ")";
    }

    private void UpdateKillsUI()
    {
        if (killsText == null)
            return;

        killsText.text = "Kills: " + kills;
    }

    private void ResetRoundState()
    {
        roundActive = false;
        timeRemaining = roundDurationSeconds;

        shotsFired = 0;
        hits = 0;
        headshots = 0;
        kills = 0;

        roundIndex = 0;

        identityLocked = false;
        lockedPlayerId = "Player";

        reloadCount = 0;
        reloadStartTime = 0f;
        reloadTimeSum = 0f;
        reloadActive = false;

        recoilSamples = 0;
        recoilErrorSum = 0f;

        burstActive = false;
        burstEnemy = null;
        burstHitPoints.Clear();

        if (playerShoot != null)
            playerShoot.enabled = false;

        if (playerMovement != null)
            playerMovement.enabled = false;

        if (playerLook != null)
            playerLook.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (startButton != null)
            startButton.SetActive(true);

        if (summaryText != null)
            summaryText.text = "Press Start";

        UpdateTimerUI();
        UpdateAccuracyUI();
        UpdateKillsUI();
        UpdateHealthUI();
    }

    private void LockIdentity()
    {
        if (identityLocked)
            return;

        string id = "Player";

        if (playerIdInput != null)
        {
            string t = playerIdInput.text;
            if (!string.IsNullOrWhiteSpace(t))
                id = t.Trim();
        }

        lockedPlayerId = id;
        identityLocked = true;

        if (playerIdInput != null)
        {
            playerIdInput.text = lockedPlayerId;
            playerIdInput.interactable = false;
            playerIdInput.readOnly = true;
            playerIdInput.gameObject.SetActive(false);
        }
    }

    private void PrepareCsvFiles()
    {
        if (!writeCsv)
            return;

        killLogPath = Path.Combine(Application.persistentDataPath, killLogFileName);
        roundSummaryPath = Path.Combine(Application.persistentDataPath, roundSummaryFileName);

        if (!File.Exists(killLogPath))
            File.WriteAllText(killLogPath, "Timestamp,PlayerId,Round,EngagementIndex,EnemyName,FirstHitLatency,TTKFromFirstHit,HeadshotKill\n");

        if (!File.Exists(roundSummaryPath))
            File.WriteAllText(roundSummaryPath, "Timestamp,PlayerId,Round,PlayerDied,ShotsFired,Hits,Headshots,Kills,AccuracyPercent,HeadshotPercent,AvgFirstHitLatency,AvgTTKFromFirstHit,ShotsPerKill,AvgRecoilError,AvgSwitchTime,ReloadCount,AvgReloadTime,HitsPerMinute\n");
    }

    private void WriteKillLogCsv(Enemy enemy, Engagement e, float firstHitLatency, float ttkFromFirstHit, bool headshotKill)
    {
        if (!writeCsv)
            return;

        if (string.IsNullOrEmpty(killLogPath))
            PrepareCsvFiles();

        string line =
            System.DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + "," +
            EscapeCsv(lockedPlayerId) + "," +
            roundIndex.ToString(CultureInfo.InvariantCulture) + "," +
            e.engagementIndex.ToString(CultureInfo.InvariantCulture) + "," +
            EscapeCsv(enemy.name) + "," +
            firstHitLatency.ToString("F4", CultureInfo.InvariantCulture) + "," +
            ttkFromFirstHit.ToString("F4", CultureInfo.InvariantCulture) + "," +
            (headshotKill ? "1" : "0") +
            "\n";

        File.AppendAllText(killLogPath, line);
    }

    private void WriteRoundSummaryCsv(bool playerDied, float acc, float hsPct, float avgFirstHit, float avgTtkFromFirstHit, float shotsPerKill, float avgRecoil, float avgSwitch, int reloads, float avgReload, float hpm)
    {
        if (!writeCsv)
            return;

        if (string.IsNullOrEmpty(roundSummaryPath))
            PrepareCsvFiles();

        string line =
            System.DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + "," +
            EscapeCsv(lockedPlayerId) + "," +
            roundIndex.ToString(CultureInfo.InvariantCulture) + "," +
            (playerDied ? "1" : "0") + "," +
            shotsFired.ToString(CultureInfo.InvariantCulture) + "," +
            hits.ToString(CultureInfo.InvariantCulture) + "," +
            headshots.ToString(CultureInfo.InvariantCulture) + "," +
            kills.ToString(CultureInfo.InvariantCulture) + "," +
            acc.ToString("F2", CultureInfo.InvariantCulture) + "," +
            hsPct.ToString("F2", CultureInfo.InvariantCulture) + "," +
            avgFirstHit.ToString("F4", CultureInfo.InvariantCulture) + "," +
            avgTtkFromFirstHit.ToString("F4", CultureInfo.InvariantCulture) + "," +
            shotsPerKill.ToString("F4", CultureInfo.InvariantCulture) + "," +
            avgRecoil.ToString("F4", CultureInfo.InvariantCulture) + "," +
            avgSwitch.ToString("F4", CultureInfo.InvariantCulture) + "," +
            reloads.ToString(CultureInfo.InvariantCulture) + "," +
            avgReload.ToString("F4", CultureInfo.InvariantCulture) + "," +
            hpm.ToString("F2", CultureInfo.InvariantCulture) +
            "\n";

        File.AppendAllText(roundSummaryPath, line);
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        bool mustQuote = value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r");
        if (!mustQuote)
            return value;

        string escaped = value.Replace("\"", "\"\"");
        return "\"" + escaped + "\"";
    }
}