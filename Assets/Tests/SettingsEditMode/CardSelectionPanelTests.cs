using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class CardSelectionPanelTests
{
    private Assembly runtimeAssembly;
    private Type panelType;
    private Type cardViewType;
    private Type cardType;
    private Type suitType;
    private Type rankType;
    private Type activeItemUseProxyType;
    private Type itemUseMenuType;
    private MethodInfo showMethod;

    [SetUp]
    public void SetUp()
    {
        runtimeAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly => assembly.GetName().Name == "Assembly-CSharp");
        Assert.That(runtimeAssembly, Is.Not.Null);

        panelType = RequireRuntimeType("CardSelectionPanel");
        cardViewType = RequireRuntimeType("BattleUiCardView");
        cardType = RequireRuntimeType("Card");
        suitType = RequireRuntimeType("Suit");
        rankType = RequireRuntimeType("Rank");
        activeItemUseProxyType = RequireRuntimeType("ActiveItemUseProxy");
        itemUseMenuType = RequireRuntimeType("ItemUseMenu");
        showMethod = panelType.GetMethod("Show", BindingFlags.Instance | BindingFlags.Public);
        Assert.That(showMethod, Is.Not.Null);
    }

    [Test]
    public void Show_DefaultSelection_TogglesScaleAndCardInteractability()
    {
        Component panel = CreatePanelFixture(
            out GameObject panelObject,
            out GameObject cardPrefabObject,
            out RectTransform cardContainer,
            out Button chooseButton);

        try
        {
            Show(panel, CreateCards(3), _ => { }, 1);

            Component[] views = GetRenderedViews(cardContainer);
            Assert.That(views, Has.Length.EqualTo(3));
            Assert.That(chooseButton.interactable, Is.False);

            GetButton(views[1]).onClick.Invoke();

            Assert.That(GetVisualScale(views[1]), Is.EqualTo(1.15f).Within(0.001f));
            Assert.That(chooseButton.interactable, Is.True);
            Assert.That(GetButton(views[0]).interactable, Is.False);
            Assert.That(GetButton(views[1]).interactable, Is.True);
            Assert.That(GetButton(views[2]).interactable, Is.False);

            GetButton(views[1]).onClick.Invoke();

            Assert.That(GetVisualScale(views[1]), Is.EqualTo(1f).Within(0.001f));
            Assert.That(chooseButton.interactable, Is.False);
            Assert.That(GetButton(views[0]).interactable, Is.True);
            Assert.That(GetButton(views[1]).interactable, Is.True);
            Assert.That(GetButton(views[2]).interactable, Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(panelObject);
            UnityEngine.Object.DestroyImmediate(cardPrefabObject);
        }
    }

    [Test]
    public void Choose_WithDuplicates_ReturnsOriginalIndicesInSelectionOrderAndCleansUp()
    {
        Component panel = CreatePanelFixture(
            out GameObject panelObject,
            out GameObject cardPrefabObject,
            out RectTransform cardContainer,
            out Button chooseButton);

        try
        {
            Array cards = CreateCards(3);
            cards.SetValue(cards.GetValue(0), 2);
            IReadOnlyList<int> result = null;
            int callbackCount = 0;

            Show(panel, cards, indices =>
            {
                callbackCount++;
                result = indices;
            }, 2);

            Component[] views = GetRenderedViews(cardContainer);
            GetButton(views[2]).onClick.Invoke();
            GetButton(views[0]).onClick.Invoke();

            Assert.That(GetButton(views[1]).interactable, Is.False);

            GetButton(views[2]).onClick.Invoke();
            GetButton(views[2]).onClick.Invoke();
            chooseButton.onClick.Invoke();

            Assert.That(callbackCount, Is.EqualTo(1));
            Assert.That(result, Is.EqualTo(new[] { 0, 2 }));
            Assert.That((bool)panelType.GetProperty("IsOpen").GetValue(panel), Is.False);
            Assert.That(panelObject.activeSelf, Is.False);
            Assert.That(cardContainer.childCount, Is.Zero);

            IList<int> mutableResult = result as IList<int>;
            Assert.That(mutableResult, Is.Not.Null);
            Assert.Throws<NotSupportedException>(() => mutableResult[0] = 99);

            chooseButton.onClick.Invoke();
            Assert.That(callbackCount, Is.EqualTo(1));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(panelObject);
            UnityEngine.Object.DestroyImmediate(cardPrefabObject);
        }
    }

    [Test]
    public void Show_RejectsInvalidArgumentsAndOverlappingRequests()
    {
        Component panel = CreatePanelFixture(
            out GameObject panelObject,
            out GameObject cardPrefabObject,
            out _,
            out _);

        try
        {
            Array cards = CreateCards(2);
            Action<IReadOnlyList<int>> callback = _ => { };

            AssertInvocationThrows<ArgumentNullException>(() => Show(panel, null, callback, 1));
            AssertInvocationThrows<ArgumentNullException>(() => Show(panel, cards, null, 1));
            AssertInvocationThrows<ArgumentOutOfRangeException>(() => Show(panel, cards, callback, 0));
            AssertInvocationThrows<ArgumentOutOfRangeException>(() => Show(panel, cards, callback, 3));

            Show(panel, cards, callback, 1);
            AssertInvocationThrows<InvalidOperationException>(() => Show(panel, cards, callback, 1));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(panelObject);
            UnityEngine.Object.DestroyImmediate(cardPrefabObject);
        }
    }

    [Test]
    public void Cancel_NotifiesOnceAndCleansGeneratedCards()
    {
        Component panel = CreatePanelFixture(
            out GameObject panelObject,
            out GameObject cardPrefabObject,
            out RectTransform cardContainer,
            out _);

        try
        {
            int cancellationCount = 0;
            EventInfo cancelledEvent = panelType.GetEvent("SelectionCancelled");
            Assert.That(cancelledEvent, Is.Not.Null);
            cancelledEvent.AddEventHandler(panel, (Action)(() => cancellationCount++));

            Show(panel, CreateCards(2), _ => { }, 1);
            MethodInfo cancel = panelType.GetMethod("Cancel", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(cancel, Is.Not.Null);
            Assert.That((bool)cancel.Invoke(panel, null), Is.True);

            Assert.That(cancellationCount, Is.EqualTo(1));
            Assert.That((bool)panelType.GetProperty("IsOpen").GetValue(panel), Is.False);
            Assert.That(panelObject.activeSelf, Is.False);
            Assert.That(cardContainer.childCount, Is.Zero);
            Assert.That((bool)cancel.Invoke(panel, null), Is.False);
            Assert.That(cancellationCount, Is.EqualTo(1));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(panelObject);
            UnityEngine.Object.DestroyImmediate(cardPrefabObject);
        }
    }

    [Test]
    public void MenuCanvasPrefab_ContainsConfiguredInactiveSevenColumnCardSelectionPanel()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Scene-wide Prefabs/MenuCanvas.prefab");
        Assert.That(prefab, Is.Not.Null);

        Transform panelTransform = FindChildRecursive(prefab.transform, "CardSelectionPanel");
        Assert.That(panelTransform, Is.Not.Null);
        Assert.That(panelTransform.parent, Is.EqualTo(prefab.transform));
        Assert.That(panelTransform.gameObject.activeSelf, Is.False);

        Component panel = panelTransform.GetComponent(panelType);
        Assert.That(panel, Is.Not.Null);

        UnityEngine.Object cardPrefab = GetPrivateField<UnityEngine.Object>(panel, "cardPrefab");
        RectTransform cardContainer = GetPrivateField<RectTransform>(panel, "cardContainer");
        GridLayoutGroup grid = GetPrivateField<GridLayoutGroup>(panel, "gridLayout");
        Button chooseButton = GetPrivateField<Button>(panel, "chooseButton");

        Assert.That(cardPrefab, Is.Not.Null);
        Assert.That(cardContainer, Is.Not.Null);
        Assert.That(grid, Is.Not.Null);
        Assert.That(chooseButton, Is.Not.Null);
        Assert.That(cardContainer.GetComponentInParent<ScrollRect>(true), Is.Not.Null);
        Assert.That(grid.constraint, Is.EqualTo(GridLayoutGroup.Constraint.FixedColumnCount));
        Assert.That(grid.constraintCount, Is.EqualTo(7));

        Component proxy = prefab.GetComponent(activeItemUseProxyType);
        Assert.That(proxy, Is.Not.Null);
        Assert.That(GetPrivateField<Component>(proxy, "cardSelectionPanel"), Is.SameAs(panel));

        Component itemUseMenu = prefab.GetComponentInChildren(itemUseMenuType, true);
        Assert.That(itemUseMenu, Is.Not.Null);
        Assert.That(GetPrivateField<Component>(itemUseMenu, "activeItemUseProxy"), Is.SameAs(proxy));
    }

    private Component CreatePanelFixture(
        out GameObject panelObject,
        out GameObject cardPrefabObject,
        out RectTransform cardContainer,
        out Button chooseButton)
    {
        panelObject = new GameObject("CardSelectionPanel", typeof(RectTransform));

        cardContainer = new GameObject(
            "CardGridContent",
            typeof(RectTransform),
            typeof(GridLayoutGroup)).GetComponent<RectTransform>();
        cardContainer.SetParent(panelObject.transform, false);

        chooseButton = new GameObject(
            "ChooseButton",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button)).GetComponent<Button>();
        chooseButton.transform.SetParent(panelObject.transform, false);

        cardPrefabObject = CreateCardPrefab();
        panelObject.SetActive(false);
        Component panel = panelObject.AddComponent(panelType);
        SetPrivateField(panel, "cardPrefab", cardPrefabObject.GetComponent(cardViewType));
        SetPrivateField(panel, "panelRoot", panelObject);
        SetPrivateField(panel, "cardContainer", cardContainer);
        SetPrivateField(panel, "gridLayout", cardContainer.GetComponent<GridLayoutGroup>());
        SetPrivateField(panel, "chooseButton", chooseButton);
        return panel;
    }

    private GameObject CreateCardPrefab()
    {
        GameObject prefabObject = new(
            "CardPrefab",
            typeof(RectTransform),
            typeof(LayoutElement));

        GameObject visualObject = new(
            "Visual",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        visualObject.transform.SetParent(prefabObject.transform, false);

        Component cardView = prefabObject.AddComponent(cardViewType);
        MethodInfo initialize = cardViewType.GetMethod("Initialize", BindingFlags.Instance | BindingFlags.Public);
        Assert.That(initialize, Is.Not.Null);
        initialize.Invoke(cardView, new object[]
        {
            visualObject.GetComponent<Image>(),
            null,
            visualObject.GetComponent<Button>()
        });
        return prefabObject;
    }

    private Array CreateCards(int count)
    {
        Array cards = Array.CreateInstance(cardType, count);
        for (int i = 0; i < count; i++)
        {
            object suit = Enum.ToObject(suitType, i % 4);
            object rank = Enum.ToObject(rankType, (i % 13) + 1);
            object card = Activator.CreateInstance(cardType, suit, rank, null);
            cards.SetValue(card, i);
        }

        return cards;
    }

    private void Show(
        Component panel,
        Array cards,
        Action<IReadOnlyList<int>> callback,
        int selectionCount)
    {
        showMethod.Invoke(panel, new object[] { cards, callback, selectionCount });
    }

    private Component[] GetRenderedViews(RectTransform cardContainer)
    {
        Component[] views = new Component[cardContainer.childCount];
        for (int i = 0; i < cardContainer.childCount; i++)
            views[i] = cardContainer.GetChild(i).GetComponent(cardViewType);
        return views;
    }

    private Button GetButton(Component view)
    {
        PropertyInfo property = cardViewType.GetProperty("Button", BindingFlags.Instance | BindingFlags.Public);
        Assert.That(property, Is.Not.Null);
        return (Button)property.GetValue(view);
    }

    private static float GetVisualScale(Component view)
    {
        Transform visual = FindChildRecursive(view.transform, "Visual");
        Assert.That(visual, Is.Not.Null);
        return visual.localScale.x;
    }

    private Type RequireRuntimeType(string typeName)
    {
        Type type = runtimeAssembly.GetType(typeName);
        Assert.That(type, Is.Not.Null, $"Runtime type {typeName} was not found.");
        return type;
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
            return null;
        if (root.name == childName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindChildRecursive(root.GetChild(i), childName);
            if (result != null)
                return result;
        }

        return null;
    }

    private static T GetPrivateField<T>(Component panel, string fieldName)
    {
        FieldInfo field = panel.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        return (T)field.GetValue(panel);
    }

    private static void SetPrivateField(Component panel, string fieldName, object value)
    {
        FieldInfo field = panel.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(panel, value);
    }

    private static void AssertInvocationThrows<T>(TestDelegate action) where T : Exception
    {
        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(action);
        Assert.That(exception.InnerException, Is.TypeOf<T>());
    }
}
