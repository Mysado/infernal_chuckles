using System.Collections.Generic;
using Entity;
using InputSystem;
using UnityEngine;

public abstract class EntityController : MonoBehaviour
{
    [SerializeField] protected float speed;
    
    protected bool initialized;
    
    protected virtual void Update()
    {
        if (!initialized)
            return;
        
        Move();
    }
    
   protected abstract void Move();
   
}
