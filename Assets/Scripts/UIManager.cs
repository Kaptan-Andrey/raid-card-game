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

    [Header("Боевой UI (необязательные ссылки)")]
    public TextMeshProUGUI squadInfoText;   // строка про выбранный отряд: племя, сила, цель
    public TextMeshProUGUI messageText;      // сообщения игроку (ошибки/подсказки)
    public Button reinforceButton;           // переключить режим: Атака / Усилить защиту
    public Button cancelButton;              // сбросить выбор отряда и режим

    [Header("Окно выбора трофея (необязательное)")]
    public GameObject trophyPanel;           // панель, по умолчанию выключена
    public TextMeshProUGUI trophyPromptText; // текст про текущего пленного
    public Button trophyKillButton;          // "Убить"
    public Button trophyMagicButton;         // "Использовать магию"

    private Dictionary<int, CardView> activeCards = new Dictionary<int, CardView>();
    private CardView deckIndicator; // объект-рубашка для колоды
    private HashSet<int> selectedSquad = new HashSet<int>(); // выбранные карты руки для атаки

    private enum ActionMode { Attack, Reinforce }
    private ActionMode mode = ActionMode.Attack;

    // Очередь спелых пленных для окна трофея
    private Player trophyPlayer;
    private List<int> trophyQueue = new List<int>();
    private Coroutine messageRoutine;

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

        // Назначаем обработчики кнопок
        WireBattleButtons();
        if (trophyPanel != null) trophyPanel.SetActive(false);
        StartCoroutine(WaitForClientAndAssignButton());
    }

    void WireBattleButtons()
    {
        if (reinforceButton != null)
            reinforceButton.onClick.AddListener(ToggleMode);
        if (cancelButton != null)
            cancelButton.onClick.AddListener(() => { ClearSquad(); mode = ActionMode.Attack; UpdateSquadInfo(); });

        if (trophyKillButton != null)
            trophyKillButton.onClick.AddListener(() => ResolveCurrentTrophy(TrophyChoice.Kill));
        if (trophyMagicButton != null)
            trophyMagicButton.onClick.AddListener(() => ResolveCurrentTrophy(TrophyChoice.UseMagic));

        UpdateSquadInfo();
    }

    void ToggleMode()
    {
        mode = mode == ActionMode.Attack ? ActionMode.Reinforce : ActionMode.Attack;
        UpdateSquadInfo();
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

    // ---- Выбор отряда, атака и усиление (клик в фазе действий) ----
    public void HandleActionClick(CardView view, Player localPlayer)
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.players.IndexOf(localPlayer) != GameManager.Instance.currentPlayerIndex)
        { Msg("Сейчас не ваш ход."); return; }

        ZoneType myHand = localPlayer.playerIndex == 0 ? ZoneType.Player1Hand : ZoneType.Player2Hand;
        ZoneType myField = localPlayer.playerIndex == 0 ? ZoneType.Player1Field : ZoneType.Player2Field;

        // Клик по своей карте руки — добавить/убрать из отряда
        if (view.currentZone == myHand)
        {
            ToggleSquad(view);
            UpdateSquadInfo();
            return;
        }

        int[] squad = SquadArray();
        if (squad.Length == 0) { Msg("Сначала выберите карты отряда (клик по своей руке)."); return; }

        // --- Режим усиления защиты: цель — свой пленный ---
        if (mode == ActionMode.Reinforce)
        {
            if (view.currentZone == myField && view.data.cardType == CardType.Settler && view.data.attachedToId == 0)
            {
                localPlayer.CmdReinforce(view.cardId, squad);
                Msg("Усиление отправлено на пленного «" + view.data.cardName + "».");
            }
            else Msg("Для усиления кликните по СВОЕМУ пленному (поселенцу).");
            return;
        }

        // --- Режим атаки: цель — Рейд или пленный противника ---
        if (view.currentZone == ZoneType.Reid || view.currentZone == ZoneType.Reider)
        {
            WarnIfWeak(squad, view.data.strength, "цели");
            localPlayer.CmdAttack(squad, view.currentZone, view.cardId, -1);
            Msg("Атака на «" + view.data.cardName + "» (сила цели " + view.data.strength + ").");
        }
        else if (view.currentZone == ZoneType.Player1Field || view.currentZone == ZoneType.Player2Field)
        {
            int targetPlayerIndex = view.currentZone == ZoneType.Player1Field ? 0 : 1;
            if (targetPlayerIndex == localPlayer.playerIndex) { Msg("Это ваш пленный — атаковать нельзя."); return; }
            if (view.data.cardType != CardType.Settler || view.data.attachedToId != 0)
            { Msg("Перехватывать можно только пленного-поселенца."); return; }

            WarnIfWeak(squad, view.data.strength, "защиты");
            localPlayer.CmdAttack(squad, view.currentZone, view.cardId, targetPlayerIndex);
            Msg("Перехват «" + view.data.cardName + "».");
        }
        else
        {
            Msg("Это не цель для атаки.");
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
        return new List<int>(selectedSquad).ToArray();
    }

    void ClearSquad()
    {
        foreach (int id in selectedSquad)
            if (activeCards.TryGetValue(id, out CardView v) && v != null) v.SetSelected(false);
        selectedSquad.Clear();
    }

    // Сумма базовой силы выбранного отряда + единое ли племя (для подсказки).
    void SquadStats(out int count, out int baseStrength, out bool sameTribe, out string tribeName)
    {
        count = 0; baseStrength = 0; sameTribe = true; tribeName = "";
        Tribe first = Tribe.Basic; bool firstSet = false;
        foreach (int id in selectedSquad)
        {
            if (!activeCards.TryGetValue(id, out CardView v) || v == null) continue;
            count++;
            baseStrength += v.data.strength;
            if (!firstSet) { first = v.data.tribe; firstSet = true; }
            else if (v.data.tribe != first) sameTribe = false;
        }
        if (firstSet) tribeName = TribeName(first);
    }

    void WarnIfWeak(int[] squad, int targetStrength, string what)
    {
        SquadStats(out _, out int baseStrength, out bool sameTribe, out _);
        if (!sameTribe) Msg("Внимание: в отряде разные племена — сервер атаку отклонит.");
        else if (baseStrength <= targetStrength)
            Msg("База отряда " + baseStrength + " ≤ " + what + " " + targetStrength + ". Может не пройти (если нет бонусов свойств).");
    }

    void UpdateSquadInfo()
    {
        if (squadInfoText == null) return;
        string modeStr = mode == ActionMode.Reinforce ? "РЕЖИМ: УСИЛЕНИЕ" : "РЕЖИМ: АТАКА";
        if (selectedSquad.Count == 0)
        {
            squadInfoText.text = modeStr + "\nОтряд пуст. Клик по своей руке — выбрать карты.";
        }
        else
        {
            SquadStats(out int count, out int strength, out bool sameTribe, out string tribeName);
            string tribePart = sameTribe ? tribeName : "РАЗНЫЕ ПЛЕМЕНА!";
            squadInfoText.text = modeStr + $"\nОтряд: {count} карт · {tribePart} · сила {strength}";
        }

        if (reinforceButton != null)
        {
            var label = reinforceButton.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = mode == ActionMode.Reinforce ? "→ В режим атаки" : "Усилить защиту";
        }
    }

    static string PhaseName(Phase p)
    {
        switch (p)
        {
            case Phase.Update: return "Обновление";
            case Phase.Trophy: return "Сбор трофеев";
            case Phase.Draw: return "Добор";
            case Phase.Action: return "Действия";
            default: return p.ToString();
        }
    }

    static string TribeName(Tribe t)
    {
        switch (t)
        {
            case Tribe.Ogres: return "Людоеды";
            case Tribe.Beasts: return "Звери";
            case Tribe.Vari: return "Вари";
            case Tribe.Ishary: return "Ишари";
            case Tribe.Unique: return "Белая тварь";
            default: return "Базовый";
        }
    }

    // Короткое сообщение игроку (гаснет через несколько секунд).
    void Msg(string text)
    {
        Debug.Log(text);
        if (messageText == null) return;
        messageText.text = text;
        if (messageRoutine != null) StopCoroutine(messageRoutine);
        messageRoutine = StartCoroutine(ClearMessageAfter(4f));
    }

    IEnumerator ClearMessageAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (messageText != null) messageText.text = "";
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
            view.Bind(card);
            view.currentZone = zone;
            view.ownerPlayerId = player.netId.GetHashCode();
            view.isLocalPlayer = true;
            activeCards[card.id] = view;
        }

        container.GetComponent<HandFanLayout>()?.UpdateFan();
        UpdateSquadInfo();
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

            view.Bind(card);
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
            if (activeCards.TryGetValue(card.id, out CardView existing) && existing != null)
            {
                // Карта уже на столе — обновим данные (зёрна, защита могли измениться)
                existing.Bind(card);
                existing.currentZone = zone;
            }
            else
            {
                GameObject cardObj = Instantiate(cardPrefab, container);
                CardView view = cardObj.GetComponent<CardView>();
                view.Bind(card);
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
            phaseText.text = "Фаза: " + PhaseName(newPhase);

        // Вне фазы Действий сбрасываем боевой выбор
        if (newPhase != Phase.Action)
        {
            ClearSquad();
            mode = ActionMode.Attack;
            UpdateSquadInfo();
        }
        // Окно трофея актуально только в фазе Трофеев
        if (newPhase != Phase.Trophy && trophyPanel != null)
            trophyPanel.SetActive(false);

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

    // Сервер просит выбрать судьбу спелых пленных (когда autoResolveTrophy = false).
    public void ShowTrophyPrompt(Player player, int[] prisonerIds)
    {
        if (prisonerIds == null || prisonerIds.Length == 0) return;
        trophyPlayer = player;
        trophyQueue = new List<int>(prisonerIds);

        if (trophyPanel == null)
        {
            // Окна нет — подскажем, как вызвать команду вручную.
            Debug.LogWarning($"Нет trophyPanel. Спелых пленных: {prisonerIds.Length}. " +
                             "Вызови player.CmdResolveTrophy(id, choice) или включи Auto Resolve Trophy.");
            return;
        }
        ShowNextTrophy();
    }

    void ShowNextTrophy()
    {
        if (trophyQueue.Count == 0)
        {
            if (trophyPanel != null) trophyPanel.SetActive(false);
            return;
        }

        int prisonerId = trophyQueue[0];
        if (trophyPanel != null) trophyPanel.SetActive(true);

        if (trophyPromptText != null)
        {
            string name = "пленный";
            if (activeCards.TryGetValue(prisonerId, out CardView v) && v != null)
                name = v.data.cardName;
            trophyPromptText.text = $"Пленный «{name}» созрел.\nЗабрать в трофей (зёрна засчитаются) или использовать магию?";
        }
    }

    void ResolveCurrentTrophy(TrophyChoice choice)
    {
        if (trophyPlayer == null || trophyQueue.Count == 0) return;
        int prisonerId = trophyQueue[0];
        trophyQueue.RemoveAt(0);
        trophyPlayer.CmdResolveTrophy(prisonerId, choice);
        ShowNextTrophy();
    }
}