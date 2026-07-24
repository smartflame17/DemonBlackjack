using System.Collections.Generic;
using UnityEngine;

public sealed class PokerHighlightController : MonoBehaviour
{
    [SerializeField] private BattleController battleController;

    private readonly HashSet<int> _highlightedCardIndices = new();
    private ScopedEventBus _subscribedBattleBus;
    private bool _highlightsPending;

    private void Awake()
    {
        battleController ??= FindFirstObjectByType<BattleController>();
    }

    private void OnEnable()
    {
        EventBus.Subscribe<BattleStartedEvent>(OnBattleStarted);
        EventBus.Subscribe<BattleEndedEvent>(OnBattleEnded);
        BindBattleBus();
        ClearHighlights();
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<BattleStartedEvent>(OnBattleStarted);
        EventBus.Unsubscribe<BattleEndedEvent>(OnBattleEnded);
        UnbindBattleBus();
        ClearHighlights();
    }

    private void LateUpdate()
    {
        if (_highlightsPending)
            TryApplyHighlights();
    }

    private void OnBattleStarted(BattleStartedEvent eventData)
    {
        BindBattleBus();
        ClearHighlights();
    }

    private void OnBattleEnded(BattleEndedEvent eventData)
    {
        ClearHighlights();
        UnbindBattleBus();
    }

    private void OnRoundStarted(RoundStartedEvent eventData)
    {
        ClearHighlights();
    }

    private void OnRoundEnded(RoundEndedEvent eventData)
    {
        ClearHighlights();
    }

    private void OnPokerResolved(PokerResolvedEvent eventData)
    {
        if (eventData.Combatant != Combatant.Player)
            return;

        _highlightedCardIndices.Clear();

        IReadOnlyList<int> cardIndices = eventData.Poker.CardIndices;
        if (cardIndices != null)
        {
            for (int i = 0; i < cardIndices.Count; i++)
            {
                if (cardIndices[i] >= 0)
                    _highlightedCardIndices.Add(cardIndices[i]);
            }
        }

        _highlightsPending = true;
    }

    private void BindBattleBus()
    {
        ScopedEventBus battleBus = battleController != null ? battleController.BattleState?.EventBus : null;
        if (ReferenceEquals(_subscribedBattleBus, battleBus))
            return;

        UnbindBattleBus();

        if (battleBus == null)
            return;

        _subscribedBattleBus = battleBus;
        _subscribedBattleBus.Subscribe<PokerResolvedEvent>(OnPokerResolved);
        _subscribedBattleBus.Subscribe<RoundStartedEvent>(OnRoundStarted);
        _subscribedBattleBus.Subscribe<RoundEndedEvent>(OnRoundEnded);
    }

    private void UnbindBattleBus()
    {
        if (_subscribedBattleBus == null)
            return;

        _subscribedBattleBus.Unsubscribe<PokerResolvedEvent>(OnPokerResolved);
        _subscribedBattleBus.Unsubscribe<RoundStartedEvent>(OnRoundStarted);
        _subscribedBattleBus.Unsubscribe<RoundEndedEvent>(OnRoundEnded);
        _subscribedBattleBus = null;
    }

    private void TryApplyHighlights()
    {
        int expectedCardCount = battleController != null
            ? battleController.BattleState?.CurrentRound?.PlayerPlayedCards.Count ?? 0
            : 0;

        int activeCardViewCount = 0;
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.gameObject.activeSelf && child.TryGetComponent(out BattleUiCardView _))
                activeCardViewCount++;
        }

        if (activeCardViewCount < expectedCardCount)
            return;

        int cardIndex = 0;
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (!child.TryGetComponent(out BattleUiCardView cardView))
                continue;

            bool highlighted = child.gameObject.activeSelf && _highlightedCardIndices.Contains(cardIndex);
            cardView.SetPokerHighlight(highlighted);
            cardIndex++;
        }

        _highlightsPending = false;
    }

    private void ClearHighlights()
    {
        _highlightedCardIndices.Clear();
        _highlightsPending = false;

        for (int i = 0; i < transform.childCount; i++)
        {
            if (transform.GetChild(i).TryGetComponent(out BattleUiCardView cardView))
                cardView.SetPokerHighlight(false);
        }
    }
}
