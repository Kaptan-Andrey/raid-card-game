using UnityEngine;
using Mirror;

public class Player : NetworkBehaviour
{
    [SyncVar] public int playerIndex;
    [SyncVar(hook = nameof(OnHandCountChanged))] public int handCount;

    public readonly SyncList<CardData> hand = new SyncList<CardData>();
    public readonly SyncList<CardData> field = new SyncList<CardData>();
    public readonly SyncList<CardData> trophies = new SyncList<CardData>();

    public override void OnStartServer()
    {
        base.OnStartServer();
        StartCoroutine(TryAddPlayer());
    }

    System.Collections.IEnumerator TryAddPlayer()
    {
        float timeout = 5f;
        while (GameManager.Instance == null && timeout > 0f)
        {
            yield return null;
            timeout -= Time.deltaTime;
        }
        if (GameManager.Instance != null)
            GameManager.Instance.AddPlayer(this);
        else
            Debug.LogError("GameManager still NULL after waiting!");
    }

    public override void OnStartClient()
    {
        hand.Callback += (op, i, o, n) =>
        {
            if (isOwned) UIManager.Instance?.RefreshOwnHand(this);
        };
        field.Callback += (op, i, o, n) => UIManager.Instance?.RefreshField(this);
        trophies.Callback += (op, i, o, n) => UIManager.Instance?.RefreshTrophies(this);

        if (isOwned && UIManager.Instance != null)
            UIManager.Instance.RefreshOwnHand(this);
    }

    void OnHandCountChanged(int oldCount, int newCount)
    {
        if (!isOwned)
            UIManager.Instance?.RefreshOpponentHand(this);
    }

    [Server]
    public void UpdateHandCount()
    {
        handCount = hand.Count;
    }

    // Команда перемещения карты (вызывается клиентом)
    [Command]
    public void CmdMoveCard(int cardId, ZoneType sourceZone, ZoneType targetZone)
    {
        // connectionToClient доступен, потому что это игрок
        GameManager.Instance.MoveCard(this, cardId, sourceZone, targetZone);
    }
    [Command]
    public void CmdDrawFromForest()
    {
        GameManager.Instance.DrawFromForest(this);
    }

    [Command]
    public void CmdDrawFromDeck()
    {
        GameManager.Instance.DrawFromDeck(this);
    }

    // --- Бой ---
    [Command]
    public void CmdAttack(int[] squadCardIds, ZoneType targetZone, int targetCardId, int targetPlayerIndex)
    {
        GameManager.Instance.Attack(this, squadCardIds, targetZone, targetCardId, targetPlayerIndex);
    }

    [Command]
    public void CmdReinforce(int prisonerId, int[] cardIds)
    {
        GameManager.Instance.Reinforce(this, prisonerId, cardIds);
    }

    // --- Зёрна и магия ---
    // targetPlayerIndex/targetPrisonerId нужны только для Тёмного зерна;
    // drawFromForest — только для Светлого (true = из Леса, false = из Колоды).
    [Command]
    public void CmdUseGrain(int prisonerId, int targetPlayerIndex, int targetPrisonerId, bool drawFromForest)
    {
        GameManager.Instance.UseGrain(this, prisonerId, targetPlayerIndex, targetPrisonerId, drawFromForest);
    }

    [Command]
    public void CmdDiscardMagicForGrains(int magicCardId, int targetPlayerIndex, int targetPrisonerId, bool drawFromForest)
    {
        GameManager.Instance.DiscardMagicForGrains(this, magicCardId, targetPlayerIndex, targetPrisonerId, drawFromForest);
    }

    // --- Трофеи ---
    [Command]
    public void CmdResolveTrophy(int prisonerId, TrophyChoice choice)
    {
        GameManager.Instance.PlayerResolveTrophy(this, prisonerId, choice);
    }

    // Сервер просит этого игрока выбрать судьбу спелых пленных
    [TargetRpc]
    public void TargetTrophyPrompt(int[] prisonerIds)
    {
        UIManager.Instance?.ShowTrophyPrompt(this, prisonerIds);
    }
}