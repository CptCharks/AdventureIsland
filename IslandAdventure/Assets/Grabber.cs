using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//Just a simple test grabber system
public class Grabber : MonoBehaviour
{
    [SerializeField] SpringJoint joint;

    [SerializeField] GameObject target;

    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.E))
        {
            if (joint != null)
            {
                Destroy(joint);
            }
            else if(target != null)
            {
                joint = target.AddComponent<SpringJoint>();
                joint.connectedBody = gameObject.GetComponent<Rigidbody>();
            }
        }
    }


    public void OnTriggerEnter(Collider other)
    {
        if(other.transform.tag == "Grabbable")
        {
            target = other.gameObject;
        }
    }

    public void OnTriggerExit(Collider other)
    {
        if (other.gameObject == target)
        {
            target = null;
        }
    }
}
