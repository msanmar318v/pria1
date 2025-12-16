using UnityEngine;

public class MenuCameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform cameraPivot;

    [Header("Movement Points")]
    [SerializeField] private Vector3 rightOffset = new Vector3(-40f, 0f, 30f);
    [SerializeField] private Vector3 leftOffset = new Vector3(-40f, 0f, -30f);

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float waitTimeAtPoint = 0.5f;

    [Header("Rotation")]
    [SerializeField] private float rotationSmooth = 3f;

    private Vector3 startPosition;
    private Vector3 targetPosition;
    private int currentPointIndex = 0;
    private float waitTimer = 0f;
    private bool isWaiting = false;

    private Vector3[] pathPoints;

    private void Start()
    {
        startPosition = transform.position;

        pathPoints = new Vector3[]
        {
            startPosition,                          // 0: Centro (inicio)
            startPosition + rightOffset,            // 1: Derecha
            startPosition,                          // 2: Centro
            startPosition + leftOffset,             // 3: Izquierda
        };

        currentPointIndex = 0;
        targetPosition = pathPoints[0];

        isWaiting = true;
        waitTimer = waitTimeAtPoint;
    }

    private void LateUpdate()
    {
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
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                moveSpeed * Time.deltaTime
            );

            if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
            {
                transform.position = targetPosition;
                isWaiting = true;
                waitTimer = waitTimeAtPoint;
            }
        }

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
        currentPointIndex++;

        if (currentPointIndex >= pathPoints.Length)
        {
            currentPointIndex = 1;
        }

        targetPosition = pathPoints[currentPointIndex];
    }
}