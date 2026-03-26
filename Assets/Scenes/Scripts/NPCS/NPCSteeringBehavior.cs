using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Rigidbody))]
public class NPCSteeringBehavior : MonoBehaviour
{
    public Transform playerCar;
    public float detectionRadius = 15.0f;
    public float repulsionStrength = 50.0f;
    
    private Rigidbody npcRigidbody;
    private NavMeshAgent agent;
    private SimpleNPCMovement movementScript;
    private bool isEvading = false;

    void Start()
    {
        npcRigidbody = GetComponent<Rigidbody>();
        agent = GetComponent<NavMeshAgent>();
        movementScript = GetComponent<SimpleNPCMovement>();

        // Adding gravity fixes the "flying" issue! 
        npcRigidbody.useGravity = true;
        
        // We freeze their rotation so they don't fall over while walking normally
        npcRigidbody.constraints = RigidbodyConstraints.FreezeRotation;

        // Auto-find the player's car if you haven't assigned it in the Inspector
        if (playerCar == null)
        {
            var cars = FindObjectsByType<CarController2_VR>(FindObjectsSortMode.None);
            if (cars.Length > 0)
            {
                playerCar = cars[0].transform;
            }
        }
    }

    void FixedUpdate()
    {
        if (playerCar == null) return;

        Vector3 directionToCar = transform.position - playerCar.position;
        // Ignore height calculation so they don't get pushed into the sky or floor
        directionToCar.y = 0;
        float distance = directionToCar.magnitude;

        // Reduced distance to 5 meters so it doesn't trigger across the whole map
        if (distance < 5.0f && distance > 0.1f)
        {
            if (!isEvading)
            {
                isEvading = true;
                // Turn off NavMesh navigation so Unity's physics system can take over
                if (agent != null) agent.enabled = false;
                if (movementScript != null) movementScript.enabled = false;
                
                // Allow them to tumble!
                npcRigidbody.constraints = RigidbodyConstraints.None;
            }

            // Clamp distance to 1 so the force doesn't mathematically explode to infinity when very close
            float safeDist = Mathf.Max(distance, 1.0f);
            float forceMagnitude = repulsionStrength / (safeDist * safeDist);
            
            Vector3 repulsionVector = directionToCar.normalized * forceMagnitude;

            // Notice we removed the upward vector here. This just pushes them aside linearly!
            npcRigidbody.AddForce(repulsionVector, ForceMode.Acceleration);
        }
    }

    // Handles physical collision if the car directly rams them
    private void OnCollisionEnter(Collision collision)
    {
        // Only go flying if the object that hit them was actually the Car!
        // This stops them from bouncing if they just fall 2 feet to the ground when spawning.
        if (collision.gameObject.GetComponentInParent<CarController2_VR>() != null)
        {
            if (!isEvading)
            {
                isEvading = true;
                if (agent != null) agent.enabled = false;
                if (movementScript != null) movementScript.enabled = false;
                npcRigidbody.constraints = RigidbodyConstraints.None;
            }
            
            // THIS is where the satisfying flying upward explosion happens!
            npcRigidbody.AddForce(collision.impulse * 3f + Vector3.up * 15f, ForceMode.Impulse);
        }
    }
}
