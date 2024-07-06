using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StarterAssets;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using Unity.VisualScripting;

public class NPCController : Agent
{
    private StarterAssetsInputs _input;
    public Transform Target;
    private ThirdPersonController _thirdPersonController;
    private float maxStepTime = 80.0f; // Tiempo máximo por episodio en segundos
    private Vector3 initialAgentPosition = new Vector3(-139.8601f, -3.375f, -11.35401f);
    private Quaternion initialAgentRotation = Quaternion.Euler(0, 90, 0);
    private float currentStepTime;
    private float closestDistanceToTarget;
    private bool firstEpisode = true;

    // Ubicaciones predeterminadas para el objetivo
    public List<Vector3> targetLocations;

    void Start()
    {
        // Initialize components
        _input = GetComponent<StarterAssetsInputs>();
        _thirdPersonController = GetComponent<ThirdPersonController>();

        // Verify that components are not null
        if (_input == null)
        {
            Debug.LogError("StarterAssetsInputs component not found!");
        }
        if (_thirdPersonController == null)
        {
            Debug.LogError("ThirdPersonController component not found!");
        }
    }

    public override void OnEpisodeBegin()
    {
        // Seleccionar una ubicación para el objetivo
        if (targetLocations != null && targetLocations.Count > 0)
        {
            int randomIndex = firstEpisode ? 0 : Random.Range(0, targetLocations.Count);
            Target.position = targetLocations[randomIndex];
            firstEpisode = false;
        }

        // Reset agent position and rotation
        ResetAgentPositionAndRotation();

        // Reset the step timer
        currentStepTime = 0.0f;

        // Reset the closest distance tracker
        closestDistanceToTarget = Vector3.Distance(this.transform.position, Target.position);

        // Reset input signals
        _input.MoveInput(Vector2.zero);
        _input.LookInput(Vector2.zero);

        // Reset ThirdPersonController (optional, if needed)
        if (_thirdPersonController != null)
        {
            _thirdPersonController.enabled = false;
            _thirdPersonController.enabled = true;
        }

        // Ensure the agent's velocity is reset
        if (TryGetComponent<Rigidbody>(out Rigidbody rb))
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Debug.Log($"Episode Begin - Cumulative Reward: {GetCumulativeReward()}");
    }

    private void ResetAgentPositionAndRotation()
    {
        this.transform.position = initialAgentPosition;
        this.transform.rotation = initialAgentRotation;
    }

    private void ChangeTargetLocation()
    {
        if (targetLocations != null && targetLocations.Count > 0)
        {
            int randomIndex = Random.Range(0, targetLocations.Count);
            Target.position = targetLocations[randomIndex];
            closestDistanceToTarget = Vector3.Distance(this.transform.position, Target.position);
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Always add target observations
        sensor.AddObservation(Target.position);

        // Add agent's position and orientation
        sensor.AddObservation(this.transform.position);
        sensor.AddObservation(this.transform.forward);

        // Single Raycast for detecting walls, keeping it horizontal
        float rayLength = 70f;
        Vector3 rayOrigin = transform.position + Vector3.up * 0.2f; // Adjust height as needed

        // Always keep the raycast direction horizontal
        Vector3 rayDirection = new Vector3(transform.forward.x, 0, transform.forward.z).normalized;

        // First Raycast (Horizontal)
        if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, rayLength))
        {
            sensor.AddObservation(hit.distance);
        }
        else
        {
            sensor.AddObservation(rayLength); // No hay colisión
        }

        // Visualize the Raycasts
        //Debug.DrawRay(rayOrigin, rayDirection * rayLength, Color.red);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Actions, size = 3 (2 for movement, 1 for horizontal camera rotation)
        Vector2 moveSignal = Vector2.zero;
        moveSignal.x = actions.ContinuousActions[0];
        moveSignal.y = actions.ContinuousActions[1];

        float cameraSignal = actions.ContinuousActions[2];

        // Apply the control to the character controller
        _input.MoveInput(moveSignal);
        _input.LookInput(new Vector2(cameraSignal, 0)); // Keep vertical axis fixed

        // Log the actions
        //Debug.Log($"OnActionReceived - Move Signal: {moveSignal}, Camera Signal: {cameraSignal}");

        // Rewards
        float distanceToTarget = Vector3.Distance(this.transform.position, Target.position);
        //Debug.Log($"Distance to Target: {distanceToTarget}");

        // Reward for reaching the target
        if (distanceToTarget < 2f)
        {
            AddReward(300.0f);
            Debug.Log("Reached target, ending episode with reward 300.0");
            Debug.Log($"Cumulative Reward after reaching target: {GetCumulativeReward()}");
            EndEpisode();
        }

        // Reward for proximity, ensuring it is only given for getting closer
        if (distanceToTarget < closestDistanceToTarget)
        {
            float progress = closestDistanceToTarget - distanceToTarget;
            AddReward(Mathf.Min(progress * 0.5f, 3.0f));
            closestDistanceToTarget = distanceToTarget; // Update the closest distance to the target
            Debug.Log($"Positive reward for progress towards target: {progress * 0.5f}");
        }

        // Penalize for each step to encourage more efficient movements
        AddReward(-0.002f);

        // Increment current step time
        currentStepTime += Time.deltaTime;

        // End the episode if the maximum step time has passed and change target location
        if (currentStepTime >= maxStepTime)
        {
            Debug.Log("Max step time reached, changing target location");
            Debug.Log($"Cumulative Reward after max step time: {GetCumulativeReward()}");
            AddReward(-100.0f); // Penalize for not reaching the target
            ChangeTargetLocation();
            currentStepTime = 0.0f; // Reset the step timer
            EndEpisode();
        }
    }
}
