using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Handles returning a pokeball to a designated socket (e.g., on the player's hip) after a delay.
/// Attach this to the pokeball prefab and assign a socket Transform.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PokeBallReturn : MonoBehaviour
{
    [Header("Socket")]
    [Tooltip("Where the pokeball should return to (e.g., a child transform on the player hip).")]
    public Transform returnSocket;
    [Tooltip("Optional name to auto-find the socket if not assigned.")]
    public string socketName = "PokeballSocket";
    public bool autoFindSocket = true;

    [Header("Timing")]
    [Tooltip("Seconds to wait after activation before returning to the socket.")]
    public float returnDelaySeconds = 5f;
    [Tooltip("Seconds to lerp the ball back to the socket once returning.")]
    public float returnLerpSeconds = 1.5f;
    [Tooltip("Start the return countdown automatically on enable.")]
    public bool autoStartOnEnable = true;
    [Header("XR Grab Handling")]
    [Tooltip("If true, pause/cancel return while the ball is held by an interactor.")]
    public bool pauseWhileHeld = true;
    [Tooltip("Optional XRGrabInteractable to hook grab events. Auto-finds on this object if not set.")]
    public UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;
    [Header("Audio")]
    [Tooltip("AudioSource used for SFX on this ball.")]
    public AudioSource audioSource;
    [Tooltip("Played when the ball is released/thrown.")]
    public AudioClip throwClip;

    private Rigidbody _rb;
    private Coroutine _returnRoutine;
    private bool _isHeld;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        TryAutoFindSocket();
        if (grabInteractable == null)
        {
            grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        }
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnGrab);
            grabInteractable.selectExited.AddListener(OnRelease);
        }
    }

    private void OnEnable()
    {
        if (autoStartOnEnable)
        {
            BeginReturnCountdown();
        }
    }

    public void BeginReturnCountdown()
    {
        if (pauseWhileHeld && _isHeld) return;
        if (_returnRoutine != null) StopCoroutine(_returnRoutine);
        _returnRoutine = StartCoroutine(ReturnRoutine());
    }

    public void ReturnNow()
    {
        if (_returnRoutine != null) StopCoroutine(_returnRoutine);
        _returnRoutine = StartCoroutine(ReturnRoutine(0f));
    }

    private IEnumerator ReturnRoutine(float? overrideDelay = null)
    {
        float wait = overrideDelay ?? returnDelaySeconds;
        if (wait > 0f)
        {
            yield return new WaitForSeconds(wait);
        }

        if (returnSocket == null)
        {
            TryAutoFindSocket();
            if (returnSocket == null) yield break;
        }

        // Prep physics for controlled move (make kinematic just for return)
        if (_rb != null)
        {
            _rb.isKinematic = true;
            _rb.useGravity = false;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        Vector3 targetPos = returnSocket.position;
        Quaternion targetRot = returnSocket.rotation;

        float duration = Mathf.Max(0.01f, returnLerpSeconds);
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float lerp = Mathf.Clamp01(t / duration);
            transform.position = Vector3.Lerp(startPos, targetPos, lerp);
            transform.rotation = Quaternion.Slerp(startRot, targetRot, lerp);
            yield return null;
        }

        transform.position = targetPos;
        transform.rotation = targetRot;

        // Keep kinematic/gravity off while docked at the socket;
        // grabbing will re-enable physics for throwing.
    }

    private void TryAutoFindSocket()
    {
        if (returnSocket != null || !autoFindSocket) return;
        if (!string.IsNullOrEmpty(socketName))
        {
            GameObject found = GameObject.Find(socketName);
            if (found != null)
            {
                returnSocket = found.transform;
            }
        }
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        _isHeld = true;
        if (_returnRoutine != null)
        {
            StopCoroutine(_returnRoutine);
            _returnRoutine = null;
        }
        if (_rb != null)
        {
            _rb.isKinematic = false;
            _rb.useGravity = true;
        }
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        _isHeld = false;
        if (_rb != null)
        {
            _rb.isKinematic = false;
            _rb.useGravity = true;
        }
        BeginReturnCountdown();

        if (audioSource != null && throwClip != null)
        {
            audioSource.PlayOneShot(throwClip);
        }
    }
}
