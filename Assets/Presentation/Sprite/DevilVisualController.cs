using System;
using UnityEngine;

public class DevilVisualController : MonoBehaviour
{
    [SerializeField] private BattleController battleController;
    [SerializeField] private GameplayAssetRegistry assetRegistry;
    [SerializeField] private SpriteRenderer devilSprite;
    
    private void Awake()
    {
        if (battleController == null)
            battleController = FindFirstObjectByType<BattleController>();
        
        if (devilSprite == null)
            devilSprite = GetComponent<SpriteRenderer>();
    }

    void OnEnable()
    {
        EventBus.Subscribe<BattleStartedEvent>(OnBattleStarted);
        EventBus.Subscribe<BattleEndedEvent>(OnBattleEnded);
    }
    void OnDisable()
    {
        EventBus.Unsubscribe<BattleStartedEvent>(OnBattleStarted);
        EventBus.Unsubscribe<BattleEndedEvent>(OnBattleEnded);
    }

    private void OnBattleStarted(BattleStartedEvent @event)
    {
        BattleState battle = battleController != null ? battleController.BattleState : null;

        if (battle == null) return;

        if (devilSprite != null && assetRegistry != null)
            devilSprite.sprite = assetRegistry.GetDevilSprite(battle.Config.DevilId);
    }
    private void OnBattleEnded(BattleEndedEvent @event)
    {
        if (devilSprite != null)
            devilSprite.sprite = null;
    }
}
