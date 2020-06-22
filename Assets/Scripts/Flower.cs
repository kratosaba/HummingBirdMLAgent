using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages a single flower with nectar
/// <summary>


public class Flower : MonoBehaviour
{
    [Tooltip("The color when the flower is full")]
    public Color fullFlowerColor = new Color(1f,0f,.3f);

    
    [Tooltip("The color when the flower is full")]
    public Color emptyFlowerColor = new Color(0.5f,0f,1f);

    ///<summary>
    /// The trigger collider representing the nectar
    ///<summary>
 
    [HideInInspector]
    public Collider nectarCollider;

    /// The solid collider representing the flower petals
    private Collider flowerCollider;

    /// The flower material
    private Material flowerMaterial;

    ///<summary>
    ///A vector pointing straight out of the flower
    ///<summary>
    
    public Vector3 FlowerUpVector
    {
        get
        {
            return nectarCollider.transform.up;
        }
    }


    ///<summary>
    ///The center position of the nectar collider
    ///<summary>
    
    public Vector3 FlowerCenterPosition
    {
        get
        {
            return nectarCollider.transform.position;
        }
    }
    ///<summary>
    ///The amount of nectar remaining in the flower 
    ///<summary>
    
    public float NectarAmount { get; private set;}


    
    ///<summary>
    /// Check if there is any nectar left 
    ///<summary>
    public bool HasNectar
    {
        get
        {
            return NectarAmount >0f;
        }
    }

    ///<summary>
    ///Attempts to remove nectar form the flower
    ///<summary>
    /// <param name="amount"> the amount of nectar to remove</param>
    // <returns> The actual amount succesfully remove </returns>
    
    public float Feed(float amount)
    {
        // track how much nectar was successfullly taken (cannot take more than is available)
        float nectarTaken = Mathf.Clamp(amount,0f,NectarAmount);

        // Sub the nectar
        NectarAmount -= amount;

        if(NectarAmount<=0)
        {
            // No nectar remaining
            NectarAmount=0;

            // Disable the flower and nectar colliders
            flowerCollider.gameObject.SetActive(false);
            nectarCollider.gameObject.SetActive(false);

            // Change the flower color to indicate that it is empty
            flowerMaterial.SetColor("_BaseColor",emptyFlowerColor);

        }

        // Return the amount of nectar that was taken
        return nectarTaken;

    }

    ///<summary>
    /// REsets the flower
    ///<summary>

    public void ResetFlower()
    {
        // refil the nectar
        NectarAmount = 1f;
        // Disable the flower and nectar colliders
        flowerCollider.gameObject.SetActive(true);
        nectarCollider.gameObject.SetActive(true);
        
        // Change the flower color to indicate that it is full
        flowerMaterial.SetColor("_BaseColor",fullFlowerColor);

    }

    private void Awake()
    {
        // when the Flower wakes up

        //Find the flower's mesh renderer and get the main material
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();

        //Find flower tand nectar colliders
        flowerCollider = transform.Find("FlowerCollider").GetComponent<Collider>();
        nectarCollider = transform.Find("FlowerNectarCollider").GetComponent<Collider>();

    }
}
