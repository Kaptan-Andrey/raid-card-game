using System.Collections.Generic;
using UnityEngine;

// Каталог картинок карт: связывает имя карты (cardName) с её спрайтом.
// Картинку нельзя передать по сети — Mirror шлёт только данные карты,
// поэтому клиент подбирает спрайт локально по имени.
//
// Как пользоваться:
//   1. Повесь этот компонент на любой объект в сцене (напр. на UIManager).
//   2. В инспекторе заполни список Entries: по одной строке на ВИД карты.
//      Имя должно совпадать с cardName из CardDatabase.cs (напр. "Багун", "Мыслав").
//      Копии одной карты используют один спрайт — дублировать не нужно.
public class CardArtDatabase : MonoBehaviour
{
    public static CardArtDatabase Instance;

    [System.Serializable]
    public struct Entry
    {
        public string cardName;   // как в CardDatabase.cs
        public Sprite sprite;     // картинка этой карты
    }

    [Tooltip("По одной строке на вид карты. Имя = cardName из CardDatabase.")]
    public Entry[] entries;

    [Tooltip("Рубашка карты (необязательно).")]
    public Sprite cardBack;

    private Dictionary<string, Sprite> map;

    void Awake()
    {
        Instance = this;
        BuildMap();
    }

    void BuildMap()
    {
        map = new Dictionary<string, Sprite>();
        if (entries == null) return;
        foreach (var e in entries)
            if (!string.IsNullOrEmpty(e.cardName) && e.sprite != null)
                map[e.cardName] = e.sprite;
    }

    // Спрайт по имени карты (null, если каталога нет или имя не найдено).
    public static Sprite Get(string cardName)
    {
        if (Instance == null || string.IsNullOrEmpty(cardName)) return null;
        if (Instance.map == null) Instance.BuildMap();
        return Instance.map.TryGetValue(cardName, out var s) ? s : null;
    }

    public static Sprite Back()
    {
        return Instance != null ? Instance.cardBack : null;
    }
}
