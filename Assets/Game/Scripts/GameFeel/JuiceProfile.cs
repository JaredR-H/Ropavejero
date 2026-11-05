using System;
using UnityEngine;

public enum JuiceAxis
{
    Vertical,
    Horizontal,
    FromDirection
}

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

    public static AnimationCurve Sine(float cycles, float phase = 0f)
    {
        return FromFunction(x => Mathf.Sin(2f * Mathf.PI * (cycles * x + phase)), Mathf.Max(8, Mathf.CeilToInt(cycles * 8f)));
    }

    public static AnimationCurve Hop(float cycles)
    {
        return FromFunction(x => 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * cycles * x), Mathf.Max(8, Mathf.CeilToInt(cycles * 8f)));
    }

    public static ShotMotion[] DefaultShots()
    {
        return new[]
        {
            new ShotMotion
            {
                name = "Dash", duration = 0.22f, axis = JuiceAxis.FromDirection, preserveVolume = true,
                stretch = FromFunction(x => Mathf.Pow(Mathf.Clamp01(1f - x), 2.2f)
                                            - 0.25f * Mathf.Sin(Mathf.PI * Mathf.Clamp01((x - 0.5f) / 0.5f))),
                stretchAmount = 0.6f
            },

            new ShotMotion
            {
                name = "Charge", duration = 0.6f, axis = JuiceAxis.Vertical, preserveVolume = true,
                stretch = FromFunction(x => -Mathf.Abs(Mathf.Sin(2f * Mathf.PI * 5f * x)) * (0.35f + 0.65f * x), 40),
                stretchAmount = 0.2f,
                rotation = FromFunction(x => Mathf.Sin(2f * Mathf.PI * 7f * x) * (0.3f + 0.7f * x), 48),
                rotationAmount = 8f
            },
            new ShotMotion
            {
                name = "Release", duration = 0.28f, axis = JuiceAxis.Vertical, preserveVolume = true,
                stretch = FromFunction(x => -Mathf.Exp(-5f * x) * Mathf.Cos(2f * Mathf.PI * 2f * x), 32),
                stretchAmount = 0.3f
            },
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
            new ShotMotion
            {
                name = "Hit", duration = 0.2f, axis = JuiceAxis.Vertical, preserveVolume = true,
                stretch = FromFunction(x => -Mathf.Sin(Mathf.PI * x)),
                stretchAmount = 0.25f,
                rotation = FromFunction(x => Mathf.Sin(2f * Mathf.PI * 3f * x) * (1f - x)),
                rotationAmount = 6f
            },

            new ShotMotion
            {
                name = "Pop", duration = 0.3f, axis = JuiceAxis.Vertical, preserveVolume = true,
                stretch = FromFunction(x => Mathf.Sin(2f * Mathf.PI * x) * (1f - x)),
                stretchAmount = 0.25f
            },
            new ShotMotion
            {
                name = "NumberBounce", duration = 0.28f, axis = JuiceAxis.Vertical, preserveVolume = true,
                stretch = FromFunction(x => Mathf.Sin(2f * Mathf.PI * x) * Mathf.Pow(Mathf.Clamp01(1f - x), 1.5f)),
                stretchAmount = 0.14f,
                yOffset = FromFunction(x => Mathf.Sin(Mathf.PI * Mathf.Clamp01(x))),
                yOffsetAmount = 4f
            },

            new ShotMotion
            {
                name = "NumberBounceStrong", duration = 0.34f, axis = JuiceAxis.Vertical, preserveVolume = true,
                stretch = FromFunction(x => Mathf.Sin(2f * Mathf.PI * x) * Mathf.Pow(Mathf.Clamp01(1f - x), 1.3f)),
                stretchAmount = 0.28f,
                rotation = FromFunction(x => Mathf.Sin(2f * Mathf.PI * 2f * x) * (1f - x)),
                rotationAmount = 4f,
                yOffset = FromFunction(x => Mathf.Sin(Mathf.PI * Mathf.Clamp01(x))),
                yOffsetAmount = 9f
            },

            new ShotMotion
            {
                name = "ButtonHover", duration = 0.3f, axis = JuiceAxis.Vertical, preserveVolume = true,
                stretch = FromFunction(x => Mathf.Sin(2f * Mathf.PI * x) * (1f - x)),
                stretchAmount = 0.1f
            },

            new ShotMotion
            {
                name = "ButtonPress", duration = 0.16f, axis = JuiceAxis.Vertical, preserveVolume = true,
                stretch = FromFunction(x => -Mathf.Sin(Mathf.PI * Mathf.Clamp01(x))),
                stretchAmount = 0.14f
            },

            new ShotMotion
            {
                name = "ButtonRelease", duration = 0.4f, axis = JuiceAxis.Vertical, preserveVolume = true,
                stretch = FromFunction(x => Mathf.Exp(-5f * x) * Mathf.Sin(2f * Mathf.PI * 2.2f * x), 32),
                stretchAmount = 0.22f
            },

            new ShotMotion
            {
                name = "ItemPop", duration = 0.6f, axis = JuiceAxis.Vertical, preserveVolume = true,
                stretch = FromFunction(x => Mathf.Exp(-4.5f * x) * Mathf.Sin(2f * Mathf.PI * 2.5f * x), 40),
                stretchAmount = 0.5f,
                rotation = FromFunction(x => Mathf.Exp(-4f * x) * Mathf.Sin(2f * Mathf.PI * 3f * x), 40),
                rotationAmount = 14f
            }
        };
    }
}
