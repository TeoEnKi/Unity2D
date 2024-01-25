using UnityEngine;

public class Obstacle : MonoBehaviour
{
    public float AvoidanceRadiusMultFactor = 1.5f;

    public Vector3 pos;
    public float avoidanceRadius;
    private void Start()
    {
        pos = transform.position;
        avoidanceRadius = AvoidanceRadius;
    }

    private void Update()
    {
        transform.position = pos;
    }
    private float AvoidanceRadius
    {
        get
        {
            return mCollider.radius * 3 * AvoidanceRadiusMultFactor;
        }
    }

    public CircleCollider2D mCollider;
}
