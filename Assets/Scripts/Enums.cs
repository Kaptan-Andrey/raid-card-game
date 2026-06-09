// Enums.cs
public enum CardType
{
    Monster,
    Settler,
    Magic,
    Reinforcement
}

public enum Tribe
{
    Ogres,      // Людоеды
    Beasts,     // Звери
    Vari,       // Вари
    Ishary,     // Ишари
    Basic,      // Базовый состав
    Unique      // Уникальная (Белая тварь)
}

public enum Phase
{
    Update,     // Обновление
    Trophy,     // Сбор трофеев
    Draw,       // Добор
    Action      // Действия
}

public enum TrophyChoice
{
    Kill,       // Убить → переместить в Трофей (зёрна засчитываются)
    UseMagic    // Использовать магию → откатить поворот, активировать эффект
}

public enum GrainColor
{
    Dark = 0,   // Тёмное — сбросить верхнюю карту Защиты пленного противника
    Light = 1,  // Светлое — взять 1 карту из Колоды/Леса сверх лимитов
    Calm = 2    // Покоя — реактивное, в ход противника
}
