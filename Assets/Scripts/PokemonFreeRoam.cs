using UnityEngine;

[RequireComponent(typeof(CaptureablePokemon))]
public class PokemonFreeRoam : MonoBehaviour
{
    [Header("Battle Integration")]
    public bool pauseWhileInBattle = true;

    [Header("Roam Settings")]
    public float roamRadius = 5f;
    public float moveSpeed = 1.5f;
    public float turnSpeed = 5f;
    public float minIdleSeconds = 1f;
    public float maxIdleSeconds = 3f;
    [Header("Facing")]
    public Transform rotateTarget;
    public float facingYawOffsetDegrees = 0f;
    [Header("Walk Bob")]
    public Transform bobTarget;
    public bool bobOnly = false;
    public float bobAmplitude = 0.05f;
    public float bobFrequency = 3f;
    public float bobBlendSpeed = 6f;
    public float walkThreshold = 0.05f;

    [Header("Height")]
    public float fixedHeight = 0f;
    public bool lockToStartHeight = true;

    private Vector3 _origin;
    private Vector3 _target;
    private float _idleTimer;
    private bool _hasTarget;
    private float _lockedY;
    private CaptureablePokemon _pokemon;
    private Vector3 _bobBaseLocalPos;
    private float _bobWeight;
    private Vector3 _lastWorldPos;

    private void Start()
    {
        _pokemon = GetComponent<CaptureablePokemon>();
        _origin = transform.position;
        _lockedY = lockToStartHeight ? _origin.y : fixedHeight;
        if (bobTarget != null) _bobBaseLocalPos = bobTarget.localPosition;
        PickNewTarget();
        _lastWorldPos = transform.position;
    }

    private void Update()
    {
        if (HandleBattlePause()) return;

        MaintainHeight();

        if (!bobOnly)
        {
            Vector2 deltaXZ;
            Vector3 direction;
            bool isMoving;
            HandleRoamMovement(out deltaXZ, out direction, out isMoving);
            HandleFacing(direction, isMoving);
        }

        Vector3 worldDelta = transform.position - _lastWorldPos;
        Vector2 worldDeltaXZ = new Vector2(worldDelta.x, worldDelta.z);

        HandleWalkBob(worldDeltaXZ);

        _lastWorldPos = transform.position;
    }

    private void PickNewTarget()
    {
        Vector2 random = Random.insideUnitCircle * roamRadius;
        _target = _origin + new Vector3(random.x, 0f, random.y);
        _target = ApplyHeightLock(_target);
        _hasTarget = true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 0.4f, 0.25f);
        Vector3 center = Application.isPlaying ? _origin : transform.position;
        Gizmos.DrawWireSphere(center, roamRadius);
    }

    private bool HandleBattlePause()
    {
        if (!pauseWhileInBattle || BattleManager.Instance == null) return false;
        if (!BattleManager.Instance.IsInBattle) return false;
        return BattleManager.Instance.playerPokemon == _pokemon || BattleManager.Instance.opponentPokemon == _pokemon;
    }

    private void HandleRoamMovement(out Vector2 deltaXZ, out Vector3 direction, out bool isMoving)
    {
        deltaXZ = Vector2.zero;
        direction = Vector3.zero;
        isMoving = false;

        if (!_hasTarget)
        {
            _idleTimer -= Time.deltaTime;
            if (_idleTimer <= 0f) PickNewTarget();
            return;
        }

        Vector3 current = ApplyHeightLock(transform.position);
        transform.position = current;

        Vector3 toTarget = _target - current;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;
        if (distance < 0.05f)
        {
            _hasTarget = false;
            _idleTimer = Random.Range(minIdleSeconds, maxIdleSeconds);
            return;
        }

        direction = toTarget / Mathf.Max(distance, 0.0001f);
        Vector3 step = direction * moveSpeed * Time.deltaTime;
        if (step.magnitude > distance) step = direction * distance;

        Vector3 nextPos = ApplyHeightLock(current + step);
        transform.position = nextPos;

        Vector3 delta = nextPos - current;
        deltaXZ = new Vector2(delta.x, delta.z);
        isMoving = deltaXZ.sqrMagnitude > 0.000001f;
    }

    private void HandleFacing(Vector3 direction, bool isMoving)
    {
        if (!isMoving) return;
        Quaternion look = Quaternion.LookRotation(direction, Vector3.up);
        if (Mathf.Abs(facingYawOffsetDegrees) > 0.001f)
        {
            look *= Quaternion.Euler(0f, facingYawOffsetDegrees, 0f);
        }
        Transform target = rotateTarget != null ? rotateTarget : transform;
        target.rotation = Quaternion.Slerp(target.rotation, look, turnSpeed * Time.deltaTime);
    }

    private void HandleWalkBob(Vector2 deltaXZ)
    {
        if (bobTarget == null) return;

        float dt = Time.deltaTime > 0f ? Time.deltaTime : 0.0001f;
        float speedXZ = deltaXZ.magnitude / dt;
        bool movingNow = bobOnly || speedXZ > walkThreshold;

        float targetWeight = movingNow ? 1f : 0f;
        _bobWeight = Mathf.MoveTowards(_bobWeight, targetWeight, bobBlendSpeed * Time.deltaTime);

        if (_bobWeight > 0.0001f)
        {
            float bob = Mathf.Sin(Time.time * Mathf.PI * 2f * bobFrequency) * bobAmplitude * _bobWeight;
            Vector3 local = _bobBaseLocalPos;
            local.y = _bobBaseLocalPos.y + bob;
            bobTarget.localPosition = local;
        }
        else
        {
            bobTarget.localPosition = Vector3.MoveTowards(bobTarget.localPosition, _bobBaseLocalPos, bobBlendSpeed * Time.deltaTime);
        }
    }

    private Vector3 ApplyHeightLock(Vector3 position)
    {
        if (lockToStartHeight || fixedHeight != 0f)
        {
            position.y = _lockedY;
        }
        return position;
    }

    private void MaintainHeight()
    {
        if (lockToStartHeight || fixedHeight != 0f)
        {
            Vector3 pos = transform.position;
            pos.y = _lockedY;
            transform.position = pos;
        }
    }
}
