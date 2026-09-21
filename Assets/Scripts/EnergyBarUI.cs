using UnityEngine;
using UnityEngine.UI;

/// <summary>Drives a filled UI Image to reflect a PlayerEnergy pool.</summary>
public class EnergyBarUI : MonoBehaviour
{
    [SerializeField] private PlayerEnergy energy;
    [SerializeField] private Image fillImage;

    private void OnEnable()
    {
        // Image.Type.Filled ignores fillAmount entirely when no sprite is assigned
        // (it falls back to drawing the full rect), so make sure one is always set.
        if (fillImage != null && fillImage.sprite == null)
            fillImage.sprite = Sprite.Create(Texture2D.whiteTexture,
                new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                new Vector2(0.5f, 0.5f), Texture2D.whiteTexture.width);

        if (energy != null) energy.EnergyChanged += OnEnergyChanged;
    }

    private void OnDisable()
    {
        if (energy != null) energy.EnergyChanged -= OnEnergyChanged;
    }

    private void Start()
    {
        if (energy != null) OnEnergyChanged(energy.Current, energy.Max);
    }

    private void OnEnergyChanged(float current, float max)
    {
        if (fillImage == null || max <= 0f) return;
        fillImage.fillAmount = Mathf.Clamp01(current / max);
    }
}
