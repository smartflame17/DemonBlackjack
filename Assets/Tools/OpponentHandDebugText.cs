using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public sealed class OpponentHandDebugText : MonoBehaviour
{
    [SerializeField] private BattleController battleController;
    [SerializeField] private TMP_Text text;
    [SerializeField] private bool autoFindBattleController = true;
    [SerializeField, Min(0.02f)] private float refreshInterval = 0.1f;

    private readonly StringBuilder _builder = new StringBuilder(256);
    private float _nextRefreshTime;

    private void Reset()
    {
        text = GetComponent<TMP_Text>();
    }

    private void Awake()
    {
        if (text == null)
            text = GetComponent<TMP_Text>();

        ResolveBattleController();
        RefreshText();
    }

    private void OnEnable()
    {
        _nextRefreshTime = 0f;
        RefreshText();
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextRefreshTime)
            return;

        _nextRefreshTime = Time.unscaledTime + refreshInterval;
        RefreshText();
    }

    private void RefreshText()
    {
        if (text == null)
            return;

        ResolveBattleController();

        BattleState battle = battleController != null ? battleController.BattleState : null;
        if (battle == null)
        {
            text.text = "Devil: none\nAffinity: 0\nOpponent hand: no active battle";
            return;
        }

        string devilId = battle.Config != null ? battle.Config.DevilId : null;
        int affinity = battle.RunState != null ? battle.RunState.GetDevilAffinity(devilId) : 0;
        IReadOnlyList<Card> opponentHand = battle.CurrentRound != null ? battle.CurrentRound.OpponentHand : null;

        _builder.Clear();
        _builder.Append("Devil: ");
        _builder.Append(string.IsNullOrWhiteSpace(devilId) ? "unknown" : devilId);
        _builder.Append('\n');
        _builder.Append("Affinity: ");
        _builder.Append(affinity);
        _builder.Append('\n');
        _builder.Append("Opponent hand");

        if (opponentHand == null)
        {
            _builder.Append(": no active round");
        }
        else if (opponentHand.Count == 0)
        {
            _builder.Append(": empty");
        }
        else
        {
            _builder.Append(" (");
            _builder.Append(opponentHand.Count);
            _builder.Append("):");

            for (int i = 0; i < opponentHand.Count; i++)
            {
                _builder.Append('\n');
                _builder.Append(i);
                _builder.Append(": ");
                _builder.Append(opponentHand[i]);
            }
        }

        text.text = _builder.ToString();
    }

    private void ResolveBattleController()
    {
        if (battleController != null || !autoFindBattleController)
            return;

        battleController = FindObjectOfType<BattleController>();
    }
}
