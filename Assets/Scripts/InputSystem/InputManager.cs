using System;
using InputSystem;
using Sisus.Init;
using UnityEngine;

[Service]
public class InputManager : MonoBehaviour, IInputManager
{
    private InputActions input;
    public InputActions Input => input; // whyyyyy Q _ Q 

    public float Horizontal { get; private set; }
    public float Vertical { get; private set; }

    public event Action OnRightAttack;
    public event Action OnLeftAttack;

    private void Awake()
    {
        input = new InputActions();
        input.Player.AttackLeft.started += _ => OnLeftAttack?.Invoke();
        input.Player.AttackRight.started += _ => OnRightAttack?.Invoke();
        input.Player.Enable();
    }

    private void Update()
    {
        Horizontal = input.Player.Movement.ReadValue<Vector2>().x;
        Vertical = input.Player.Movement.ReadValue<Vector2>().y;
    }
}
