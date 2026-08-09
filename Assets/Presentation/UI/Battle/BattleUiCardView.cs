using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Coffee.UIEffects;

public sealed class BattleUiCardView : MonoBehaviour
{
    private static readonly int EffectEnabledId = Shader.PropertyToID("_EffectEnabled");

    [SerializeField] private Image image;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Button button;
    [SerializeField] private RectTransform visualRoot;
    [SerializeField] private Image upgradeImage;
    [SerializeField] private TooltipTrigger tooltipTrigger;
    [SerializeField] private UIEffect uiEffect;
    [SerializeField] private Material upgradeMaterial;

    private Material _runtimeMaterial;
    private bool _hasBasePosition;

    public Button Button => button;
    public RectTransform RectTransform => transform as RectTransform;

    private void Awake()
    {
        EnsureRuntimeMaterial();
        SetPokerHighlight(false);
    }

    public void Initialize(Image cardImage, TMP_Text cardLabel, Button cardButton)
    {
        image = cardImage;
        label = cardLabel;
        button = cardButton;
        visualRoot = cardImage != null ? cardImage.rectTransform : transform as RectTransform;
        EnsureRuntimeMaterial();
        if (button != null && button.targetGraphic == null)
            button.targetGraphic = image;

        SetPokerHighlight(false);
    }

    public Button EnsureButton()
    {
        EnsureReferences();

        if (button == null && image != null)
            button = image.gameObject.AddComponent<Button>();

        if (button != null && button.targetGraphic == null)
            button.targetGraphic = image;

        return button;
    }

    public void Bind(Card card, Sprite sprite, bool faceUp, bool interactable)
    {
        Bind(card, sprite, faceUp, interactable, null);
    }

    public void Bind(Card card, Sprite sprite, bool faceUp, bool interactable, GameplayAssetRegistry assetRegistry)
    {
        EnsureRuntimeMaterial();

        if (image != null)
            image.sprite = sprite;

        if (label != null)
            label.text = faceUp ? FormatCard(card) : string.Empty;

        if (button != null)
            button.interactable = interactable;

        BindUpgrade(card, faceUp, assetRegistry);

        if (faceUp && card.HasModifier)
            tooltipTrigger.Bind(card.ModifierId);
        else
            tooltipTrigger.Clear();
    }

    public void BindBack(Sprite sprite)
    {
        EnsureRuntimeMaterial();

        if (image != null)
            image.sprite = sprite;

        if (label != null)
            label.text = string.Empty;

        if (button != null)
            button.interactable = false;

        ClearUpgrade();
        tooltipTrigger.Clear();
    }

    public void SetVisualColor(Color color)
    {
        EnsureReferences();

        if (image != null)
            image.color = color;

        if (label != null)
            label.color = color;

        if (upgradeImage != null)
            upgradeImage.color = color;
    }

    public void SetPokerHighlight(bool highlighted)
    {
        EnsureReferences();

        if (uiEffect != null)
            uiEffect.edgeMode = highlighted ? EdgeMode.Shiny : EdgeMode.None;
    }

    public void EnsureRuntimeMaterial()
    {
        EnsureReferences();

        if (_runtimeMaterial == null)
        {
            Material sourceMaterial = upgradeMaterial != null
                ? upgradeMaterial
                : image != null ? image.material : null;
            if (sourceMaterial != null)
                _runtimeMaterial = Instantiate(sourceMaterial);
        }

        if (image != null && _runtimeMaterial != null)
            image.material = _runtimeMaterial;
    }

    private void EnsureReferences()
    {
        if (image == null)
            image = GetComponent<Image>();

        if (button == null)
            button = GetComponentInChildren<Button>(true);

        if (label == null)
            label = GetComponentInChildren<TMP_Text>();

        if (visualRoot == null)
            visualRoot = image != null ? image.rectTransform : transform as RectTransform;

        if (upgradeImage == null)
            upgradeImage = FindChildImage("Upgrade");

        uiEffect ??= GetComponentInChildren<UIEffect>(true);
        uiEffect ??= gameObject.AddComponent<UIEffect>();

        tooltipTrigger ??= GetComponent<TooltipTrigger>() ?? gameObject.AddComponent<TooltipTrigger>();
    }

    private void BindUpgrade(Card card, bool faceUp, GameplayAssetRegistry assetRegistry)
    {
        if (upgradeImage == null)
            return;

        if (!faceUp || !card.HasModifier || assetRegistry == null)
        {
            ClearUpgrade();
            return;
        }

        upgradeImage.sprite = assetRegistry.GetCardUpgradeSprite(card.ModifierId);
        upgradeImage.gameObject.SetActive(upgradeImage.sprite != null);
        SetShaderEffectEnabled(true);
    }

    private void ClearUpgrade()
    {
        if (upgradeImage == null)
            return;

        upgradeImage.sprite = null;
        upgradeImage.gameObject.SetActive(false);
        SetShaderEffectEnabled(false);
    }

    private void SetShaderEffectEnabled(bool enabled)
    {
        _runtimeMaterial?.SetFloat(EffectEnabledId, enabled ? 1f : 0f);
    }

    private Image FindChildImage(string childName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == childName && children[i].TryGetComponent(out Image childImage))
                return childImage;
        }

        return null;
    }

    private static string FormatCard(Card card)
    {
        return $"{RankLabel(card.Rank)}\n{SuitLabel(card.Suit)}";
    }

    private static string RankLabel(Rank rank)
    {
        return rank switch
        {
            Rank.Ace => "A",
            Rank.Jack => "J",
            Rank.Queen => "Q",
            Rank.King => "K",
            _ => ((int)rank).ToString()
        };
    }

    private static string SuitLabel(Suit suit)
    {
        return suit switch
        {
            Suit.Hearts => "H",
            Suit.Diamonds => "D",
            Suit.Clubs => "C",
            Suit.Spades => "S",
            _ => "?"
        };
    }
}
