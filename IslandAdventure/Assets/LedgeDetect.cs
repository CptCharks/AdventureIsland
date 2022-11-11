using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LedgeDetect : MonoBehaviour
{
    [SerializeField] MainPlayerController player;

    private void Start()
    {
        player = GetComponentInParent<MainPlayerController>();
    }

    public void OnTriggerEnter(Collider other)
    {
        if(other.tag == "Ledge")
            player.Jump();
    }

}
