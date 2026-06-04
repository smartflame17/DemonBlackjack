using UnityEngine;
using UnityEngine.UI;

public sealed class GameplayCanvasCoordinator : MonoBehaviour
{
    [SerializeField] private RunManager runManager;
    [SerializeField] private Canvas battleCanvas;
    [SerializeField] private Canvas mapCanvas;
    [SerializeField] private Canvas shopCanvas;
    [SerializeField] private Canvas debugCanvas;
    [SerializeField] private Button devil1BattleButton;
    [SerializeField] private Button devil2BattleButton;
    [SerializeField] private Button devil3BattleButton;
    [SerializeField] private Button devil4BattleButton;
    [SerializeField] private bool keepDebugCanvasVisible = true;

    private void Awake()
    {
        if (runManager == null)
            runManager = FindFirstObjectByType<RunManager>();

        battleCanvas ??= FindCanvas("BattleUI");
        mapCanvas ??= FindCanvas("MapUI");
        shopCanvas ??= FindCanvas("ShopUI");
        debugCanvas ??= FindCanvas("DebugCanvas");

        if (devil1BattleButton == null)
            devil1BattleButton = GameObject.Find("Devil1BattleButton")?.GetComponent<Button>();

        if (devil2BattleButton == null)
            devil2BattleButton = GameObject.Find("Devil2BattleButton")?.GetComponent<Button>();

        if (devil3BattleButton == null)
            devil3BattleButton = GameObject.Find("Devil3BattleButton")?.GetComponent<Button>();

        if (devil4BattleButton == null)
            devil4BattleButton = GameObject.Find("Devil4BattleButton")?.GetComponent<Button>();
    }

    private void OnEnable()
    {
        EventBus.Subscribe<RunPhaseChangedEvent>(OnRunPhaseChanged);

        if (devil1BattleButton != null)
            devil1BattleButton.onClick.AddListener(StartDevil1Battle);
        if (devil2BattleButton != null)
            devil2BattleButton.onClick.AddListener(StartDevil2Battle);
        if (devil3BattleButton != null)
            devil3BattleButton.onClick.AddListener(StartDevil3Battle);
        if (devil4BattleButton != null)
            devil4BattleButton.onClick.AddListener(StartDevil4Battle);

        ApplyPhase(runManager != null && runManager.RunState != null ? runManager.RunState.Phase : RunPhase.Map);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<RunPhaseChangedEvent>(OnRunPhaseChanged);

        if (devil1BattleButton != null)
            devil1BattleButton.onClick.RemoveListener(StartDevil1Battle);
        if (devil2BattleButton != null)
            devil2BattleButton.onClick.RemoveListener(StartDevil2Battle);
        if (devil3BattleButton != null)
            devil3BattleButton.onClick.RemoveListener(StartDevil3Battle);
        if (devil4BattleButton != null)
            devil4BattleButton.onClick.RemoveListener(StartDevil4Battle);
    }

    private void StartBattle()
    {
        runManager?.StartBattle();
    }

    private void StartDevil1Battle()
    {
        Debug.Log("Starting devil1 battle");
        StartBattle("devil1");
    }

    private void StartDevil2Battle()
    {
        Debug.Log("Starting devil2 battle");
        StartBattle("devil2");
    }

    private void StartDevil3Battle()
    {
        Debug.Log("Starting devil3 battle");
        StartBattle("devil3");
    }

    private void StartDevil4Battle()
    {
        Debug.Log("Starting devil4 battle");
        StartBattle("devil4");
    }

    private void StartBattle(string devilId)
    {
        if (runManager == null)
            return;

        var config = new BattleConfig(devilId, 100, devilId: devilId);
        runManager.StartBattle(config);
    }

    private void OnRunPhaseChanged(RunPhaseChangedEvent eventData)
    {
        ApplyPhase(eventData.Phase);
    }

    private void ApplyPhase(RunPhase phase)
    {
        if (phase != RunPhase.Battle && battleCanvas != null)
        {
            battleCanvas.GetComponent<BattleUiPresenter>()?.ClearGeneratedBattleCards();
            ClearGeneratedCardsUnder(battleCanvas.transform, "PlayerHand");
            ClearGeneratedCardsUnder(battleCanvas.transform, "DevilHand");
            ClearGeneratedCardsUnder(battleCanvas.transform, "PlayPile");
        }

        SetCanvasActive(battleCanvas, phase == RunPhase.Battle);
        SetCanvasActive(mapCanvas, phase == RunPhase.Map || phase == RunPhase.Encounter || phase == RunPhase.Rewards);
        SetCanvasActive(shopCanvas, phase == RunPhase.Shop);
        SetCanvasActive(debugCanvas, keepDebugCanvasVisible);
    }

    private static void SetCanvasActive(Canvas canvas, bool active)
    {
        if (canvas != null)
            canvas.gameObject.SetActive(active);
    }

    private static Canvas FindCanvas(string name)
    {
        Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i].gameObject.name == name)
                return canvases[i];
        }

        return null;
    }

    private static void ClearGeneratedCardsUnder(Transform root, string containerName)
    {
        if (root == null)
            return;

        Transform container = FindChildRecursive(root, containerName);
        if (container == null)
            return;

        for (int i = container.childCount - 1; i >= 0; i--)
            DestroyImmediate(container.GetChild(i).gameObject);
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
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
