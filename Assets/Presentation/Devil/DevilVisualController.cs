using System;
using UnityEngine;

public class DevilVisualController : MonoBehaviour
{
    [SerializeField] private BattleController battleController;
    [SerializeField] private GameplayAssetRegistry assetRegistry;
    [SerializeField] private SpriteRenderer devilSprite;
    
    private string _currentDevilId;

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
        _currentDevilId = battle.Config.DevilId;
        if (devilSprite != null && assetRegistry != null)
            devilSprite.sprite = assetRegistry.GetDevilSprite(_currentDevilId);
    }
    private void OnBattleEnded(BattleEndedEvent @event)
    {
        _currentDevilId = null;
        if (devilSprite != null)
            devilSprite.sprite = null;
    }

    // Exposed wrapper for dialogue system
    public void SetDevilEmotion(string emotion)
    {
        if (devilSprite != null && assetRegistry != null)
            devilSprite.sprite = assetRegistry.GetDevilSpriteByEmotion(_currentDevilId, emotion);
        devilSprite.sortingOrder = 10;
    }
}
