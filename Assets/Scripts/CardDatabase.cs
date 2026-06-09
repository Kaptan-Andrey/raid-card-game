using System.Collections.Generic;

// ============================================================================
//  CardDatabase — единственное место, где заданы все карты игры.
//
//  СЕЙЧАС ЗДЕСЬ ЗАГЛУШКИ. Заполни реальными данными с карт:
//    • strength      — сила каждого монстра
//    • isAdferous    — true для адферных тварей (не могут быть Защитой,
//                      сбрасываются после атаки), false для смертных
//    • darkGrains / lightGrains / calmGrains — зёрна, которые поселенец
//                      приносит в Трофей на полном обороте
//    • strength у поселенцев — сила цели, которую надо превзойти в Рейде
//
//  Раздача колод (количество карт по племенам) уже соответствует правилам.
// ============================================================================
public static class CardDatabase
{
    // --- ЗАГЛУШКИ силы. Поменяй на реальные значения. ---
    const int PLACEHOLDER_MONSTER_STRENGTH = 3;
    const int PLACEHOLDER_SETTLER_STRENGTH = 2;

    public static List<CardData> BuildMonsterDeck()
    {
        var deck = new List<CardData>();
        int id = 0;

        // 2 крупных племени по 19 карт
        for (int i = 0; i < 19; i++) deck.Add(Monster(id++, "Людоед", Tribe.Ogres));
        for (int i = 0; i < 19; i++) deck.Add(Monster(id++, "Зверь", Tribe.Beasts));

        // 2 малых племени по 5 карт
        for (int i = 0; i < 5; i++) deck.Add(Monster(id++, "Вари", Tribe.Vari));
        for (int i = 0; i < 5; i++) deck.Add(Monster(id++, "Ишари", Tribe.Ishary));

        // 13 карт базового состава
        for (int i = 0; i < 4; i++) deck.Add(Monster(id++, "Круговерт", Tribe.Basic));
        for (int i = 0; i < 2; i++) deck.Add(Monster(id++, "Адепт горящего", Tribe.Basic));
        for (int i = 0; i < 3; i++) deck.Add(Monster(id++, "Гунуити", Tribe.Basic));
        for (int i = 0; i < 2; i++) deck.Add(Monster(id++, "Багун", Tribe.Basic));
        deck.Add(Monster(id++, "Фаун", Tribe.Basic));
        deck.Add(Monster(id++, "Тёмный пастух", Tribe.Basic));

        // 1 уникальная карта
        deck.Add(Monster(id++, "Белая тварь", Tribe.Unique));

        return deck; // 62 карты
    }

    public static List<CardData> BuildSettlerDeck()
    {
        var deck = new List<CardData>();
        int id = 100;

        // TODO: реальная раскладка зёрен по делениям трекера — сфоткать с карт.
        // Порядок = снизу вверх по трекеру; последнее зерно тратится первым.
        var D = GrainColor.Dark; var L = GrainColor.Light; var C = GrainColor.Calm;

        // 3 Рейдера — у Рейдера зёрен нет (это стартовая цель Рейда)
        for (int i = 0; i < 3; i++) deck.Add(Settler(id++, "Рейдер"));
        // 3 Поселенца
        for (int i = 0; i < 3; i++) deck.Add(Settler(id++, "Поселенец", D, D, L));
        // 3 Поселенки
        for (int i = 0; i < 3; i++) deck.Add(Settler(id++, "Поселенка", L, L, D));
        // 2 Сестры Оссим
        for (int i = 0; i < 2; i++) deck.Add(Settler(id++, "Сестра Оссим", C, C, L, D));
        // 1 Тёмный Талант
        deck.Add(Settler(id++, "Тёмный Талант", D, D, D));
        // 1 Светлый Талант
        deck.Add(Settler(id++, "Светлый Талант", L, L, L));

        return deck; // 13 карт
    }

    static CardData Monster(int id, string name, Tribe tribe) => new CardData
    {
        id = id,
        cardName = name,
        cardType = CardType.Monster,
        tribe = tribe,
        strength = PLACEHOLDER_MONSTER_STRENGTH, // TODO: реальная сила
        isAdferous = false,                      // TODO: true для адферных тварей
        attachedToId = 0,
        isDefender = false
    };

    static CardData Settler(int id, string name, params GrainColor[] grains)
    {
        int dark = 0, light = 0, calm = 0;
        var seq = new int[grains.Length];
        for (int i = 0; i < grains.Length; i++)
        {
            seq[i] = (int)grains[i];
            if (grains[i] == GrainColor.Dark) dark++;
            else if (grains[i] == GrainColor.Light) light++;
            else calm++;
        }
        return new CardData
        {
            id = id,
            cardName = name,
            cardType = CardType.Settler,
            strength = PLACEHOLDER_SETTLER_STRENGTH, // TODO: реальная сила цели
            darkGrains = dark,    // итог для подсчёта победы
            lightGrains = light,
            calmGrains = calm,
            grainSequence = seq,  // порядок по делениям трекера
            grainRotationSteps = 0,
            attachedToId = 0,
            isDefender = false
        };
    }
}
