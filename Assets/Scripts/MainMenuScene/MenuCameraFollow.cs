using UnityEngine;

public class MenuCameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform cameraPivot;

    [Header("Movement Points")]
    [SerializeField] private Vector3 rightOffset = new Vector3(15f, 0f, 20f);
    [SerializeField] private Vector3 leftOffset = new Vector3(15f, 0f, -20f);

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float waitTimeAtPoint = 0.5f;

    [Header("Rotation")]
    [SerializeField] private float rotationSmooth = 3f;

    private Vector3 startPosition;
    private Vector3 targetPosition;
    private int currentPointIndex = 0;
    private float waitTimer = 0f;
    private bool isWaiting = false;

    // Puntos del recorrido: centro -> derecha -> centro -> izquierda -> (repite)
    private Vector3[] pathPoints;

    private void Start()
    {
        // Guardar la posición inicial desde Unity
        startPosition = transform.position;

        // Definir los puntos del recorrido
        pathPoints = new Vector3[]
        {
            startPosition,                          // 0: Centro (inicio)
            startPosition + rightOffset,            // 1: Derecha
            startPosition,                          // 2: Centro
            startPosition + leftOffset,             // 3: Izquierda
        };

        // Empezar en el punto inicial
        currentPointIndex = 0;
        targetPosition = pathPoints[0];

        // Pequeña espera inicial antes de empezar el movimiento
        isWaiting = true;
        waitTimer = waitTimeAtPoint;
    }

    private void LateUpdate()
    {
        // Manejar tiempo de espera en cada punto
        if (isWaiting)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                isWaiting = false;
                MoveToNextPoint();
            }
        }
        else
        {
            // Mover la cámara hacia el punto objetivo
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                moveSpeed * Time.deltaTime
            );

            // Comprobar si hemos llegado al punto objetivo
            if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
            {
                transform.position = targetPosition;
                isWaiting = true;
                waitTimer = waitTimeAtPoint;
            }
        }

        // Rotar la cámara para mirar siempre al pivot
        if (cameraPivot != null)
        {
            Vector3 direction = cameraPivot.position - transform.position;
            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    Time.deltaTime * rotationSmooth
                );
            }
        }
    }

    private void MoveToNextPoint()
    {
        // Avanzar al siguiente punto en el recorrido
        currentPointIndex++;

        // Si llegamos al final, volver al principio (después del punto 0 inicial)
        if (currentPointIndex >= pathPoints.Length)
        {
            currentPointIndex = 1; // Volver al punto 1 (derecha) para continuar el ciclo
        }

        targetPosition = pathPoints[currentPointIndex];
    }
}