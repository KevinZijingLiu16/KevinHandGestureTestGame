using UnityEngine;

/// <summary>A projectile disappears when it hits solid scenery.</summary>
public class UpwardBullet : MonoBehaviour
{
    private void OnCollisionEnter2D(Collision2D collision)
    {
        Destroy(gameObject);
    }
}
