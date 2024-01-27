using InputSystem;
using UnityEngine;

public class PlayerController : EntityController
{
    private IInputManager inputManager;

    public void Initialize(IInputManager inputManager)
    {
        this.inputManager = inputManager;
        initialized = true;
    }
    
    protected override void Move()
    {
        var position = transform.position;
        transform.position = new Vector3(position.x + inputManager.Horizontal * speed * Time.deltaTime, position.y, position.z);
    }
}
