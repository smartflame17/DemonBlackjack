using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class TutorialScenarioController : MonoBehaviour, IBattleInputGate
{
    [SerializeField] private TutorialScenarioConfig scenarioConfig;
    [SerializeField] private RunManager runManager;
    [SerializeField] private BattleController battleController;
    [SerializeField] private TutorialInstructionOverlay instructionOverlay;
    [SerializeField] private bool autoStart = true;
    [SerializeField] private bool logDialogueLines = true;

    private TutorialScenarioConfig runtimeConfig;
    private ScopedEventBus battleBus;
    private readonly TutorialInputSequence inputSequence = new();
    private int currentRoundIndex = -1;
    private bool started;
    private bool allowRoundStart;
    private bool waitingForBustResolution;
    private bool secondRoundHandRefilled;
    private Canvas systemPreviewCanvas;
    private CanvasGroup systemPreviewCanvasGroup;
    private Vector3 systemPreviewOriginalScale;
    private bool systemPreviewScaleCaptured;

    public TutorialStepKey CurrentStepKey { get; private set; }
    public bool IsComplete { get; private set; }

    private void Awake()
    {
        if (runManager == null)
            runManager = FindFirstObjectByType<RunManager>();

        if (battleController == null)
            battleController = FindFirstObjectByType<BattleController>();

        if (instructionOverlay == null)
            instructionOverlay = GetComponent<TutorialInstructionOverlay>() ?? gameObject.AddComponent<TutorialInstructionOverlay>();
    }

    private IEnumerator Start()
    {
        yield return null;

        if (autoStart)
            StartTutorial();
    }

    private void OnDisable()
    {
        if (battleController != null && ReferenceEquals(battleController.InputGate, this))
            battleController.InputGate = null;

        HideSystemPreview();
        UnsubscribeFromBattle();
    }

    public void StartTutorial()
    {
        if (started || runManager == null || battleController == null)
            return;

        started = true;
        IsComplete = false;
        currentRoundIndex = -1;
        waitingForBustResolution = false;
        secondRoundHandRefilled = false;
        runtimeConfig = scenarioConfig != null ? scenarioConfig : TutorialScenarioConfig.CreateDefaultRuntime();
        HideGameplayCanvases();
        SetObjectActive("DevilImage", true);
        ShowStep(TutorialStepKey.SceneBoot);
    }

    public void ContinueTutorial()
    {
        switch (CurrentStepKey)
        {
            case TutorialStepKey.SceneBoot:
                ShowStep(TutorialStepKey.RuleIntro);
                break;
            case TutorialStepKey.RuleIntro:
                StartFirstRound();
                break;
            case TutorialStepKey.R1BustExplanation:
                ShowStep(TutorialStepKey.R1Stand);
                break;
            case TutorialStepKey.R1Settlement:
                StartSecondRound();
                break;
            case TutorialStepKey.R2PairExplanation:
                ShowStep(TutorialStepKey.R2PlayTripleFour);
                break;
            case TutorialStepKey.R2TripleExplanation:
                ShowStep(TutorialStepKey.R2RefillExplanation);
                break;
            case TutorialStepKey.R2RefillExplanation:
                ShowStep(TutorialStepKey.R2PlayFourOfAKind);
                break;
            case TutorialStepKey.R2FourOfAKindExplanation:
                ShowStep(TutorialStepKey.R2Stand);
                break;
            case TutorialStepKey.R2Settlement:
                SetObjectActive("RoundResultPanel", false);
                ShowStep(TutorialStepKey.FinalResponse);
                break;
            case TutorialStepKey.FinalResponse:
                ShowStep(TutorialStepKey.SystemPreview);
                break;
            case TutorialStepKey.SystemPreview:
                ShowStep(TutorialStepKey.Complete);
                break;
            case TutorialStepKey.Complete:
                CompleteTutorial();
                break;
        }
    }

    public void SelectTutorialChoice(int choiceIndex)
    {
        ContinueTutorial();
    }

    public bool CanStartRound(BattleState battle, int wager)
    {
        return allowRoundStart && started && !IsComplete;
    }

    public bool CanPlayCard(BattleState battle, int handIndex)
    {
        return IsCardPlayStep(CurrentStepKey)
            && inputSequence.CanPlayCard(battle?.CurrentRound?.PlayerHand, handIndex);
    }

    public bool CanHit(BattleState battle)
    {
        return !waitingForBustResolution
            && IsHitStep(CurrentStepKey)
            && inputSequence.CanHit(battle?.PlayerDrawPile);
    }

    public bool CanStand(BattleState battle)
    {
        return IsStandStep(CurrentStepKey) && inputSequence.CanStand();
    }

    public bool CanUseActiveItem(BattleState battle, string itemId)
    {
        return false;
    }

    public void NotifyStandAccepted(BattleState battle)
    {
        inputSequence.NotifyStandAccepted();
        CompleteOpponentResponseAfterStand(battle);
    }

    private void StartFirstRound()
    {
        if (runtimeConfig == null || !runtimeConfig.TryGetRound(0, out TutorialRoundSpec round))
            return;

        if (runManager.RunState == null)
            runManager.StartRun(runtimeConfig.Seed);

        currentRoundIndex = 0;
        inputSequence.BeginRound(round);
        var config = new BattleConfig(
            round.EncounterId,
            round.OpponentStartingMoney,
            new TutorialDevilStrategy(round.OpponentDrawValue, round.OpponentStandScore),
            round.PlayerStartingHandSize,
            round.TargetScore,
            round.BurstThreshold,
            round.BaseWager,
            round.DevilId,
            playerDeckOverride: round.CreatePlayerDeck(),
            opponentDeckOverride: round.CreateOpponentDeck());

        runManager.StartBattle(config);
        battleController.InputGate = this;
        SubscribeToBattle(battleController.BattleState);
        ShowBattleCanvas();
        ShowStep(TutorialStepKey.R1PlayThree);
        StartConfiguredRound(round);
    }

    private void StartSecondRound()
    {
        if (runtimeConfig == null || !runtimeConfig.TryGetRound(1, out TutorialRoundSpec round))
            return;

        BattleState existingBattle = battleController?.BattleState;
        if (existingBattle == null)
            return;

        battleController.CompletePendingVisualTransition();
        if (!ReferenceEquals(existingBattle, battleController.BattleState))
            return;

        currentRoundIndex = 1;
        secondRoundHandRefilled = false;
        inputSequence.BeginRound(round);
        ShowBattleCanvas();
        ShowStep(TutorialStepKey.R2HitFour);
        StartConfiguredRound(round);
    }

    private void StartConfiguredRound(TutorialRoundSpec round)
    {
        allowRoundStart = true;
        bool roundStarted = battleController.StartNextRound(round.BaseWager);
        allowRoundStart = false;

        if (!roundStarted)
            Debug.LogError("[Tutorial] Failed to start the configured round.", this);

        SetObjectActive("RoundResultPanel", false);
        HideBlockingGameplayPanels();
        RestoreMoneyLogLayout();
    }

    private void SubscribeToBattle(BattleState battle)
    {
        UnsubscribeFromBattle();
        battleBus = battle?.EventBus;
        if (battleBus == null)
            return;

        battleBus.Subscribe<CardPlayedEvent>(OnCardPlayed);
        battleBus.Subscribe<PlayerHitUsedEvent>(OnPlayerHitUsed);
        battleBus.Subscribe<PokerResolvedEvent>(OnPokerResolved);
        battleBus.Subscribe<HandRefilledEvent>(OnHandRefilled);
        battleBus.Subscribe<BurstOccurredEvent>(OnBurstOccurred);
        battleBus.Subscribe<RoundEndedEvent>(OnRoundEnded);
    }

    private void UnsubscribeFromBattle()
    {
        if (battleBus == null)
            return;

        battleBus.Unsubscribe<CardPlayedEvent>(OnCardPlayed);
        battleBus.Unsubscribe<PlayerHitUsedEvent>(OnPlayerHitUsed);
        battleBus.Unsubscribe<PokerResolvedEvent>(OnPokerResolved);
        battleBus.Unsubscribe<HandRefilledEvent>(OnHandRefilled);
        battleBus.Unsubscribe<BurstOccurredEvent>(OnBurstOccurred);
        battleBus.Unsubscribe<RoundEndedEvent>(OnRoundEnded);
        battleBus = null;
    }

    private void OnCardPlayed(CardPlayedEvent eventData)
    {
        if (eventData.Owner != Combatant.Player)
            return;

        inputSequence.NotifyCardPlayed(eventData.Card);
        if (CurrentStepKey == TutorialStepKey.R1PlayThree && eventData.Card.Rank == Rank.Three)
            ShowStep(TutorialStepKey.R1HitNine);
    }

    private void OnPlayerHitUsed(PlayerHitUsedEvent eventData)
    {
        inputSequence.NotifyPlayerHitUsed(eventData.Card);

        if (CurrentStepKey == TutorialStepKey.R1HitNine && eventData.Card.Rank == Rank.Nine)
        {
            ShowStep(TutorialStepKey.R1HitTen);
            return;
        }

        if (CurrentStepKey == TutorialStepKey.R1HitTen && eventData.Card.Rank == Rank.Ten)
        {
            waitingForBustResolution = true;
            return;
        }

        if (CurrentStepKey == TutorialStepKey.R2HitFour && eventData.Card.Rank == Rank.Four)
            ShowStep(TutorialStepKey.R2PlayPairFour);
    }

    private void OnBurstOccurred(BurstOccurredEvent eventData)
    {
        if (eventData.Combatant != Combatant.Player
            || CurrentStepKey != TutorialStepKey.R1HitTen
            || eventData.Score != 22)
            return;

        waitingForBustResolution = false;
        ShowStep(TutorialStepKey.R1BustExplanation);
    }

    private void OnPokerResolved(PokerResolvedEvent eventData)
    {
        if (eventData.Combatant != Combatant.Player)
            return;

        if (CurrentStepKey == TutorialStepKey.R2PlayPairFour
            && eventData.Poker.Rank == PokerHandRank.Pair)
        {
            ShowStep(TutorialStepKey.R2PairExplanation);
            return;
        }

        if (CurrentStepKey == TutorialStepKey.R2PlayTripleFour
            && eventData.Poker.Rank == PokerHandRank.ThreeOfAKind)
        {
            ShowStep(TutorialStepKey.R2TripleExplanation);
            return;
        }

        if (CurrentStepKey == TutorialStepKey.R2PlayFourOfAKind
            && eventData.Poker.Rank == PokerHandRank.FourOfAKind)
            ShowStep(TutorialStepKey.R2FourOfAKindExplanation);
    }

    private void OnHandRefilled(HandRefilledEvent eventData)
    {
        if (currentRoundIndex == 1
            && CurrentStepKey == TutorialStepKey.R2PlayTripleFour
            && eventData.HandCount == 3)
            secondRoundHandRefilled = true;
    }

    private void OnRoundEnded(RoundEndedEvent eventData)
    {
        HideBlockingGameplayPanels();

        if (currentRoundIndex == 0)
        {
            ShowStep(TutorialStepKey.R1Settlement);
            StartCoroutine(RestoreMoneyLogLayoutAfterRoundEnd());
            return;
        }

        if (currentRoundIndex == 1)
        {
            ShowStep(TutorialStepKey.R2Settlement);
            StartCoroutine(RestoreMoneyLogLayoutAfterRoundEnd());
        }
    }

    private void CompleteOpponentResponseAfterStand(BattleState battle)
    {
        int remainingTurns = 8;
        while (remainingTurns-- > 0
            && battle != null
            && battle.CurrentRound != null
            && battle.Phase == BattlePhase.PlayerPhase
            && battle.CurrentRound.PlayerStood)
        {
            if (!battle.TryStand())
                break;
        }
    }

    private void ShowStep(TutorialStepKey key)
    {
        if (runtimeConfig == null || !runtimeConfig.TryGetStep(key, out TutorialStepSpec step))
            step = TutorialScenarioConfig.CreateDefaultSteps().Find(candidate => candidate.Key == key);

        if (step == null)
            return;

        if (key == TutorialStepKey.R2RefillExplanation && !secondRoundHandRefilled)
            Debug.LogWarning("[Tutorial] Refill explanation opened before the expected hand refill event.", this);

        CurrentStepKey = key;
        LogStep(step);
        if (key == TutorialStepKey.SystemPreview)
            ShowSystemPreview();
        instructionOverlay?.Show(step, step.ActionText, ContinueTutorial, SelectTutorialChoice, step.GuideTarget);
        HideBlockingGameplayPanels();
    }

    private void CompleteTutorial()
    {
        IsComplete = true;
        if (battleController != null && ReferenceEquals(battleController.InputGate, this))
            battleController.InputGate = null;
        HideSystemPreview();
        instructionOverlay?.Hide();
    }

    private static bool IsCardPlayStep(TutorialStepKey key)
    {
        return key == TutorialStepKey.R1PlayThree
            || key == TutorialStepKey.R2PlayPairFour
            || key == TutorialStepKey.R2PlayTripleFour
            || key == TutorialStepKey.R2PlayFourOfAKind;
    }

    private static bool IsHitStep(TutorialStepKey key)
    {
        return key == TutorialStepKey.R1HitNine
            || key == TutorialStepKey.R1HitTen
            || key == TutorialStepKey.R2HitFour;
    }

    private static bool IsStandStep(TutorialStepKey key)
    {
        return key == TutorialStepKey.R1Stand || key == TutorialStepKey.R2Stand;
    }

    private static void HideGameplayCanvases()
    {
        SetCanvasActive("MenuCanvas", false);
        SetCanvasActive("MapUI", false);
        SetCanvasActive("ShopUI", false);
        SetCanvasActive("BattleUI", false);
    }

    private static void ShowBattleCanvas()
    {
        SetCanvasActive("MenuCanvas", false);
        SetCanvasActive("MapUI", false);
        SetCanvasActive("ShopUI", false);
        SetCanvasActive("BattleUI", true);
    }

    private static void HideBlockingGameplayPanels()
    {
        SetObjectActive("RoundStartPanel", false);
        SetObjectActive("BattleResultPanel", false);
        SetObjectActive("ToShopButton", false);
        SetObjectActive("ContinueBattleButton", false);
    }

    private void ShowSystemPreview()
    {
        SetCanvasActive("BattleUI", false);
        SetCanvasActive("MapUI", false);
        SetCanvasActive("MenuCanvas", true);

        systemPreviewCanvas = FindSceneCanvas("ShopUI");
        if (systemPreviewCanvas == null)
            return;

        if (!systemPreviewScaleCaptured)
        {
            systemPreviewOriginalScale = systemPreviewCanvas.transform.localScale;
            systemPreviewScaleCaptured = true;
        }

        systemPreviewCanvas.transform.localScale = Vector3.one;
        systemPreviewCanvas.gameObject.SetActive(true);
        systemPreviewCanvasGroup = systemPreviewCanvas.GetComponent<CanvasGroup>();
        if (systemPreviewCanvasGroup != null)
        {
            systemPreviewCanvasGroup.alpha = 1f;
            systemPreviewCanvasGroup.interactable = false;
            systemPreviewCanvasGroup.blocksRaycasts = false;
        }

        SetDescendantActive(systemPreviewCanvas.transform, "MainPanel", true);
        SetDescendantActive(systemPreviewCanvas.transform, "UpgradePanel", false);
        SetDescendantActive(systemPreviewCanvas.transform, "ActiveItemPanel", false);
        SetDescendantActive(systemPreviewCanvas.transform, "RelicPanel", false);
        SetDescendantActive(systemPreviewCanvas.transform, "NavigationDialoguePanel", false);
        SetDescendantActive(systemPreviewCanvas.transform, "ShopNPC", false);
        SetDescendantActive(systemPreviewCanvas.transform, "ContinueButton", false);

        Button[] buttons = systemPreviewCanvas.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
            buttons[i].interactable = false;

        Canvas menuCanvas = FindSceneCanvas("MenuCanvas");
        if (menuCanvas != null)
        {
            for (int i = 0; i < menuCanvas.transform.childCount; i++)
            {
                Transform child = menuCanvas.transform.GetChild(i);
                child.gameObject.SetActive(child.name == "TopMenuBarPanel");
            }

            Button[] menuButtons = menuCanvas.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < menuButtons.Length; i++)
                menuButtons[i].interactable = false;
        }
    }

    private void HideSystemPreview()
    {
        if (systemPreviewCanvas == null)
            systemPreviewCanvas = FindSceneCanvas("ShopUI");

        if (systemPreviewCanvas != null)
        {
            systemPreviewCanvas.gameObject.SetActive(false);
            if (systemPreviewScaleCaptured)
                systemPreviewCanvas.transform.localScale = systemPreviewOriginalScale;
        }

        SetCanvasActive("MenuCanvas", false);
    }

    private IEnumerator RestoreMoneyLogLayoutAfterRoundEnd()
    {
        yield return null;
        RestoreMoneyLogLayout();
    }

    private static void RestoreMoneyLogLayout()
    {
        RectTransform moneyLog = FindSceneRect("MoneyLog");
        RectTransform defaultPosition = FindSceneRect("MoneyLogTransform");
        if (moneyLog == null || defaultPosition == null)
            return;

        DG.Tweening.DOTween.Kill(moneyLog);
        moneyLog.anchoredPosition = defaultPosition.anchoredPosition;
        moneyLog.localScale = defaultPosition.localScale;
    }

    private static void SetCanvasActive(string canvasName, bool active)
    {
        Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i].gameObject.scene.IsValid() && canvases[i].gameObject.name == canvasName)
                canvases[i].gameObject.SetActive(active);
        }
    }

    private static Canvas FindSceneCanvas(string canvasName)
    {
        Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i].gameObject.scene.IsValid() && canvases[i].gameObject.name == canvasName)
                return canvases[i];
        }

        return null;
    }

    private static void SetDescendantActive(Transform root, string objectName, bool active)
    {
        if (root == null)
            return;

        Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            if (descendants[i].name == objectName)
                descendants[i].gameObject.SetActive(active);
        }
    }

    private static void SetObjectActive(string objectName, bool active)
    {
        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i].gameObject.scene.IsValid() && transforms[i].gameObject.name == objectName)
                transforms[i].gameObject.SetActive(active);
        }
    }

    private static RectTransform FindSceneRect(string objectName)
    {
        RectTransform[] rects = Resources.FindObjectsOfTypeAll<RectTransform>();
        for (int i = 0; i < rects.Length; i++)
        {
            if (rects[i].gameObject.scene.IsValid() && rects[i].name == objectName)
                return rects[i];
        }

        return null;
    }

    private void LogStep(TutorialStepSpec step)
    {
        if (logDialogueLines && step != null && !string.IsNullOrWhiteSpace(step.Body))
            Debug.Log($"[Tutorial] {step.Body}", this);
    }
}
