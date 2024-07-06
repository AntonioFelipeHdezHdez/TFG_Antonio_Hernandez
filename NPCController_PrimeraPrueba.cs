using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StarterAssets;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class NPCController : Agent
{
    private StarterAssetsInputs _input;
    public Transform Target;
    private ThirdPersonController _thirdPersonController;
    private float maxStepTime = 80.0f; // Tiempo máximo por episodio en segundos
    private Vector3 lastPosition;
    private float currentStepTime;
    private float closestDistanceToTarget;

    void Start()
    {
        _input = GetComponent<StarterAssetsInputs>();
        _thirdPersonController = GetComponent<ThirdPersonController>();
    }

    public override void OnEpisodeBegin()
    {
        // Reset agent position and rotation
        this.transform.position = new Vector3(-139.8601f, -3.375f, -11.35401f);
        this.transform.rotation = Quaternion.Euler(0, 90, 0);

        // Reset the step timer
        currentStepTime = 0.0f;

        // Reset position tracking
        lastPosition = this.transform.position;

        // Reset the closest distance tracker
        closestDistanceToTarget = Vector3.Distance(this.transform.position, Target.position);

        // Reset input signals
        _input.MoveInput(Vector2.zero);
        _input.LookInput(Vector2.zero);

        // Reset ThirdPersonController (optional, if needed)
        _thirdPersonController.enabled = false;
        _thirdPersonController.enabled = true;

        // Ensure the agent's velocity is reset
        if (TryGetComponent<Rigidbody>(out Rigidbody rb))
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Debug.Log($"Episode Begin - Cumulative Reward: {GetCumulativeReward()}");
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Target and Agent positions in global coordinates
        sensor.AddObservation(Target.position);
        sensor.AddObservation(this.transform.position);
        sensor.AddObservation(this.transform.forward);

        // Single Raycast for detecting walls, keeping it horizontal
        float rayLength = 70f;
        Vector3 rayOrigin = transform.position + Vector3.up * 0.2f; // Adjust height as needed

        // Always keep the raycast direction horizontal
        Vector3 rayDirection = new Vector3(transform.forward.x, 0, transform.forward.z).normalized;

        if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, rayLength))
        {
            if (hit.collider.CompareTag("Wall"))
            {
                sensor.AddObservation(hit.distance);
                //Debug.Log($"Raycast hit wall at distance: {hit.distance}");
            }
            else
            {
                sensor.AddObservation(rayLength); // No hay colisión con pared
                //Debug.Log($"Raycast hit non-wall object at distance: {hit.distance}");
            }
        }
        else
        {
            sensor.AddObservation(rayLength); // No hay colisión
            //Debug.Log("Raycast hit nothing.");
        }

        // Visualizar el Raycast
        Debug.DrawRay(rayOrigin, rayDirection * rayLength, Color.red);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Actions, size = 3 (2 for movement, 1 for horizontal camera rotation)
        Vector2 moveSignal = Vector2.zero;
        moveSignal.x = actions.ContinuousActions[0];
        moveSignal.y = actions.ContinuousActions[1];

        float cameraSignal = actions.ContinuousActions[2];

        // Aplicar el control al controlador del personaje
        _input.MoveInput(moveSignal);
        _input.LookInput(new Vector2(cameraSignal, 0)); // Mantener el eje vertical fijo

        // Log the actions
        //Debug.Log($"OnActionReceived - Move Signal: {moveSignal}, Camera Signal: {cameraSignal}");

        // Recompensas
        float distanceToTarget = Vector3.Distance(this.transform.position, Target.position);
        //Debug.Log($"Distance to Target: {distanceToTarget}");

        // Recompensa por alcanzar el objetivo
        if (distanceToTarget < 1.5f)
        {
            AddReward(100.0f);
            Debug.Log("Reached target, ending episode with reward 100.0");
            Debug.Log($"Cumulative Reward after reaching target: {GetCumulativeReward()}");
            EndEpisode();
        }

        // Recompensa por acercarse al objetivo (solo si es la distancia más cercana hasta ahora)
        if (distanceToTarget < closestDistanceToTarget)
        {
            float progress = closestDistanceToTarget - distanceToTarget;
            AddReward(progress * 0.5f);
            closestDistanceToTarget = distanceToTarget; // Actualiza la distancia más cercana
            Debug.Log($"Positive reward for progress towards target: {progress * 0.5f}");
        }

        // Penalizar por cada paso para incentivar movimientos más eficientes
        AddReward(-0.002f);

        // Incrementar el tiempo actual del episodio
        currentStepTime += Time.deltaTime;

        // Terminar el episodio si ha pasado el tiempo máximo
        if (currentStepTime >= maxStepTime)
        {
            Debug.Log("Max step time reached, ending episode with penalty -1.0");
            Debug.Log($"Cumulative Reward at max step time: {GetCumulativeReward()}");
            EndEpisode();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            AddReward(-0.1f); // Penalización por chocar contra una pared
            Debug.Log("Collided with wall, applying penalty -0.1.");
            Debug.Log($"Cumulative Reward after collision: {GetCumulativeReward()}");
        }
    }
}
