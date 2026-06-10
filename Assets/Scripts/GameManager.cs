using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    const int MaxHand = 5;        // лимит руки

    [Header("Decks")]
    public readonly SyncList<CardData> monsterDeck = new SyncList<CardData>();
    public readonly SyncList<CardData> settlerDeck = new SyncList<CardData>();
    public readonly SyncList<CardData> forest = new SyncList<CardData>();
    public readonly SyncList<CardData> reidTargets = new SyncList<CardData>();
    public readonly SyncList<CardData> reiderTarget = new SyncList<CardData>();
    public readonly SyncList<CardData> discard = new SyncList<CardData>();

    [SyncVar(hook = nameof(OnCurrentPhaseChanged))]
    public Phase currentPhase = Phase.Update;

    [SyncVar]
    public int currentPlayerIndex = 0;

    [SyncVar]
    public int turnStepCount = 0;

    [SyncVar(hook = nameof(OnWinnerChanged))]
    public int winnerIndex = -1;

    // Временно: пока нет UI выбора трофея — сервер сам "убивает" спелых пленных,
    // чтобы цикл игры не вставал. Поставь false, когда сделаешь окно выбора.
    public bool autoResolveTrophy = true;

    public List<Player> players = new List<Player>();

    void Awake()
    {
        Instance = this;
    }

    public override void OnStartServer()
    {
        InitializeMonsterDeck();
        InitializeSettlerDeck();
        SetupReidAndForest();
        NetworkServer.Spawn(gameObject);
        StartTurn();

        if (players.Count == 0)
            StartCoroutine(WaitForLocalPlayer());
    }

    IEnumerator WaitForLocalPlayer()
    {
        Player localPlayer = null;
        while (localPlayer == null)
        {
            localPlayer = FindObjectOfType<Player>();
            yield return null;
        }
        AddPlayer(localPlayer);
    }

    #region Инициализация колод
    [Server]
    void InitializeMonsterDeck()
    {
        monsterDeck.Clear();
        foreach (var c in CardDatabase.BuildMonsterDeck())
            monsterDeck.Add(c);
        Shuffle(monsterDeck);
    }

    [Server]
    void InitializeSettlerDeck()
    {
        settlerDeck.Clear();
        foreach (var c in CardDatabase.BuildSettlerDeck())
            settlerDeck.Add(c);
        Shuffle(settlerDeck);
    }

    [Server]
    void SetupReidAndForest()
    {
        // 1 Рейдер -> зона Рейдера, остальных Рейдеров убрать в сброс
        int rIdx = -1;
        for (int i = 0; i < settlerDeck.Count; i++)
            if (CardDatabase.IsReider(settlerDeck[i].cardName)) { rIdx = i; break; }
        if (rIdx >= 0)
        {
            reiderTarget.Add(settlerDeck[rIdx]);
            settlerDeck.RemoveAt(rIdx);
        }
        for (int i = settlerDeck.Count - 1; i >= 0; i--)
            if (CardDatabase.IsReider(settlerDeck[i].cardName))
            {
                discard.Add(settlerDeck[i]);
                settlerDeck.RemoveAt(i);
            }

        // все остальные поселенцы -> в общую зону "Рейд"
        while (settlerDeck.Count > 0)
        {
            reidTargets.Add(settlerDeck[0]);
            settlerDeck.RemoveAt(0);
        }

        // Свойство Рейдера «Морра»: убрать из игры поселенцев с < 2 зёрен
        if (reiderTarget.Count > 0 && reiderTarget[0].cardName == "Морра")
        {
            for (int i = reidTargets.Count - 1; i >= 0; i--)
            {
                var c = reidTargets[i];
                if (c.darkGrains + c.lightGrains + c.calmGrains < 2)
                {
                    discard.Add(c);
                    reidTargets.RemoveAt(i);
                }
            }
        }

        // Лес — 4 верхние карты колоды монстров
        for (int i = 0; i < 4; i++)
        {
            if (monsterDeck.Count == 0) break;
            CardData c = monsterDeck[0];
            monsterDeck.RemoveAt(0);
            forest.Add(c);
        }
    }
    #endregion

    #region Ход игры и фазы
    [Server]
    void StartTurn()
    {
        if (winnerIndex >= 0) return;
        currentPhase = Phase.Update;
        NextPhase();
    }

    [Server]
    void NextPhase()
    {
        if (winnerIndex >= 0) return;
        switch (currentPhase)
        {
            case Phase.Update:
                DoUpdatePhase();
                currentPhase = Phase.Trophy;
                NextPhase();
                break;
            case Phase.Trophy:
                HandleTrophyPhase();
                break;
        }
    }

    [Server]
    void DoUpdatePhase()
    {
        // Свойство Рейдера «Габи»: в начале каждого хода сбросить Лес и выложить новый
        if (reiderTarget.Count > 0 && reiderTarget[0].cardName == "Габи")
        {
            for (int i = forest.Count - 1; i >= 0; i--)
            {
                discard.Add(forest[i]);
                forest.RemoveAt(i);
            }
        }

        // Пополнение Леса до 4
        while (forest.Count < 4 && monsterDeck.Count > 0)
        {
            CardData c = monsterDeck[0];
            monsterDeck.RemoveAt(0);
            forest.Add(c);
        }

        // Накопление зёрен — поворот каждого СВОЕГО пленного (только поселенцы)
        Player p = CurrentPlayer();
        if (p != null)
        {
            for (int i = 0; i < p.field.Count; i++)
            {
                CardData c = p.field[i];
                if (c.cardType != CardType.Settler || c.attachedToId != 0) continue;
                int max = MaxStepsFor(c);
                if (max <= 0) continue; // пленный без зёрен (например, Рейдер) не поворачивается
                c.grainRotationSteps = Mathf.Min(c.grainRotationSteps + 1, max);
                p.field[i] = c;
            }
        }
    }

    [Server]
    void HandleTrophyPhase()
    {
        Player p = CurrentPlayer();
        if (p == null) { EnterDrawPhase(); return; }

        List<int> ripe = RipePrisoners(p);
        if (ripe.Count == 0) { EnterDrawPhase(); return; }

        if (autoResolveTrophy)
        {
            foreach (int id in ripe)
            {
                ResolveTrophy(p, id, TrophyChoice.Kill);
                if (winnerIndex >= 0) return;
            }
            EnterDrawPhase();
        }
        else
        {
            // Ждём решения игрока: клиент вызовет CmdResolveTrophy по каждому пленному
            p.TargetTrophyPrompt(ripe.ToArray());
        }
    }

    [Server]
    void EnterDrawPhase()
    {
        if (winnerIndex >= 0) return;
        currentPhase = Phase.Draw;
        turnStepCount = 0;

        // Если добирать нечего — сразу к действиям
        Player p = CurrentPlayer();
        bool canDraw = p != null && p.hand.Count < MaxHand && (forest.Count > 0 || monsterDeck.Count > 0);
        if (!canDraw)
            currentPhase = Phase.Action;
    }

    [Server]
    void EndTurn()
    {
        if (winnerIndex >= 0) return;
        if (players.Count > 0)
            currentPlayerIndex = (currentPlayerIndex + 1) % players.Count;
        StartTurn();
    }

    void OnCurrentPhaseChanged(Phase oldPhase, Phase newPhase)
    {
        UIManager.Instance?.UpdatePhaseDisplay(newPhase);
    }

    void OnWinnerChanged(int oldValue, int newValue)
    {
        if (newValue >= 0)
            UIManager.Instance?.ShowWinner(newValue);
    }
    #endregion

    #region Трофеи и победа
    [Server]
    List<int> RipePrisoners(Player p)
    {
        var list = new List<int>();
        foreach (var c in p.field)
        {
            int max = MaxStepsFor(c);
            if (c.cardType == CardType.Settler && c.attachedToId == 0 && max > 0 && c.grainRotationSteps >= max)
                list.Add(c.id);
        }
        return list;
    }

    [Server]
    int MaxStepsFor(CardData c) => c.grainSequence != null ? c.grainSequence.Length : 0;

    // Вызывается клиентом (через Player.CmdResolveTrophy)
    [Server]
    public void PlayerResolveTrophy(Player p, int prisonerId, TrophyChoice choice)
    {
        if (currentPhase != Phase.Trophy) return;
        if (players.IndexOf(p) != currentPlayerIndex) return;

        int idx = IndexInList(p.field, prisonerId);
        if (idx < 0 || p.field[idx].grainRotationSteps < MaxStepsFor(p.field[idx])) return;

        ResolveTrophy(p, prisonerId, choice);
        if (winnerIndex >= 0) return;

        if (RipePrisoners(p).Count == 0)
            EnterDrawPhase();
    }

    [Server]
    void ResolveTrophy(Player p, int prisonerId, TrophyChoice choice)
    {
        int idx = IndexInList(p.field, prisonerId);
        if (idx < 0) return;
        CardData prisoner = p.field[idx];

        if (choice == TrophyChoice.Kill)
        {
            // Защиту этого пленного — в сброс
            for (int i = p.field.Count - 1; i >= 0; i--)
                if (p.field[i].attachedToId == prisonerId)
                {
                    discard.Add(p.field[i]);
                    p.field.RemoveAt(i);
                }
            idx = IndexInList(p.field, prisonerId);
            if (idx >= 0) p.field.RemoveAt(idx);

            p.trophies.Add(prisoner);
            CheckVictory(p);
        }
        else // UseMagic — откатить поворот на один оборот
        {
            prisoner.grainRotationSteps = Mathf.Max(0, prisoner.grainRotationSteps - 1);
            p.field[idx] = prisoner;
            // TODO: применить эффект накопленных зёрен
        }
    }

    [Server]
    void CheckVictory(Player p)
    {
        int dark = 0, light = 0, calm = 0;
        foreach (var c in p.trophies)
        {
            dark += c.darkGrains;
            light += c.lightGrains;
            calm += c.calmGrains;
        }
        int total = dark + light + calm;

        // Победа: 4 зерна любых цветов ИЛИ 3 зерна одного цвета
        if (total >= 4 || dark >= 3 || light >= 3 || calm >= 3)
        {
            winnerIndex = p.playerIndex;
            currentPhase = Phase.Action;
        }
    }
    #endregion

    #region Фаза действий: бой
    // Атака: цель в Рейде (новая) ИЛИ перехват пленного у другого игрока.
    [Server]
    public void Attack(Player attacker, int[] squadIds, ZoneType targetZone, int targetCardId, int targetPlayerIndex)
    {
        if (winnerIndex >= 0) return;
        if (currentPhase != Phase.Action) return;
        if (players.IndexOf(attacker) != currentPlayerIndex) return;
        if (squadIds == null || squadIds.Length == 0) return;

        // Собрать отряд из руки
        var squad = new List<CardData>();
        foreach (int id in squadIds)
        {
            int idx = IndexInList(attacker.hand, id);
            if (idx < 0) return; // карта не в руке
            squad.Add(attacker.hand[idx]);
        }

        // Все одного племени
        Tribe tribe = squad[0].tribe;
        int squadStrength = 0;
        foreach (var c in squad)
        {
            if (c.tribe != tribe) return;
            squadStrength += GetEffectiveStrength(c, attacker, 0);
        }

        if (targetZone == ZoneType.Reid || targetZone == ZoneType.Reider)
        {
            SyncList<CardData> zone = targetZone == ZoneType.Reid ? reidTargets : reiderTarget;
            int tIdx = IndexInList(zone, targetCardId);
            if (tIdx < 0) return;
            CardData target = zone[tIdx];
            if (squadStrength <= target.strength) return; // строго больше силы цели

            zone.RemoveAt(tIdx);
            RemoveSquadFromHand(attacker, squadIds);
            CapturePrisoner(attacker, target, squad);
        }
        else if (targetZone == ZoneType.Player1Field || targetZone == ZoneType.Player2Field)
        {
            Player defender = players.Find(pl => pl.playerIndex == targetPlayerIndex);
            if (defender == null || defender == attacker) return;
            int tIdx = IndexInList(defender.field, targetCardId);
            if (tIdx < 0) return;
            CardData target = defender.field[tIdx];
            if (target.cardType != CardType.Settler || target.attachedToId != 0) return;

            int defenseStrength = GetDefenseStrength(defender, targetCardId);
            if (squadStrength <= defenseStrength) return; // строго больше суммы Защиты

            // Сбросить всю текущую Защиту цели
            for (int i = defender.field.Count - 1; i >= 0; i--)
                if (defender.field[i].attachedToId == targetCardId)
                {
                    discard.Add(defender.field[i]);
                    defender.field.RemoveAt(i);
                }
            // Забрать пленного
            int pIdx = IndexInList(defender.field, targetCardId);
            if (pIdx >= 0) defender.field.RemoveAt(pIdx);

            RemoveSquadFromHand(attacker, squadIds);
            CapturePrisoner(attacker, target, squad);
        }
        else return;

        attacker.UpdateHandCount();
    }

    [Server]
    void CapturePrisoner(Player attacker, CardData prisoner, List<CardData> squad)
    {
        prisoner.grainRotationSteps = 0;
        prisoner.attachedToId = 0;
        prisoner.ownerPlayerId = attacker.netId.GetHashCode();
        attacker.field.Add(prisoner);

        foreach (var c in squad)
        {
            CardData card = c;
            if (!CanBeDefender(card))
            {
                // Адферные твари сбрасываются после атаки, не становятся Защитой
                // (исключение — Фаун: он может оставаться в защите)
                card.attachedToId = 0;
                discard.Add(card);
            }
            else
            {
                card.attachedToId = prisoner.id;
                card.ownerPlayerId = attacker.netId.GetHashCode();
                attacker.field.Add(card);
            }
        }
    }

    // Усиление защиты: добавить карты с руки поверх Защиты своего пленного.
    [Server]
    public void Reinforce(Player p, int prisonerId, int[] cardIds)
    {
        if (winnerIndex >= 0) return;
        if (currentPhase != Phase.Action) return;
        if (players.IndexOf(p) != currentPlayerIndex) return;
        if (cardIds == null || cardIds.Length == 0) return;

        int pIdx = IndexInList(p.field, prisonerId);
        if (pIdx < 0 || p.field[pIdx].cardType != CardType.Settler) return;

        // Племя текущей Защиты (если есть)
        bool hasDefenseTribe = false;
        Tribe defenseTribe = Tribe.Basic;
        foreach (var c in p.field)
            if (c.attachedToId == prisonerId) { defenseTribe = c.tribe; hasDefenseTribe = true; break; }

        // Собрать добавляемые карты
        var add = new List<CardData>();
        foreach (int id in cardIds)
        {
            int idx = IndexInList(p.hand, id);
            if (idx < 0) return;
            CardData c = p.hand[idx];
            if (!CanBeDefender(c)) return; // адферные не могут быть Защитой (кроме Фауна)
            add.Add(c);
        }

        // Все одного племени и совпадают с существующей Защитой
        Tribe tribe = add[0].tribe;
        foreach (var c in add) if (c.tribe != tribe) return;
        if (hasDefenseTribe && defenseTribe != tribe) return;

        foreach (int id in cardIds)
        {
            int idx = IndexInList(p.hand, id);
            CardData card = p.hand[idx];
            p.hand.RemoveAt(idx);
            card.attachedToId = prisonerId;
            card.ownerPlayerId = p.netId.GetHashCode();
            p.field.Add(card);
        }
        p.UpdateHandCount();
    }

    [Server]
    int GetDefenseStrength(Player owner, int prisonerId)
    {
        int s = 0;
        foreach (var c in owner.field)
            if (c.attachedToId == prisonerId) s += GetEffectiveStrength(c, owner, prisonerId);
        return s;
    }

    // ---- Эффекты карт, влияющие на силу ----
    [Server]
    bool CanBeDefender(CardData c) => !c.isAdferous || c.cardName == "Фаун";

    // Сила карты с учётом её свойства. defendingPrisonerId — пленный, которого
    // эта карта защищает (0, если карта в атаке / не в защите).
    [Server]
    int GetEffectiveStrength(CardData card, Player owner, int defendingPrisonerId)
    {
        int s = card.strength;
        switch (card.cardName)
        {
            case "Мёртвый Ош":
                // Сила = сумме сил карт в Трофее (кроме поселенцев). TODO: + «Вава».
                s = 0;
                foreach (var t in owner.trophies)
                    if (t.cardType != CardType.Settler) s += t.strength;
                break;
            case "Обжора":
                // +сумма макс. зёрен своих пленных (кроме «С Зёрнами Покоя»)
                s += SumPrisonerGrains(owner, excludeCalm: true);
                break;
            case "Щенок":
                // +макс. зёрна пленного, которого он защищает
                if (defendingPrisonerId != 0)
                    s += PrisonerMaxGrains(owner, defendingPrisonerId);
                break;
        }
        return s;
    }

    [Server]
    int SumPrisonerGrains(Player owner, bool excludeCalm)
    {
        int sum = 0;
        foreach (var c in owner.field)
        {
            if (c.cardType != CardType.Settler || c.attachedToId != 0) continue;
            if (excludeCalm && c.calmGrains > 0) continue; // «С Зёрнами Покоя»
            sum += c.darkGrains + c.lightGrains + c.calmGrains;
        }
        return sum;
    }

    [Server]
    int PrisonerMaxGrains(Player owner, int prisonerId)
    {
        foreach (var c in owner.field)
            if (c.id == prisonerId && c.cardType == CardType.Settler)
                return c.darkGrains + c.lightGrains + c.calmGrains;
        return 0;
    }

    [Server]
    void RemoveSquadFromHand(Player attacker, int[] squadIds)
    {
        foreach (int id in squadIds)
        {
            int idx = IndexInList(attacker.hand, id);
            if (idx >= 0) attacker.hand.RemoveAt(idx);
        }
    }
    #endregion

    #region Использование зёрен и магии
    // Выкачать верхнее зерно своего пленного: повернуть на 90° по часовой
    // (спустить деление) и применить эффект этого зерна.
    [Server]
    public void UseGrain(Player p, int prisonerId, int targetPlayerIndex, int targetPrisonerId, bool drawFromForest)
    {
        if (winnerIndex >= 0 || currentPhase != Phase.Action) return;
        if (players.IndexOf(p) != currentPlayerIndex) return;

        int idx = IndexInList(p.field, prisonerId);
        if (idx < 0) return;
        CardData prisoner = p.field[idx];
        if (prisoner.cardType != CardType.Settler || prisoner.attachedToId != 0) return;
        if (prisoner.grainRotationSteps <= 0) return; // нет накопленных зёрен
        if (prisoner.grainSequence == null || prisoner.grainSequence.Length < prisoner.grainRotationSteps) return;

        // верхнее (последнее доступное) зерно
        GrainColor color = (GrainColor)prisoner.grainSequence[prisoner.grainRotationSteps - 1];
        prisoner.grainRotationSteps--; // спустить одно деление
        p.field[idx] = prisoner;

        ApplyGrainEffect(p, color, targetPlayerIndex, targetPrisonerId, drawFromForest);
    }

    // Сбросить карту "Магия" с руки и применить эффекты всех её зёрен (свойство не активируется).
    [Server]
    public void DiscardMagicForGrains(Player p, int magicCardId, int targetPlayerIndex, int targetPrisonerId, bool drawFromForest)
    {
        if (winnerIndex >= 0 || currentPhase != Phase.Action) return;
        if (players.IndexOf(p) != currentPlayerIndex) return;

        int idx = IndexInList(p.hand, magicCardId);
        if (idx < 0) return;
        CardData magic = p.hand[idx];
        if (magic.cardType != CardType.Magic) return;

        p.hand.RemoveAt(idx);
        discard.Add(magic);
        p.UpdateHandCount();

        if (magic.magicGrains != null && magic.magicGrains.Length >= 3)
        {
            for (int i = 0; i < magic.magicGrains[0]; i++) ApplyGrainEffect(p, GrainColor.Dark, targetPlayerIndex, targetPrisonerId, drawFromForest);
            for (int i = 0; i < magic.magicGrains[1]; i++) ApplyGrainEffect(p, GrainColor.Light, targetPlayerIndex, targetPrisonerId, drawFromForest);
            for (int i = 0; i < magic.magicGrains[2]; i++) ApplyGrainEffect(p, GrainColor.Calm, targetPlayerIndex, targetPrisonerId, drawFromForest);
        }
    }

    [Server]
    void ApplyGrainEffect(Player p, GrainColor color, int targetPlayerIndex, int targetPrisonerId, bool drawFromForest)
    {
        switch (color)
        {
            case GrainColor.Light:
                // Взять 1 карту из Колоды или Леса — игнорируя лимит руки и лимит Леса
                if (drawFromForest && forest.Count > 0)
                {
                    CardData c = forest[0]; forest.RemoveAt(0); p.hand.Add(c);
                }
                else if (monsterDeck.Count > 0)
                {
                    CardData c = monsterDeck[0]; monsterDeck.RemoveAt(0); p.hand.Add(c);
                }
                p.UpdateHandCount();
                break;

            case GrainColor.Dark:
                // Сбросить верхнюю (последнюю добавленную) карту Защиты выбранного пленного противника
                Player opp = players.Find(pl => pl.playerIndex == targetPlayerIndex);
                if (opp == null || opp == p) return;
                int topIdx = -1;
                for (int i = 0; i < opp.field.Count; i++)
                    if (opp.field[i].attachedToId == targetPrisonerId) topIdx = i; // последняя = наибольший индекс
                if (topIdx >= 0)
                {
                    discard.Add(opp.field[topIdx]);
                    opp.field.RemoveAt(topIdx);
                }
                break;

            case GrainColor.Calm:
                // Реактивное зерно — тратится в ХОД ПРОТИВНИКА (отмена эффекта/розыгрыша).
                // TODO: нужна система окон реакции (приоритет). Пока не реализовано.
                Debug.LogWarning("Зерно покоя — реактивный эффект, пока не реализован.");
                break;
        }
    }
    #endregion

    #region Команды игроков
    [Server]
    public void AddPlayer(Player player)
    {
        players.Add(player);
        player.playerIndex = players.Count - 1;
        for (int i = 0; i < 4; i++)
        {
            if (monsterDeck.Count == 0) break;
            CardData c = monsterDeck[0];
            monsterDeck.RemoveAt(0);
            player.hand.Add(c);
        }
        player.UpdateHandCount();
    }

    [Command(requiresAuthority = false)]
    public void CmdEndTurn(NetworkConnectionToClient sender = null)
    {
        Player player = sender?.identity?.GetComponent<Player>();
        if (player == null || players.IndexOf(player) != currentPlayerIndex) return;

        if (currentPhase == Phase.Draw)
        {
            // Завершить добор -> к действиям
            currentPhase = Phase.Action;
            return;
        }
        if (currentPhase == Phase.Action)
            EndTurn();
    }

    [Server]
    public void DrawFromForest(Player player)
    {
        if (currentPhase != Phase.Draw || players.IndexOf(player) != currentPlayerIndex) return;
        if (turnStepCount >= 1 || player.hand.Count >= MaxHand || forest.Count == 0) return;

        CardData card = forest[0];
        forest.RemoveAt(0);
        player.hand.Add(card);
        player.UpdateHandCount();
        turnStepCount++;
        CheckAndFinishDraw();
    }

    [Server]
    public void DrawFromDeck(Player player)
    {
        if (currentPhase != Phase.Draw || players.IndexOf(player) != currentPlayerIndex) return;
        if (player.hand.Count >= MaxHand || monsterDeck.Count == 0) return;

        CardData card = monsterDeck[0];
        monsterDeck.RemoveAt(0);
        player.hand.Add(card);
        player.UpdateHandCount();
        CheckAndFinishDraw();
    }

    [Server]
    void CheckAndFinishDraw()
    {
        Player current = CurrentPlayer();
        if (current == null) return;
        bool canDrawMore = (turnStepCount < 1 && forest.Count > 0) || (current.hand.Count < MaxHand && monsterDeck.Count > 0);
        if (!canDrawMore)
            currentPhase = Phase.Action;
    }
    #endregion

    #region Вспомогательные методы
    [Server]
    Player CurrentPlayer() => players.Count > currentPlayerIndex ? players[currentPlayerIndex] : null;

    [Server]
    int IndexInList(SyncList<CardData> list, int id)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i].id == id) return i;
        return -1;
    }

    [Server]
    public CardData FindCard(Player player, ZoneType zone, int cardId)
    {
        SyncList<CardData> list = GetZoneList(player, zone);
        if (list == null) return default;
        foreach (CardData c in list)
            if (c.id == cardId) return c;
        return default;
    }

    [Server]
    public void RemoveCardFromZone(Player player, ZoneType zone, CardData card)
    {
        SyncList<CardData> list = GetZoneList(player, zone);
        if (list == null) return;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (list[i].id == card.id)
            {
                list.RemoveAt(i);
                break;
            }
        }
    }

    [Server]
    public void AddCardToZone(Player player, ZoneType zone, CardData card)
    {
        SyncList<CardData> list = GetZoneList(player, zone);
        if (list == null) return;

        if (zone == ZoneType.Player1Hand || zone == ZoneType.Player1Field || zone == ZoneType.Player1Trophy)
            card.ownerPlayerId = (player != null && player.playerIndex == 0) ? player.netId.GetHashCode() : 0;
        else if (zone == ZoneType.Player2Hand || zone == ZoneType.Player2Field || zone == ZoneType.Player2Trophy)
            card.ownerPlayerId = (player != null && player.playerIndex == 1) ? player.netId.GetHashCode() : 0;
        else
            card.ownerPlayerId = 0;

        list.Add(card);
    }

    [Server]
    public SyncList<CardData> GetZoneList(Player player, ZoneType zone)
    {
        switch (zone)
        {
            case ZoneType.Player1Hand:
            case ZoneType.Player2Hand:
                return (player != null && ((player.playerIndex == 0 && zone == ZoneType.Player1Hand) || (player.playerIndex == 1 && zone == ZoneType.Player2Hand))) ? player.hand : null;
            case ZoneType.Player1Field:
            case ZoneType.Player2Field:
                return (player != null && ((player.playerIndex == 0 && zone == ZoneType.Player1Field) || (player.playerIndex == 1 && zone == ZoneType.Player2Field))) ? player.field : null;
            case ZoneType.Player1Trophy:
            case ZoneType.Player2Trophy:
                return (player != null && ((player.playerIndex == 0 && zone == ZoneType.Player1Trophy) || (player.playerIndex == 1 && zone == ZoneType.Player2Trophy))) ? player.trophies : null;
            case ZoneType.Deck: return monsterDeck;
            case ZoneType.Forest: return forest;
            case ZoneType.Reid: return reidTargets;
            case ZoneType.Reider: return reiderTarget;
            case ZoneType.Discard: return discard;
            default: return null;
        }
    }

    [Server]
    public bool IsValidMove(ZoneType from, ZoneType to)
    {
        return true;
    }

    [Server]
    public void MoveCard(Player sourcePlayer, int cardId, ZoneType sourceZone, ZoneType targetZone)
    {
        CardData card = FindCard(sourcePlayer, sourceZone, cardId);
        if (card.id != cardId) return;

        Player targetPlayer = null;
        switch (targetZone)
        {
            case ZoneType.Player1Hand: case ZoneType.Player1Field: case ZoneType.Player1Trophy:
                targetPlayer = players.Find(p => p.playerIndex == 0); break;
            case ZoneType.Player2Hand: case ZoneType.Player2Field: case ZoneType.Player2Trophy:
                targetPlayer = players.Find(p => p.playerIndex == 1); break;
        }

        if (!IsValidMove(sourceZone, targetZone)) return;

        RemoveCardFromZone(sourcePlayer, sourceZone, card);
        AddCardToZone(targetPlayer, targetZone, card);

        if (IsHandZone(sourceZone)) sourcePlayer.UpdateHandCount();
        if (IsHandZone(targetZone) && targetPlayer != null) targetPlayer.UpdateHandCount();
    }

    bool IsHandZone(ZoneType zone) => zone == ZoneType.Player1Hand || zone == ZoneType.Player2Hand;

    [Server]
    void Shuffle(SyncList<CardData> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
    #endregion
}
