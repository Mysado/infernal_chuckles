using Entity;
using UnityEngine;

public class Starter : MonoBehaviour
{
   [SerializeField] private PlayerController player;
   [SerializeField] private InputManager inputManager;
   

   private void Start()
   {
       var newPlayer = Instantiate(player, new Vector3(0, 1.5f, 0), Quaternion.identity);
       newPlayer.Initialize(inputManager);
   }
}
