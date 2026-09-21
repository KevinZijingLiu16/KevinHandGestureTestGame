using UnityEngine;

/// <summary>Player's health pool; enemies chip it down on contact or while attacking the ground/walls.</summary>
public class PlayerHealth : MonoBehaviour
{
    // Fires whenever the pool changes, with (current, max).
    public event System.Action<float, float> HealthChanged;

    [SerializeField] private float maxHealth = 100f;

    private float currentHealth;

    public float Current => currentHealth;
    public float Max => maxHealth;
    public bool IsDead => currentHealth <= 0f;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    private void Start()
    {
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(float amount)
    {
        if (amount <= 0f || IsDead) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }
}
