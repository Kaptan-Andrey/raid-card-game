using Mirror;
using UnityEngine;
using UnityEngine.EventSystems;

public class DropZone : MonoBehaviour, IDropHandler
{
    public ZoneType zoneType;

    public void OnDrop(PointerEventData eventData)
    {
        CardView card = eventData.pointerDrag?.GetComponent<CardView>();
        if (card == null) return;

        // Получаем локального игрока через клиентское соединение
        Player localPlayer = NetworkClient.connection?.identity?.GetComponent<Player>();
        if (localPlayer == null)
        {
            Debug.LogWarning("Local player not found!");
            return;
        }

        // Вызываем команду на игроке – она автоматически передаст правильного sender'а
        localPlayer.CmdMoveCard(card.cardId, card.currentZone, zoneType);

        // Удаляем карту из UI мгновенно
        Destroy(card.gameObject);
    }
}