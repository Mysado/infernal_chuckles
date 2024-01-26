using Sisus.Init;

public class PlayerController : MonoBehaviour<InputManager>
{
    private InputManager inputManager;
    
    protected override void Init(InputManager inputManager)
    {
        this.inputManager = inputManager;
    }
    
}
