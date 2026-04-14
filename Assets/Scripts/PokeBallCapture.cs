using UnityEngine;

public class PokeBallCapture : MonoBehaviour
{
    [Header("Ball Settings")]
    public float destroyAfterCaptureDelay = 0.5f;
    [Tooltip("Multiplier applied to capture chance (1 = standard ball).")]
    public float captureBonus = 1f;

    [Header("Release Settings")]
    [Tooltip("If true, release the captured Pokemon on the next collision after capture (for example, when the ball hits the floor).")]
    public bool releaseOnAnyCollisionAfterCapture = true;
    [Tooltip("Used only when releaseOnAnyCollisionAfterCapture is false.")]
    public string releaseCollisionTag = "Ground";
    [Tooltip("Distance in front of the ball where the Pokemon should appear when it is thrown out.")]
    public float releaseDistanceFromBall = 1f;
    [Tooltip("Vertical lift applied to the release position to avoid ground clipping.")]
    public float releaseHeightOffset = 0.25f;
    [Tooltip("If true, uses the ball's velocity direction to decide where to spawn the Pokemon; falls back to forward if velocity is low.")]
    public bool useVelocityDirection = true;
    [Tooltip("Impulse applied to the Pokemon when it pops out of the ball.")]
    public float releaseLaunchForce = 5f;
    [Tooltip("Ignore the first collision after a capture so the ball falling to the ground does not immediately release the Pokemon.")]
    public bool ignoreFirstCollisionAfterCapture = true;

    [Header("Target Filters")]
    [Tooltip("Skip targets that are already owned by the player.")]
    public bool ignorePlayerOwnedTargets = true;
    [Tooltip("Skip targets already captured by another ball.")]
    public bool ignoreAlreadyCapturedTargets = true;

    private CaptureablePokemon capturedPokemon;
    private bool hasReleased = false;
    private bool ignoreNextCollisionAfterCapture = false;

    private void OnCollisionEnter(Collision collision)
    {
        if (hasReleased) return;

        // If we have not captured anything yet, see if we hit a Pokemon
        if (capturedPokemon == null)
        {
            CaptureablePokemon pokemon = collision.gameObject.GetComponentInParent<CaptureablePokemon>();

            if (pokemon != null && IsAllowedTarget(pokemon))
            {
                bool caught = pokemon.TryCapture(captureBonus, out float chanceUsed);

                if (caught)
                {
                    capturedPokemon = pokemon;
                    ignoreNextCollisionAfterCapture = ignoreFirstCollisionAfterCapture;

                    Debug.Log($"Pokeball hit {pokemon.pokemonName}! Capture succeeded (chance used {chanceUsed:P0}).");

                    // TODO: play catch VFX / SFX here

                    // Optionally stop the ball from bouncing forever so gravity drops it to the floor
                    Rigidbody rb = GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }

                    // Keep the ball alive so it can release on the next collision
                    return;
                }
                else
                {
                    Debug.Log($"Pokeball hit {pokemon.pokemonName} but failed to capture (chance used {chanceUsed:P0}).");
                }

            }
        }

        // Optional: skip the first collision after capture (e.g., when the ball hits the ground right after catching)
        if (capturedPokemon != null && capturedPokemon.isCaptured && ignoreNextCollisionAfterCapture)
        {
            ignoreNextCollisionAfterCapture = false;
            return;
        }

        // If something is captured and we hit the floor/anything else, throw it back out
        if (capturedPokemon != null && capturedPokemon.isCaptured && ShouldReleaseFromCollision(collision))
        {
            ReleaseCapturedPokemon();
        }
    }

    private bool ShouldReleaseFromCollision(Collision collision)
    {
        if (releaseOnAnyCollisionAfterCapture) return true;
        return collision.gameObject.CompareTag(releaseCollisionTag);
    }

    private void ReleaseCapturedPokemon()
    {
        if (capturedPokemon == null) return;

        // Choose a direction to spawn next to the ball
        Vector3 direction = transform.forward;
        Rigidbody ballBody = GetComponent<Rigidbody>();
        if (useVelocityDirection && ballBody != null && ballBody.linearVelocity.sqrMagnitude > 0.01f)
        {
            direction = ballBody.linearVelocity.normalized;
        }

        Vector3 releasePosition = transform.position + direction * releaseDistanceFromBall;
        releasePosition += Vector3.up * releaseHeightOffset;
        Quaternion releaseRotation = transform.rotation;

        capturedPokemon.Release(releasePosition, releaseRotation);

        Rigidbody pokemonBody = capturedPokemon.GetComponent<Rigidbody>();
        if (pokemonBody != null && releaseLaunchForce > 0f)
        {
            pokemonBody.AddForce(direction * releaseLaunchForce, ForceMode.VelocityChange);
        }

        hasReleased = true;

        // Stop the ball after it has done its job
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Destroy or hide the ball after a moment
        if (destroyAfterCaptureDelay > 0f)
        {
            Destroy(gameObject, destroyAfterCaptureDelay);
        }
    }

    private bool IsAllowedTarget(CaptureablePokemon pokemon)
    {
        if (ignorePlayerOwnedTargets && pokemon.isPlayerOwned) return false;
        if (ignoreAlreadyCapturedTargets && pokemon.isCaptured) return false;
        return true;
    }
}
