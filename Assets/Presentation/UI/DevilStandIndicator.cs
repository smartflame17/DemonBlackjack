using UnityEngine;
using UnityEngine.UI;

public sealed class DevilStandIndicator : MonoBehaviour
{
    [SerializeField] private BattleController battleController;
    [SerializeField] private Image image;
    [SerializeField] private Color standColor = Color.red;

    private BattleState _subscribedBattle;
    private Color _defaultColor;
    private bool _hasDefaultColor;

    public void Configure(BattleController controller)
    {
        battleController = controller;
        SubscribeToCurrentBattle();
    }

    private void Awake()
    {
        image ??= GetComponent<Image>();
        CacheDefaultColor();
    }

    private void OnEnable()
    {
        if (battleController == null)
            battleController = FindFirstObjectByType<BattleController>();

        SubscribeToCurrentBattle();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (battleController != null && battleController.BattleState != _subscribedBattle)
            SubscribeToCurrentBattle();
    }

    public void ApplyChoice(DevilTurnChoice choice)
    {
        image ??= GetComponent<Image>();
        if (image == null)
            return;

        CacheDefaultColor();
        image.color = choice == DevilTurnChoice.Stand ? standColor : _defaultColor;
    }

    private void OnDevilTurnChoice(DevilTurnChoiceEvent eventData)
    {
        ApplyChoice(eventData.Choice);
    }

    private void SubscribeToCurrentBattle()
    {
        BattleState currentBattle = battleController != null ? battleController.BattleState : null;
        if (_subscribedBattle == currentBattle)
            return;

        Unsubscribe();
        _subscribedBattle = currentBattle;
        _subscribedBattle?.EventBus.Subscribe<DevilTurnChoiceEvent>(OnDevilTurnChoice);
    }

    private void Unsubscribe()
    {
        if (_subscribedBattle == null)
            return;

        _subscribedBattle.EventBus.Unsubscribe<DevilTurnChoiceEvent>(OnDevilTurnChoice);
        _subscribedBattle = null;
    }

    private void CacheDefaultColor()
    {
        if (_hasDefaultColor)
            return;

        image ??= GetComponent<Image>();
        if (image == null)
            return;

        _defaultColor = image.color;
        _hasDefaultColor = true;
    }
}
