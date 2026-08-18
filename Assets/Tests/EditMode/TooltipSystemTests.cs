#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class TooltipSystemTests
{
    private readonly List<Object> _createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        EventBus.Clear();
        for (int i = _createdObjects.Count - 1; i >= 0; i--)
        {
            if (_createdObjects[i] != null)
                Object.DestroyImmediate(_createdObjects[i]);
        }
        _createdObjects.Clear();
    }

    [Test]
    public void ExistingShopAsset_RetainsGenericContentAndPrice()
    {
        ActiveItemDefinition definition = AssetDatabase.LoadAssetAtPath<ActiveItemDefinition>(
            "Assets/Content/ActiveItem/draw_three.asset");

        Assert.That(definition, Is.Not.Null);
        Assert.That(definition, Is.InstanceOf<TooltipContentDefinition>());
        Assert.That(definition.Id, Is.EqualTo("draw_three"));
        Assert.That(definition.DisplayName, Is.Not.Empty);
        Assert.That(definition.Description, Is.Not.Empty);
        Assert.That(definition.Price, Is.EqualTo(10));
    }

    [Test]
    public void Catalog_ResolvesShopAndDevilDefinitionsCaseInsensitively()
    {
        ActiveItemDefinition shopDefinition = Track(ScriptableObject.CreateInstance<ActiveItemDefinition>());
        shopDefinition.Initialize("shop_entry", "Shop", "Shop description", 5);
        DevilAbilityDefinition devilDefinition = Track(ScriptableObject.CreateInstance<DevilAbilityDefinition>());
        devilDefinition.Initialize("devil1", "todo", "todo");
        TooltipContentCatalog catalog = Track(ScriptableObject.CreateInstance<TooltipContentCatalog>());
        catalog.Initialize(new TooltipContentDefinition[] { shopDefinition, devilDefinition });

        Assert.That(catalog.TryGetDefinition("SHOP_ENTRY", out TooltipContentDefinition shopResult), Is.True);
        Assert.That(shopResult, Is.SameAs(shopDefinition));
        Assert.That(catalog.TryGetDefinition("DEVIL1", out TooltipContentDefinition devilResult), Is.True);
        Assert.That(devilResult, Is.SameAs(devilDefinition));
        Assert.That(catalog.TryGetDefinition("missing", out _), Is.False);
        Assert.That(catalog.TryGetDefinition(" ", out _), Is.False);
    }

    [Test]
    public void CatalogValidation_ReportsBlankAndDuplicateIds()
    {
        DevilAbilityDefinition first = Track(ScriptableObject.CreateInstance<DevilAbilityDefinition>());
        first.Initialize("devil1", "First", "First");
        DevilAbilityDefinition duplicate = Track(ScriptableObject.CreateInstance<DevilAbilityDefinition>());
        duplicate.Initialize("DEVIL1", "Duplicate", "Duplicate");
        DevilAbilityDefinition blank = Track(ScriptableObject.CreateInstance<DevilAbilityDefinition>());
        blank.Initialize("", "Blank", "Blank");
        TooltipContentCatalog catalog = Track(ScriptableObject.CreateInstance<TooltipContentCatalog>());
        catalog.name = "ValidationCatalog";
        catalog.Initialize(new TooltipContentDefinition[] { first, duplicate, blank });

        List<string> errors = Invoke<List<string>>(catalog, "GetValidationErrors");

        Assert.That(errors, Does.Contain("ValidationCatalog contains duplicate tooltip ID 'DEVIL1'."));
        Assert.That(errors, Does.Contain("ValidationCatalog contains a tooltip definition with an empty ID."));
    }

    [Test]
    public void TooltipPanel_UsesFallbackAndHidesWhenNeitherIdExists()
    {
        TooltipContentCatalog catalog = CreateDevilCatalog();
        TooltipPanel panel = CreateTooltipPanel(catalog, out TMP_Text displayName, out TMP_Text description);
        TooltipTrigger owner = Track(new GameObject("Owner")).AddComponent<TooltipTrigger>();

        Assert.That(panel.Show("tutorial_battle", "DEVIL1", owner, Vector2.zero), Is.True);
        Assert.That(displayName.text, Is.EqualTo("todo"));
        Assert.That(description.text, Is.EqualTo("todo"));
        Assert.That(panel.gameObject.activeSelf, Is.True);

        Assert.That(panel.Show("missing", "also_missing", owner, Vector2.zero), Is.False);
        Assert.That(panel.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void TooltipTrigger_ClearRemovesPrimaryAndFallbackAndHidesOwnedPanel()
    {
        TooltipContentCatalog catalog = CreateDevilCatalog();
        TooltipPanel panel = CreateTooltipPanel(catalog, out _, out _);
        TooltipTrigger trigger = Track(new GameObject("Trigger")).AddComponent<TooltipTrigger>();
        SetField(trigger, "tooltipPanel", panel);

        trigger.Bind("tutorial_battle", "devil1");
        Assert.That(trigger.ContentId, Is.EqualTo("tutorial_battle"));
        Assert.That(trigger.FallbackContentId, Is.EqualTo("devil1"));
        trigger.OnPointerEnter(new PointerEventData(null) { position = Vector2.zero });
        Assert.That(panel.gameObject.activeSelf, Is.True);

        trigger.Clear();

        Assert.That(trigger.ContentId, Is.Null);
        Assert.That(trigger.FallbackContentId, Is.Null);
        Assert.That(panel.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void BattleUiPresenter_BindsEncounterAndDevilFallbackFromConfig()
    {
        GameObject controllerObject = Track(new GameObject("BattleController"));
        BattleController controller = controllerObject.AddComponent<BattleController>();
        GameObject presenterObject = Track(new GameObject("Presenter"));
        presenterObject.SetActive(false);
        GameObject iconObject = new("DevilAbilityInfo");
        iconObject.transform.SetParent(presenterObject.transform);
        TooltipTrigger trigger = iconObject.AddComponent<TooltipTrigger>();
        BattleUiPresenter presenter = presenterObject.AddComponent<BattleUiPresenter>();
        SetField(presenter, "battleController", controller);
        SetField(presenter, "devilAbilityTooltipTrigger", trigger);

        controller.InitializeBattle(
            new RunState(123),
            new BattleConfig("tutorial_battle", 100, devilId: "devil1"));

        Invoke(presenter, "RefreshDevilAbilityTooltip", controller.BattleState);

        Assert.That(trigger.ContentId, Is.EqualTo("tutorial_battle"));
        Assert.That(trigger.FallbackContentId, Is.EqualTo("devil1"));

        controller.CleanupBattle();
        Invoke(presenter, "RefreshDevilAbilityTooltip", null);
        Assert.That(trigger.ContentId, Is.Null);
        Assert.That(trigger.FallbackContentId, Is.Null);
    }

    [Test]
    public void AuthoredCatalogAndBattlePrefab_ContainRequiredDefinitionsAndIconBinding()
    {
        TooltipContentCatalog catalog = AssetDatabase.LoadAssetAtPath<TooltipContentCatalog>(
            "Assets/Content/TooltipContentCatalog.asset");
        Assert.That(catalog, Is.Not.Null);
        Assert.That(catalog.Definitions.Count, Is.GreaterThanOrEqualTo(39));
        Assert.That(catalog.TryGetDefinition(ActiveItemResolver.BurstDeny, out _), Is.True);
        Assert.That(catalog.TryGetDefinition(ActiveItemResolver.StockBuy, out _), Is.True);
        Assert.That(catalog.TryGetDefinition(ActiveItemResolver.StockSell, out _), Is.True);
        for (int i = 1; i <= 4; i++)
        {
            Assert.That(catalog.TryGetDefinition($"devil{i}", out TooltipContentDefinition definition), Is.True);
            Assert.That(definition, Is.InstanceOf<DevilAbilityDefinition>());
            Assert.That(definition.DisplayName, Is.EqualTo("todo"));
            Assert.That(definition.Description, Is.EqualTo("todo"));
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Scene-wide Prefabs/BattleUI.prefab");
        Assert.That(prefab, Is.Not.Null);
        Transform icon = FindChild(prefab.transform, "DevilAbilityInfo");
        Assert.That(icon, Is.Not.Null);
        TooltipTrigger trigger = icon.GetComponent<TooltipTrigger>();
        Assert.That(trigger, Is.Not.Null);
        Assert.That(icon.GetComponent<TMP_Text>().raycastTarget, Is.True);
        BattleUiPresenter presenter = prefab.GetComponent<BattleUiPresenter>();
        Assert.That(GetField<TooltipTrigger>(presenter, "devilAbilityTooltipTrigger"), Is.SameAs(trigger));
    }

    private TooltipContentCatalog CreateDevilCatalog()
    {
        DevilAbilityDefinition definition = Track(ScriptableObject.CreateInstance<DevilAbilityDefinition>());
        definition.Initialize("devil1", "todo", "todo");
        TooltipContentCatalog catalog = Track(ScriptableObject.CreateInstance<TooltipContentCatalog>());
        catalog.Initialize(new[] { definition });
        return catalog;
    }

    private TooltipPanel CreateTooltipPanel(TooltipContentCatalog catalog, out TMP_Text displayName, out TMP_Text description)
    {
        GameObject canvasObject = Track(new GameObject("Canvas", typeof(RectTransform), typeof(Canvas)));
        GameObject panelObject = new("TooltipPanel", typeof(RectTransform));
        panelObject.transform.SetParent(canvasObject.transform);
        panelObject.SetActive(false);
        TooltipPanel panel = panelObject.AddComponent<TooltipPanel>();
        displayName = new GameObject("DisplayNameText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI))
            .GetComponent<TMP_Text>();
        description = new GameObject("DescriptionText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI))
            .GetComponent<TMP_Text>();
        displayName.transform.SetParent(panelObject.transform);
        description.transform.SetParent(panelObject.transform);
        SetField(panel, "catalog", catalog);
        SetField(panel, "displayNameText", displayName);
        SetField(panel, "descriptionText", description);
        return panel;
    }

    private T Track<T>(T target) where T : Object
    {
        _createdObjects.Add(target);
        return target;
    }

    private static Transform FindChild(Transform root, string name)
    {
        if (root.name == name)
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindChild(root.GetChild(i), name);
            if (result != null)
                return result;
        }
        return null;
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(target, value);
    }

    private static T GetField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, fieldName);
        return (T)field.GetValue(target);
    }

    private static T Invoke<T>(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, methodName);
        return (T)method.Invoke(target, null);
    }

    private static void Invoke(object target, string methodName, object argument)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, methodName);
        method.Invoke(target, new[] { argument });
    }
}
#endif
