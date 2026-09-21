using UnityEngine;

public class GestureInputProvider : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private PlayerInputState inputState;


    [Header("Horizontal Movement")]
    [Range(0f, 1f)]
    [SerializeField]
    private float leftThreshold = 0.35f;

    [Range(0f, 1f)]
    [SerializeField]
    private float rightThreshold = 0.65f;


    [Header("Jump")]
    [SerializeField]
    private float jumpVelocityThreshold = 1.2f;

    [SerializeField]
    private float jumpCooldown = 0.5f;


    [Header("Smoothing")]
    [SerializeField]
    private float smoothingSpeed = 10f;


    [Header("Mirror")]
    [SerializeField]
    private bool mirrorX = true;
    public bool MirrorX => mirrorX;


    private Vector2 smoothedHandPosition;

    private float previousHandY;

    private bool hasPreviousFrame;

    private float lastJumpTime;


    public void ProcessHand(Vector2 normalizedHandPosition)
    {
        if (mirrorX)
        {
            normalizedHandPosition.x =
                1f - normalizedHandPosition.x;
        }


        if (!hasPreviousFrame)
        {
            smoothedHandPosition =
                normalizedHandPosition;

            previousHandY =
                normalizedHandPosition.y;

            hasPreviousFrame = true;
        }


        smoothedHandPosition =
            Vector2.Lerp(
                smoothedHandPosition,
                normalizedHandPosition,
                Time.deltaTime * smoothingSpeed
            );


        inputState.SetGestureActive(true);


        ProcessHorizontal(
            smoothedHandPosition.x
        );


        ProcessJump(
            smoothedHandPosition.y
        );


        previousHandY =
            smoothedHandPosition.y;
    }


    public void ClearHand()
    {
        inputState.SetGestureActive(false);

        hasPreviousFrame = false;
    }


    private void ProcessHorizontal(float x)
    {
        float horizontal = 0f;


        if (x < leftThreshold)
        {
            horizontal = -1f;
        }
        else if (x > rightThreshold)
        {
            horizontal = 1f;
        }


        inputState.SetGestureMoveInput(
            new Vector2(horizontal, 0f)
        );
    }


    private void ProcessJump(float currentY)
    {
        if (!hasPreviousFrame)
            return;


        float deltaY =
            previousHandY - currentY;


        float verticalVelocity =
            deltaY / Time.deltaTime;


        if (
            verticalVelocity >
            jumpVelocityThreshold
            &&
            Time.time >
            lastJumpTime + jumpCooldown
        )
        {
            inputState.PressJump();

            lastJumpTime =
                Time.time;
        }
    }
}
