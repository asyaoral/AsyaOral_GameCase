using UnityEngine;

public class TileVisual : MonoBehaviour
{
    // 0: default, 1: A, 2: B, 3: C
    public Sprite[] sprites;

    SpriteRenderer sr;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    public void SetTier(int tier)
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sprites == null || sprites.Length == 0) return;

        tier = Mathf.Clamp(tier, 0, sprites.Length - 1);
        if (sprites[tier] != null) sr.sprite = sprites[tier];
    }
}
