using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum GameMode
{
    Explore,
    Battle
}

public enum BattlePhase
{
    Idle,
    Intro,
    PlayerTurn,
    OpponentTurn,
    Victory,
    Defeat,
    Cleanup
}

public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }

    [Header("UI")]
    [Tooltip("Assign a Canvas/Panel that contains the battle UI. It will be toggled on/off by the battle state.")]
    public GameObject battleUIPanel;
    [Tooltip("Optional text element to show the matchup (e.g., \"Pikachu vs Bulbasaur\").")]
    public TMP_Text battleTitleText;
    [Tooltip("Optional controller for HP bars, action buttons, and prompts.")]
    public BattleUIController battleUI;

    [Header("Battle Settings")]
    [Tooltip("Move used when a Pokemon has no moves defined.")]
    public MoveDefinition fallbackMove;
    [Tooltip("Seconds to wait between turns for readability.")]
    public float turnDelaySeconds = 1.5f;
    [Tooltip("If true, automatically play turns when no UI is provided.")]
    public bool autoBattleWhenNoUI = true;
    [Tooltip("Allow capture attempts from the battle UI against the wild Pokemon.")]
    public bool allowCaptureInBattle = true;
    [Header("Audio")]
    [Tooltip("AudioSource for explore/ambient loop (clip assigned on the AudioSource).")]
    public AudioSource exploreMusicSource;
    [Tooltip("AudioSource for battle loop (clip assigned on the AudioSource).")]
    public AudioSource battleMusicSource;
    [Tooltip("AudioSource used for one-shot SFX (clip assigned on the AudioSource).")]
    public AudioSource sfxSource;

    [Header("Debug")]
    public bool enableDebugLogs = true;

    [Header("State (runtime)")]
    public GameMode currentMode = GameMode.Explore;
    public BattlePhase currentPhase = BattlePhase.Idle;
    public CaptureablePokemon playerPokemon;
    public CaptureablePokemon opponentPokemon;

    private bool isResolvingTurn;
    private bool waitingForMoveChoice;

    public bool IsInBattle => currentMode == GameMode.Battle;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (enableDebugLogs) Debug.Log("[BattleManager] Awake and ready");
        // If you want it to persist across scenes, uncomment the next line.
        // DontDestroyOnLoad(gameObject);

        EnsureUIReferences();
        EnsureAudioSources();
        DisableBattleUI();

        if (battleUI != null)
        {
            battleUI.Bind(this);
        }
    }

    private void Start()
    {
        // Start ambient music on boot
        if (currentMode == GameMode.Explore)
        {
            StopMusic(battleMusicSource);
            PlayMusic(exploreMusicSource, true);
        }
    }


    private void OnDestroy()
    {
        if (battleUI != null)
        {
            battleUI.Unbind();
        }
    }

    private void EnsureUIReferences()
    {
        // Try to auto-locate the BattleUIController if not assigned.
        if (battleUI == null)
        {
            battleUI = GetComponentInChildren<BattleUIController>(true);
            if (enableDebugLogs && battleUI != null) Debug.Log($"[BattleManager] Found BattleUIController in children: {battleUI.gameObject.name}");
        }
        if (battleUI == null)
        {
            battleUI = FindObjectOfType<BattleUIController>(true);
            if (enableDebugLogs && battleUI != null) Debug.Log($"[BattleManager] Found BattleUIController in scene: {battleUI.gameObject.name}");
        }

        // Choose a panel to toggle (explicit, or controller GameObject)
        if (battleUIPanel == null && battleUI != null)
        {
            battleUIPanel = battleUI.gameObject;
            if (enableDebugLogs) Debug.Log($"[BattleManager] Using battleUI.gameObject as battleUIPanel: {battleUIPanel.name}");
        }

        if (battleUI == null && enableDebugLogs)
        {
            Debug.LogWarning("[BattleManager] No BattleUIController found in scene.");
        }
    }

    private void EnsureAudioSources()
    {
        if (sfxSource == null)
        {
            Transform child = transform.Find("One Time Audio Source");
            if (child != null) sfxSource = child.GetComponent<AudioSource>();
        }
        if (sfxSource == null)
        {
            // fallback: any AudioSource that is not the battle or explore source
            AudioSource[] sources = GetComponentsInChildren<AudioSource>(true);
            foreach (var src in sources)
            {
                if (src != exploreMusicSource && src != battleMusicSource)
                {
                    sfxSource = src;
                    break;
                }
            }
        }

        if (sfxSource == null && enableDebugLogs)
        {
            Debug.LogWarning("[BattleManager] No SFX AudioSource assigned/found; attack sounds will not play.");
        }
        else if (enableDebugLogs && sfxSource.clip == null)
        {
            Debug.LogWarning($"[BattleManager] SFX AudioSource '{sfxSource.gameObject.name}' has no clip assigned; attack sounds will be silent.");
        }
        else if (enableDebugLogs)
        {
            Debug.Log($"[BattleManager] SFX AudioSource '{sfxSource.gameObject.name}' ready (volume {sfxSource.volume}, spatialBlend {sfxSource.spatialBlend}).");
        }
    }

    public void StartBattle(CaptureablePokemon playerOwned, CaptureablePokemon opponent)
    {
        if (IsInBattle) return;
        if (playerOwned == null || opponent == null) return;

        playerOwned.ResetForBattle();
        opponent.ResetForBattle();

        FaceEachOther(playerOwned.transform, opponent.transform);

        currentMode = GameMode.Battle;
        currentPhase = BattlePhase.Intro;
        playerPokemon = playerOwned;
        opponentPokemon = opponent;

        if (enableDebugLogs) Debug.Log($"[BattleManager] StartBattle: {playerOwned.pokemonName} (owned) vs {opponent.pokemonName} (wild)");

        // start battle music, stop explore music
        StopMusic(exploreMusicSource);
        PlayMusic(battleMusicSource, true);

        EnsureUIReferences();
        EnableBattleUI();

        if (battleTitleText != null)
        {
            battleTitleText.text = $"Battle: {playerOwned.pokemonName} vs {opponent.pokemonName}";
        }

        battleUI?.ShowIntro(playerPokemon, opponentPokemon);

        StartCoroutine(RunBattleIntro());
    }

    private IEnumerator RunBattleIntro()
    {
        if (turnDelaySeconds > 0f)
        {
            yield return new WaitForSeconds(turnDelaySeconds);
        }

        BeginPlayerTurn();
    }

    private void BeginPlayerTurn()
    {
        if (!IsInBattle) return;

        currentPhase = BattlePhase.PlayerTurn;
        battleUI?.ShowPrompt($"What should {playerPokemon?.pokemonName ?? "your Pokemon"} do?");
        waitingForMoveChoice = false;

        if (battleUI == null && autoBattleWhenNoUI)
        {
            PlayerChoosesAttack();
        }
    }

    public void PlayerOpenMoveMenu()
    {
        if (!IsInBattle || currentPhase != BattlePhase.PlayerTurn) return;
        waitingForMoveChoice = true;
        battleUI?.ShowMoveSelection(playerPokemon);
        if (enableDebugLogs) Debug.Log("[BattleManager] Player opened move menu.");
    }

    public void PlayerChoosesAttack(int moveIndex = -1, MoveDefinition chosenMove = null)
    {
        if (!IsInBattle || currentPhase != BattlePhase.PlayerTurn || isResolvingTurn) return;

        MoveDefinition moveToUse = chosenMove;
        if (moveIndex >= 0)
        {
            if (!playerPokemon.TryConsumePP(moveIndex, out moveToUse))
            {
                battleUI?.ShowMessage("No PP left for that move!");
                return;
            }
        }
        else
        {
            if (!TryChooseMoveForPokemon(playerPokemon, out moveIndex, out moveToUse))
            {
                battleUI?.ShowMessage("No moves available!");
                return;
            }
            playerPokemon.TryConsumePP(moveIndex, out moveToUse);
        }

        if (enableDebugLogs) Debug.Log($"[BattleManager] Player chose move {(moveToUse != null ? moveToUse.moveName : "null")} (index {moveIndex}).");

        waitingForMoveChoice = false;
        battleUI?.HideMoveSelection();
        StartCoroutine(ResolveAttack(playerPokemon, opponentPokemon, moveToUse, BeginOpponentTurn));
    }

    public void PlayerAttemptsCapture(float ballBonus = 1f)
    {
        if (!IsInBattle || currentPhase != BattlePhase.PlayerTurn || isResolvingTurn) return;

        if (!allowCaptureInBattle)
        {
            battleUI?.ShowMessage("Capturing is disabled in this battle.");
            return;
        }

        bool success = opponentPokemon.TryCapture(ballBonus, out float chanceUsed);

        if (success)
        {
            battleUI?.ShowMessage($"Captured {opponentPokemon.pokemonName}! (chance used {chanceUsed:P0})");
            DeclareVictory();
            return;
        }

        battleUI?.ShowMessage($"The wild {opponentPokemon.pokemonName} broke free (chance used {chanceUsed:P0}).");
        StartCoroutine(DelayThen(() => { HideMoveUI(); BeginOpponentTurn(); }));
    }

    public void PlayerRunsFromBattle()
    {
        if (!IsInBattle) return;
        battleUI?.ShowMessage("You fled from battle.");
        EndBattle();
    }

    private IEnumerator ResolveAttack(CaptureablePokemon attacker, CaptureablePokemon defender, MoveDefinition move, System.Action onComplete)
    {
        isResolvingTurn = true;

        MoveResult result = ExecuteMove(attacker, defender, move);
        if (enableDebugLogs)
        {
            Debug.Log($"[BattleManager] {attacker?.pokemonName} used {move?.moveName ?? "null"} -> hit:{result.hit} dmg:{result.damage} fainted:{result.targetFainted}");
        }
        if (sfxSource != null && sfxSource.clip != null)
        {
            sfxSource.PlayOneShot(sfxSource.clip);
        }
        else if (enableDebugLogs)
        {
            Debug.LogWarning("[BattleManager] Attack sound not played: missing sfxSource or clip.");
        }
        bool isOpponentAttack = attacker == opponentPokemon;
        battleUI?.ReportMove(attacker, defender, move, result, isOpponentAttack);
        battleUI?.RefreshHealth(playerPokemon, opponentPokemon);

        if (turnDelaySeconds > 0f)
        {
            yield return new WaitForSeconds(turnDelaySeconds);
        }

        if (CheckForBattleEnd())
        {
            isResolvingTurn = false;
            yield break;
        }

        HideMoveUI();
        isResolvingTurn = false;
        onComplete?.Invoke();
    }

    private IEnumerator DelayThen(System.Action next)
    {
        if (turnDelaySeconds > 0f)
        {
            yield return new WaitForSeconds(turnDelaySeconds);
        }

        next?.Invoke();
    }

    private MoveResult ExecuteMove(CaptureablePokemon attacker, CaptureablePokemon defender, MoveDefinition move)
    {
        if (move == null)
        {
            move = fallbackMove;
        }

        if (move == null)
        {
            if (enableDebugLogs) Debug.LogWarning("[BattleManager] No move available; dealing 1 damage.");
            defender.ApplyDamage(1);
            return new MoveResult { hit = true, damage = 1, targetFainted = defender.IsFainted };
        }

        bool hit = Random.value <= move.accuracy;
        if (!hit)
        {
            if (enableDebugLogs) Debug.Log($"[BattleManager] {attacker?.pokemonName} missed with {move.moveName}.");
            return new MoveResult { hit = false, damage = 0, targetFainted = false };
        }

        int damage = ComputeDamage(attacker, defender, move.power);
        defender.ApplyDamage(damage);

        return new MoveResult { hit = true, damage = damage, targetFainted = defender.IsFainted };
    }

    private int ComputeDamage(CaptureablePokemon attacker, CaptureablePokemon defender, int movePower)
    {
        int power = Mathf.Max(1, movePower);
        float raw = power + attacker.Attack * 0.8f - defender.Defense * 0.35f;
        int damage = Mathf.Max(1, Mathf.RoundToInt(raw));

        if (enableDebugLogs)
        {
            Debug.Log($"[BattleManager] {attacker.pokemonName} hits {defender.pokemonName} for {damage} (power {power}).");
        }

        return damage;
    }

    private bool TryChooseMoveForPokemon(CaptureablePokemon pokemon, out int moveIndex, out MoveDefinition move)
    {
        moveIndex = -1;
        move = fallbackMove;
        if (pokemon == null)
        {
            return move != null;
        }

        MoveDefinition[] moves = pokemon.Moves;
        if (moves != null && moves.Length > 0)
        {
            // collect non-null moves
            int usableCount = 0;
            int[] usableIndices = new int[moves.Length];
            for (int i = 0; i < moves.Length; i++)
            {
                if (moves[i] != null)
                {
                    usableIndices[usableCount++] = i;
                }
            }

            if (usableCount > 0)
            {
                int choice = Random.Range(0, usableCount);
                moveIndex = usableIndices[choice];
                move = moves[moveIndex];
                if (enableDebugLogs) Debug.Log($"[BattleManager] Chose move '{move.moveName}' (index {moveIndex}) for {pokemon.pokemonName}.");
                return true;
            }
        }

        return move != null;
    }

    private void BeginOpponentTurn()
    {
        if (!IsInBattle || isResolvingTurn) return;
        if (opponentPokemon == null || opponentPokemon.IsFainted || opponentPokemon.isCaptured)
        {
            DeclareVictory();
            return;
        }

        currentPhase = BattlePhase.OpponentTurn;

        if (!TryChooseMoveForPokemon(opponentPokemon, out int moveIndex, out MoveDefinition move))
        {
            move = fallbackMove;
        }

        if (moveIndex >= 0)
        {
            opponentPokemon.TryConsumePP(moveIndex, out move);
        }

        if (enableDebugLogs) Debug.Log($"[BattleManager] Opponent turn using move {(move != null ? move.moveName : "null")} (index {moveIndex}).");

        StartCoroutine(ResolveAttack(opponentPokemon, playerPokemon, move, BeginPlayerTurn));
    }

    private bool CheckForBattleEnd()
    {
        if (opponentPokemon == null || opponentPokemon.IsFainted || opponentPokemon.isCaptured)
        {
            DeclareVictory();
            return true;
        }

        if (playerPokemon == null || playerPokemon.IsFainted)
        {
            DeclareDefeat();
            return true;
        }

        return false;
    }

    private void DeclareVictory()
    {
        currentPhase = BattlePhase.Victory;
        if (enableDebugLogs) Debug.Log("[BattleManager] Victory!");
        battleUI?.ShowMessage($"You won! {opponentPokemon?.pokemonName ?? "Opponent"} is out of the fight.");
        if (opponentPokemon != null && opponentPokemon.IsFainted)
        {
            opponentPokemon.Faint();
        }
        EndBattle();
    }

    private void DeclareDefeat()
    {
        currentPhase = BattlePhase.Defeat;
        if (enableDebugLogs) Debug.Log("[BattleManager] Defeat.");
        battleUI?.ShowMessage($"{playerPokemon?.pokemonName ?? "Your Pokemon"} fainted!");
        if (playerPokemon != null && playerPokemon.IsFainted)
        {
            playerPokemon.Faint();
        }
        EndBattle();
    }

    public void EndBattle()
    {
        currentMode = GameMode.Explore;
        currentPhase = BattlePhase.Cleanup;
        if (enableDebugLogs) Debug.Log("[BattleManager] EndBattle");

        playerPokemon = null;
        opponentPokemon = null;

        DisableBattleUI();

        currentPhase = BattlePhase.Idle;

        if (battleUI != null)
        {
            battleUI.Clear();
            battleUI.HideMoveSelection();
            battleUI.HideHPPanels();
        }

        // stop battle music, start explore music
        StopMusic(battleMusicSource);
        PlayMusic(exploreMusicSource, true);
    }

    private void FaceEachOther(Transform a, Transform b)
    {
        if (a == null || b == null) return;
        Vector3 dirToB = b.position - a.position;
        Vector3 dirToA = a.position - b.position;

        dirToB.y = 0;
        dirToA.y = 0;

        if (dirToB.sqrMagnitude > 0.001f)
        {
            a.rotation = Quaternion.LookRotation(dirToB.normalized, Vector3.up);
        }

        if (dirToA.sqrMagnitude > 0.001f)
        {
            b.rotation = Quaternion.LookRotation(dirToA.normalized, Vector3.up);
        }
    }

    private void HideMoveUI()
    {
        battleUI?.HideMoveSelection();
    }

    private void PlayMusic(AudioSource source, bool loop)
    {
        if (source == null) return;
        if (source.clip == null)
        {
            if (enableDebugLogs) Debug.LogWarning("[BattleManager] PlayMusic called but AudioSource has no clip assigned.");
            return;
        }
        source.loop = loop;
        if (!source.isPlaying)
        {
            source.Play();
        }
    }

    private void StopMusic(AudioSource source)
    {
        if (source == null) return;
        source.Stop();
    }

    private void EnableBattleUI()
    {
        if (battleUI != null)
        {
            if (enableDebugLogs) Debug.Log($"[BattleManager] Enabling battleUI root '{battleUI.gameObject.name}'");
            battleUI.gameObject.SetActive(true);
        }
        if (battleUIPanel != null && battleUIPanel != battleUI?.gameObject)
        {
            if (enableDebugLogs) Debug.Log($"[BattleManager] Enabling battleUIPanel '{battleUIPanel.name}'");
            battleUIPanel.SetActive(true);
        }
        else if (battleUI == null && battleUIPanel == null && enableDebugLogs)
        {
            Debug.LogWarning("[BattleManager] No battle UI to enable.");
        }
    }

    private void DisableBattleUI()
    {
        if (battleUIPanel != null && battleUIPanel != battleUI?.gameObject)
        {
            if (enableDebugLogs) Debug.Log($"[BattleManager] Disabling battleUIPanel '{battleUIPanel.name}'");
            battleUIPanel.SetActive(false);
        }
        if (battleUI != null)
        {
            if (enableDebugLogs) Debug.Log($"[BattleManager] Disabling battleUI root '{battleUI.gameObject.name}'");
            battleUI.gameObject.SetActive(false);
        }
    }
}
