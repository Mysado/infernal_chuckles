using System.Collections.Generic;
using Entity;
using InputSystem;
using Sirenix.OdinInspector;
using UnityEngine;

public abstract class EntityController : SerializedMonoBehaviour
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
