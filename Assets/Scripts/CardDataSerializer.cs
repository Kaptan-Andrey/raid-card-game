using Mirror;
using UnityEngine;

// CardDataSerializer.cs (замените методы Write/Read)
public static class CardDataSerializer
{
    public static void WriteCardData(this NetworkWriter writer, CardData card)
    {
        writer.WriteInt(card.id);
        writer.WriteInt(card.suit);
        writer.WriteInt(card.rank);
        writer.WriteInt(card.ownerPlayerId);
        writer.WriteInt((int)card.cardType);
        writer.WriteInt((int)card.tribe);
        writer.WriteInt(card.strength);
        writer.WriteString(card.cardName);
        writer.WriteBool(card.isAdferous);
        writer.WriteInt(card.attachedToId);
        writer.WriteInt(card.darkGrains);
        writer.WriteInt(card.lightGrains);
        writer.WriteInt(card.calmGrains);
        writer.WriteInt(card.grainRotationSteps);
        writer.WriteArray(card.grainSequence);
        writer.WriteArray(card.magicGrains);
        writer.WriteBool(card.isDefender);
    }

    public static CardData ReadCardData(this NetworkReader reader)
    {
        return new CardData
        {
            id = reader.ReadInt(),
            suit = reader.ReadInt(),
            rank = reader.ReadInt(),
            ownerPlayerId = reader.ReadInt(),
            cardType = (CardType)reader.ReadInt(),
            tribe = (Tribe)reader.ReadInt(),
            strength = reader.ReadInt(),
            cardName = reader.ReadString(),
            isAdferous = reader.ReadBool(),
            attachedToId = reader.ReadInt(),
            darkGrains = reader.ReadInt(),
            lightGrains = reader.ReadInt(),
            calmGrains = reader.ReadInt(),
            grainRotationSteps = reader.ReadInt(),
            grainSequence = reader.ReadArray<int>(),
            magicGrains = reader.ReadArray<int>(),
            isDefender = reader.ReadBool()
        };
    }
}

public class CardDataRegistration : MonoBehaviour
{
    void Awake()
    {
        Reader<CardData>.read = CardDataSerializer.ReadCardData;
        Writer<CardData>.write = CardDataSerializer.WriteCardData;
    }
}