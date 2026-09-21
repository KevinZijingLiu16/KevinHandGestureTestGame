using UnityEngine;
using UnityEngine.UI;

/// <summary>Drives a filled UI Image to reflect a PlayerHealth pool.</summary>
public class HealthBarUI : MonoBehaviour
{
    [SerializeField] private PlayerHealth health;
    [SerializeField] private Image fillImage;

    private void OnEnable()
    {
        // Image.Type.Filled ignores fillAmount entirely when no sprite is assigned
        // (it falls back to drawing the full rect), so make sure one is always set.
        if (fillImage != null && fillImage.sprite == null)
            fillImage.sprite = Sprite.Create(Texture2D.whiteTexture,
                new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                new Vector2(0.5f, 0.5f), Texture2D.whiteTexture.width);

        if (health != null) health.HealthChanged += OnHealthChanged;
    }

    private void OnDisable()
    {
        if (health != null) health.HealthChanged -= OnHealthChanged;
    }

    private void Start()
    {
        if (health != null) OnHealthChanged(health.Current, health.Max);
    }

    private void OnHealthChanged(float current, float max)
    {
        if (fillImage == null || max <= 0f) return;
        fillImage.fillAmount = Mathf.Clamp01(current / max);
    }
}
