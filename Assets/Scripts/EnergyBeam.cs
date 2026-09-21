using UnityEngine;

/// <summary>
/// A single laser segment with no travel time: every call to <see cref="UpdateBeam"/> re-casts the
/// full length instantly, shattering every breakable along the way and stopping at the first solid
/// obstacle. The owner (<see cref="EnergyWaveShooter"/>) calls this once per frame while the beam
/// should stay alive and destroys the GameObject the moment it should stop.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class EnergyBeam : MonoBehaviour
{
    private LineRenderer line;
    private Transform origin;
    private Collider2D ownerCollider;
    private float range;
    private RaycastHit2D[] hitBuffer = new RaycastHit2D[16];
    private ContactFilter2D contactFilter;

    public void Initialize(Transform origin, Collider2D ownerCollider, float range, float width, Color color)
    {
        this.origin = origin;
        this.ownerCollider = ownerCollider;
        this.range = range;
        contactFilter = ContactFilter2D.noFilter;

        line = GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.startWidth = width;
        line.endWidth = width * 0.6f;
        line.numCapVertices = 4;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = color;
        var tail = color;
        tail.a *= 0.15f;
        line.endColor = tail;
        line.sortingOrder = 10;
    }

    /// <summary>Re-casts the beam along <paramref name="direction"/> for this frame only.</summary>
    public void UpdateBeam(Vector2 direction)
    {
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        Vector2 start = origin.position;

        // Start the cast a little outside the player's own collider so it doesn't self-hit.
        float startOffset = 0.1f;
        if (ownerCollider != null)
        {
            var bounds = ownerCollider.bounds;
            startOffset += Mathf.Abs(direction.x) * bounds.extents.x + Mathf.Abs(direction.y) * bounds.extents.y;
        }
        start += direction * startOffset;

        float length = Mathf.Max(0f, range - startOffset);
        if (length > 0f)
        {
            int count = Physics2D.Raycast(start, direction, contactFilter, hitBuffer, length);
            System.Array.Sort(hitBuffer, 0, count, Comparer.Instance);
            for (int i = 0; i < count; i++)
            {
                var hit = hitBuffer[i];
                if (hit.collider == null) continue;
                if (ownerCollider != null && hit.collider == ownerCollider) continue;
                if (hit.collider.GetComponentInParent<ShatterFragment>() != null) continue;

                var shatterable = hit.collider.GetComponentInParent<ShatterOnHit>();
                if (shatterable != null)
                {
                    shatterable.Shatter(hit.point);
                    continue; // Pierce through breakables instead of stopping.
                }

                length = hit.distance; // First solid obstacle stops the beam here.
                break;
            }
        }

        Vector2 end = start + direction * length;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }

    private class Comparer : System.Collections.Generic.IComparer<RaycastHit2D>
    {
        public static readonly Comparer Instance = new Comparer();
        public int Compare(RaycastHit2D a, RaycastHit2D b) => a.distance.CompareTo(b.distance);
    }
}
