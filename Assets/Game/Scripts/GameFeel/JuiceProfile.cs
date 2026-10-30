using System;
using UnityEngine;

public enum JuiceAxis
{
    Vertical,        // estira hacia arriba / abajo
    Horizontal,      // estira hacia los lados
    FromDirection    // eje X o Y segun la direccion que se pase al reproducir (ej. el dash)
}

/// Movimiento continuo en ciclo (reposo, caminar). Las curvas van de 0 a 1 = UN ciclo completo.
[Serializable]
public class LoopMotion
{
    [Tooltip("Segundos que dura un ciclo.")]
    [Min(0.05f)] public float period = 1f;

    [Header("Squash & Stretch (valor de la curva x Amount; positivo = estira, negativo = aplasta)")]
    public AnimationCurve stretch = JuiceCurves.Zero();
    public float stretchAmount = 0.05f;

    [Header("Balanceo (rotacion en grados)")]
    public AnimationCurve rotation = JuiceCurves.Zero();
    public float rotationAmount = 2f;

    [Header("Salto / rebote vertical (unidades)")]
    public AnimationCurve yOffset = JuiceCurves.Zero();
    public float yOffsetAmount = 0f;
}

/// Animacion de un solo disparo (dash, golpe, carga...). Las curvas van de 0 a 1 = toda la duracion.
[Serializable]
public class ShotMotion
{
    public string name = "Shot";
    [Min(0.02f)] public float duration = 0.3f;
    public JuiceAxis axis = JuiceAxis.Vertical;
    [Tooltip("Si estira en un eje, aplasta el otro para conservar el volumen.")]
    public bool preserveVolume = true;

    [Header("Squash & Stretch (curva x Amount; positivo = estira, negativo = aplasta)")]
    public AnimationCurve stretch = JuiceCurves.Zero();
    public float stretchAmount = 0.3f;

    [Header("Balanceo (grados)")]
    public AnimationCurve rotation = JuiceCurves.Zero();
    public float rotationAmount = 0f;

    [Header("Desplazamiento vertical (unidades)")]
    public AnimationCurve yOffset = JuiceCurves.Zero();
    public float yOffsetAmount = 0f;
}

/// Perfil de animacion reutilizable: se crea una vez (Create > Ropavejero > Juice Profile) y se asigna a cualquier
/// JuiceAnimator (jugador, enemigos, arcos, recolectables, botones...). Todo se edita con curvas y valores.
[CreateAssetMenu(fileName = "JuiceProfile", menuName = "Ropavejero/Juice Profile")]
public class JuiceProfile : ScriptableObject
{
    [Header("Reposo (idle): balanceo lento y squash & stretch sutil")]
    public LoopMotion idle = new LoopMotion
    {
        period = 2.2f,
        stretch = JuiceCurves.Sine(1f),
        stretchAmount = 0.025f,
        rotation = JuiceCurves.Sine(1f, 0.25f),
        rotationAmount = 1.2f,
        yOffset = JuiceCurves.Zero(),
        yOffsetAmount = 0f
    };

    [Header("Caminar: se nota mas el balanceo y el squash & stretch")]
    public LoopMotion move = new LoopMotion
    {
        period = 0.42f,
        stretch = JuiceCurves.Sine(2f),
        stretchAmount = 0.09f,
        rotation = JuiceCurves.Sine(1f),
        rotationAmount = 5f,
        yOffset = JuiceCurves.Hop(2f),
        yOffsetAmount = 0.05f
    };

    [Tooltip("Que tan rapido se pasa de reposo a caminar y al reves.")]
    [Min(0.1f)] public float blendSpeed = 8f;

    [Header("Animaciones de un disparo (se llaman por nombre)")]
    public ShotMotion[] shots = JuiceCurves.DefaultShots();

    public ShotMotion FindShot(string shotName)
    {
        if (shots == null) return null;
        foreach (var s in shots)
            if (s != null && string.Equals(s.name, shotName, StringComparison.OrdinalIgnoreCase)) return s;
        return null;
    }
}

/// Ayudantes para crear curvas por codigo (valores por defecto). Despues se editan a mano en el inspector.
public static class JuiceCurves
{
    public static AnimationCurve Zero() => AnimationCurve.Constant(0f, 1f, 0f);

