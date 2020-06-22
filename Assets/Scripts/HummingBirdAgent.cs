using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;

/// <summary>
/// A Humingbird Machine Learning Agent
/// <summary>


public class HummingBirdAgent : Agent
{
    [Tooltip("Force to apply when moving")]
    public float moveForce = 2f;

    [Tooltip("Speed to pitch up or down")]
    public float pitchSpeed = 100f;
    
    [Tooltip("Speed to yaw around the up axis")]
    public float yawSpeed = 100f;

    [Tooltip("Transform at the tip of the beak")]
    public Transform beakTip;

    [Tooltip("The agent camera")]
    public Camera agentCamera;

    [Tooltip("Where is it training mode or gameplay mode")]
    public bool trainingMode;

    // the rigid body of the agent
    private RigidBody rigidBody;

    // The flower area that the agent is in
    private FlowerArea flowerArea;

    // The nearest flower to the agent
    private FlowerArea nearestFlower;

    //Alows for smoother pitch changes
    private float SmoothPitchChange = 0f;

    
    //Alows for smoother Yaw changes
    private float SmoothYawChange = 0f;

    // Maximum angle that the bird can pitch up or down
    private const float MaxPitchAngle = 80f;

    // Maximum distance from the beak tip to accept nectar collision
    private const float BeakTipRadius = 0.008f;

    // Whether the agent is frozen (intentionally not flying)
    private bool frozen = false;
    
    /// <summary>
    ///  Amount of nectar the agent has obtained this episode
    /// <summary>
    public float NectarObtained {get; private set;}

    /// <summary>
    /// Initialize the agent
    /// <summary>
    public override void Initialize()
    {
        rigidBody = GetComponent<RigidBody>();
        flowerArea = GetComponentInParent<flowerArea>();

        // if not training mode, max step, play forever
        if(!trainingMode) MaxStep = 0;

    }

    public override void OnEpisodeBegin()
    {
        if(trainingMode)
        {
            // Only reset flowes in training when there is one agent per area
            flowerArea.ResetFlower();
        }

        // Reset nectar obtained
        NectarObtained = 0f;

        // Zero out velocities so that movement stops before a new episde begins
        rigidBody.velocity = Vector3.zero;
        rigidBody.angularvelocity = Vector3.zero;

        // Default to spawining in fornt of a flower
        bool inFrontOffFlower = true;
        if(trainingMode)
        {
            //Spawn in front of the flower 50% of the time training
            inFrontOffFlower = UnityEngine.Random.value > .5f;
        }

        // Move the agent to a new random position
        MoveToSafeRandomPosition(inFrontOffFlower);

        // Recalculate the nearest flower now that  the agent has moved
        UpdateNearestFlower();
    }

    /// <summary>
    /// Move the agent to safe random position (i.e. doesn't collide with anything)
    /// If in forn of flower, also point the beak at the flower
    /// <summary>
    /// <param name=inFrontOffFlower>Whether to choose a spot in front a flower</param>
    private void MoveToSafeRandomPosition(bool inFrontOffFlower)
    {
        bool safePositionFound = false;
        int attemptsRemaining = 100; // prevents infinite loop

        Vector3 potentialPosition = Vector3.zero;
        Quaterninon potetialRotation = new Quaterninon();
        
        // Loop until a safe positon is found or we run out of attempts
        while(!safePositionFound && attemptsRemaining > 0)
        {
            attemptsRemaining--;
            if(inFrontOffFlower)
            {
                // Pick a random flower
                FlowerArea randomFlower = flowerArea.Flowers[UnityEngine.Random.Range(0, flowerArea.Flowers.Count)];

                // Position 10 to 20 cm in front of the flower
                float distanceFromFlower = UnityEngine.Random.Range(0.1f,0.2f);
                potentialPosition = randomFlower.transform.position + randomFlower.FlowerUpVector * distanceFromFlower;

                // Point beak at flower (bird's head is center of transform)
                Vector3 toFlower = randomFlower.FlowersCenterPosition - potentialPosition;
                potentialRotation = Quaterninon.LookRotation(toFlower, Vector3.up);
            }
            else
            {
                // Pick random heigth form the ground
                float heigth = UnityEngince.Random.Range(1.2f,2.5f);

                // Pick a random radius from the center pf the area
                float radius = UnityEngine.Random.Range(2f,7f);

                // Pick a random direction rotated around the y axis
                Quaterninon direction = Quaterninon.Euler(0f,Unity.Random.Range(-100f,100f),0f);

                // Combine this 3 to pick a potetial position
                potentialPosition = flowerArea.transform.position + Vector3.up*heigth + direction *  Vector3.forward *radius;

                // Choose and set random pitch and yaw
                float pitch = UnityEngine.Random.Range(-60f,60f);
                float yaw = UnityEngine.Random.Range(-180f,180f);
                potetialRotation = Quaterninon.Euler(pitch,yaw,0f);
            }

            // Check to see if the agent the agent will collide with anything
            Collider[] colliders = Physics.OverlapSphere(potentialPosition,0.05f);

            // Safe position has been found if no colliders are overlap
            safePositionFound = colliders.Length == 0;

        }

        Debug.Assert(safePositionFound, "Couldn't find a safe position to spawn");

        // Set position and Rotation
        transform.position = potentialPosition;
        transform.rotation = potetialRotation;
    }
    
    /// <summary>
    /// Update the nearest flower to the agent
    /// <summary>
    private void UpdateNearestFlower()
    {
        foreach (Flower flower in flowerArea.Flowers)
        {
            if(nearestFlower==null && flower.HasNectar)
            {   
                // No current nearest flower and this flower has nectar, so set to this flower 
                nearestFlower = flower;
            }
            else if ( flower.HasNectar)
            {
                // calculate distance to this flower and distance to teh current neartest flower
                float distanceToFlower = Vector3.Distance(flower.transform.position,beakTip.position);
                float distanceToCurrentNearestFlower = Vector3.Distance(nearestFlower.transform.position,beakTip.position);

                // If current nearest flower is empty OR this flower is closer, Update the nearest flower
                if(!nearestFlower.HasNectar || distanceToFlower < distanceToCurrentNearestFlower)
                {
                    nearestFlower = flower;
                }
            }
        }
    }
}
