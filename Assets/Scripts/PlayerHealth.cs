using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private GameSessionManger sessionManager;

    private int currentHealth;
    private bool isDead;

    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public bool IsDead => isDead;

    void Start()
    {
        ResetHealth();
        Debug.Log("PlayerHealth: Started with " + currentHealth);
    }

    public void ResetHealth()
    {
        isDead = false;
        currentHealth = maxHealth;

        if (sessionManager != null)
            sessionManager.UpdateHealthUI();
    }

    public void TakeDamage(int amount)
    {
        if (isDead)
            return;

        currentHealth -= amount;
        if (currentHealth < 0)
            currentHealth = 0;

        Debug.Log("PlayerHealth: Took damage " + amount + ", now " + currentHealth);

        if (sessionManager != null)
            sessionManager.UpdateHealthUI();

        if (currentHealth == 0)
            Die();
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;
        Debug.Log("PlayerHealth: Player died");

        if (sessionManager != null)
            sessionManager.OnPlayerDied();
    }
}
