using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;

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
    private Rigidbody rigidBody;

    // The flower area that the agent is in
    private FlowerArea flowerArea;

    // The nearest flower to the agent
    private Flower nearestFlower;

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
        rigidBody = GetComponent<Rigidbody>();
        flowerArea = GetComponentInParent<FlowerArea>();

        // if not training mode, max step, play forever
        if(!trainingMode) MaxStep = 0;

    }

    public override void OnEpisodeBegin()
    {
        if (trainingMode)
        {
            // Only reset flowers in training when there is one agent per area
            flowerArea.ResetFlowers();
        }

        // Reset nectar obtained
        NectarObtained = 0f;

        // Zero out velocities so that movement stops before a new episode begins
        GetComponent<Rigidbody>().velocity = Vector3.zero;
        GetComponent<Rigidbody>().angularVelocity = Vector3.zero;

        // Default to spawning in front of a flower
        bool inFrontOfFlower = true;
        if (trainingMode)
        {
            // Spawn in front of flower 50% of the time during training
            inFrontOfFlower = UnityEngine.Random.value > .5f;
        }

        // Move the agent to a new random position
        MoveToSafeRandomPosition(inFrontOfFlower);

        // Recalculate the nearest flower now that the agent has moved
        UpdateNearestFlower();
    }
    /// <summary>
    /// Called when an action is received from either the player input or the NN.
    ///  vectorAction[i] represents:
    /// Index0: move vector x (+1= right, -1= left , 0= no movement)
    /// Index1: move vector y (+1= up, -1= down , 0= no movement)
    /// Index2: move vector z (+1= forward, -1= backwards , 0= no movement)
    /// Index3: pitch angle (+1= pitch up, -1= pitch down , 0= no movement)
    /// Index4: yawn angle (+1= yaw right, -1= yaw left , 0= no movement)
    /// <summary>
    /// <param name=vectorAction> The action to take</param>

    public override void OnActionReceived(float[] vectorAction)
    {
        // Don't take actions if frozen
        if(frozen) return;

        // Calculate movement vector
        Vector3 move = new Vector3(vectorAction[0],vectorAction[1],vectorAction[2]);

        // Add force in the direction of the move vector
        rigidBody.AddForce(move* moveForce);

        // Get current rotation
        Vector3 rotationVector = transform.rotation.eulerAngles;

        //Calculate pitch and yaw rotation
        float pitchChange = vectorAction[3];
        float yawChange = vectorAction[4];

        // Calculate smooth rotation changes
        SmoothPitchChange = Mathf.MoveTowards(SmoothPitchChange, pitchChange, 2f* Time.fixedDeltaTime);
        SmoothYawChange = Mathf.MoveTowards(SmoothYawChange, yawChange, 2f * Time.fixedDeltaTime);

        // Calculate new pitch and yaw based on smooth rotation
        // Clampo pitch to avoid flipping upside down
        float pitch = rotationVector.x + SmoothPitchChange * Time.fixedDeltaTime * pitchSpeed;
        if ( pitch > 180f) pitch -= 360f;
        pitch = Mathf.Clamp(pitch, -MaxPitchAngle, MaxPitchAngle);

        float yaw = rotationVector.y + SmoothYawChange * Time.fixedDeltaTime * yawSpeed;

        // Apply new rotation
        transform.rotation = Quaternion.Euler(pitch,yaw,0f);
    }

    /// <summary>
    /// Collect vector observations from the enviroment 
    /// <summary>
    /// <param name=sensor> The vector sensor</param>
    public override void CollectObservations(VectorSensor sensor)
    {
        if( nearestFlower == null)
        {
            sensor.AddObservation(new float[10]);
            return;
        }

        // Observe the agents local roation (4 observations)
        sensor.AddObservation(transform.localRotation.normalized);


        // Get a vector form the beak tip to the nearest flower 
        Vector3 toFlower = nearestFlower.FlowerCenterPosition - beakTip.position;

        //Observe a normalized vector pointing to the nearest flower (3 observations)
        sensor.AddObservation(toFlower.normalized);

        // Observe a dot product that indicates whether the beak tip is in front the flower (1 observation)
        // (+1 means that the beak tip in flront of the flower, -1 means firectly behind)
        sensor.AddObservation(Vector3.Dot(toFlower.normalized, -nearestFlower.FlowerUpVector.normalized));

        // Observe a dot product that indicates whether the beak tip is in towards the flower (1 observation)
        // (+1 means that the beak tip in dirrectly to the flower, -1 means directly away)
        sensor.AddObservation(Vector3.Dot(beakTip.forward.normalized, -nearestFlower.FlowerUpVector.normalized));

        // Observe the relative distance from the beak tip to the flower (1 observation)
        sensor.AddObservation(toFlower.magnitude / FlowerArea.AreaDiameter);

    }

    /// <summary>
    /// When the behavior type is set to Heuristic only on the agents Behavior Parameters
    /// this function will be called. It's return type values will be fed into 
    /// <see cref="OnActionReceived(float[])"> instead of using the NN
    /// <summary>
    /// <param name=actionOut> The output action vector</param>
   public override void Heuristic(float[] actionsOut)
    {
        // Create placeholders for all movement/turning
        Vector3 forward = Vector3.zero;
        Vector3 left = Vector3.zero;
        Vector3 up = Vector3.zero;
        float pitch = 0f;
        float yaw = 0f;

        // Convert keyboard inputs to movement and turning
        // All values should be between -1 and +1

        // Forward/backward
        if (Input.GetKey(KeyCode.W)) forward = transform.forward;
        else if (Input.GetKey(KeyCode.S)) forward = -transform.forward;

        // Left/right
        if (Input.GetKey(KeyCode.A)) left = -transform.right;
        else if (Input.GetKey(KeyCode.D)) left = transform.right;

        // Up/down
        if (Input.GetKey(KeyCode.E)) up = transform.up;
        else if (Input.GetKey(KeyCode.C)) up = -transform.up;

        // Pitch up/down
        if (Input.GetKey(KeyCode.UpArrow)) pitch = 1f;
        else if (Input.GetKey(KeyCode.DownArrow)) pitch = -1f;

        // Turn left/right
        if (Input.GetKey(KeyCode.LeftArrow)) yaw = -1f;
        else if (Input.GetKey(KeyCode.RightArrow)) yaw = 1f;

        // Combine the movement vectors and normalize
        Vector3 combined = (forward + left + up).normalized;

        // Add the 3 movement values, pitch, and yaw to the actionsOut array
        actionsOut[0] = combined.x;
        actionsOut[1] = combined.y;
        actionsOut[2] = combined.z;
        actionsOut[3] = pitch;
        actionsOut[4] = yaw;
    }

    /// <summary> 
    /// Prevent agent from moving and takuing actions
    /// <summary> 

    public void FreezeAgent()
    {
        Debug.Assert(trainingMode == false, "Freeze/Unfreeze not supported in trianing");
        frozen = true;
        rigidBody.Sleep();
    }
    /// <summary> 
    /// Resume agent movement and actions
    /// <summary> 

      public void UnFreezeAgent()
    {
        Debug.Assert(trainingMode == false, "Freeze/Unfreeze not supported in trianing");
        frozen = false;
        rigidBody.WakeUp();
    }

    /// <summary>
    /// Move the agent to safe random position (i.e. doesn't collide with anything)
    /// If in forn of flower, also point the beak at the flower
    /// <summary>
    /// <param name=inFrontOffFlower>Whether to choose a spot in front a flower</param>
    private void MoveToSafeRandomPosition(bool inFrontOfFlower)
    {
        bool safePositionFound = false;
        int attemptsRemaining = 100; // Prevent an infinite loop
        Vector3 potentialPosition = Vector3.zero;
        Quaternion potentialRotation = new Quaternion();

        // Loop until a safe position is found or we run out of attempts
        while (!safePositionFound && attemptsRemaining > 0)
        {
            attemptsRemaining--;
            if (inFrontOfFlower)
            {
                // Pick a random flower
                Flower randomFlower = flowerArea.Flowers[UnityEngine.Random.Range(0, flowerArea.Flowers.Count)];

                // Position 10 to 20 cm in front of the flower
                float distanceFromFlower = UnityEngine.Random.Range(.1f, .2f);
                potentialPosition = randomFlower.transform.position + randomFlower.FlowerUpVector * distanceFromFlower;

                // Point beak at flower (bird's head is center of transform)
                Vector3 toFlower = randomFlower.FlowerCenterPosition - potentialPosition;
                potentialRotation = Quaternion.LookRotation(toFlower, Vector3.up);
            }
            else
            {
                // Pick a random height from the ground
                float height = UnityEngine.Random.Range(1.2f, 2.5f);

                // Pick a random radius from the center of the area
                float radius = UnityEngine.Random.Range(2f, 7f);

                // Pick a random direction rotated around the y axis
                Quaternion direction = Quaternion.Euler(0f, UnityEngine.Random.Range(-180f, 180f), 0f);

                // Combine height, radius, and direction to pick a potential position
                potentialPosition = flowerArea.transform.position + Vector3.up * height + direction * Vector3.forward * radius;

                // Choose and set random starting pitch and yaw
                float pitch = UnityEngine.Random.Range(-60f, 60f);
                float yaw = UnityEngine.Random.Range(-180f, 180f);
                potentialRotation = Quaternion.Euler(pitch, yaw, 0f);
            }

            // Check to see if the agent will collide with anything
            Collider[] colliders = Physics.OverlapSphere(potentialPosition, 0.05f);

            // Safe position has been found if no colliders are overlapped
            safePositionFound = colliders.Length == 0;
        }

        Debug.Assert(safePositionFound, "Could not find a safe position to spawn");

        // Set the position and rotation
        transform.position = potentialPosition;
        transform.rotation = potentialRotation;
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

    /// <summary>
    /// Called when the agent collider enters a trigger collider
    /// <summary>
    private void OnTriggerEnter( Collider other)
    {
        TriggerEnterOrStay(other);
    }

    /// <summary>
    /// Called when the agent collider stays a trigger collider
    /// <summary>
    private void OnTriggerStay( Collider other)
    {
        TriggerEnterOrStay(other);
    }

    /// <summary>
    ///Handles when the agent's collider enters or stays in the trigger collider
    /// <summary>
    
    private void TriggerEnterOrStay(Collider collider)
    {
        // Check if agent is colliding with nectar
        if (collider.CompareTag("nectar"))
        {
            Vector3 closestPointBeakTip = collider.ClosestPoint(beakTip.position);

            // Check if the closest collision point is close to the beak tip
            // Note: a collision with anything but the beak tip should not count
            if(Vector3.Distance(beakTip.position, closestPointBeakTip)< BeakTipRadius)
            {
                // Look up the flower for this nectar collider
                Flower flower = flowerArea.GetFlowerFromNectar(collider);

                //Attempt to take 0.01 nectar
                // Note this is per fixed timestep, meaning it happens every 0.02  or 50x per second
                float nectarReceived = flower.Feed(.01f);

                // Keep track of nectar obtained
                NectarObtained += nectarReceived;

                if (trainingMode)
                {
                    // Calculate reward for getting nectar
                    float bonus = .02f * Mathf.Clamp01(Vector3.Dot(transform.forward.normalized,-nearestFlower.FlowerUpVector.normalized));
                    AddReward(.01f+bonus);
                }

                // If flower is empty update the nearest flower
                if( !flower.HasNectar)
                {
                    UpdateNearestFlower();
                }
            }

        }
    }

    /// <summary>
    ///Called when the agent collides with something solid
    /// <summary>
    /// <param name="collision"> the collision info</param>

    private void OnCollisionEnter(Collision collision)
    {
        if( trainingMode && collision.collider.CompareTag("boundary"))
        {
            // Collided with the area boundary, give a negative reward
            AddReward(-.5f);
        }
    }

    /// <summary>
    ///eVERy frame
    /// <summary>
  
    private void Update()
    {
        // Draw a line form the beak tip to the nearest flower
        if(nearestFlower !=null)
        {
            Debug.DrawLine(beakTip.position, nearestFlower.FlowerCenterPosition, Color.green);
        }
 
    }

    private void FixedUpdated()
    {   
        // Avoids scenario where nearest flower nexar is stolen by opponent and not updated
        if(nearestFlower !=null && !nearestFlower.HasNectar)
        {
            UpdateNearestFlower();
        }
    }
}
