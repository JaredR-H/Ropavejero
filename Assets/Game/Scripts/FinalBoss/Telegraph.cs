using System.Collections.Generic;
using UnityEngine;

/// Aviso en el suelo de un ataque. Guardalo para poder quitarlo antes de tiempo con Hide().
public class TelegraphHandle
{
    internal SpriteRenderer sr;
    internal float elapsed;
    internal float duration;
    internal bool alive;

    public bool IsAlive => alive;
    public void Hide()
    {
        alive = false;
        if (sr != null) sr.enabled = false;
    }
}

/// Atajos: Telegraph.Rect(...) y Telegraph.Circle(...) dibujan en el suelo donde va a caer un ataque:
/// rectangulos o circulos rojos que parpadean cada vez mas rapido hasta que llega el golpe.
public static class Telegraph
{
    /// Rectangulo que sale desde 'origin' hacia 'direction' con largo 'length' y ancho 'width'.
    public static TelegraphHandle Rect(Vector2 origin, Vector2 direction, float length, float width, float duration)
    {
        var m = TelegraphManager.Get();
        Sfx.Play(SfxId.Telegraph, origin);            // varios a la vez suenan como uno (cooldown del sonido)
        return m != null ? m.ShowRect(origin, direction, length, width, duration) : null;
    }

    /// Circulo de radio 'radius' centrado en 'center'.
    public static TelegraphHandle Circle(Vector2 center, float radius, float duration)
    {
        var m = TelegraphManager.Get();
        Sfx.Play(SfxId.Telegraph, center);
        return m != null ? m.ShowCircle(center, radius, duration) : null;
    }
}
