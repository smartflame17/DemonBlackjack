#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public sealed class ShopInflationTests
{
    private Assembly runtimeAssembly;
    private Type battleConfigType;
    private Type battleStateType;
    private Type runStateDataType;
    private Type runStateType;
    private Type shopOfferGeneratorType;

    [SetUp]
    public void SetUp()
    {
        runtimeAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly => assembly.GetName().Name == "Assembly-CSharp");
        Assert.That(runtimeAssembly, Is.Not.Null);

        battleConfigType = RequireType("BattleConfig");
        battleStateType = RequireType("BattleState");
        runStateDataType = RequireType("RunStateData");
        runStateType = RequireType("RunState");
        shopOfferGeneratorType = RequireType("ShopOfferGenerator");
    }

    [Test]
    public void ShopPrice_UsesBasePriceCurrentInflationAndConfiguredRounding()
    {
        int price = (int)InvokeStatic(shopOfferGeneratorType, "CalculatePrice", 1050, 1.2f);
        int minimumPositivePrice = (int)InvokeStatic(shopOfferGeneratorType, "CalculatePrice", 10, 1f);

        Assert.That(price, Is.EqualTo(1300));
        Assert.That(minimumPositivePrice, Is.EqualTo(100));
    }

    [Test]
    public void ShopPriceInflation_AdvancesWhenRoundResolves()
    {
        object run = CreateRun();
        object battle = Activator.CreateInstance(battleStateType, run, CreateBattleConfig());

        try
        {
            Assert.That((bool)Invoke(battle, "StartRound", 10), Is.True);
            object round = GetProperty(battle, "CurrentRound");
            Invoke(round, "MarkOpponentStood");
            Assert.That((bool)Invoke(battle, "TryStand"), Is.True);

            Type gameSettingsType = RequireType("GameplayConstants.GameSettingConfig");
            float configuredRate = (float)gameSettingsType
                .GetField("ShopItemPriceInflationRate", BindingFlags.Public | BindingFlags.Static)
                .GetValue(null);
            Assert.That((float)GetProperty(run, "CurrentShopPriceInflationRate"), Is.EqualTo(configuredRate).Within(0.0001f));
        }
        finally
        {
            Invoke(battle, "Dispose");
        }
    }

    [Test]
    public void ShopPriceInflation_PersistsThroughSaveAndLoad()
    {
        object run = CreateRun();
        Invoke(run, "AdvanceShopPriceInflation");
        Invoke(run, "AdvanceShopPriceInflation");

        object data = Invoke(run, "ToData");
        object loaded = InvokeStatic(runStateType, "FromData", data, 100);

        Assert.That(
            (float)GetProperty(loaded, "CurrentShopPriceInflationRate"),
            Is.EqualTo((float)GetProperty(run, "CurrentShopPriceInflationRate")));
    }

    [Test]
    public void LegacySave_DefaultsShopPriceInflationToOne()
    {
        object data = Activator.CreateInstance(runStateDataType);
        runStateDataType.GetField("seed").SetValue(data, 7);
        runStateDataType.GetField("money").SetValue(data, 100);

        object loaded = InvokeStatic(runStateType, "FromData", data, 100);

        float defaultRate = (float)shopOfferGeneratorType
            .GetField("DefaultPriceInflationRate", BindingFlags.Public | BindingFlags.Static)
            .GetValue(null);
        Assert.That((float)GetProperty(loaded, "CurrentShopPriceInflationRate"), Is.EqualTo(defaultRate));
    }

    private object CreateRun()
    {
        return Activator.CreateInstance(runStateType, new object[] { 7, 100, 3 });
    }

    private object CreateBattleConfig()
    {
        return Activator.CreateInstance(
            battleConfigType,
            new object[] { "inflation-test", 100, null, 3, 21, 10, null, null, null, null });
    }

    private Type RequireType(string name)
    {
        Type type = runtimeAssembly.GetType(name);
        Assert.That(type, Is.Not.Null, name);
        return type;
    }

    private static object Invoke(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        Assert.That(method, Is.Not.Null, methodName);
        return method.Invoke(target, arguments);
    }

    private static object InvokeStatic(Type type, string methodName, params object[] arguments)
    {
        MethodInfo method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public);
        Assert.That(method, Is.Not.Null, methodName);
        return method.Invoke(null, arguments);
    }

    private static object GetProperty(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        Assert.That(property, Is.Not.Null, propertyName);
        return property.GetValue(target);
    }
}
#endif