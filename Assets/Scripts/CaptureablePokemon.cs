using UnityEngine;

public class CaptureablePokemon : MonoBehaviour
{
    [Header("Info")]
    public string pokemonName = "Testmon";

    [Header("Stats")]
    [Tooltip("Optional level for display/UI.")]
    public int level = 5;
    public int baseHP = 30;
    public int baseAttack = 12;
    public int baseDefense = 8;
    [Range(1, 255)]
    public int catchRate = 190;

    [Header("Moves")]
    [Tooltip("Moves available to this Pokemon. Order defines the slots shown in UI.")]
    public MoveDefinition[] moves;

    [Header("Capture Settings")]
    public bool isCaptured = false;
    [Tooltip("Set true after this Pokemon has been captured by the player so battle triggers can differentiate owned vs wild.")]
    public bool isPlayerOwned = false;
    [Tooltip("Higher numbers make this Pokemon harder to catch.")]
    public float captureDifficultyMultiplier = 1f;

    [Header("Runtime Stats")]
    [SerializeField] private PokemonRuntimeStats runtimeStats = new PokemonRuntimeStats();
    [SerializeField] private int[] moveCurrentPP;

    [Header("Debug")]
    public bool logRuntimeChanges = false;
    [Tooltip("Layer used for the battle trigger collider when owned.")]
    public string playerTriggerLayerName = "PlayerPokemonTrigger";
    [Tooltip("Layer used for the battle trigger collider when wild.")]
    public string wildTriggerLayerName = "WildPokemonTrigger";

    public int MaxHP => Mathf.Max(1, baseHP);
    public int CurrentHP => Mathf.Clamp(runtimeStats.currentHP, 0, MaxHP);
    public int Attack => runtimeStats.attack <= 0 ? baseAttack : runtimeStats.attack;
    public int Defense => runtimeStats.defense < 0 ? baseDefense : runtimeStats.defense;
    public bool IsFainted => CurrentHP <= 0;
    public bool IsWild => !isPlayerOwned;
    public MoveDefinition[] Moves => moves;
    public MoveDefinition PrimaryMove => (Moves != null && Moves.Length > 0 && Moves[0] != null) ? Moves[0] : null;

    private void Awake()
    {
        EnsureRuntimeIsInitialized();
        ApplyOwnershipState();
        LogTriggerState();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureRuntimeIsInitialized();
        ApplyOwnershipState();
    }
#endif

    public void EnsureRuntimeIsInitialized()
    {
        runtimeStats.attack = Mathf.Max(1, baseAttack);
        runtimeStats.defense = Mathf.Max(0, baseDefense);

        if (runtimeStats.currentHP <= 0 || runtimeStats.currentHP > MaxHP)
        {
            runtimeStats.currentHP = MaxHP;
        }

        ResetMovePP(false);

    }

    public void ApplyOwnershipState()
    {
        // Find battle trigger collider
        PokemonBattleTrigger trigger = GetComponentInChildren<PokemonBattleTrigger>(true);
        if (trigger == null) return;

        string targetLayerName = isPlayerOwned ? playerTriggerLayerName : wildTriggerLayerName;
        int layer = LayerMask.NameToLayer(targetLayerName);
        if (layer == -1)
        {
            Debug.LogWarning($"[CaptureablePokemon] Layer '{targetLayerName}' not found for {pokemonName}. Please create it in Project Settings > Tags and Layers.");
            return;
        }

        trigger.gameObject.layer = layer;
        Collider col = trigger.GetComponent<Collider>();
        if (col != null)
        {
            col.gameObject.layer = layer;
        }
    }

    private void LogTriggerState()
    {
        PokemonBattleTrigger trigger = GetComponentInChildren<PokemonBattleTrigger>(true);
        if (trigger == null)
        {
            if (logRuntimeChanges) Debug.Log($"[CaptureablePokemon] {pokemonName}: no PokemonBattleTrigger found.");
            return;
        }
        string layerName = LayerMask.LayerToName(trigger.gameObject.layer);
        if (logRuntimeChanges)
        {
            Debug.Log($"[CaptureablePokemon] {pokemonName} owned:{isPlayerOwned} triggerLayer:{layerName}");
        }
    }

