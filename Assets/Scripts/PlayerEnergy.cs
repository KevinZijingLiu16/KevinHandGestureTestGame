using UnityEngine;

/// <summary>Shared energy pool the player's attacks draw from; regenerates over time when not spent.</summary>
public class PlayerEnergy : MonoBehaviour
{
    // Fires whenever the pool changes, with (current, max).
    public event System.Action<float, float> EnergyChanged;

    [SerializeField] private float maxEnergy = 100f;
    [SerializeField] private float regenPerSecond = 12f;
    [SerializeField] private float regenDelaySeconds = 0.6f;

    private float currentEnergy;
    private float lastSpendTime = float.NegativeInfinity;

    public float Current => currentEnergy;
    public float Max => maxEnergy;

    private void Awake()
    {
        currentEnergy = maxEnergy;
    }

    private void Start()
    {
        EnergyChanged?.Invoke(currentEnergy, maxEnergy);
    }

    private void Update()
    {
        if (currentEnergy >= maxEnergy) return;
        if (Time.time - lastSpendTime < regenDelaySeconds) return;

        float next = Mathf.Min(maxEnergy, currentEnergy + regenPerSecond * Time.deltaTime);
        if (next == currentEnergy) return;
        currentEnergy = next;
        EnergyChanged?.Invoke(currentEnergy, maxEnergy);
    }

    /// <summary>Spends <paramref name="amount"/> energy if enough is available; leaves the pool untouched otherwise.</summary>
    public bool TrySpend(float amount)
    {
        if (amount <= 0f) return true;
        if (currentEnergy < amount) return false;

        currentEnergy -= amount;
        lastSpendTime = Time.time;
        EnergyChanged?.Invoke(currentEnergy, maxEnergy);
        return true;
    }
}
