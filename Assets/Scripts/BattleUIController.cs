using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class BattleUIController : MonoBehaviour
{
    [Header("Name/HP")]
    public TMP_Text playerNameText;
    public TMP_Text opponentNameText;
    public Slider playerHealthBar;
    public Slider opponentHealthBar;
    public TMP_Text playerHpLabel;
    public TMP_Text opponentHpLabel;
    public TMP_Text playerNameSecondary;
    public TMP_Text opponentNameSecondary;
    public Image playerHPFill;
    public Image opponentHPFill;
    public HPBar playerHPBar;
    public HPBar opponentHPBar;
    public GameObject userHPPanel;
    public GameObject wildHPPanel;
    [Header("HP Follow")]
    public Vector3 userHpOffset = new Vector3(0f, 1.4f, 0f);
    public Vector3 wildHpOffset = new Vector3(0f, 1.4f, 0f);
    public UIFollowTarget userHpFollow;
    public UIFollowTarget wildHpFollow;
    [Header("HP Fill Smoothing")]
    public bool smoothHPFill = false;
    public float hpFillSmoothSpeed = 8f;

    [Header("Messaging")]
    public TMP_Text messageText;
    public TMP_Text ppScreenText;
    public GameObject ppPanel;

    [Header("Panels")]
    [Tooltip("Root panel for the main battle overlay.")]
    public GameObject mainPanel;
    [Tooltip("Panel containing move buttons.")]
    public GameObject movePanel;
    [Tooltip("Automatically try to locate UI references by name if fields are unassigned.")]
    public bool autoWireIfMissing = true;

    [Header("Buttons")]
    public Button attackButton;
    public Button captureButton;
    public Button runButton;
    public Button moveButton1;
    public Button moveButton2;
    public Button moveButton3;
    public Button moveButton4;
    public TMP_Text moveButton1Label;
    public TMP_Text moveButton2Label;
    public TMP_Text moveButton3Label;
    public TMP_Text moveButton4Label;

    private BattleManager _manager;
    private float _playerTargetFill = 1f;
    private float _opponentTargetFill = 1f;

    public void Bind(BattleManager manager)
    {
        _manager = manager;
        if (autoWireIfMissing)
        {
            AutoWireIfNeeded();
        }
        HookButtons();
    }

    public void Unbind()
    {
        UnhookButtons();
        _manager = null;
    }

    public void ShowIntro(CaptureablePokemon player, CaptureablePokemon opponent)
    {
        if (autoWireIfMissing)
        {
            AutoWireIfNeeded();
        }
        AssignHpTargets(player != null ? player.transform : null, opponent != null ? opponent.transform : null);
        SetNames(player, opponent);
        RefreshHealth(player, opponent);
        ShowMessage($"Wild {opponent?.pokemonName ?? "Pokemon"} appeared!");
        HideMoveSelection();
        if (mainPanel != null) mainPanel.SetActive(true);
        ShowHPPanels();
    }

    public void RefreshHealth(CaptureablePokemon player, CaptureablePokemon opponent)
    {
        if (playerHealthBar != null && player != null)
        {
            playerHealthBar.maxValue = player.MaxHP;
            playerHealthBar.value = player.CurrentHP;
        }
        if (playerHPBar != null && player != null)
        {
            playerHPBar.SetHP(player.CurrentHP, player.MaxHP);
        }

        if (opponentHealthBar != null && opponent != null)
        {
            opponentHealthBar.maxValue = opponent.MaxHP;
            opponentHealthBar.value = opponent.CurrentHP;
        }
        if (opponentHPBar != null && opponent != null)
        {
            opponentHPBar.SetHP(opponent.CurrentHP, opponent.MaxHP);
        }

        if (playerHpLabel != null && player != null)
        {
            playerHpLabel.text = $"{player.CurrentHP}/{player.MaxHP}";
        }

        if (opponentHpLabel != null && opponent != null)
        {
            opponentHpLabel.text = $"{opponent.CurrentHP}/{opponent.MaxHP}";
        }

        if (playerHPFill != null && player != null)
        {
            float fill = Mathf.Clamp01((float)player.CurrentHP / player.MaxHP);
            _playerTargetFill = fill;
            if (!smoothHPFill) playerHPFill.fillAmount = fill;
        }

        if (opponentHPFill != null && opponent != null)
        {
            float fill = Mathf.Clamp01((float)opponent.CurrentHP / opponent.MaxHP);
            _opponentTargetFill = fill;
            if (!smoothHPFill) opponentHPFill.fillAmount = fill;
        }
    }

    public void ReportMove(CaptureablePokemon attacker, CaptureablePokemon defender, MoveDefinition move, MoveResult result, bool isOpponentAttack = false)
    {
        if (messageText == null) return;

        string moveName = move?.moveName ?? "move";
        string prefix = isOpponentAttack ? "Wild " : string.Empty;
        ShowMessage($"{prefix}{(attacker?.pokemonName ?? "Pokemon")} used {moveName}!");
    }

    public void ShowMessage(string text)
    {
        if (messageText != null)
        {
            messageText.text = text;
        }
        if (ppScreenText != null)
        {
            ppScreenText.text = text;
        }
    }

    public void ShowPrompt(string prompt)
    {
        ShowMessage(prompt);
    }

    public void Clear()
    {
        if (messageText != null)
        {
            messageText.text = string.Empty;
        }
        if (ppScreenText != null)
        {
            ppScreenText.text = string.Empty;
        }
        ClearHpTargets();
        HideMoveSelection();
    }

    private void SetNames(CaptureablePokemon player, CaptureablePokemon opponent)
    {
        string playerLabel = player != null ? $"{player.pokemonName}   Lv:{player.level}" : "--";
        string opponentLabel = opponent != null ? $"{opponent.pokemonName}   Lv:{opponent.level}" : "--";

        if (playerNameText != null)
        {
            playerNameText.text = playerLabel;
        }

        if (opponentNameText != null)
        {
            opponentNameText.text = opponentLabel;
        }

        if (playerNameSecondary != null)
        {
            playerNameSecondary.text = playerLabel;
        }

        if (opponentNameSecondary != null)
        {
            opponentNameSecondary.text = opponentLabel;
        }
    }

    public void ShowMoveSelection(CaptureablePokemon player)
    {
        if (autoWireIfMissing)
        {
            AutoWireIfNeeded();
        }
        if (mainPanel != null) mainPanel.SetActive(true);
        if (movePanel != null) movePanel.SetActive(true);
        if (ppPanel != null) ppPanel.SetActive(true);
        PopulateMoveButtons(player);
        UpdatePPInfo(player, FindFirstUsableMoveIndex(player));
        ShowMessage("Choose a move");
    }

    public void HideMoveSelection()
    {
        if (movePanel != null) movePanel.SetActive(false);
        if (ppPanel != null) ppPanel.SetActive(false);
    }

    public void HideHPPanels()
    {
        if (userHPPanel != null) userHPPanel.SetActive(false);
        if (wildHPPanel != null) wildHPPanel.SetActive(false);
        if (userHpFollow != null) userHpFollow.enabled = false;
        if (wildHpFollow != null) wildHpFollow.enabled = false;
    }

    public void ShowHPPanels()
    {
        if (userHPPanel != null) userHPPanel.SetActive(true);
        if (wildHPPanel != null) wildHPPanel.SetActive(true);
        if (userHpFollow != null) userHpFollow.enabled = true;
        if (wildHpFollow != null) wildHpFollow.enabled = true;
    }

    private void PopulateMoveButtons(CaptureablePokemon player)
    {
        MoveDefinition[] moves = player != null ? player.Moves : null;
        SetMoveButton(moveButton1, moveButton1Label, moves, 0, player);
        SetMoveButton(moveButton2, moveButton2Label, moves, 1, player);
        SetMoveButton(moveButton3, moveButton3Label, moves, 2, player);
        SetMoveButton(moveButton4, moveButton4Label, moves, 3, player);
    }

    private void SetMoveButton(Button button, TMP_Text label, MoveDefinition[] moves, int index, CaptureablePokemon player)
    {
        if (button == null && label == null) return;

        if (moves != null && index < moves.Length && moves[index] != null)
        {
            string moveName = moves[index].moveName;
            if (label != null) label.text = moveName;
            if (button != null)
            {
                int currentPP = player != null ? player.GetCurrentPP(index) : moves[index].maxPP;
                button.interactable = currentPP > 0;
            }
        }
        else
        {
            if (label != null) label.text = "--";
            if (button != null) button.interactable = false;
        }
    }

    public void AssignHpTargets(Transform player, Transform wild)
    {
        if (userHpFollow == null && userHPPanel != null)
        {
            userHpFollow = userHPPanel.GetComponent<UIFollowTarget>();
            if (userHpFollow == null) userHpFollow = userHPPanel.AddComponent<UIFollowTarget>();
        }
        if (wildHpFollow == null && wildHPPanel != null)
        {
            wildHpFollow = wildHPPanel.GetComponent<UIFollowTarget>();
            if (wildHpFollow == null) wildHpFollow = wildHPPanel.AddComponent<UIFollowTarget>();
        }

        if (userHpFollow != null)
        {
            userHpFollow.target = player;
            userHpFollow.offset = userHpOffset;
        }
        if (wildHpFollow != null)
        {
            wildHpFollow.target = wild;
            wildHpFollow.offset = wildHpOffset;
        }
    }

    public void ClearHpTargets()
    {
        if (userHpFollow != null) userHpFollow.target = null;
        if (wildHpFollow != null) wildHpFollow.target = null;
    }

    private void HookButtons()
    {
        if (attackButton != null)
        {
            attackButton.onClick.AddListener(OnAttackClicked);
        }

        if (captureButton != null)
        {
            captureButton.onClick.AddListener(OnCaptureClicked);
        }

        if (runButton != null)
        {
            runButton.onClick.AddListener(OnRunClicked);
        }

        if (moveButton1 != null) moveButton1.onClick.AddListener(() => OnMoveClicked(0));
        if (moveButton2 != null) moveButton2.onClick.AddListener(() => OnMoveClicked(1));
        if (moveButton3 != null) moveButton3.onClick.AddListener(() => OnMoveClicked(2));
        if (moveButton4 != null) moveButton4.onClick.AddListener(() => OnMoveClicked(3));

        HookHover(moveButton1, 0);
        HookHover(moveButton2, 1);
        HookHover(moveButton3, 2);
        HookHover(moveButton4, 3);
    }

    private void UnhookButtons()
    {
        if (attackButton != null)
        {
            attackButton.onClick.RemoveListener(OnAttackClicked);
        }

        if (captureButton != null)
        {
            captureButton.onClick.RemoveListener(OnCaptureClicked);
        }

        if (runButton != null)
        {
            runButton.onClick.RemoveListener(OnRunClicked);
        }

        if (moveButton1 != null) moveButton1.onClick.RemoveAllListeners();
        if (moveButton2 != null) moveButton2.onClick.RemoveAllListeners();
        if (moveButton3 != null) moveButton3.onClick.RemoveAllListeners();
        if (moveButton4 != null) moveButton4.onClick.RemoveAllListeners();
    }

    private void OnAttackClicked()
    {
        _manager?.PlayerOpenMoveMenu();
    }

    private void OnCaptureClicked()
    {
        _manager?.PlayerAttemptsCapture();
    }

    private void OnRunClicked()
    {
        _manager?.PlayerRunsFromBattle();
    }

    private void OnMoveClicked(int index)
    {
        UpdatePPInfo(_manager?.playerPokemon, index);
        _manager?.PlayerChoosesAttack(index);
    }

    private void OnMoveHover(int index)
    {
        UpdatePPInfo(_manager?.playerPokemon, index);
    }

    private void UpdatePPInfo(CaptureablePokemon player, int moveIndex)
    {
        if (ppScreenText == null || player == null) return;
        MoveDefinition move = player.GetMove(moveIndex);
        if (move == null)
        {
            ppScreenText.text = "PP --/--\nTYPE/Normal";
            return;
        }

        int currentPP = player.GetCurrentPP(moveIndex);
        int maxPP = Mathf.Max(1, move.maxPP);
        ppScreenText.text = $"PP {currentPP}/{maxPP}\nTYPE/Normal";
    }

    private int FindFirstUsableMoveIndex(CaptureablePokemon player)
    {
        if (player == null || player.Moves == null) return -1;
        for (int i = 0; i < player.Moves.Length; i++)
        {
            if (player.Moves[i] != null && player.GetCurrentPP(i) > 0)
            {
                return i;
            }
        }
        return -1;
    }

    private void AutoWireIfNeeded()
    {
        Transform root = transform;
        // Try to resolve everything under this controller first; if missing, fall back to scene-wide lookup.
        Transform sceneRoot = null;

        if (mainPanel == null)
        {
            mainPanel = FindByName(root, "Battle Overlay Screen");
        }
        if (movePanel == null)
        {
            movePanel = FindByName(root, "Move Screen");
        }
        if (ppPanel == null)
        {
            ppPanel = FindByName(root, "PP Screen");
        }

        Transform mainRoot = mainPanel != null ? mainPanel.transform : root;
        Transform moveRoot = movePanel != null ? movePanel.transform : root;
        Transform ppRoot = ppPanel != null ? ppPanel.transform : moveRoot;

        if (attackButton == null) attackButton = FindByName<Button>(mainRoot, "Fight Button");
        if (captureButton == null) captureButton = FindByName<Button>(mainRoot, "Bag Button");
        if (runButton == null) runButton = FindByName<Button>(mainRoot, "Run Button");

        if (messageText == null) messageText = FindByName<TMP_Text>(mainRoot, "Dialog Text");
        if (ppScreenText == null) ppScreenText = FindByName<TMP_Text>(ppRoot, "PP dialog");
        if (ppScreenText == null) ppScreenText = FindByName<TMP_Text>(ppRoot, "PP Dialog");

        if (playerNameText == null) playerNameText = FindByName<TMP_Text>(mainRoot, "Player Name");
        if (opponentNameText == null) opponentNameText = FindByName<TMP_Text>(mainRoot, "Opponent Name");
        if (playerHpLabel == null) playerHpLabel = FindByName<TMP_Text>(mainRoot, "Player HP Label");
        if (opponentHpLabel == null) opponentHpLabel = FindByName<TMP_Text>(mainRoot, "Opponent HP Label");
        if (playerHealthBar == null) playerHealthBar = FindByName<Slider>(mainRoot, "Player HP Slider");
        if (opponentHealthBar == null) opponentHealthBar = FindByName<Slider>(mainRoot, "Opponent HP Slider");
        if (playerNameSecondary == null) playerNameSecondary = FindByName<TMP_Text>(root, "User Pokemon Name");
        if (opponentNameSecondary == null) opponentNameSecondary = FindByName<TMP_Text>(root, "Wild Pokemon Name");
        if (playerHPFill == null) playerHPFill = FindHPFill(root, "User Pokemon HP");
        if (opponentHPFill == null) opponentHPFill = FindHPFill(root, "Wild Pokemon HP");
        if (userHPPanel == null) userHPPanel = FindByName(root, "User Pokemon HP");
        if (wildHPPanel == null) wildHPPanel = FindByName(root, "Wild Pokemon HP");
        if (playerHealthBar == null) playerHealthBar = FindByName<Slider>(root, "User Slider");
        if (opponentHealthBar == null) opponentHealthBar = FindByName<Slider>(root, "Wild Slider");
        if (playerHPBar == null && userHPPanel != null) playerHPBar = userHPPanel.GetComponentInChildren<HPBar>(true);
        if (opponentHPBar == null && wildHPPanel != null) opponentHPBar = wildHPPanel.GetComponentInChildren<HPBar>(true);

        // Scene-wide fallbacks if panels are on a separate world-space canvas
        if (playerNameSecondary == null) playerNameSecondary = FindByNameInScene<TMP_Text>("User Pokemon Name");
        if (opponentNameSecondary == null) opponentNameSecondary = FindByNameInScene<TMP_Text>("Wild Pokemon Name");
        if (userHPPanel == null) userHPPanel = FindByNameInScene("User Pokemon HP");
        if (wildHPPanel == null) wildHPPanel = FindByNameInScene("Wild Pokemon HP");
        if (playerHealthBar == null && userHPPanel != null) playerHealthBar = FindByName<Slider>(userHPPanel.transform, "User Slider");
        if (opponentHealthBar == null && wildHPPanel != null) opponentHealthBar = FindByName<Slider>(wildHPPanel.transform, "Wild Slider");
        if (playerHPFill == null && userHPPanel != null) playerHPFill = FindHPFill(userHPPanel.transform, userHPPanel.name);
        if (opponentHPFill == null && wildHPPanel != null) opponentHPFill = FindHPFill(wildHPPanel.transform, wildHPPanel.name);
        if (playerHPBar == null && userHPPanel != null) playerHPBar = userHPPanel.GetComponentInChildren<HPBar>(true);
        if (opponentHPBar == null && wildHPPanel != null) opponentHPBar = wildHPPanel.GetComponentInChildren<HPBar>(true);

        // If world-space bars exist, prefer those over the screen-space sliders/fills.
        if (userHPPanel != null)
        {
            var worldSlider = FindByName<Slider>(userHPPanel.transform, "User Slider");
            if (worldSlider != null) playerHealthBar = worldSlider;
            var worldFill = FindHPFill(userHPPanel.transform, userHPPanel.name);
            if (worldFill != null) playerHPFill = worldFill;
            var hpBar = userHPPanel.GetComponentInChildren<HPBar>(true);
            if (hpBar != null) playerHPBar = hpBar;
        }
        if (wildHPPanel != null)
        {
            var worldSlider = FindByName<Slider>(wildHPPanel.transform, "Wild Slider");
            if (worldSlider != null) opponentHealthBar = worldSlider;
            var worldFill = FindHPFill(wildHPPanel.transform, wildHPPanel.name);
            if (worldFill != null) opponentHPFill = worldFill;
            var hpBar = wildHPPanel.GetComponentInChildren<HPBar>(true);
            if (hpBar != null) opponentHPBar = hpBar;
        }

        if (userHpFollow == null && userHPPanel != null) userHpFollow = userHPPanel.GetComponent<UIFollowTarget>();
        if (wildHpFollow == null && wildHPPanel != null) wildHpFollow = wildHPPanel.GetComponent<UIFollowTarget>();

        if (moveButton1 == null) moveButton1 = FindByName<Button>(moveRoot, "Fight Button");
        if (moveButton2 == null) moveButton2 = FindByName<Button>(moveRoot, "Bag Button");
        if (moveButton3 == null) moveButton3 = FindByName<Button>(moveRoot, "Pokemon Button");
        if (moveButton4 == null) moveButton4 = FindByName<Button>(moveRoot, "Run Button");

        if (moveButton1Label == null && moveButton1 != null) moveButton1Label = moveButton1.GetComponentInChildren<TMP_Text>();
        if (moveButton2Label == null && moveButton2 != null) moveButton2Label = moveButton2.GetComponentInChildren<TMP_Text>();
        if (moveButton3Label == null && moveButton3 != null) moveButton3Label = moveButton3.GetComponentInChildren<TMP_Text>();
        if (moveButton4Label == null && moveButton4 != null) moveButton4Label = moveButton4.GetComponentInChildren<TMP_Text>();
    }

    private void Update()
    {
        if (!smoothHPFill) return;
        if (playerHPFill != null && !Mathf.Approximately(playerHPFill.fillAmount, _playerTargetFill))
        {
            playerHPFill.fillAmount = Mathf.MoveTowards(playerHPFill.fillAmount, _playerTargetFill, hpFillSmoothSpeed * Time.deltaTime);
        }
        if (opponentHPFill != null && !Mathf.Approximately(opponentHPFill.fillAmount, _opponentTargetFill))
        {
            opponentHPFill.fillAmount = Mathf.MoveTowards(opponentHPFill.fillAmount, _opponentTargetFill, hpFillSmoothSpeed * Time.deltaTime);
        }
    }

    private GameObject FindByName(Transform root, string name)
    {
        Transform t = FindByNameTransform(root, name);
        return t != null ? t.gameObject : null;
    }

    private T FindByName<T>(Transform root, string name) where T : Component
    {
        Transform t = FindByNameTransform(root, name);
        return t != null ? t.GetComponent<T>() : null;
    }

    private Transform FindByNameTransform(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindByNameTransform(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    private void HookHover(Button button, int index)
    {
        if (button == null) return;
        EventTrigger trigger = button.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = button.gameObject.AddComponent<EventTrigger>();
        }

        if (trigger.triggers == null)
        {
            trigger.triggers = new System.Collections.Generic.List<EventTrigger.Entry>();
        }

        // Avoid piling up duplicate entries for the same index
        trigger.triggers.RemoveAll(e => e.eventID == EventTriggerType.PointerEnter);

        EventTrigger.Entry entry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerEnter
        };
        entry.callback.AddListener(_ => OnMoveHover(index));
        trigger.triggers.Add(entry);
    }

    private Image FindHPFill(Transform root, string parentName)
    {
        Transform parent = FindByNameTransform(root, parentName);
        if (parent == null) parent = root;

        // common naming variants
        Image img = FindByName<Image>(parent, "HP_Fill");
        if (img == null) img = FindByName<Image>(parent, "HP Fill");
        if (img == null) img = FindByName<Image>(parent, "Fill");
        if (img == null) img = FindByName<Image>(parent, "User Background");
        if (img == null) img = FindByName<Image>(parent, "Wild Background");

        // try inside sliders
        if (img == null)
        {
            var slider = parent.GetComponentInChildren<Slider>();
            if (slider != null)
            {
                img = FindByName<Image>(slider.transform, "Fill");
                if (img == null) img = FindByName<Image>(slider.transform, "Fill Area");
            }
        }

        // last resort: first Image under parent (excluding parent itself)
        if (img == null)
        {
            foreach (var image in parent.GetComponentsInChildren<Image>())
            {
                if (image.transform != parent) { img = image; break; }
            }
        }
        return img;
    }

    private GameObject FindByNameInScene(string name)
    {
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t.hideFlags != HideFlags.None) continue;
            if (t.name == name) return t.gameObject;
        }
        return null;
    }

    private T FindByNameInScene<T>(string name) where T : Component
    {
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t.hideFlags != HideFlags.None) continue;
            if (t.name == name)
            {
                T c = t.GetComponent<T>();
                if (c != null) return c;
            }
        }
        return null;
    }
}
