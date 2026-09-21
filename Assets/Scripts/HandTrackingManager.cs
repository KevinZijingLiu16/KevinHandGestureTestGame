using System.Collections;
using UnityEngine;

using Stopwatch = System.Diagnostics.Stopwatch;

using Mediapipe;
using Mediapipe.Tasks.Core;
using Mediapipe.Tasks.Vision.Core;
using Mediapipe.Tasks.Vision.HandLandmarker;

public class HandTrackingManager : MonoBehaviour
{
    /// <summary>Per-finger tip/PIP-to-wrist distance ratio; higher means straighter (more extended).</summary>
    public readonly struct FingerCurl
    {
        public readonly float Index;
        public readonly float Middle;
        public readonly float Ring;
        public readonly float Pinky;

        public FingerCurl(float index, float middle, float ring, float pinky)
        {
            Index = index;
            Middle = middle;
            Ring = ring;
            Pinky = pinky;
        }
    }

    // Four-finger curl score: lower values indicate a closed fist; null means tracking lost.
    public event System.Action<float?> FistSample;
    // Raw per-finger curl ratios sampled alongside FistSample; null means tracking lost.
    public event System.Action<FingerCurl?> FingerCurlSample;
    // Emitted before FistSample; null when the middle finger cannot provide a reliable aim.
    public event System.Action<Vector2?> AimSample;
    [Header("References")]
    [SerializeField]
    private WebcamManager webcamManager;

    [SerializeField]
    private TextAsset handLandmarkerModel;
    [SerializeField]
    private GestureInputProvider gestureInputProvider;


    [Header("Detection Settings")]
    [SerializeField]
    private int numHands = 1;

    [SerializeField]
    [Range(0f, 1f)]
    private float minHandDetectionConfidence = 0.5f;

    [SerializeField]
    [Range(0f, 1f)]
    private float minHandPresenceConfidence = 0.5f;

    [SerializeField]
    [Range(0f, 1f)]
    private float minTrackingConfidence = 0.5f;


    [Header("Debug")]
    [SerializeField]
    private bool printHandPosition = true;


    private HandLandmarker handLandmarker;

    private Stopwatch stopwatch;

    private Mediapipe.Unity.Experimental.TextureFrame textureFrame;


    private IEnumerator Start()
    {
        Debug.Log("Waiting for webcam...");

        // 等待 WebcamManager 初始化摄像头
        yield return new WaitUntil(
            () =>
                webcamManager != null &&
                webcamManager.WebcamTexture != null &&
                webcamManager.WebcamTexture.width > 16
        );


        Debug.Log(
            $"Webcam ready: " +
            $"{webcamManager.WebcamTexture.width} x " +
            $"{webcamManager.WebcamTexture.height}"
        );


        // 检查模型
        if (handLandmarkerModel == null)
        {
            Debug.LogError(
                "Hand Landmarker model is missing!"
            );

            yield break;
        }


        Debug.Log(
            $"Model loaded: " +
            $"{handLandmarkerModel.name}"
        );


        // MediaPipe 配置
        var baseOptions =
            new BaseOptions(
                BaseOptions.Delegate.CPU,
                modelAssetBuffer:
                    handLandmarkerModel.bytes
            );


        var options =
            new HandLandmarkerOptions(
                baseOptions: baseOptions,
                runningMode:
                    RunningMode.VIDEO,
                numHands:
                    numHands,
                minHandDetectionConfidence:
                    minHandDetectionConfidence,
                minHandPresenceConfidence:
                    minHandPresenceConfidence,
                minTrackingConfidence:
                    minTrackingConfidence
            );


        // 创建 HandLandmarker
        handLandmarker =
            HandLandmarker.CreateFromOptions(
                options
            );


        if (handLandmarker == null)
        {
            Debug.LogError(
                "Failed to create HandLandmarker."
            );

            yield break;
        }


        // 时间戳计时器
        stopwatch =
            new Stopwatch();

        stopwatch.Start();


        var webcam =
            webcamManager.WebcamTexture;


        // 创建 MediaPipe TextureFrame
        textureFrame =
            new Mediapipe.Unity.Experimental.TextureFrame(
                webcam.width,
                webcam.height,
                TextureFormat.RGBA32
            );


        Debug.Log(
            "Hand Landmarker initialized successfully."
        );
    }


