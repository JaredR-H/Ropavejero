using UnityEngine;

/// Sprites sencillos generados por codigo, para no depender de arte: una sombra eliptica suave y un cuadrado blanco.
public static class ProceduralSprites
{
    static Sprite ellipse;
    static Sprite square;

    /// Elipse blanca con el borde suave (se tine con el color del SpriteRenderer). 1 unidad de ancho.
    public static Sprite Ellipse
    {
        get
        {
            if (ellipse != null) return ellipse;

            const int w = 64, h = 32;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float nx = (x + 0.5f) / w * 2f - 1f;
                    float ny = (y + 0.5f) / h * 2f - 1f;
                    float d = Mathf.Sqrt(nx * nx + ny * ny);
                    float a = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.55f, 1f, d));
                    pixels[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();

            ellipse = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
            ellipse.hideFlags = HideFlags.HideAndDontSave;
            return ellipse;
        }
    }

    /// Cuadrado blanco de 1 x 1 unidades.
    public static Sprite Square
    {
        get
        {
            if (square != null) return square;

            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply();

            square = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            square.hideFlags = HideFlags.HideAndDontSave;
            return square;
        }
    }
}
