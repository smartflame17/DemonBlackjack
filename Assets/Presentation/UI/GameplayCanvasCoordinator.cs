using UnityEngine;
using UnityEngine.UI;

public sealed class GameplayCanvasCoordinator : MonoBehaviour
{
    [SerializeField] private RunManager runManager;
    [SerializeField] private Canvas battleCanvas;
    [SerializeField] private Canvas mapCanvas;
    [SerializeField] private Canvas shopCanvas;
    [SerializeField] private Canvas debugCanvas;
    [SerializeField] private Button battleTestButton;
    [SerializeField] private bool keepDebugCanvasVisible = true;

    private void Awake()
    {
        if (runManager == null)
            runManager = FindFirstObjectByType<RunManager>();

        battleCanvas ??= FindCanvas("BattleUI");
        mapCanvas ??= FindCanvas("MapUI");
        shopCanvas ??= FindCanvas("ShopUI");
        debugCanvas ??= FindCanvas("DebugCanvas");

        if (battleTestButton == null)
            battleTestButton = GameObject.Find("BattleTestButton")?.GetComponent<Button>();
    }

    private void OnEnable()
    {
        EventBus.Subscribe<RunPhaseChangedEvent>(OnRunPhaseChanged);

        if (battleTestButton != null)
            battleTestButton.onClick.AddListener(StartBattle);

        ApplyPhase(runManager != null && runManager.RunState != null ? runManager.RunState.Phase : RunPhase.Map);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<RunPhaseChangedEvent>(OnRunPhaseChanged);

        if (battleTestButton != null)
            battleTestButton.onClick.RemoveListener(StartBattle);
    }

    private void StartBattle()
    {
        runManager?.StartBattle();
    }

    private void OnRunPhaseChanged(RunPhaseChangedEvent eventData)
    {
        ApplyPhase(eventData.Phase);
    }

    private void ApplyPhase(RunPhase phase)
    {
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
}
