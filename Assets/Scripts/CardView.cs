using Mirror;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class CardView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [HideInInspector] public int cardId;
    [HideInInspector] public ZoneType currentZone;
    [HideInInspector] public int ownerPlayerId;
    [HideInInspector] public bool isLocalPlayer;
    [HideInInspector] public CardData data;   // данные карты для отрисовки (если карта лицом)

    [Header("Визуал карты (все поля необязательны — заполняй те, что есть на префабе)")]
    public Image artworkImage;           // КАРТИНКА карты (подбирается по имени из CardArtDatabase)
    public TMP_Text maturityText;        // индикатор созревания пленного, напр. "2/3"
    public GameObject selectionBorder;   // рамка/подсветка выбора в отряд
    public GameObject defenderMark;      // значок "карта стоит в Защите"
    public GameObject faceRoot;          // вся лицевая часть карты (если делаешь флип лицо/рубашка)
    public GameObject backRoot;          // рубашка (если флип на этом же префабе)

    [Header("Тексты (НЕ нужны, если на картинке всё нарисовано)")]
    public TMP_Text nameText;            // имя карты
    public TMP_Text strengthText;        // сила (число в углу)
    public TMP_Text tribeTypeText;       // племя / тип
    public TMP_Text grainsText;          // сводка зёрен ("●2 ○1")
    public Image backgroundImage;        // фон — красим по племени/типу (если нет картинки)

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

    // ---- Отрисовка ----
    // Привязать карту лицом: запоминает данные и обновляет тексты/цвет.
    public void Bind(CardData card)
    {
        data = card;
        cardId = card.id;

        ShowFace();

        // Картинка карты (приоритетный способ отображения)
        if (artworkImage)
        {
            Sprite art = CardArtDatabase.Get(card.cardName);
            if (art != null) artworkImage.sprite = art;
        }

        // Индикатор созревания: только у поселенцев с трекером зёрен
        if (maturityText)
        {
            int max = card.grainSequence != null ? card.grainSequence.Length : 0;
            bool showMaturity = card.cardType == CardType.Settler && card.attachedToId == 0 && max > 0;
            maturityText.gameObject.SetActive(showMaturity);
            if (showMaturity) maturityText.text = card.grainRotationSteps + "/" + max;
        }

        // Тексты — заполняются, только если они есть на префабе
        if (nameText) nameText.text = card.cardName;
        if (strengthText) strengthText.text = card.cardType == CardType.Magic ? "" : card.strength.ToString();
        if (tribeTypeText) tribeTypeText.text = TribeTypeLabel(card);
        if (grainsText) grainsText.text = GrainsLabel(card);
        if (defenderMark) defenderMark.SetActive(card.attachedToId != 0);
        if (backgroundImage) backgroundImage.color = ColorFor(card);

        SetSelected(IsSelectedForSquad);
    }

    void ShowFace()
    {
        if (faceRoot) faceRoot.SetActive(true);
        if (backRoot) backRoot.SetActive(false);
    }

    public void SetAsCardBack()
    {
        if (faceRoot) faceRoot.SetActive(false);
        if (backRoot) backRoot.SetActive(true);
        if (canvasGroup) canvasGroup.blocksRaycasts = false;
        isLocalPlayer = false;
    }

    static string TribeTypeLabel(CardData c)
    {
        if (c.cardType == CardType.Settler) return "Поселенец";
        if (c.cardType == CardType.Magic) return "Магия";
        switch (c.tribe)
        {
            case Tribe.Ogres: return "Людоеды";
            case Tribe.Beasts: return "Звери";
            case Tribe.Vari: return "Вари";
            case Tribe.Ishary: return "Ишари";
            case Tribe.Unique: return "Белая тварь";
            default: return "Монстр";
        }
    }

    // Сводка зёрен: для поселенца — его "урожай", для магии — её значки.
    static string GrainsLabel(CardData c)
    {
        int d, l, k;
        if (c.cardType == CardType.Magic && c.magicGrains != null && c.magicGrains.Length >= 3)
        { d = c.magicGrains[0]; l = c.magicGrains[1]; k = c.magicGrains[2]; }
        else { d = c.darkGrains; l = c.lightGrains; k = c.calmGrains; }

        string s = "";
        if (d > 0) s += "●" + d + " ";   // тёмное
        if (l > 0) s += "○" + l + " ";   // светлое
        if (k > 0) s += "◐" + k + " ";   // покоя
        return s.Trim();
    }

    static Color ColorFor(CardData c)
    {
        if (c.cardType == CardType.Settler) return new Color(0.85f, 0.78f, 0.55f); // песочный
        if (c.cardType == CardType.Magic)   return new Color(0.55f, 0.45f, 0.75f); // фиолетовый
        switch (c.tribe)
        {
            case Tribe.Ogres:  return new Color(0.80f, 0.45f, 0.40f);
            case Tribe.Beasts: return new Color(0.55f, 0.70f, 0.45f);
            case Tribe.Vari:   return new Color(0.45f, 0.65f, 0.78f);
            case Tribe.Ishary: return new Color(0.50f, 0.50f, 0.58f);
            case Tribe.Unique: return new Color(0.92f, 0.92f, 0.92f);
            default:           return new Color(0.70f, 0.70f, 0.70f);
        }
    }

    // ---- Перетаскивание (осталось как было) ----
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

        // Фаза действий — выбор отряда, атака, усиление (через UIManager)
        if (phase == Phase.Action)
            UIManager.Instance?.HandleActionClick(this, localPlayer);
    }

    // Подсветка выбранной для отряда карты
    public bool IsSelectedForSquad;
    public void SetSelected(bool on)
    {
        IsSelectedForSquad = on;
        if (selectionBorder) selectionBorder.SetActive(on);
        transform.localScale = on ? Vector3.one * 1.12f : Vector3.one;
    }
}