    public void HealToFull()
    {
        runtimeStats.currentHP = MaxHP;
        if (logRuntimeChanges) Debug.Log($"{pokemonName} healed to full ({CurrentHP}/{MaxHP}).", this);
    }

    public void ApplyDamage(int amount)
    {
        amount = Mathf.Max(0, amount);
        int previous = CurrentHP;
        runtimeStats.currentHP = Mathf.Max(0, CurrentHP - amount);

        if (logRuntimeChanges) Debug.Log($"[Pokemon] {pokemonName} took {amount} damage ({previous} -> {CurrentHP}).", this);

        if (IsFainted)
        {
            Faint();
        }
    }

    public void ReceiveHeal(int amount)
    {
        amount = Mathf.Max(0, amount);
        runtimeStats.currentHP = Mathf.Min(MaxHP, CurrentHP + amount);
        if (logRuntimeChanges) Debug.Log($"[Pokemon] {pokemonName} healed {amount} HP ({CurrentHP}/{MaxHP}).", this);
    }

    public float CalculateCaptureChance(float ballBonus = 1f)
    {
        float hpFactor = Mathf.Clamp01(1f - (float)CurrentHP / Mathf.Max(1, MaxHP));
        float baseRate = Mathf.Clamp01(catchRate / 255f);
        float difficulty = Mathf.Max(0.1f, captureDifficultyMultiplier);
        float chance = (0.35f + hpFactor * 0.65f) * baseRate * ballBonus / difficulty;
        return Mathf.Clamp01(chance);
    }

    public bool TryCapture(float ballBonus, out float chanceUsed)
    {
        chanceUsed = CalculateCaptureChance(ballBonus);
        bool success = Random.value <= chanceUsed;
        if (success)
        {
            Capture();
        }
        else if (logRuntimeChanges)
        {
            Debug.Log($"{pokemonName} broke free (chance used {chanceUsed:P0}).", this);
        }

        return success;
    }

    public void Capture()
    {
        if (isCaptured) return;

        isCaptured = true;
        isPlayerOwned = true;
        ApplyOwnershipState();

        if (runtimeStats.currentHP <= 0)
        {
            runtimeStats.currentHP = Mathf.Max(1, MaxHP / 2);
        }

        Debug.Log($"Captured {pokemonName}!");
        gameObject.SetActive(false);
    }

    public void Release(Vector3 position, Quaternion rotation)
    {
        if (!isCaptured) return;

        transform.SetPositionAndRotation(position, rotation);
        gameObject.SetActive(true);
        isCaptured = false;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Debug.Log($"Released {pokemonName}!");
    }

    public void Faint()
    {
        runtimeStats.currentHP = 0;
        if (logRuntimeChanges) Debug.Log($"{pokemonName} fainted and was despawned.", this);
        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }

    public MoveDefinition GetMove(int index)
    {
        if (moves == null || moves.Length == 0) return null;
        if (index < 0 || index >= moves.Length) return null;
        return moves[index];
    }

    public int GetCurrentPP(int index)
    {
        if (moveCurrentPP == null || index < 0 || index >= moveCurrentPP.Length) return 0;
        return moveCurrentPP[index];
    }

    public bool TryConsumePP(int index, out MoveDefinition move)
    {
        move = GetMove(index);
        if (move == null) return false;
        if (moveCurrentPP == null || index < 0 || index >= moveCurrentPP.Length) return false;
        if (moveCurrentPP[index] <= 0) return false;

        moveCurrentPP[index] = Mathf.Max(0, moveCurrentPP[index] - 1);
        return true;
    }

    public void ResetMovePP(bool force = true)
    {
        if (moves == null || moves.Length == 0) return;
        if (moveCurrentPP == null || moveCurrentPP.Length != moves.Length)
        {
            moveCurrentPP = new int[moves.Length];
        }

        if (force || moveCurrentPP.Length != moves.Length)
        {
            for (int i = 0; i < moves.Length; i++)
            {
                moveCurrentPP[i] = moves[i] != null ? Mathf.Max(1, moves[i].maxPP) : 0;
            }
        }
    }

    public void ResetForBattle()
    {
        runtimeStats.currentHP = MaxHP;
        ResetMovePP(true);
    }
}

[System.Serializable]
public class PokemonRuntimeStats
{
    public int currentHP;
    public int attack;
    public int defense;
}
