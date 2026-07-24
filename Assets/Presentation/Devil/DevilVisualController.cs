using UnityEngine;
using DG.Tweening;

public class DevilVisualController : MonoBehaviour
{
    [SerializeField] private BattleController battleController;
    [SerializeField] private GameplayAssetRegistry assetRegistry;
    [SerializeField] private SpriteRenderer devilSprite;
    [SerializeField] private float spriteFadeDuration = 0.1f;
    private string _currentDevilId;
    private string _currentEmotionId;
    private Tween spriteTransition;

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

    private void OnDestroy()
    {
        spriteTransition?.Kill();
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
        if (devilSprite == null && assetRegistry == null) return;
        if (_currentEmotionId == emotion) return;

        Sprite newSprite = assetRegistry.GetDevilSpriteByEmotion(_currentDevilId, emotion);
        devilSprite.sortingOrder = 10;
        TransitionToSprite(newSprite, () => _currentEmotionId = emotion);
    }

    // Callback on dialogue system when bark ends, to reset devil sprite to default
    public void OnBarkEnd(Transform actor)
    {
        //Debug.Log("Bark ended for actor: " + actor.name);
        if (devilSprite == null || assetRegistry == null) return;

        Sprite defaultSprite = assetRegistry.GetDevilSprite(_currentDevilId);
        TransitionToSprite(defaultSprite, () => _currentEmotionId = null);
    }

    public void OnConversationEnd(Transform actor)
    {
        if (devilSprite == null || assetRegistry == null) return;

        Sprite defaultSprite = assetRegistry.GetDevilSprite(_currentDevilId);
        TransitionToSprite(defaultSprite, () => _currentEmotionId = null);
    }

    private void TransitionToSprite(Sprite newSprite, TweenCallback onChanged = null)
    {
        if (devilSprite == null || newSprite == null)
            return;

        if (devilSprite.sprite == newSprite)
        {
            onChanged?.Invoke();
            return;
        }
        // Prevent multiple transitions from fighting over the same renderer.
        spriteTransition?.Kill();

        // Reset alpha to 1 before starting the transition, in case it was left at 0 from a previous transition.
        Color color = devilSprite.color;
        color.a = 1f;
        devilSprite.color = color;

        spriteTransition = DOTween.Sequence()
            .Append(devilSprite.DOFade(0.3f, spriteFadeDuration))
            .AppendCallback(() =>
            {
                devilSprite.sprite = newSprite;
            })
            .Append(devilSprite.DOFade(1f, spriteFadeDuration))
            .SetEase(Ease.InOutSine);
    }
}
