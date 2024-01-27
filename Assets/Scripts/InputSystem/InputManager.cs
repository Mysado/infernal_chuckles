using System;
using InputSystem;
using Sisus.Init;
using UnityEngine;

[Service]
public class InputManager : MonoBehaviour, IInputManager
{
    public float Horizontal { get; private set; }
    public float Vertical { get; private set; }

    public event Action<AttackPosition> OnRightAttack;
    public event Action<AttackPosition> OnLeftAttack;

    private InputActions input;

    private void Awake()
    {
        input = new InputActions();
        input.Player.AttackLeft.started += _ => OnLeftAttack?.Invoke(AttackPosition.Body);
        input.Player.AttackRight.started += _ => OnRightAttack?.Invoke(AttackPosition.Body);
        input.Player.AttackLeftDown.started += _ => OnLeftAttack?.Invoke(AttackPosition.Legs);
        input.Player.AttackRightDown.started += _ => OnRightAttack?.Invoke(AttackPosition.Legs);
        input.Player.AttackLeftTop.started += _ => OnLeftAttack?.Invoke(AttackPosition.Head);
        input.Player.AttackRightTop.started += _ => OnRightAttack?.Invoke(AttackPosition.Head);
        input.Player.Enable();
    }

    private void Update()
    {
        Horizontal = input.Player.Movement.ReadValue<Vector2>().x;
        Vertical = input.Player.Movement.ReadValue<Vector2>().y;
    }
}
