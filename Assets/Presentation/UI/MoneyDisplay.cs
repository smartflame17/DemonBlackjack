using System.Collections;
using System.Collections.Generic;
using DamageNumbersPro;
using TMPro;
using UnityEngine;

public sealed class MoneyDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text moneyText;
    [SerializeField] private Combatant combatant = Combatant.Player;
    [SerializeField] private RunManager runManager;
    [SerializeField] private BattleController battleController;

    [Header("Text Effects")]
    [SerializeField] private DamageNumber damageNumber;
    [SerializeField, Min(0f)] private float textEffectDelaySeconds = 0.2f;

    private ScopedEventBus _subscribedBattleBus;
    private readonly Queue<TextEffectRequest> _pendingTextEffects = new();
    private Coroutine _textEffectRoutine;

    private readonly struct TextEffectRequest
    {
        public readonly RectTransform Target;
        public readonly Vector2 Offset;
        public readonly float Amount;

        public TextEffectRequest(RectTransform target, Vector2 offset, float amount)
        {
            Target = target;
            Offset = offset;
            Amount = amount;
        }
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        EventBus.Subscribe<BattleStartedEvent>(OnBattleStarted);
        EventBus.Subscribe<BattleEndedEvent>(OnBattleEnded);
        EventBus.Subscribe<RunPhaseChangedEvent>(OnRunPhaseChanged);

        if (combatant == Combatant.Player)
            EventBus.Subscribe<MoneyChangedEvent>(OnMoneyChanged);
        else
            BindBattleBus();

        Refresh();
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<BattleStartedEvent>(OnBattleStarted);
        EventBus.Unsubscribe<BattleEndedEvent>(OnBattleEnded);
        EventBus.Unsubscribe<RunPhaseChangedEvent>(OnRunPhaseChanged);

        if (combatant == Combatant.Player)
            EventBus.Unsubscribe<MoneyChangedEvent>(OnMoneyChanged);

        UnbindBattleBus();
        ClearPendingTextEffects();
    }

    public void Refresh()
    {
        ResolveReferences();

        if (combatant == Combatant.Opponent)
            BindBattleBus();

        int amount = combatant == Combatant.Player
            ? runManager != null && runManager.RunState != null ? runManager.RunState.Money : 0
            : battleController != null && battleController.BattleState != null ? battleController.BattleState.OpponentMoney : 0;

        SetMoney(amount);
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
        _subscribedBattleBus.Subscribe<MoneyChangedEvent>(OnMoneyChanged);
    }

    private void UnbindBattleBus()
    {
        if (_subscribedBattleBus == null)
            return;

        _subscribedBattleBus.Unsubscribe<MoneyChangedEvent>(OnMoneyChanged);
        _subscribedBattleBus = null;
    }

    private void OnMoneyChanged(MoneyChangedEvent eventData)
    {
        if (eventData.Owner != combatant)
            return;

        QueueTextEffect(eventData.Delta);
        SetMoney(eventData.CurrentMoney);
    }

    private void OnBattleStarted(BattleStartedEvent eventData)
    {
        ResolveReferences();
        if (combatant == Combatant.Opponent)
            BindBattleBus();

        SetMoney(combatant == Combatant.Player ? eventData.PlayerMoney : eventData.OpponentMoney);
    }

    private void OnBattleEnded(BattleEndedEvent eventData)
    {
        if (combatant == Combatant.Opponent)
        {
            UnbindBattleBus();
            SetMoney(0);
            return;
        }

        Refresh();
    }

    private void OnRunPhaseChanged(RunPhaseChangedEvent eventData)
    {
        Refresh();
    }

    private void ResolveReferences()
    {
        moneyText ??= GetComponent<TMP_Text>();
        runManager ??= FindFirstObjectByType<RunManager>();
        battleController ??= FindFirstObjectByType<BattleController>();
    }

    private void QueueTextEffect(float amount)
    {
        if (amount == 0f || damageNumber == null || moneyText == null)
            return;

        _pendingTextEffects.Enqueue(new TextEffectRequest(moneyText.rectTransform, Vector2.zero, amount));

        if (_textEffectRoutine == null && isActiveAndEnabled)
            _textEffectRoutine = StartCoroutine(PlayQueuedTextEffects());
    }

    private IEnumerator PlayQueuedTextEffects()
    {
        while (_pendingTextEffects.Count > 0)
        {
            TextEffectRequest request = _pendingTextEffects.Dequeue();

            if (textEffectDelaySeconds > 0f)
                yield return new WaitForSeconds(textEffectDelaySeconds);
            else
                yield return null;

            damageNumber.SpawnGUI(request.Target, request.Offset, request.Amount);
        }

        _textEffectRoutine = null;
    }

    private void ClearPendingTextEffects()
    {
        if (_textEffectRoutine != null)
            StopCoroutine(_textEffectRoutine);

        _textEffectRoutine = null;
        _pendingTextEffects.Clear();
    }

    private void SetMoney(int amount)
    {
        if (moneyText == null)
            return;

        int clampedAmount = Mathf.Max(0, amount);
        string displayText = NumberFormatter.Abbreviate(amount);
        moneyText.text = combatant == Combatant.Opponent ? $"상대 $"+ displayText : "$"+ displayText;
    }
}
