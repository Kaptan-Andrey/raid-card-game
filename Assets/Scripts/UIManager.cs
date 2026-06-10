using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("Containers")]
    public Transform Player1HandContainer;
    public Transform Player2HandContainer;
    public Transform Player1FieldContainer;
    public Transform Player2FieldContainer;
    public Transform Player1TrophyContainer;
    public Transform Player2TrophyContainer;
    public Transform DeckContainer;
    public Transform DiscardContainer;
    public Transform ForestContainer;
    public Transform ReidContainer;
    public Transform ReiderContainer;

    [Header("Prefabs")]
    public GameObject cardPrefab;
    public GameObject cardBackPrefab;

    [Header("UI")]
    public TextMeshProUGUI phaseText;
    public Button endTurnButton;

    private Dictionary<int, CardView> activeCards = new Dictionary<int, CardView>();
    private CardView deckIndicator; // объект-рубашка для колоды
    private HashSet<int> selectedSquad = new HashSet<int>(); // выбранные карты руки для атаки

    void Awake()
    {
        Instance = this;
        activeCards.Clear();
    }

    

    IEnumerator Start()
    {
        while (GameManager.Instance == null)
            yield return null;

        // Подписка на общие зоны
        GameManager.Instance.forest.Callback += (op, i, o, n) => SyncZoneFromList(ForestContainer, GameManager.Instance.forest, ZoneType.Forest);
        GameManager.Instance.reidTargets.Callback += (op, i, o, n) => SyncZoneFromList(ReidContainer, GameManager.Instance.reidTargets, ZoneType.Reid);
        GameManager.Instance.reiderTarget.Callback += (op, i, o, n) => SyncZoneFromList(ReiderContainer, GameManager.Instance.reiderTarget, ZoneType.Reider);
        GameManager.Instance.discard.Callback += (op, i, o, n) => SyncZoneFromList(DiscardContainer, GameManager.Instance.discard, ZoneType.Discard);
        // Для колоды используем специальный индикатор, а не множество карт
        GameManager.Instance.monsterDeck.Callback += (op, i, o, n) => UpdateDeckIndicator();

        // Принудительная синхронизация при старте
        SyncZoneFromList(ForestContainer, GameManager.Instance.forest, ZoneType.Forest);
        SyncZoneFromList(ReidContainer, GameManager.Instance.reidTargets, ZoneType.Reid);
        SyncZoneFromList(ReiderContainer, GameManager.Instance.reiderTarget, ZoneType.Reider);
        SyncZoneFromList(DiscardContainer, GameManager.Instance.discard, ZoneType.Discard);
        UpdateDeckIndicator();

        // Назначаем обработчик кнопки
        StartCoroutine(WaitForClientAndAssignButton());
        // Тест: создать одну карту в руке напрямую
        if (cardPrefab != null && Player1HandContainer != null)
        {
            GameObject testCard = Instantiate(cardPrefab, Player1HandContainer);
            testCard.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            Debug.Log("Test card created");
        }
    }

    IEnumerator WaitForClientAndAssignButton()
    {
        while (!NetworkClient.active)
            yield return null;

        if (endTurnButton != null)
        {
            endTurnButton.onClick.AddListener(() =>
            {
                if (NetworkClient.active && GameManager.Instance != null)
                    GameManager.Instance.CmdEndTurn();
            });
        }
    }

    void UpdateDeckIndicator()
    {
        // Удаляем старый индикатор, если есть
        if (deckIndicator != null)
        {
            Destroy(deckIndicator.gameObject);
            deckIndicator = null;
        }

        // Если колода не пуста, создаём одну карту рубашкой как индикатор
        if (GameManager.Instance != null && GameManager.Instance.monsterDeck.Count > 0)
        {
            GameObject cardObj = Instantiate(cardBackPrefab, DeckContainer);
            CardView view = cardObj.GetComponent<CardView>();
            if (view != null)
            {
                view.currentZone = ZoneType.Deck;
                view.isLocalPlayer = false; // нельзя перетаскивать
                deckIndicator = view;
            }
        }
    }

    // ---- Выбор отряда и атака (клик в фазе действий) ----
    public void HandleActionClick(CardView view, Player localPlayer)
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.players.IndexOf(localPlayer) != GameManager.Instance.currentPlayerIndex) return;

        ZoneType myHand = localPlayer.playerIndex == 0 ? ZoneType.Player1Hand : ZoneType.Player2Hand;

        // Клик по своей карте руки — добавить/убрать из отряда
        if (view.currentZone == myHand)
        {
            ToggleSquad(view);
            return;
        }

        // Клик по цели — атаковать выбранным отрядом
        int[] squad = SquadArray();
        if (squad.Length == 0) { Debug.Log("Сначала выберите карты отряда (клик по руке)."); return; }

        if (view.currentZone == ZoneType.Reid || view.currentZone == ZoneType.Reider)
        {
            localPlayer.CmdAttack(squad, view.currentZone, view.cardId, -1);
            ClearSquad();
        }
        else if (view.currentZone == ZoneType.Player1Field || view.currentZone == ZoneType.Player2Field)
        {
            int targetPlayerIndex = view.currentZone == ZoneType.Player1Field ? 0 : 1;
            if (targetPlayerIndex == localPlayer.playerIndex) return; // свой пленный — не цель атаки
            localPlayer.CmdAttack(squad, view.currentZone, view.cardId, targetPlayerIndex);
            ClearSquad();
        }
    }

    void ToggleSquad(CardView view)
    {
        if (selectedSquad.Contains(view.cardId))
        {
            selectedSquad.Remove(view.cardId);
            view.SetSelected(false);
        }
        else
        {
            selectedSquad.Add(view.cardId);
            view.SetSelected(true);
        }
    }

    int[] SquadArray()
    {
        var list = new List<int>(selectedSquad);
        return list.ToArray();
    }

    void ClearSquad()
    {
        foreach (int id in selectedSquad)
            if (activeCards.TryGetValue(id, out CardView v) && v != null) v.SetSelected(false);
        selectedSquad.Clear();
    }

    // Обновление своей руки
    public void RefreshOwnHand(Player player)
    {
        if (!player.isOwned) return;
        selectedSquad.Clear(); // рука перерисовывается — сбрасываем выбор
        Transform container = player.playerIndex == 0 ? Player1HandContainer : Player2HandContainer;
        ZoneType zone = player.playerIndex == 0 ? ZoneType.Player1Hand : ZoneType.Player2Hand;

        foreach (Transform child in container)
        {
            CardView view = child.GetComponent<CardView>();
            if (view != null) activeCards.Remove(view.cardId);
            Destroy(child.gameObject);
        }

        for (int i = 0; i < player.hand.Count; i++)
        {
            CardData card = player.hand[i];
            GameObject cardObj = Instantiate(cardPrefab, container);
            CardView view = cardObj.GetComponent<CardView>();
            view.cardId = card.id;
            view.currentZone = zone;
            view.ownerPlayerId = player.netId.GetHashCode();
            view.isLocalPlayer = true;
            activeCards[card.id] = view;
        }

        container.GetComponent<HandFanLayout>()?.UpdateFan();
    }

    // Рука противника
    public void RefreshOpponentHand(Player player)
    {
        if (player.isOwned) return;
        Transform container = player.playerIndex == 0 ? Player1HandContainer : Player2HandContainer;
        foreach (Transform child in container)
            Destroy(child.gameObject);
        for (int i = 0; i < player.handCount; i++)
        {
            GameObject cardObj = Instantiate(cardBackPrefab, container);
            CardView view = cardObj.GetComponent<CardView>();
            if (view != null) view.SetAsCardBack();
        }
        container.GetComponent<HandFanLayout>()?.UpdateFan();
    }

    public void RefreshField(Player player)
    {
        Transform container = player.playerIndex == 0 ? Player1FieldContainer : Player2FieldContainer;
        ZoneType zone = player.playerIndex == 0 ? ZoneType.Player1Field : ZoneType.Player2Field;
        SyncPersonalZone(container, player.field, zone, player);
    }

    public void RefreshTrophies(Player player)
    {
        Transform container = player.playerIndex == 0 ? Player1TrophyContainer : Player2TrophyContainer;
        ZoneType zone = player.playerIndex == 0 ? ZoneType.Player1Trophy : ZoneType.Player2Trophy;
        SyncPersonalZone(container, player.trophies, zone, player);
    }

    void SyncZoneFromList(Transform container, SyncList<CardData> list, ZoneType zone)
    {
        // 1. Полностью очищаем контейнер и словарь
        foreach (Transform child in container)
        {
            CardView view = child.GetComponent<CardView>();
            if (view != null) activeCards.Remove(view.cardId);
            Destroy(child.gameObject);
        }

        // 2. Создаём все карты заново
        for (int i = 0; i < list.Count; i++)
        {
            CardData card = list[i];
            GameObject cardObj = Instantiate(cardPrefab, container);
            CardView view = cardObj.GetComponent<CardView>();
            RectTransform rt = cardObj.GetComponent<RectTransform>();
            rt.localPosition = Vector3.zero;
            rt.localScale = Vector3.one;
            rt.anchoredPosition = Vector2.zero;

            view.cardId = card.id;
            view.currentZone = zone;
            view.ownerPlayerId = 0;
            view.isLocalPlayer = false;
            activeCards[card.id] = view;
        }
    }

    void SyncPersonalZone(Transform container, SyncList<CardData> list, ZoneType zone, Player owner)
    {
        List<int> currentIds = new List<int>();
        foreach (var c in list) currentIds.Add(c.id);

        foreach (Transform child in container)
        {
            CardView view = child.GetComponent<CardView>();
            if (view != null && !currentIds.Contains(view.cardId))
            {
                Destroy(child.gameObject);
                activeCards.Remove(view.cardId);
            }
        }

        for (int i = 0; i < list.Count; i++)
        {
            CardData card = list[i];
            if (!activeCards.ContainsKey(card.id))
            {
                GameObject cardObj = Instantiate(cardPrefab, container);
                CardView view = cardObj.GetComponent<CardView>();
                view.cardId = card.id;
                view.currentZone = zone;
                view.ownerPlayerId = owner.netId.GetHashCode();
                view.isLocalPlayer = owner.isOwned;
                activeCards[card.id] = view;
            }
        }
    }

    public void MoveCardToZone(CardView view, ZoneType zone, int ownerPlayerId)
    {
        Transform target = GetContainerForZone(zone, ownerPlayerId);
        if (target == null) return;
        view.transform.SetParent(target);
        view.currentZone = zone;
        view.ownerPlayerId = ownerPlayerId;
    }

    Transform GetContainerForZone(ZoneType zone, int ownerPlayerId)
    {
        switch (zone)
        {
            case ZoneType.Player1Hand: return Player1HandContainer;
            case ZoneType.Player2Hand: return Player2HandContainer;
            case ZoneType.Player1Field: return Player1FieldContainer;
            case ZoneType.Player2Field: return Player2FieldContainer;
            case ZoneType.Player1Trophy: return Player1TrophyContainer;
            case ZoneType.Player2Trophy: return Player2TrophyContainer;
            case ZoneType.Deck: return DeckContainer;
            case ZoneType.Discard: return DiscardContainer;
            case ZoneType.Forest: return ForestContainer;
            case ZoneType.Reid: return ReidContainer;
            case ZoneType.Reider: return ReiderContainer;
            default: return null;
        }
    }

    public void UpdatePhaseDisplay(Phase newPhase)
    {
        if (phaseText != null)
            phaseText.text = "Фаза: " + newPhase.ToString();

        if (endTurnButton != null)
        {
            bool isMyTurn = false;
            if (GameManager.Instance != null)
            {
                Player localPlayer = NetworkClient.connection?.identity?.GetComponent<Player>();
                if (localPlayer != null)
                    isMyTurn = (GameManager.Instance.players.IndexOf(localPlayer) == GameManager.Instance.currentPlayerIndex);
            }
            // В фазе добора кнопка завершает добор, в фазе действий — заканчивает ход
            endTurnButton.interactable = (newPhase == Phase.Draw || newPhase == Phase.Action) && isMyTurn;
        }
    }

    // Сервер сообщил, что игра окончена
    public void ShowWinner(int winnerIndex)
    {
        if (phaseText != null)
            phaseText.text = "Победил игрок " + (winnerIndex + 1) + "!";
        if (endTurnButton != null)
            endTurnButton.interactable = false;
    }

    // Сервер просит выбрать судьбу спелых пленных.
    // TODO: показать настоящее окно с кнопками "Убить" / "Магия" по каждому пленному.
    // Кнопки должны вызывать player.CmdResolveTrophy(prisonerId, TrophyChoice.Kill | UseMagic).
    public void ShowTrophyPrompt(Player player, int[] prisonerIds)
    {
        if (prisonerIds == null) return;
        Debug.Log($"Нужно выбрать трофей для {prisonerIds.Length} пленных. " +
                  "Подключи UI и вызови player.CmdResolveTrophy(id, choice).");
    }
}