using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
public class HandFanLayout : MonoBehaviour
{
    [SerializeField] private float radius = 800f;
    [SerializeField] private float maxAngle = 20f;
    [SerializeField] private float yOffset = 0f;
    [SerializeField] private bool mirrorForOpponent = false;

    private RectTransform rectTransform;
    private List<RectTransform> cards = new List<RectTransform>();

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    void Update()
    {
        UpdateFan();
    }

    public void UpdateFan()
    {
        cards.Clear();
        foreach (Transform child in transform)
        {
            if (child.gameObject.activeSelf)
            {
                RectTransform childRect = child as RectTransform;
                if (childRect != null) cards.Add(childRect);
            }
        }

        int count = cards.Count;
        if (count == 0) return;

        float arcAngle = Mathf.Min(maxAngle, (count - 1) * 3f);
        float startAngle = -arcAngle / 2f;
        float angleStep = (count > 1) ? arcAngle / (count - 1) : 0f;

        for (int i = 0; i < count; i++)
        {
            float angle = startAngle + i * angleStep;
            if (mirrorForOpponent) angle = -angle;

            float rad = angle * Mathf.Deg2Rad;
            float x = Mathf.Sin(rad) * radius;
            float y = Mathf.Cos(rad) * radius - radius;

            RectTransform card = cards[i];
            card.localPosition = new Vector3(x, y + yOffset, 0);
            card.localRotation = Quaternion.Euler(0, 0, angle);
            card.SetSiblingIndex(i);
        }
    }

    void OnTransformChildrenChanged()
    {
        UpdateFan();
    }
}