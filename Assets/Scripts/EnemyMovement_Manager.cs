using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Moves an enemy with DOTween.
///   Patrol         : walks along X between patrol points (empty GameObjects), waits, turns, continues.
///                    Only the X of each point is used, the enemy keeps its own height.
///   RotateInPlace  : (cameras) stays put and rotates its vision between look angles: wait - rotate - wait - rotate.
///   Stationary     : does nothing (static guard).
/// Freezes during Tetra's time stop and on game over.
/// Works together with EnemyVision_Manager on the same object (turns the vision with the enemy).
/// </summary>
[DisallowMultipleComponent]
public class EnemyMovement_Manager : MonoBehaviour
{
    public enum MovementType { Patrol, RotateInPlace, Stationary }
    public enum PatrolLoop
    {
        /// <summary>A, B, C, B, A, B...</summary>
        PingPong,
        /// <summary>A, B, C, A, B, C...</summary>
        Loop
    }

    [SerializeField] MovementType movementType = MovementType.Patrol;
    [SerializeField] EnemyMovementProfile profile;
    [Tooltip("Empty = EnemyVision_Manager on this object.")]
    [SerializeField] EnemyVision_Manager vision;

    [Header("Patrol")]
    [Tooltip("Patrol points (empty GameObjects). Only their X position is used.")]
    [SerializeField] List<Transform> patrolPoints = new List<Transform>();
    [SerializeField] PatrolLoop loopMode = PatrolLoop.PingPong;
    [Tooltip("Body sprite flipped when walking left. Empty = SpriteRenderer on this object.")]
    [SerializeField] SpriteRenderer body;
    [Tooltip("Tick if the body sprite faces left by default.")]
    [SerializeField] bool spriteFacesLeft = false;
    [Tooltip("Vision angle while facing right (mirrored when facing left). 0 = straight ahead, -15 = slightly down.")]
    [SerializeField] float patrolLookAngle = -10f;

    [Header("Rotate In Place (cameras)")]
    [Tooltip("Look angles to rotate between (0 = right, -90 = down, 180 = left). Ping-pongs through the list.")]
    [SerializeField] List<float> lookAngles = new List<float> { -45f, -135f };
    [Tooltip("Optional visual (camera head) rotated to match the vision. Its rotation = look angle + offset.")]
    [SerializeField] Transform rotatingVisual;
    [SerializeField] float visualAngleOffset = 0f;

    [Header("Freeze")]
    [SerializeField] bool freezeDuringTimeStop = true;

    [Header("Events")]
    public UnityEvent onMoveStart;
    public UnityEvent<int> onPointReached;
    public UnityEvent onWaitStart;
    public UnityEvent onTurn;
    public UnityEvent onRotateStart;
    public UnityEvent onRotateEnd;
    public UnityEvent onFrozen;
    public UnityEvent onUnfrozen;

    public MovementType Type => movementType;
    public bool FacingRight { get; private set; } = true;
    public bool IsFrozen { get; private set; }

    Sequence sequence;
    int pointIndex;
    int direction = 1;
    int angleIndex;
    int angleDirection = 1;

    #region Unity

    void Awake()
    {
        if (vision == null) vision = GetComponent<EnemyVision_Manager>();
        if (body == null) body = GetComponent<SpriteRenderer>();
    }

    void OnEnable()
    {
        TimeStop_Manager.TimeStopChanged += OnTimeStopChanged;
        GameOver_Manager.GameOverTriggered += OnGameOver;
    }

    void OnDisable()
    {
        TimeStop_Manager.TimeStopChanged -= OnTimeStopChanged;
        GameOver_Manager.GameOverTriggered -= OnGameOver;
    }

    void Start()
    {
        if (profile == null && movementType != MovementType.Stationary)
        {
            Debug.LogWarning($"[EnemyMovement] '{name}' has no EnemyMovementProfile.", this);
            return;
        }

        switch (movementType)
        {
            case MovementType.Patrol:
                patrolPoints.RemoveAll(p => p == null);
                if (patrolPoints.Count == 0) { Debug.LogWarning($"[EnemyMovement] '{name}' has no patrol points.", this); break; }
                pointIndex = ClosestPointIndex();
                SetFacing(patrolPoints.Count > 1 ? patrolPoints[NextIndex(pointIndex, out _)].position.x >= transform.position.x : FacingRight, true);
                PatrolStep();
                break;

            case MovementType.RotateInPlace:
                if (lookAngles.Count == 0) break;
                angleIndex = 0;
                ApplyLookAngle(lookAngles[0]);
                RotateStep();
                break;
        }

        if (freezeDuringTimeStop && TimeStop_Manager.IsTimeStopped) SetFrozen(true);
    }

    void OnDestroy() => sequence?.Kill();

    #endregion

    #region Patrol

