using System.Collections.Generic;
using Entity;
using InputSystem;
using UnityEngine;

public abstract class EntityController : MonoBehaviour
{
    [SerializeField] protected float speed;
    [SerializeField] protected string discomfortTag;
    [SerializeField] protected bool ignoreComfort;

    protected ComfortZoneController comfortZone;
    protected bool isSomeoneInConfortZone;
    private List<Collider> othersInComfortZone = new();
    protected bool initialized;

    protected virtual void Awake()
    {
        comfortZone = GetComponentInChildren<ComfortZoneController>();
        comfortZone.OnTriggerEntered += ComfortZone_OnTriggerEntered;
        comfortZone.OnTriggerExited += ComfortZone_OnTriggerExited;
    }
    
    protected virtual void Update()
    {
        if (!initialized)
            return;
        
        if(ignoreComfort || !isSomeoneInConfortZone)
            Move();
    }
    
   protected abstract void Move();
    
    private void ComfortZone_OnTriggerEntered(Collider other)
    {
        if(other.CompareTag(discomfortTag) && !othersInComfortZone.Contains(other))
            othersInComfortZone.Add(other);
        
        CheckEntityComfort();
    }

    private void ComfortZone_OnTriggerExited(Collider other)
    {
        if(othersInComfortZone.Contains(other))
            othersInComfortZone.Remove(other);
        
        CheckEntityComfort();
    }

    private void CheckEntityComfort()
    {
        isSomeoneInConfortZone = othersInComfortZone.Count > 0;
    }
}
