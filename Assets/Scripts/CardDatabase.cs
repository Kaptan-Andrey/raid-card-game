using System.Collections.Generic;

// ============================================================================
//  CardDatabase — все карты игры в одном месте.
//  Заполнено по фото карт (см. КАРТЫ_КАТАЛОГ.md в корне проекта).
//
//  ТОЧНО ИЗВЕСТНО: базовый состав (13), уникальная (1), все поселенцы (13),
//  и именные карты племён, попавшие на фото.
//  ПОД ВОПРОСОМ (помечено TODO):
//    • количества копий именных карт внутри племён (19/19/5/5) — на фото по
//      одному образцу, поэтому остаток добит обобщёнными картами-заглушками;
//    • сила Тёмного Пастуха и Белой твари (на фото не читается);
//    • точные зёрна Поселенца/Поселенки (фото нечёткие).
// ============================================================================
public static class CardDatabase
{
    public static List<CardData> BuildMonsterDeck()
    {
        var deck = new List<CardData>();
        int id = 0;

        // ---- Большое племя ЛЮДОЕДЫ (19) ----
        deck.Add(Monster(ref id, "Ош Людоедов", Tribe.Ogres, 4));
        deck.Add(Monster(ref id, "Арош Людоедов", Tribe.Ogres, 3));
        deck.Add(Monster(ref id, "Налётчик", Tribe.Ogres, 2));
        deck.Add(Monster(ref id, "Глиф Людоедов", Tribe.Ogres, 1));
        PadTribe(deck, ref id, "Людоед", Tribe.Ogres, 3, 19 - 4); // TODO: реальные карты/силы

        // ---- Большое племя ЗВЕРИ (19) ----
        deck.Add(Monster(ref id, "Обжора", Tribe.Beasts, 1));
        deck.Add(Monster(ref id, "Щенок", Tribe.Beasts, 2));
        deck.Add(Monster(ref id, "Ночной Кошмар", Tribe.Beasts, 4));
        PadTribe(deck, ref id, "Зверь", Tribe.Beasts, 3, 19 - 3); // TODO

        // ---- Малое племя ВАРИ (5) ----
        deck.Add(Monster(ref id, "Вари Отшельник", Tribe.Vari, 3));
        deck.Add(Monster(ref id, "Мёртвый Ош", Tribe.Vari, 5));
        PadTribe(deck, ref id, "Вари", Tribe.Vari, 3, 5 - 2); // TODO

        // ---- Малое племя ИШАРИ (5) ----
        deck.Add(Monster(ref id, "Молодой Ишари", Tribe.Ishary, 3));
        deck.Add(Monster(ref id, "Древний Ишари", Tribe.Ishary, 3));
        PadTribe(deck, ref id, "Ишари", Tribe.Ishary, 3, 5 - 2); // TODO

        // ---- Базовый состав (13) — известен полностью ----
        for (int i = 0; i < 4; i++) deck.Add(Monster(ref id, "Круговерт", Tribe.Basic, 6));            // смертный
        for (int i = 0; i < 2; i++) deck.Add(Monster(ref id, "Адепт горящего", Tribe.Basic, 7));       // смертный
        for (int i = 0; i < 3; i++) deck.Add(Monster(ref id, "Гунуити", Tribe.Basic, 8, adferous: true));
        for (int i = 0; i < 2; i++) deck.Add(Monster(ref id, "Багун", Tribe.Basic, 12, adferous: true));
        deck.Add(Monster(ref id, "Фаун", Tribe.Basic, 10, adferous: true));   // особый: может быть защитой (см. свойство)
        deck.Add(Monster(ref id, "Тёмный Пастух", Tribe.Basic, 5, adferous: true)); // TODO: сила не читается на фото

        // ---- Уникальная (1) ----
        deck.Add(Monster(ref id, "Белая тварь", Tribe.Unique, 0, adferous: true)); // TODO: копирует силу цели

        return deck; // 62 карты
    }

    public static List<CardData> BuildSettlerDeck()
    {
        var deck = new List<CardData>();
        int id = 100;
        var D = GrainColor.Dark; var L = GrainColor.Light; var C = GrainColor.Calm;

        // 3 Рейдера — каждый своего цвета, макс 3 зерна
        deck.Add(Settler(ref id, "Мыслав", L, L, L)); // TODO: цвет сверить (на фото похоже на светлый)
        deck.Add(Settler(ref id, "Морра", C, C, C));  // Зёрна Покоя
        deck.Add(Settler(ref id, "Габи", D, D, D));

        // 3 Поселенца — тёмные (TODO: точное число делений сверить, на фото видно 0/1)
        for (int i = 0; i < 3; i++) deck.Add(Settler(ref id, "Поселенец", D));
        // 3 Поселенки — светлые (TODO: чёткого фото нет)
        for (int i = 0; i < 3; i++) deck.Add(Settler(ref id, "Поселенка", L));
        // 2 Сестры Оссим — Покоя, макс 2 («С Зёрнами Покоя»)
        for (int i = 0; i < 2; i++) deck.Add(Settler(ref id, "Сестра Оссим", C, C));
        // 1 Тёмный Талант — тёмные, макс 2
        deck.Add(Settler(ref id, "Тёмный Талант", D, D));
        // 1 Светлый Талант — светлые, макс 2
        deck.Add(Settler(ref id, "Светлый Талант", L, L));

        return deck; // 13 карт
    }

    // Имена карт-Рейдеров (нужно для подготовки зоны Рейда)
    public static bool IsReider(string name) =>
        name == "Мыслав" || name == "Морра" || name == "Габи";

    static CardData Monster(ref int id, string name, Tribe tribe, int strength, bool adferous = false) => new CardData
    {
        id = id++,
        cardName = name,
        cardType = CardType.Monster,
        tribe = tribe,
        strength = strength,
        isAdferous = adferous,
        attachedToId = 0,
        isDefender = false
    };

    // Добивает племя обобщёнными картами до нужного количества (TODO: заменить на реальные).
    static void PadTribe(List<CardData> deck, ref int id, string baseName, Tribe tribe, int strength, int count)
    {
        for (int i = 0; i < count; i++)
            deck.Add(Monster(ref id, baseName + " #" + (i + 1), tribe, strength));
    }

    static CardData Settler(ref int id, string name, params GrainColor[] grains)
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
            id = id++,
            cardName = name,
            cardType = CardType.Settler,
            strength = 0,           // TODO: сила цели для Рейда (на фото поселенцев силы не видно)
            darkGrains = dark,      // итог для подсчёта победы
            lightGrains = light,
            calmGrains = calm,
            grainSequence = seq,    // порядок по делениям трекера
            grainRotationSteps = 0,
            attachedToId = 0,
            isDefender = false
        };
    }
}
