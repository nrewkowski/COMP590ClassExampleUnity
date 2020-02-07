using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    public Material colorfulMaterial;
    void OnCollisionEnter(Collision other){
        Collectible c=other.collider.gameObject.GetComponent<Collectible>();
        if (c){
            other.collider.gameObject.GetComponent<MeshRenderer>().material=colorfulMaterial;
        }
    }
}
