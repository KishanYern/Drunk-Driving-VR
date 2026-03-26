using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class SimpleNPCMovement : MonoBehaviour
{
    [Header("Wander Settings")]
    [Tooltip("How far the NPC can wander from its current position in one trip.")]
    public float wanderRadius = 20f;

    [Tooltip("How long the NPC waits at a destination before walking to a new one.")]
    public float waitTime = 3f;

    private NavMeshAgent agent;
    private float timer;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        
        // Start the timer at waitTime so it picks a destination immediately
        timer = waitTime; 
    }

    void Update()
    {
        // If the agent is calculating a path or still moving toward its goal, keep waiting.
        if (agent.pathPending || agent.remainingDistance > 0.5f)
        {
            return;
        }

        // The agent has arrived at its destination and is standing still
        timer += Time.deltaTime;

        // Has it waited long enough?
        if (timer >= waitTime)
        {
            Vector3 newPos = RandomNavSphere(transform.position, wanderRadius, NavMesh.AllAreas);
            agent.SetDestination(newPos);
            timer = 0; // Reset timer
        }
    }

    /// <summary>
    /// Helper calculation to find a random valid point on the baked NavMesh.
    /// </summary>
    public static Vector3 RandomNavSphere(Vector3 origin, float dist, int layermask)
    {
        // Give it a random point in a sphere around the NPC
        Vector3 randomDirection = Random.insideUnitSphere * dist;
        randomDirection += origin;

        NavMeshHit navHit;
        
        // Have Unity find the nearest actual walk-able surface to that random point
        if (NavMesh.SamplePosition(randomDirection, out navHit, dist, layermask))
        {
            return navHit.position; // Found a good spot
        }
        
        // If it can't find a spot, just stand there until next try
        return origin; 
    }
}
