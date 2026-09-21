using UnityEngine;

/// <summary>One piece of a shattered prop: fades out near the end of its life, then removes itself.</summary>
public class ShatterFragment : MonoBehaviour
{
    private SpriteRenderer visual;
    private float deathTime;
    private float fadeSeconds;
    private float startAlpha;

    /// <summary>Starts the countdown; the piece fades during the last <paramref name="fade"/> seconds.</summary>
    public void Launch(float lifetime, float fade)
    {
        visual = GetComponent<SpriteRenderer>();
        startAlpha = visual != null ? visual.color.a : 1f;
        fadeSeconds = Mathf.Clamp(fade, 0f, lifetime);
        deathTime = Time.time + lifetime;
    }

    private void Update()
    {
        float remaining = deathTime - Time.time;
        if (remaining <= 0f)
        {
            Destroy(gameObject);
            return;
        }
        if (visual == null || fadeSeconds <= 0f || remaining > fadeSeconds) return;
        var color = visual.color;
        color.a = startAlpha * (remaining / fadeSeconds);
        visual.color = color;
    }
}
