using System.Collections;
using UnityEngine;

public sealed class BattleVisualCommandRunner : MonoBehaviour
{
    [SerializeField] private BattleController battleController;
    [SerializeField] private BattleUiPresenter battleUiPresenter;
    [SerializeField] private float commandDelaySeconds = 0.05f;

    private Coroutine _drainRoutine;

    private void Awake()
    {
        if (battleController == null)
            battleController = FindFirstObjectByType<BattleController>();

        if (battleUiPresenter == null)
            battleUiPresenter = GetComponent<BattleUiPresenter>();
    }

    private void OnEnable()
    {
        EventBus.Subscribe<BattleStartedEvent>(OnBattleStarted);
        EventBus.Subscribe<BattleEndedEvent>(OnBattleEnded);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<BattleStartedEvent>(OnBattleStarted);
        EventBus.Unsubscribe<BattleEndedEvent>(OnBattleEnded);
    }

    private void Update()
    {
        if (_drainRoutine == null && battleController != null && battleController.CommandQueue != null && battleController.CommandQueue.HasPendingCommands)
            _drainRoutine = StartCoroutine(DrainCommands());
    }

    private IEnumerator DrainCommands()
    {
        while (battleController != null && battleController.TryDequeueVisualCommand(out VisualCommand command))
        {
            yield return PlayCommand(command);
            battleController.NotifyVisualsResolved(command.Type);
            battleUiPresenter?.Refresh();
        }

        battleUiPresenter?.Refresh();
        _drainRoutine = null;
    }

    private IEnumerator PlayCommand(VisualCommand command)
    {
        battleUiPresenter?.Refresh();

        if (commandDelaySeconds > 0f)
            yield return new WaitForSeconds(commandDelaySeconds);
        else
            yield return null;
    }

    private void OnBattleStarted(BattleStartedEvent eventData)
    {
        if (isActiveAndEnabled && _drainRoutine == null)
            _drainRoutine = StartCoroutine(DrainCommands());
    }

    private void OnBattleEnded(BattleEndedEvent eventData)
    {
        if (isActiveAndEnabled && _drainRoutine == null)
            _drainRoutine = StartCoroutine(DrainCommands());
    }
}
