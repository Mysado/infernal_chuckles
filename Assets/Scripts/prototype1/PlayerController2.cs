using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting.Antlr3.Runtime.Tree;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController2 : MonoBehaviour
{
    [SerializeField] private int maxHealth;
    [SerializeField] private GameObject spear;
    public InputActions input;
    private int currentHealth;
    void Start()
    {
        currentHealth = maxHealth;
        input = new InputActions();
        input.Player.Enable();
        input.Player.AttackLeft.performed += crt => AttackLeft();
        input.Player.AttackRight.performed += crt => AttackRight();
    }

    void Update()
    {

    }

    private void AttackLeft()
    {
        Vector3 targetDirection = (transform.position - transform.forward) - transform.position;
        var newDirection = Vector3.RotateTowards(-transform.forward, targetDirection, 999, 0.0f);
        spear.transform.rotation = Quaternion.LookRotation(newDirection);
        Debug.Log("left");
    }
    private void AttackRight()
    {
        Vector3 targetDirection = (transform.position + transform.forward) - transform.position;
        var newDirection = Vector3.RotateTowards(transform.forward, targetDirection, 999, 0.0f);
        spear.transform.rotation = Quaternion.LookRotation(newDirection);
        Debug.Log("right");
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            TakeDamage();
            Destroy(other.gameObject);
            
        }
        
    }

    private void TakeDamage()
    {
        currentHealth--;
        if (currentHealth <= 0)
            Time.timeScale = 0;
    }
}
