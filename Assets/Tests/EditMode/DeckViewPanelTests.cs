#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public sealed class DeckViewPanelTests
{
    [Test]
    public void Show_WithDiscardedCards_RendersPrimaryAndTintedDiscardedCards()
    {
        GameObject panelObject = new("DeckViewPanel");
        GameObject prefabObject = CreateCardPrefab();

        try
        {
            DeckViewPanel panel = panelObject.AddComponent<DeckViewPanel>();
            RectTransform rankRoot = CreateRankRoot(panelObject.transform);

            SetPrivateField(panel, "panelRoot", panelObject);
            SetPrivateField(panel, "rankGridViewRoot", rankRoot);
            SetPrivateField(panel, "cardPrefab", prefabObject.GetComponent<BattleUiCardView>());

            DeckViewOptions options = DeckViewOptions.Default;
            options.Flags |= DeckViewFlags.ShowDiscardedCards;

            panel.Show(
                new[] { new Card(Suit.Hearts, Rank.Ace) },
                new[] { new Card(Suit.Clubs, Rank.Two) },
                options);

            BattleUiCardView[] views = panelObject.GetComponentsInChildren<BattleUiCardView>();
            Assert.That(views.Length, Is.EqualTo(2));

            Image primaryImage = rankRoot.GetChild((int)Rank.Ace - 1).GetComponentInChildren<Image>();
            Image discardedImage = rankRoot.GetChild((int)Rank.Two - 1).GetComponentInChildren<Image>();

            Assert.That(primaryImage.color, Is.EqualTo(Color.white));
            Assert.That(discardedImage.color, Is.EqualTo(new Color(0.5f, 0.5f, 0.5f, 0.7f)));
        }
        finally
        {
            Object.DestroyImmediate(panelObject);
            Object.DestroyImmediate(prefabObject);
        }
    }

    private static GameObject CreateCardPrefab()
    {
        GameObject prefabObject = new("CardPrefab", typeof(RectTransform), typeof(Image), typeof(BattleUiCardView));
        Image image = prefabObject.GetComponent<Image>();
        BattleUiCardView cardView = prefabObject.GetComponent<BattleUiCardView>();
        cardView.Initialize(image, null, null);
        return prefabObject;
    }

    private static RectTransform CreateRankRoot(Transform parent)
    {
        RectTransform rankRoot = new GameObject("RankGridViewRoot", typeof(RectTransform)).GetComponent<RectTransform>();
        rankRoot.SetParent(parent, false);

        for (int i = 0; i < 13; i++)
        {
            RectTransform group = new GameObject($"RankGroup{i}", typeof(RectTransform)).GetComponent<RectTransform>();
            group.SetParent(rankRoot, false);
        }

        return rankRoot;
    }

    private static void SetPrivateField<T>(DeckViewPanel panel, string fieldName, T value)
    {
        FieldInfo field = typeof(DeckViewPanel).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(panel, value);
    }
}
#endif