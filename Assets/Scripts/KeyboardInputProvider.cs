using UnityEngine;
using UnityEngine.InputSystem;

public class KeyboardInputProvider : MonoBehaviour
{
    [SerializeField]
    private PlayerInputState inputState;

    public void OnMove(InputAction.CallbackContext context)
    {
        Vector2 move = context.ReadValue<Vector2>();

        inputState.SetKeyboardMoveInput(move);
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            inputState.PressJump();
        }
    }
}