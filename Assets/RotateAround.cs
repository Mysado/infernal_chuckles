using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotateAround : MonoBehaviour
{
    [SerializeField] private float speed;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        var rotationToAdd = new Vector3(0, Time.deltaTime * speed, 0);
        transform.Rotate(rotationToAdd);
    }
}
