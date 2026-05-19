using System;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GameplayAssetRegistry))]
public sealed class GameplayAssetRegistryEditor : Editor
{
    private const string DefaultRegistryPath = "Assets/Content/GameplayAssetRegistry.asset";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        if (GUILayout.Button("Populate Missing Card Slots"))
            PopulateMissingCardSlots((GameplayAssetRegistry)target);
    }

    [MenuItem("Demon Blackjack/Create Gameplay Asset Registry")]
    public static void CreateRegistryAsset()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Content"))
            AssetDatabase.CreateFolder("Assets", "Content");

        GameplayAssetRegistry registry = AssetDatabase.LoadAssetAtPath<GameplayAssetRegistry>(DefaultRegistryPath);
        if (registry == null)
        {
            registry = CreateInstance<GameplayAssetRegistry>();
            AssetDatabase.CreateAsset(registry, DefaultRegistryPath);
        }

        PopulateMissingCardSlots(registry);
        Selection.activeObject = registry;
        EditorGUIUtility.PingObject(registry);
    }

    private static void PopulateMissingCardSlots(GameplayAssetRegistry registry)
    {
        if (registry == null)
            return;

        SerializedObject serializedRegistry = new(registry);
        SerializedProperty cardSprites = serializedRegistry.FindProperty("cardSprites");

        foreach (Suit suit in Enum.GetValues(typeof(Suit)))
        {
            foreach (Rank rank in Enum.GetValues(typeof(Rank)))
            {
                if (ContainsCardEntry(cardSprites, suit, rank))
                    continue;

                int index = cardSprites.arraySize;
                cardSprites.InsertArrayElementAtIndex(index);

                SerializedProperty entry = cardSprites.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("suit").enumValueIndex = (int)suit;
                entry.FindPropertyRelative("rank").enumValueIndex = (int)rank - 1;
                entry.FindPropertyRelative("sprite").objectReferenceValue = null;
            }
        }

        serializedRegistry.ApplyModifiedProperties();
        EditorUtility.SetDirty(registry);
        AssetDatabase.SaveAssets();
    }

    private static bool ContainsCardEntry(SerializedProperty cardSprites, Suit suit, Rank rank)
    {
        for (int i = 0; i < cardSprites.arraySize; i++)
        {
            SerializedProperty entry = cardSprites.GetArrayElementAtIndex(i);
            Suit entrySuit = (Suit)entry.FindPropertyRelative("suit").enumValueIndex;
            Rank entryRank = (Rank)(entry.FindPropertyRelative("rank").enumValueIndex + 1);

            if (entrySuit == suit && entryRank == rank)
                return true;
        }

        return false;
    }
}
