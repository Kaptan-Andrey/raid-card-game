// CardData.cs
using UnityEngine;

[System.Serializable]
public struct CardData
{
    public int id;
    public int suit;
    public int rank;
    public int ownerPlayerId;

    public CardType cardType;
    public Tribe tribe;
    public int strength;

    public string cardName;   // имя карты (Круговерт, Багун, Рейдер...)
    public bool isAdferous;   // адферная тварь: не может быть Защитой, сбрасывается после атаки
    public int attachedToId;  // id пленного, которого защищает эта карта (0 = свободна / сама пленный)

    // Зёрна, которые карта поселенца приносит в Трофей (его "урожай") — итог для победы
    public int darkGrains;
    public int lightGrains;
    public int calmGrains;
    public int grainRotationSteps; // текущее деление трекера 0..длина grainSequence
    // Порядок зёрен по делениям трекера (каждый элемент = (int)GrainColor).
    // Длина = максимальный поворот пленного. Верхнее (последнее доступное) зерно
    // тратится первым при "Использовании зёрен".
    public int[] grainSequence;

    // Для магии
    public int[] magicGrains; // длина 3: счётчики [dark, light, calm]
    public bool isDefender;
}
