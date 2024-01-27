using Sisus.Init;
using UnityEngine;

[Service]
public class InputManager : MonoBehaviour
{
    private InputActions input;

    public float Horizontal => horizontal;
    public InputActions Input => input;

    private float horizontal;

    private void Awake()
    {
        input = new InputActions();
        input.Player.Enable();
    }

    private void Update()
    {
        horizontal = input.Player.Movement.ReadValue<Vector2>().x;
    }
}
