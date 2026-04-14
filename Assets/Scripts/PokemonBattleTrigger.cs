using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PokemonBattleTrigger : MonoBehaviour
{
    [Tooltip("Seconds to wait between battle triggers to avoid rapid re-entry.")]
    public float battleCooldownSeconds = 1f;
    [Tooltip("Enable verbose debug logging for encounter triggers.")]
    public bool enableDebugLogs = true;
    [Tooltip("If true, skip battles when either Pokemon is fainted or already captured.")]
    public bool skipInactivePokemon = true;
    [Tooltip("Optional override for the BattleManager; defaults to the singleton instance.")]
    public BattleManager battleManager;

    private CaptureablePokemon pokemon;
    private float lastBattleTime = -Mathf.Infinity;

    private void Awake()
    {
        pokemon = GetComponentInParent<CaptureablePokemon>();
        if (pokemon == null)
        {
            Debug.LogWarning("PokemonBattleTrigger could not find CaptureablePokemon on this object or its parents.");
        }

        // Ensure collider is set as trigger
        Collider col = GetComponent<Collider>();
        if (col != null && col.isTrigger == false)
        {
            Debug.LogWarning("PokemonBattleTrigger requires the collider to be marked as Trigger.");
        }

        if (enableDebugLogs) Debug.Log("[PokemonBattleTrigger] Awake on " + gameObject.name);
    }

    private void OnTriggerEnter(Collider other)
    {
        battleManager = battleManager != null ? battleManager : BattleManager.Instance;

        if (pokemon == null)
        {
            if (enableDebugLogs) Debug.LogWarning("[PokemonBattleTrigger] No CaptureablePokemon found on trigger.");
            return;
        }
        // Only initiate from player-owned Pokemon
        if (!pokemon.isPlayerOwned)
        {
            if (enableDebugLogs) Debug.Log($"[PokemonBattleTrigger] Ignoring trigger; {pokemon.pokemonName} is not player-owned.");
            return;
        }
        if (battleManager == null)
        {
            if (enableDebugLogs) Debug.LogWarning("[PokemonBattleTrigger] No BattleManager in scene.");
            return;
        }
        if (battleManager.IsInBattle)
        {
            if (enableDebugLogs) Debug.Log("[PokemonBattleTrigger] Already in battle, ignoring trigger.");
            return;
        }

        // Prevent spamming
        if (Time.time - lastBattleTime < battleCooldownSeconds)
        {
            if (enableDebugLogs) Debug.Log("[PokemonBattleTrigger] Cooldown active, ignoring trigger.");
            return;
        }

        CaptureablePokemon otherPokemon = other.GetComponentInParent<CaptureablePokemon>();
        if (otherPokemon == null)
        {
            if (enableDebugLogs) Debug.Log($"[PokemonBattleTrigger] Triggered by {other.name} but no CaptureablePokemon found.");
            return;
        }
        if (otherPokemon == pokemon)
        {
            if (enableDebugLogs) Debug.Log("[PokemonBattleTrigger] Triggered self, ignoring.");
            return;
        }

        bool ownedA = pokemon.isPlayerOwned;
        bool ownedB = otherPokemon.isPlayerOwned;

        // Only proceed if exactly one is owned.
        if (ownedA == ownedB)
        {
            if (enableDebugLogs) Debug.Log($"[PokemonBattleTrigger] Ignoring trigger; need one owned and one wild. ThisOwned:{ownedA} OtherOwned:{ownedB}");
            return;
        }

        CaptureablePokemon playerOwned = ownedA ? pokemon : otherPokemon;
        CaptureablePokemon wild = ownedA ? otherPokemon : pokemon;

        if (skipInactivePokemon && (playerOwned.IsFainted || wild.IsFainted || wild.isCaptured))
        {
            if (enableDebugLogs) Debug.Log("[PokemonBattleTrigger] One of the Pokemon is fainted or already captured; skipping battle.");
            return;
        }

        if (enableDebugLogs)
        {
            Debug.Log($"[PokemonBattleTrigger] Trigger enter: owned {playerOwned.pokemonName} vs wild {wild.pokemonName} (collider: {other.name})");
        }

        lastBattleTime = Time.time;
        battleManager.StartBattle(playerOwned, wild);
    }
}
