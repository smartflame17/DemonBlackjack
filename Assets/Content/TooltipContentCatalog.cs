using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TooltipContentCatalog", menuName = "Demon Blackjack/Tooltip Content Catalog")]
public sealed class TooltipContentCatalog : ScriptableObject
{
    [SerializeField] private List<TooltipContentDefinition> definitions = new();

    private Dictionary<string, TooltipContentDefinition> _lookup;

    public IReadOnlyList<TooltipContentDefinition> Definitions => definitions;

    public bool TryGetDefinition(string id, out TooltipContentDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            definition = null;
            return false;
        }

        EnsureLookup();
        return _lookup.TryGetValue(id, out definition) && definition != null;
    }

    public void Initialize(IEnumerable<TooltipContentDefinition> contentDefinitions)
    {
        definitions.Clear();
        if (contentDefinitions != null)
            definitions.AddRange(contentDefinitions);
        _lookup = null;
    }

    public static TooltipContentCatalog CreateRuntimeDefault()
    {
        TooltipContentCatalog tooltipCatalog = CreateInstance<TooltipContentCatalog>();
        tooltipCatalog.hideFlags = HideFlags.DontSave;

        ShopCatalog shopCatalog = ShopCatalog.CreateRuntimeDefault();
        for (int i = 0; i < shopCatalog.ActiveItems.Count; i++)
            tooltipCatalog.definitions.Add(shopCatalog.ActiveItems[i]);
        for (int i = 0; i < shopCatalog.Relics.Count; i++)
            tooltipCatalog.definitions.Add(shopCatalog.Relics[i]);
        for (int i = 0; i < shopCatalog.CardUpgrades.Count; i++)
            tooltipCatalog.definitions.Add(shopCatalog.CardUpgrades[i]);

        ActiveItemDefinition stockSell = CreateInstance<ActiveItemDefinition>();
        stockSell.hideFlags = HideFlags.DontSave;
        stockSell.Initialize(
            ActiveItemResolver.StockSell,
            "주식 매도",
            "사용 시 현재 가치만큼 주식을 매도합니다.\n원금: {stock_original_amount}\n현재 가치: {stock_current_amount}",
            0);
        tooltipCatalog.definitions.Add(stockSell);

        for (int i = 1; i <= 4; i++)
        {
            DevilAbilityDefinition definition = CreateInstance<DevilAbilityDefinition>();
            definition.hideFlags = HideFlags.DontSave;
            definition.Initialize($"devil{i}", "todo", "todo");
            tooltipCatalog.definitions.Add(definition);
        }

        return tooltipCatalog;
    }

    private void OnValidate()
    {
        _lookup = null;
        List<string> errors = GetValidationErrors();
        for (int i = 0; i < errors.Count; i++)
            Debug.LogError(errors[i], this);
    }

    private List<string> GetValidationErrors()
    {
        var errors = new List<string>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < definitions.Count; i++)
        {
            TooltipContentDefinition definition = definitions[i];
            if (definition == null)
                continue;

            if (string.IsNullOrWhiteSpace(definition.Id))
            {
                errors.Add($"{name} contains a tooltip definition with an empty ID.");
                continue;
            }

            if (!ids.Add(definition.Id))
                errors.Add($"{name} contains duplicate tooltip ID '{definition.Id}'.");
        }

        return errors;
    }

    private void EnsureLookup()
    {
        if (_lookup != null)
            return;

        _lookup = new Dictionary<string, TooltipContentDefinition>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < definitions.Count; i++)
        {
            TooltipContentDefinition definition = definitions[i];
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id) || _lookup.ContainsKey(definition.Id))
                continue;

            _lookup.Add(definition.Id, definition);
        }
    }
}