    void PatrolStep()
    {
        int target = NextIndex(pointIndex, out int newDirection);
        float targetX = patrolPoints[target].position.x;
        bool goRight = targetX >= transform.position.x;

        sequence?.Kill();
        sequence = DOTween.Sequence().SetLink(gameObject);

        // Turn first if the next point is behind us.
        if (goRight != FacingRight)
        {
            sequence.AppendCallback(() => { SetFacing(goRight, false); onTurn?.Invoke(); });
            if (vision != null && profile.turnDuration > 0f)
            {
                sequence.Append(DOTween.To(() => vision.LookAngle, a => vision.LookAngle = a, FacingAngle(goRight), profile.turnDuration)
                    .SetEase(Ease.InOutSine));
            }
            else sequence.AppendCallback(() => ApplyLookAngle(FacingAngle(goRight)));
        }

        float distance = Mathf.Abs(targetX - transform.position.x);
        sequence.AppendCallback(() => onMoveStart?.Invoke());
        sequence.Append(transform.DOMoveX(targetX, Mathf.Max(distance / profile.moveSpeed, 0.01f)).SetEase(profile.moveEase));
        sequence.AppendCallback(() =>
        {
            pointIndex = target;
            direction = newDirection;
            onPointReached?.Invoke(target);
            onWaitStart?.Invoke();
        });
        sequence.AppendInterval(profile.waitDuration);
        sequence.OnComplete(PatrolStep);
        if (IsFrozen) sequence.Pause();
    }

    int NextIndex(int current, out int newDirection)
    {
        newDirection = direction;
        if (patrolPoints.Count <= 1) return current;

        if (loopMode == PatrolLoop.Loop)
            return (current + 1) % patrolPoints.Count;

        int next = current + direction;
        if (next < 0 || next >= patrolPoints.Count)
        {
            newDirection = -direction;
            next = current + newDirection;
        }
        return next;
    }

    int ClosestPointIndex()
    {
        int best = 0;
        float bestDist = float.MaxValue;
        for (int i = 0; i < patrolPoints.Count; i++)
        {
            float d = Mathf.Abs(patrolPoints[i].position.x - transform.position.x);
            if (d < bestDist) { bestDist = d; best = i; }
        }
        return best;
    }

    float FacingAngle(bool right) => right ? patrolLookAngle : 180f - patrolLookAngle;

    void SetFacing(bool right, bool applyVision)
    {
        FacingRight = right;
        if (body != null) body.flipX = right == spriteFacesLeft;
        if (applyVision) ApplyLookAngle(FacingAngle(right));
    }

    #endregion

    #region Rotate in place

    void RotateStep()
    {
        if (lookAngles.Count < 2) return;

        int next = angleIndex + angleDirection;
        if (next < 0 || next >= lookAngles.Count)
        {
            angleDirection = -angleDirection;
            next = angleIndex + angleDirection;
        }
        float targetAngle = lookAngles[next];
        float startAngle = vision != null ? vision.LookAngle : lookAngles[angleIndex];

        sequence?.Kill();
        sequence = DOTween.Sequence().SetLink(gameObject);
        sequence.AppendInterval(profile.rotateWaitDuration);
        sequence.AppendCallback(() => onRotateStart?.Invoke());
        sequence.Append(DOTween.To(() => startAngle, ApplyLookAngle, targetAngle, profile.rotateDuration).SetEase(profile.rotateEase));
        sequence.AppendCallback(() =>
        {
            angleIndex = next;
            onRotateEnd?.Invoke();
        });
        sequence.OnComplete(RotateStep);
        if (IsFrozen) sequence.Pause();
    }

    #endregion

    void ApplyLookAngle(float angle)
    {
        if (vision != null) vision.LookAngle = angle;
        if (rotatingVisual != null) rotatingVisual.rotation = Quaternion.Euler(0f, 0f, angle + visualAngleOffset);
    }

    #region Freeze

    void OnTimeStopChanged(bool stopped)
    {
        if (freezeDuringTimeStop) SetFrozen(stopped);
    }

    void OnGameOver() => SetFrozen(true);

    public void SetFrozen(bool frozen)
    {
        if (IsFrozen == frozen) return;
        IsFrozen = frozen;
        if (sequence != null && sequence.IsActive())
        {
            if (frozen) sequence.Pause();
            else sequence.Play();
        }
        if (frozen) onFrozen?.Invoke();
        else onUnfrozen?.Invoke();
    }

    #endregion

    #region Gizmos

    void OnDrawGizmosSelected()
    {
        if (movementType != MovementType.Patrol || patrolPoints == null) return;
        Gizmos.color = new Color(1f, 0.5f, 0.2f, 0.9f);
        float y = transform.position.y;
        Vector3? prev = null;
        foreach (var p in patrolPoints)
        {
            if (p == null) continue;
            Vector3 pos = new Vector3(p.position.x, y, 0f);
            Gizmos.DrawWireSphere(pos, 0.2f);
            if (prev.HasValue) Gizmos.DrawLine(prev.Value, pos);
            prev = pos;
        }
    }

    #endregion
}
