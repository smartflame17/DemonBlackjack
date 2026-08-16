using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class SettingsMenuTests
{
    private Type displayControllerType;
    private Type resolutionOptionType;

    [SetUp]
    public void SetUp()
    {
        Assembly runtimeAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly => assembly.GetName().Name == "Assembly-CSharp");
        Assert.That(runtimeAssembly, Is.Not.Null);

        displayControllerType = runtimeAssembly.GetType("DisplaySettingsController");
        resolutionOptionType = runtimeAssembly.GetType("DisplayResolutionOption");
        Assert.That(displayControllerType, Is.Not.Null);
        Assert.That(resolutionOptionType, Is.Not.Null);
    }

    [Test]
    public void ResolutionOptions_KeepDistinctRefreshRatesAndSortDescending()
    {
        object normalized = Normalize(
            NewOption(1280, 720, 60, 1),
            NewOption(1920, 1080, 60, 1),
            NewOption(1920, 1080, 144, 1),
            NewOption(1920, 1080, 60, 1));
        List<object> result = ToObjectList(normalized);

        Assert.That(result, Has.Count.EqualTo(3));
        AssertOption(result[0], 1920, 1080, 144, 1);
        AssertOption(result[1], 1920, 1080, 60, 1);
        AssertOption(result[2], 1280, 720, 60, 1);
    }

    [Test]
    public void ResolutionLabel_FormatsFractionalRefreshRate()
    {
        MethodInfo format = GetStaticMethod("FormatResolutionOption");
        string label = (string)format.Invoke(
            null,
            new[] { NewOption(1920, 1080, 60000, 1001) });

        Assert.That(label, Is.EqualTo("1920 × 1080 @ 59.94 Hz"));
    }

    [Test]
    public void ResolutionFallback_PrefersDesiredDimensionsThenCurrentResolution()
    {
        object available = Normalize(
            NewOption(1920, 1080, 60, 1),
            NewOption(1920, 1080, 144, 1),
            NewOption(1280, 720, 60, 1));
        MethodInfo resolve = GetStaticMethod("ResolveResolutionOption");

        object sameSizeFallback = resolve.Invoke(
            null,
            new[]
            {
                available,
                NewOption(1920, 1080, 165, 1),
                NewOption(1280, 720, 60, 1)
            });
        AssertOption(sameSizeFallback, 1920, 1080, 144, 1);

        object currentFallback = resolve.Invoke(
            null,
            new[]
            {
                available,
                NewOption(2560, 1440, 144, 1),
                NewOption(1280, 720, 60, 1)
            });
        AssertOption(currentFallback, 1280, 720, 60, 1);
    }

    [Test]
    public void MenuCanvas_SettingsPrefabHasOrderedTabsAndControllers()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Scene-wide Prefabs/MenuCanvas.prefab");
        Assert.That(prefab, Is.Not.Null);

        GameObject instance = UnityEngine.Object.Instantiate(prefab);
        try
        {
            Transform settingMenu = FindChildRecursive(instance.transform, "SettingMenu");
            Assert.That(settingMenu, Is.Not.Null);
            Component menuController = GetComponentNamed(settingMenu, "SettingMenuController");
            Assert.That(menuController, Is.Not.Null);

            Transform tabBar = FindChildRecursive(settingMenu, "TabBar");
            Assert.That(tabBar, Is.Not.Null);
            Assert.That(GetComponentNamed(tabBar, "HorizontalLayoutGroup"), Is.Not.Null);
            Assert.That(tabBar.childCount, Is.EqualTo(3));
            Assert.That(tabBar.GetChild(0).name, Is.EqualTo("GeneralTabButton"));
            Assert.That(tabBar.GetChild(1).name, Is.EqualTo("DisplayTabButton"));
            Assert.That(tabBar.GetChild(2).name, Is.EqualTo("AudioTabButton"));

            Transform generalTab = FindChildRecursive(settingMenu, "GeneralTab");
            Transform displayTab = FindChildRecursive(settingMenu, "DisplayTab");
            Transform audioTab = FindChildRecursive(settingMenu, "AudioTab");
            Assert.That(generalTab, Is.Not.Null);
            Assert.That(displayTab, Is.Not.Null);
            Assert.That(audioTab, Is.Not.Null);
            Assert.That(GetComponentNamed(generalTab, "GeneralSettingsController"), Is.Not.Null);
            Assert.That(GetComponentNamed(displayTab, "DisplaySettingsController"), Is.Not.Null);
            Assert.That(GetComponentNamed(audioTab, "VolumeSettingsController"), Is.Not.Null);
            Assert.That(CountComponentsNamed(displayTab, "TMP_Dropdown"), Is.EqualTo(2));
            Assert.That(FindChildRecursive(generalTab, "ReturnToMainMenuButton"), Is.Not.Null);

            MethodInfo showTab = menuController.GetType().GetMethod(
                "ShowTab",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(showTab, Is.Not.Null);

            showTab.Invoke(menuController, new object[] { 1 });
            Assert.That(generalTab.gameObject.activeSelf, Is.False);
            Assert.That(displayTab.gameObject.activeSelf, Is.True);
            Assert.That(audioTab.gameObject.activeSelf, Is.False);

            showTab.Invoke(menuController, new object[] { 0 });
            Assert.That(generalTab.gameObject.activeSelf, Is.True);
            Assert.That(displayTab.gameObject.activeSelf, Is.False);
            Assert.That(audioTab.gameObject.activeSelf, Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void MenuScene_UsesSharedSettingsMenuWithoutReturnButton()
    {
        Scene scene = EditorSceneManager.OpenScene(
            "Assets/Scenes/MenuScene.unity",
            OpenSceneMode.Additive);

        try
        {
            Transform settingMenuButton = FindInScene(scene, "SettingMenuButton");
            Assert.That(settingMenuButton, Is.Not.Null);
            Assert.That(FindInScene(scene, "LoadGameButton"), Is.Null);

            Button button = settingMenuButton.GetComponent<Button>();
            Assert.That(button, Is.Not.Null);
            Assert.That(button.interactable, Is.True);

            Component hover = GetComponentNamed(settingMenuButton, "EnlargeOnHover");
            Assert.That(hover, Is.InstanceOf<Behaviour>());
            Assert.That(((Behaviour)hover).enabled, Is.True);

            Transform labelTransform = FindChildRecursive(settingMenuButton, "Text (TMP)");
            Component label = GetComponentNamed(labelTransform, "TextMeshProUGUI");
            Assert.That(label, Is.Not.Null);
            PropertyInfo textProperty = label.GetType().GetProperty("text");
            Assert.That(textProperty, Is.Not.Null);
            Assert.That(textProperty.GetValue(label), Is.EqualTo("설정"));

            Transform settingMenu = FindInScene(scene, "SettingMenu");
            Assert.That(settingMenu, Is.Not.Null);
            Assert.That(settingMenu.gameObject.activeSelf, Is.False);
            Component settingMenuController = GetComponentNamed(settingMenu, "SettingMenuController");
            Assert.That(settingMenuController, Is.Not.Null);

            Transform returnButton = FindChildRecursive(settingMenu, "ReturnToMainMenuButton");
            Assert.That(returnButton, Is.Not.Null);
            Assert.That(returnButton.gameObject.activeSelf, Is.False);

            Component mainMenu = FindComponentInScene(scene, "MainMenu");
            Assert.That(mainMenu, Is.Not.Null);
            SerializedObject serializedMainMenu = new SerializedObject(mainMenu);
            Assert.That(
                serializedMainMenu.FindProperty("settingMenuButton").objectReferenceValue,
                Is.SameAs(button));
            Assert.That(
                serializedMainMenu.FindProperty("settingMenuController").objectReferenceValue,
                Is.SameAs(settingMenuController));
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    private object Normalize(params object[] options)
    {
        Array typedOptions = Array.CreateInstance(resolutionOptionType, options.Length);
        for (int i = 0; i < options.Length; i++)
            typedOptions.SetValue(options[i], i);

        return GetStaticMethod("NormalizeResolutionOptions").Invoke(null, new object[] { typedOptions });
    }

    private object NewOption(int width, int height, uint numerator, uint denominator)
    {
        return Activator.CreateInstance(
            resolutionOptionType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new object[] { width, height, numerator, denominator },
            null);
    }

    private MethodInfo GetStaticMethod(string name)
    {
        MethodInfo method = displayControllerType.GetMethod(
            name,
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, name);
        return method;
    }

    private void AssertOption(
        object option,
        int width,
        int height,
        uint numerator,
        uint denominator)
    {
        Assert.That(GetProperty<int>(option, "Width"), Is.EqualTo(width));
        Assert.That(GetProperty<int>(option, "Height"), Is.EqualTo(height));
        Assert.That(GetProperty<uint>(option, "RefreshNumerator"), Is.EqualTo(numerator));
        Assert.That(GetProperty<uint>(option, "RefreshDenominator"), Is.EqualTo(denominator));
    }

    private T GetProperty<T>(object target, string propertyName)
    {
        PropertyInfo property = resolutionOptionType.GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(property, Is.Not.Null, propertyName);
        return (T)property.GetValue(target);
    }

    private static List<object> ToObjectList(object enumerable)
    {
        return ((IEnumerable)enumerable).Cast<object>().ToList();
    }

    private static Component GetComponentNamed(Transform root, string typeName)
    {
        if (root == null)
            return null;

        return root.GetComponents<Component>()
            .FirstOrDefault(component => component != null && component.GetType().Name == typeName);
    }

    private static Component FindComponentInScene(Scene scene, string typeName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Component[] components = root.GetComponentsInChildren<Component>(true);
            Component result = components.FirstOrDefault(
                component => component != null && component.GetType().Name == typeName);
            if (result != null)
                return result;
        }

        return null;
    }

    private static Transform FindInScene(Scene scene, string childName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform result = FindChildRecursive(root.transform, childName);
            if (result != null)
                return result;
        }

        return null;
    }

    private static int CountComponentsNamed(Transform root, string typeName)
    {
        int count = 0;
        Component[] components = root.GetComponentsInChildren<Component>(true);
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] != null && components[i].GetType().Name == typeName)
                count++;
        }
        return count;
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
}
