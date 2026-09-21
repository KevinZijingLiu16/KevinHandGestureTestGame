using UnityEngine;

/// <summary>
/// While the player holds a "peace sign" (index + middle finger extended, ring + pinky curled),
/// fires a continuous laser beam that drains energy every second and cuts through every
/// breakable in its path. Releasing the gesture, running out of energy, or losing tracking
/// stops the beam immediately.
/// </summary>
public class EnergyWaveShooter : MonoBehaviour
{
    [SerializeField] private HandTrackingManager handTracking;
    [SerializeField] private PlayerEnergy energy;

    [Header("Gesture")]
    [SerializeField] private float extendedThreshold = 1.15f;
    [SerializeField] private float curledThreshold = 0.95f;
    [SerializeField] private float confirmationSeconds = 0.15f;
    [SerializeField] private float trackingTimeout = 0.3f;

    [Header("Middle Finger Aim")]
    [SerializeField] private float aimSmoothingSpeed = 15f;
    [SerializeField] private float aimMemorySeconds = 0.3f;

    [Header("Beam")]
    [SerializeField] private float energyDrainPerSecond = 60f;
    [SerializeField] private float beamRange = 15f;
    [SerializeField] private float beamWidth = 0.3f;
    [SerializeField] private Color beamColor = new Color(0.35f, 0.85f, 1f);

    private float candidateSince = -1f;
    private float lastSampleTime = float.NegativeInfinity;
    private bool peaceSignHeld;
    private Vector2 aimDirection;
    private float lastAimTime = float.NegativeInfinity;
    private EnergyBeam activeBeam;

    private void OnEnable()
    {
        candidateSince = -1f;
        lastAimTime = float.NegativeInfinity;
        peaceSignHeld = false;
        if (handTracking != null)
        {
            handTracking.FingerCurlSample += OnFingerCurlSample;
            handTracking.AimSample += OnAimSample;
        }
    }

    private void OnDisable()
    {
        if (handTracking != null)
        {
            handTracking.FingerCurlSample -= OnFingerCurlSample;
            handTracking.AimSample -= OnAimSample;
        }
        candidateSince = -1f;
        peaceSignHeld = false;
        StopBeam();
    }

    private void OnAimSample(Vector2? direction)
    {
        if (!direction.HasValue) return; // Keep the last known direction while fingers move.
        var value = direction.Value;
        if (float.IsNaN(value.x) || float.IsNaN(value.y) ||
            float.IsInfinity(value.x) || float.IsInfinity(value.y) || value.sqrMagnitude < 0.0001f) return;
        float now = Time.unscaledTime;
        if (now - lastAimTime > trackingTimeout)
            aimDirection = value.normalized;
        else
        {
            float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
            float target = Mathf.Atan2(value.y, value.x) * Mathf.Rad2Deg;
            angle = Mathf.LerpAngle(angle, target, 1f - Mathf.Exp(-aimSmoothingSpeed * (now - lastAimTime)));
            aimDirection = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
        }
        lastAimTime = now;
    }

    // Only updates whether the peace sign is currently confirmed. The beam itself is driven from
    // Update() every rendered frame so it stays smooth regardless of the webcam's own frame rate.
    private void OnFingerCurlSample(HandTrackingManager.FingerCurl? curl)
    {
        float now = Time.unscaledTime;
        if (!curl.HasValue || now - lastSampleTime > trackingTimeout)
        {
            candidateSince = -1f;
            peaceSignHeld = false;
        }
        lastSampleTime = now;
        if (!curl.HasValue)
        {
            lastAimTime = float.NegativeInfinity;
            return;
        }

        var value = curl.Value;
        bool isPeaceSign = value.Index > extendedThreshold && value.Middle > extendedThreshold &&
            value.Ring < curledThreshold && value.Pinky < curledThreshold;
        if (!isPeaceSign)
        {
            candidateSince = -1f;
            peaceSignHeld = false;
            return;
        }
        if (candidateSince < 0f) candidateSince = now;
        if (now - candidateSince >= confirmationSeconds) peaceSignHeld = true;
    }

    private void Update()
    {
        float now = Time.unscaledTime;
        bool trackingFresh = now - lastSampleTime <= trackingTimeout;
        bool aimFresh = now - lastAimTime <= aimMemorySeconds;
        bool wantsBeam = peaceSignHeld && trackingFresh && aimFresh && Time.timeScale > 0f;

        if (!wantsBeam)
        {
            StopBeam();
            return;
        }

        float cost = energyDrainPerSecond * Time.deltaTime;
        if (energy == null || !energy.TrySpend(cost))
        {
            StopBeam();
            return;
        }

        if (activeBeam == null) activeBeam = CreateBeam();
        activeBeam.UpdateBeam(aimDirection);
    }

    private EnergyBeam CreateBeam()
    {
        var beamObject = new GameObject("EnergyBeam");
        var beam = beamObject.AddComponent<EnergyBeam>();
        beam.Initialize(transform, GetComponent<Collider2D>(), beamRange, beamWidth, beamColor);
        return beam;
    }

    private void StopBeam()
    {
        if (activeBeam == null) return;
        Destroy(activeBeam.gameObject);
        activeBeam = null;
    }

    private void OnDestroy()
    {
        StopBeam();
    }
}
