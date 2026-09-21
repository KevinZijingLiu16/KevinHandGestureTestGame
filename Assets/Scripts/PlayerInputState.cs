using UnityEngine;

public class PlayerInputState : MonoBehaviour
{
    private Vector2 keyboardMoveInput;
    private Vector2 gestureMoveInput;

    private bool gestureActive;

    private bool jumpPressed;

    public Vector2 MoveInput
    {
        get
        {
            if (gestureActive)
                return gestureMoveInput;

            return keyboardMoveInput;
        }
    }

    public void SetKeyboardMoveInput(Vector2 input)
    {
        keyboardMoveInput = input;
    }

    public void SetGestureMoveInput(Vector2 input)
    {
        gestureMoveInput = input;
    }

    public void SetGestureActive(bool active)
    {
        gestureActive = active;

        if (!active)
        {
            gestureMoveInput = Vector2.zero;
        }
    }

    public void PressJump()
    {
        jumpPressed = true;
    }

    public bool ConsumeJump()
    {
        if (!jumpPressed)
            return false;

        jumpPressed = false;
        return true;
    }
}