    /// Curva suave a partir de una funcion de 0 a 1.
    public static AnimationCurve FromFunction(Func<float, float> f, int samples = 24)
    {
        var keys = new Keyframe[samples + 1];
        const float e = 0.001f;
        for (int i = 0; i <= samples; i++)
        {
            float x = (float)i / samples;
            float slope = (f(x + e) - f(x - e)) / (2f * e);
            keys[i] = new Keyframe(x, f(x), slope, slope);
        }
        return new AnimationCurve(keys);
    }

    /// Seno con 'cycles' ciclos completos dentro de la curva.
    public static AnimationCurve Sine(float cycles, float phase = 0f)
    {
        return FromFunction(x => Mathf.Sin(2f * Mathf.PI * (cycles * x + phase)), Mathf.Max(8, Mathf.CeilToInt(cycles * 8f)));
    }

    /// Rebote de 0 a 1 y de vuelta, 'cycles' veces.
    public static AnimationCurve Hop(float cycles)
    {
        return FromFunction(x => 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * cycles * x), Mathf.Max(8, Mathf.CeilToInt(cycles * 8f)));
    }

    public static ShotMotion[] DefaultShots()
    {
        return new[]
        {
            // Dash: estiramiento pronunciado al arrancar que se relaja, con un pequeno aplastamiento al final
            new ShotMotion
            {
                name = "Dash", duration = 0.22f, axis = JuiceAxis.FromDirection, preserveVolume = true,
                stretch = FromFunction(x => Mathf.Pow(Mathf.Clamp01(1f - x), 2.2f)
                                            - 0.25f * Mathf.Sin(Mathf.PI * Mathf.Clamp01((x - 0.5f) / 0.5f))),
                stretchAmount = 0.6f
            },

            // Carga: balanceo rapido y pulsos de squash & stretch que crecen (explosion, apuntar)
            new ShotMotion
            {
                name = "Charge", duration = 0.6f, axis = JuiceAxis.Vertical, preserveVolume = true,
                stretch = FromFunction(x => -Mathf.Abs(Mathf.Sin(2f * Mathf.PI * 5f * x)) * (0.35f + 0.65f * x), 40),
                stretchAmount = 0.2f,
                rotation = FromFunction(x => Mathf.Sin(2f * Mathf.PI * 7f * x) * (0.3f + 0.7f * x), 48),
                rotationAmount = 8f
            },

            // Soltar el disparo: aplasta de golpe y rebota
            new ShotMotion
            {
                name = "Release", duration = 0.28f, axis = JuiceAxis.Vertical, preserveVolume = true,
                stretch = FromFunction(x => -Mathf.Exp(-5f * x) * Mathf.Cos(2f * Mathf.PI * 2f * x), 32),
                stretchAmount = 0.3f
            },

            // Arco: se tensa (estira) y al soltar se aplasta
            new ShotMotion
            {
                name = "BowDraw", duration = 0.5f, axis = JuiceAxis.Horizontal, preserveVolume = true,
                stretch = FromFunction(x => x * x),
                stretchAmount = 0.35f
            },
            new ShotMotion
            {
                name = "BowRelease", duration = 0.3f, axis = JuiceAxis.Horizontal, preserveVolume = true,
                stretch = FromFunction(x => -Mathf.Exp(-6f * x) * Mathf.Cos(2f * Mathf.PI * 3f * x), 32),
                stretchAmount = 0.45f
            },

            // Golpe recibido: aplastamiento y temblor
            new ShotMotion
            {
                name = "Hit", duration = 0.2f, axis = JuiceAxis.Vertical, preserveVolume = true,
                stretch = FromFunction(x => -Mathf.Sin(Mathf.PI * x)),
                stretchAmount = 0.25f,
                rotation = FromFunction(x => Mathf.Sin(2f * Mathf.PI * 3f * x) * (1f - x)),
                rotationAmount = 6f
            },

            // Pop: para recolectables, botones y aparicion de objetos
            new ShotMotion
            {
                name = "Pop", duration = 0.3f, axis = JuiceAxis.Vertical, preserveVolume = true,
                stretch = FromFunction(x => Mathf.Sin(2f * Mathf.PI * x) * (1f - x)),
                stretchAmount = 0.25f
            }
        };
    }
}
