using UnityEngine;

public class FollowLight2D : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float smoothTime = 0.15f;
    [SerializeField] private Vector2 offset = Vector2.zero;

    private Vector3 velocity;

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 goal = target.position + (Vector3)offset;
        goal.z = transform.position.z;
        transform.position = Vector3.SmoothDamp(transform.position, goal, ref velocity, smoothTime);
    }
}
