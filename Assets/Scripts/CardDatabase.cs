using System.Collections.Generic;

// ============================================================================
//  CardDatabase — все карты игры в одном месте.
//  Количества и силы — по фото карт и уточнениям игрока (см. КАРТЫ_КАТАЛОГ.md).
//
//  Колода монстров = 62 карты (19 Людоедов + 19 Зверей + 5 Вари + 5 Ишари
//  + 13 базового состава + 1 уникальная). Все суммы сходятся.
//
//  Осталось под вопросом (TODO):
//    • сила «Шабаш Зверей» (нет на фото);
//    • свойства карт как игровые эффекты — пока НЕ реализованы (только сила/тип/зёрна).
//    • Тёмный Пастух и Белая тварь силы НЕ имеют (особые свойства) → strength 0.
// ============================================================================
public static class CardDatabase
{
    public static List<CardData> BuildMonsterDeck()
    {
        var deck = new List<CardData>();
        int id = 0;

        // ---- Большое племя ЛЮДОЕДЫ (19) ----
        Add(deck, ref id, 2, "Ош Людоедов", Tribe.Ogres, 4);
        Add(deck, ref id, 9, "Налётчик", Tribe.Ogres, 2);
        // Глиф Людоедов — карта типа Магия (без силы).
        // Основное применение: +1 к силе и возможность призвать из Леса более слабых людоедов.
        // Альтернативное применение: сбросить ради зёрен карты = 1 Тёмное.
        // magicGrains = [dark,light,calm]. TODO: основной эффект не реализован.
        Add(deck, ref id, 4, "Глиф Людоедов", Tribe.Ogres, 0, type: CardType.Magic,
            magicGrains: new int[] { 1, 0, 0 });
        Add(deck, ref id, 4, "Арош Людоедов", Tribe.Ogres, 3); // только племенное свойство

        // ---- Большое племя ЗВЕРИ (19) ----
        // Шабаш Зверей — карта типа Магия (без силы).
        // Основное применение: наложить на своего пленного у Зверей — можно тратить
        // 1 зерно его цвета каждый ход, пленный при этом не крутится и не копит зёрна.
        // Альтернативное применение: сбросить ради зёрен карты = 1 Светлое.
        // magicGrains = [dark,light,calm]. TODO: основной эффект не реализован.
        Add(deck, ref id, 3, "Шабаш Зверей", Tribe.Beasts, 0, type: CardType.Magic,
            magicGrains: new int[] { 0, 1, 0 });
        Add(deck, ref id, 2, "Ночной Кошмар", Tribe.Beasts, 4);
        Add(deck, ref id, 4, "Обжора", Tribe.Beasts, 1);
        Add(deck, ref id, 10, "Щенок", Tribe.Beasts, 2);

        // ---- Малое племя ВАРИ (5) ----
        Add(deck, ref id, 3, "Вари Отшельник", Tribe.Vari, 3);
        Add(deck, ref id, 2, "Мёртвый Ош", Tribe.Vari, 5);

        // ---- Малое племя ИШАРИ (5) ----
        Add(deck, ref id, 1, "Древний Ишари", Tribe.Ishary, 3);
        Add(deck, ref id, 4, "Молодой Ишари", Tribe.Ishary, 3);

        // ---- Базовый состав (13) ----
        Add(deck, ref id, 4, "Круговерт", Tribe.Basic, 6);                 // смертный
        Add(deck, ref id, 2, "Адепт горящего", Tribe.Basic, 7);           // смертный, «Ритуальное сжигание»
        Add(deck, ref id, 3, "Гунуити", Tribe.Basic, 8, adferous: true);
        Add(deck, ref id, 2, "Багун", Tribe.Basic, 12, adferous: true);
        Add(deck, ref id, 1, "Фаун", Tribe.Basic, 10, adferous: true);    // особый: может быть защитой
        Add(deck, ref id, 1, "Тёмный Пастух", Tribe.Basic, 0, adferous: true); // силы нет

        // ---- Уникальная (1) ----
        Add(deck, ref id, 1, "Белая тварь", Tribe.Unique, 0, adferous: true);  // силы нет, копирует цель

        return deck; // 62 карты
    }

    public static List<CardData> BuildSettlerDeck()
    {
        var deck = new List<CardData>();
        int id = 100;
        var D = GrainColor.Dark; var L = GrainColor.Light; var C = GrainColor.Calm;

        // 3 Рейдера (по 1), макс 3 зерна своего цвета
        AddSettler(deck, ref id, 1, "Мыслав", L, L, L); // без особого свойства
        AddSettler(deck, ref id, 1, "Морра", C, C, C);  // Зёрна Покоя
        AddSettler(deck, ref id, 1, "Габи", D, D, D);

        // Поселенцы/Поселенки — максимум 1 зерно (трекер 0–1)
        AddSettler(deck, ref id, 2, "Поселенец", D);
        AddSettler(deck, ref id, 2, "Поселенка", L);

        // 2 Сестры Оссим — Покоя, макс 2 («С Зёрнами Покоя»)
        AddSettler(deck, ref id, 2, "Сестра Оссим", C, C);

        // Таланты — макс 2 своего цвета
        AddSettler(deck, ref id, 1, "Тёмный Талант", D, D);
        AddSettler(deck, ref id, 1, "Светлый Талант", L, L);

        return deck;
    }

    // Имена карт-Рейдеров (нужно для подготовки зоны Рейда)
    public static bool IsReider(string name) =>
        name == "Мыслав" || name == "Морра" || name == "Габи";

    static void Add(List<CardData> deck, ref int id, int count, string name, Tribe tribe, int strength,
                    bool adferous = false, CardType type = CardType.Monster, int[] magicGrains = null)
    {
        for (int i = 0; i < count; i++)
            deck.Add(new CardData
            {
                id = id++,
                cardName = name,
                cardType = type,
                tribe = tribe,
                strength = strength,
                isAdferous = adferous,
                magicGrains = magicGrains == null ? null : (int[])magicGrains.Clone(),
                attachedToId = 0,
                isDefender = false
            });
    }

    static void AddSettler(List<CardData> deck, ref int id, int count, string name, params GrainColor[] grains)
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
        for (int n = 0; n < count; n++)
            deck.Add(new CardData
            {
                id = id++,
                cardName = name,
                cardType = CardType.Settler,
                strength = 0,
                darkGrains = dark,
                lightGrains = light,
                calmGrains = calm,
                grainSequence = (int[])seq.Clone(),
                grainRotationSteps = 0,
                attachedToId = 0,
                isDefender = false
            });
    }
}
