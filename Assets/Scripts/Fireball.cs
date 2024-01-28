using Entity;
using Score;
using Sisus.Init;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.InputSystem.HID;

public class Fireball : MonoBehaviour<ComboController, ScoreController>
{
    public int speed;
    private ComboController comboController;
    private ScoreController scoreController;

    protected override void Init( ComboController comboController, ScoreController scoreController)
    {
        this.comboController = comboController;
        this.scoreController = scoreController;
    }

    private void Update()
    {
        transform.position += transform.right * speed * Time.deltaTime;

    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            other.GetComponent<EnemyController>().TakeDamage(AttackPosition.Body, true);
            comboController.IncreaseComboCounter();
            scoreController.AddScorePoints(1);
        }
    }
    private void OnBecameInvisible()
    {
        Destroy(gameObject);
    }
}
