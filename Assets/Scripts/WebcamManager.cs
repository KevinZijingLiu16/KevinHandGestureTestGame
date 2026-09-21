using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class WebcamManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField]
    private RawImage preview;

    [Header("Camera Settings")]
    [SerializeField]
    private int requestedWidth = 640;

    [SerializeField]
    private int requestedHeight = 480;

    [SerializeField]
    private int requestedFPS = 30;

    public WebCamTexture WebcamTexture { get; private set; }

    private IEnumerator Start()
    {
        if (WebCamTexture.devices.Length == 0)
        {
            Debug.LogError("No webcam detected.");
            yield break;
        }

        foreach (WebCamDevice device in WebCamTexture.devices)
        {
            Debug.Log($"Webcam: {device.name}");
        }

        WebCamDevice selectedDevice =
            WebCamTexture.devices[0];

        WebcamTexture = new WebCamTexture(
            selectedDevice.name,
            requestedWidth,
            requestedHeight,
            requestedFPS
        );

        WebcamTexture.Play();

        yield return new WaitUntil(
            () => WebcamTexture.width > 16
        );

        if (preview != null)
        {
            preview.texture = WebcamTexture;
        }

        Debug.Log(
            $"Webcam started: " +
            $"{WebcamTexture.width} x " +
            $"{WebcamTexture.height}"
        );
    }

    private void OnDestroy()
    {
        if (WebcamTexture != null &&
            WebcamTexture.isPlaying)
        {
            WebcamTexture.Stop();
        }
    }
}