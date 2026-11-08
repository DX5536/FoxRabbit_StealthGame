using UnityEngine;

/// <summary>
/// Test hazard / platform that ping-pongs between two points and freezes while TimeStop_Manager.IsTimeStopped.
/// Example of how gameplay objects can react to Tetra's time stop.
/// </summary>
public class TimeStopMover : MonoBehaviour
{
    [SerializeField] Vector2 moveOffset = new Vector2(0f, 3f);
    [SerializeField] float period = 2f;
    [SerializeField] bool freezeDuringTimeStop = true;

    Vector3 start;
    float t;

    void Start() => start = transform.position;

    void Update()
    {
        if (freezeDuringTimeStop && TimeStop_Manager.IsTimeStopped) return;
        t += Time.deltaTime;
        float k = (Mathf.Sin(t / period * Mathf.PI * 2f) + 1f) * 0.5f;
        transform.position = start + (Vector3)(moveOffset * k);
    }
}