    private void Update()
    {
        if (handLandmarker == null)
            return;

        if (webcamManager == null)
            return;

        if (webcamManager.WebcamTexture == null)
            return;


        var webcam =
            webcamManager.WebcamTexture;


        // 摄像头没有产生新帧就不处理
        if (!webcam.didUpdateThisFrame)
        {
            return;
        }


        // 将 WebcamTexture 拷贝到 MediaPipe TextureFrame
        textureFrame.ReadTextureOnCPU(
            webcam,
            flipHorizontally: false,
            flipVertically: true
        );


        // 构建 MediaPipe Image
        using var image =
            textureFrame.BuildCPUImage();


        // VIDEO 模式要求时间戳严格递增
        long timestamp =
            stopwatch.ElapsedMilliseconds;


        // 执行手部识别
        HandLandmarkerResult result =
            handLandmarker.DetectForVideo(
                image,
                timestamp
            );


        ProcessResult(result);
    }


    private void ProcessResult(
        HandLandmarkerResult result
    )
    {
        // 没检测到手
        if (
    result.handLandmarks == null ||
    result.handLandmarks.Count == 0
)
{
    gestureInputProvider?.ClearHand();
    FistSample?.Invoke(null);
    FingerCurlSample?.Invoke(null);

    return;
}


        // 第一只手
        var landmarks =
            result.handLandmarks[0].landmarks;


        if (
            landmarks == null ||
            landmarks.Count < 21
        )
        {
            gestureInputProvider?.ClearHand();
            FistSample?.Invoke(null);
            FingerCurlSample?.Invoke(null);
            return;
        }

        // Compare fingertip/wrist distance to PIP/wrist distance in aspect-corrected image space.
        // All four fingers must curl; thumb position is deliberately unrestricted.
        float aspect = webcamManager.WebcamTexture.width / (float)webcamManager.WebcamTexture.height;
        var wristPosition = new Vector2(landmarks[0].x * aspect, landmarks[0].y);
        float curlScore = 0f;
        // Ratios land in tip order (8, 12, 16, 20) = index, middle, ring, pinky.
        var fingerRatios = new float[4];
        int fingerIndex = 0;
        for (int tip = 8; tip <= 20; tip += 4)
        {
            var tipPosition = new Vector2(landmarks[tip].x * aspect, landmarks[tip].y);
            var pipPosition = new Vector2(landmarks[tip - 2].x * aspect, landmarks[tip - 2].y);
            float pipDistance = Vector2.Distance(pipPosition, wristPosition);
            if (pipDistance < 0.0001f)
            {
                FistSample?.Invoke(null);
                FingerCurlSample?.Invoke(null);
                return;
            }
            float ratio = Vector2.Distance(tipPosition, wristPosition) / pipDistance;
            fingerRatios[fingerIndex++] = ratio;
            curlScore = Mathf.Max(curlScore, ratio);
        }
        FingerCurlSample?.Invoke(new FingerCurl(fingerRatios[0], fingerRatios[1], fingerRatios[2], fingerRatios[3]));
        var middleBase = new Vector2(landmarks[9].x * aspect, landmarks[9].y);
        var middlePip = new Vector2(landmarks[10].x * aspect, landmarks[10].y);
        var middleTip = new Vector2(landmarks[12].x * aspect, landmarks[12].y);
        var middleDirection = middleTip - middleBase;
        bool middleExtended = Vector2.Distance(middleTip, wristPosition) >
            Vector2.Distance(middlePip, wristPosition) * 1.15f;
        if (middleExtended && middleDirection.sqrMagnitude > 0.0001f)
        {
            // Image Y points down; game Y points up. Match horizontal movement mirroring.
            middleDirection.y = -middleDirection.y;
            if (gestureInputProvider != null && gestureInputProvider.MirrorX)
                middleDirection.x = -middleDirection.x;
            AimSample?.Invoke(middleDirection.normalized);
        }
        else
        {
            AimSample?.Invoke(null);
        }
        FistSample?.Invoke(curlScore);


        // Landmark 0 = Wrist
        var wrist =
            landmarks[0];


        // Landmark 9 = Middle Finger MCP
        var middleMcp =
            landmarks[9];


        // 用 wrist 与 middle MCP 中点
        // 暂时近似作为掌心
       float palmX =
    (wrist.x + middleMcp.x) * 0.5f;

        float palmY =
    (wrist.y + middleMcp.y) * 0.5f;


        gestureInputProvider?.ProcessHand(
    new Vector2(palmX, palmY)
);


        if (printHandPosition)
        {
            Debug.Log(
                $"Hand detected | " +
                $"Palm X: {palmX:F3}, " +
                $"Palm Y: {palmY:F3}"
            );
        }
    }


    private void OnDestroy()
    {
        // TextureFrame
        if (textureFrame != null)
        {
            textureFrame.Dispose();
            textureFrame = null;
        }


        // MediaPipe Task
        if (handLandmarker != null)
        {
            handLandmarker.Close();
            handLandmarker = null;
        }


        // Stopwatch
        if (stopwatch != null)
        {
            stopwatch.Stop();
            stopwatch = null;
        }
    }
}
