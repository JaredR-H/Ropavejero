using UnityEngine;

/// Ayudante para cambiar el sprite de un SpriteRenderer por estado.
/// Si el estado no tiene sprite asignado, muestra el sprite que el SpriteRenderer tenía en el editor.
public class SpriteSwapper
{
    readonly SpriteRenderer sr;
    readonly Sprite fallback;

    public SpriteSwapper(SpriteRenderer renderer)
    {
        sr = renderer;
        fallback = renderer != null ? renderer.sprite : null;
    }

    public void Show(Sprite sprite)
    {
        if (sr == null) return;
        sr.sprite = sprite != null ? sprite : fallback;
    }
}
