using Mirror;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CardView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [HideInInspector] public int cardId;
    [HideInInspector] public ZoneType currentZone;
    [HideInInspector] public int ownerPlayerId;
    [HideInInspector] public bool isLocalPlayer;

    private Canvas canvas;
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Transform originalParent;
    private ZoneType originalZone;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        canvas = GetComponentInParent<Canvas>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!isLocalPlayer)
        {
            eventData.pointerDrag = null;
            return;
        }

        originalParent = rectTransform.parent;
        originalZone = currentZone;

        transform.SetParent(canvas.transform);
        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            eventData.position,
            canvas.worldCamera,
            out Vector2 localPoint
        );
        rectTransform.localPosition = localPoint;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;

        if (rectTransform.parent == canvas.transform)
        {
            transform.SetParent(originalParent);
            currentZone = originalZone;
            HandFanLayout layout = originalParent?.GetComponent<HandFanLayout>();
            if (layout) layout.UpdateFan();
        }
    }

    bool CanClick()
    {
        if (GameManager.Instance == null) { Debug.Log("Can't click: GameManager is null"); return false; }
        if (GameManager.Instance.currentPhase != Phase.Draw) { Debug.Log("Can't click: not Draw phase, current=" + GameManager.Instance.currentPhase); return false; }
        Player localPlayer = NetworkClient.connection?.identity?.GetComponent<Player>();
        if (localPlayer == null) { Debug.Log("Can't click: localPlayer is null"); return false; }
        if (GameManager.Instance.players.IndexOf(localPlayer) != GameManager.Instance.currentPlayerIndex) { Debug.Log("Can't click: not your turn"); return false; }
        return (currentZone == ZoneType.Forest || currentZone == ZoneType.Deck);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (GameManager.Instance == null) return;
        Player localPlayer = NetworkClient.connection?.identity?.GetComponent<Player>();
        if (localPlayer == null) return;

        Phase phase = GameManager.Instance.currentPhase;

        // Фаза добора — клик по Лесу/Колоде добирает карту
        if (phase == Phase.Draw && (currentZone == ZoneType.Forest || currentZone == ZoneType.Deck))
        {
            if (currentZone == ZoneType.Forest) localPlayer.CmdDrawFromForest();
            else localPlayer.CmdDrawFromDeck();
            return;
        }

        // Фаза действий — выбор отряда и атака (через UIManager)
        if (phase == Phase.Action)
            UIManager.Instance?.HandleActionClick(this, localPlayer);
    }

    // Подсветка выбранной для отряда карты (масштаб, т.к. позицию задаёт веер)
    public bool IsSelectedForSquad;
    public void SetSelected(bool on)
    {
        IsSelectedForSquad = on;
        transform.localScale = on ? Vector3.one * 1.12f : Vector3.one;
    }

    public void SetAsCardBack()
    {
        canvasGroup.blocksRaycasts = false;
        isLocalPlayer = false;
    }
}