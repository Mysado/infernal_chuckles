namespace InputSystem
{
    using System;

    public interface IInputManager
    {
        public float Horizontal { get; }
        public float Vertical { get; }
        public event Action OnRightAttack;
        public event Action OnLeftAttack;
    }
}