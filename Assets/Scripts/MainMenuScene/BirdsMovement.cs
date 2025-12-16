using UnityEngine;

public class BirdsMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;

    private readonly float xDirection = -1f;
    private readonly float zRatio = 0.2f;

    private Vector3 initialPosition;

    private void Start()
    {
        initialPosition = transform.position;
    }

    private void Update()
    {
        float xMovement = xDirection * moveSpeed * Time.deltaTime;
        float zMovement = xMovement * zRatio;
        transform.position += new Vector3(xMovement, 0f, zMovement);

        if (transform.position.x < -250f)
        {
            transform.position = initialPosition;
        }
    }
}