using System;
using UnityEngine;

public enum JuiceAxis
{
    Vertical,        // estira hacia arriba / abajo
    Horizontal,      // estira hacia los lados
    FromDirection    // eje X o Y segun la direccion que se pase al reproducir (ej. el dash)
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

/// Animacion de un solo disparo (dash, golpe, carga...). Las curvas van de 0 a 1 = toda la duracion.
[Serializable]
public class ShotMotion
{
    public string name = "Shot";
    [Min(0.02f)] public float duration = 0.3f;
    public JuiceAxis axis = JuiceAxis.Vertical;
    [Tooltip("Si estira en un eje, aplasta el otro para conservar el volumen.")]
    public bool preserveVolume = true;
    [Tooltip("Cuanto puede ensancharse como maximo el otro eje al conservar el volumen (3 = hasta 3 veces). " +
             "Util para aplastar casi hasta una linea sin que se ensanche de mas.")]
    [Min(1f)] public float maxCrossScale = 3f;
    [Tooltip("Invierte la rotacion cuando la direccion que se pasa apunta a la izquierda (inclinarse hacia atras o hacia adelante segun hacia donde se ataca).")]
    public bool mirrorRotationByDirection = false;

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
            },

            // ---- UI (los desplazamientos verticales van en PIXELES, no en unidades del mundo) ----

            // Numero que cambia (timer, muertes, monedas): rebote sutil
            new ShotMotion
            {
                name = "NumberBounce", duration = 0.28f, axis = JuiceAxis.Vertical, preserveVolume = true,
                stretch = FromFunction(x => Mathf.Sin(2f * Mathf.PI * x) * Mathf.Pow(Mathf.Clamp01(1f - x), 1.5f)),
                stretchAmount = 0.14f,
                yOffset = FromFunction(x => Mathf.Sin(Mathf.PI * Mathf.Clamp01(x))),
                yOffsetAmount = 4f
            },

            // Numero que cambia en modo urgente (poco tiempo): rebote mas notorio
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

            // Botones: al pasar el mouse
            new ShotMotion
            {
                name = "ButtonHover", duration = 0.3f, axis = JuiceAxis.Vertical, preserveVolume = true,
                stretch = FromFunction(x => Mathf.Sin(2f * Mathf.PI * x) * (1f - x)),
                stretchAmount = 0.1f
            },

            // Botones: al presionar (aplasta)
            new ShotMotion
            {
                name = "ButtonPress", duration = 0.16f, axis = JuiceAxis.Vertical, preserveVolume = true,
                stretch = FromFunction(x => -Mathf.Sin(Mathf.PI * Mathf.Clamp01(x))),
                stretchAmount = 0.14f
            },

            // Botones: al soltar (rebota como un resorte)
            new ShotMotion
            {
                name = "ButtonRelease", duration = 0.4f, axis = JuiceAxis.Vertical, preserveVolume = true,
                stretch = FromFunction(x => Mathf.Exp(-5f * x) * Mathf.Sin(2f * Mathf.PI * 2.2f * x), 32),
                stretchAmount = 0.22f
            },

            // Objeto que aparece en la UI al recogerlo: rebote grande
            new ShotMotion
            {
                name = "ItemPop", duration = 0.6f, axis = JuiceAxis.Vertical, preserveVolume = true,
                stretch = FromFunction(x => Mathf.Exp(-4.5f * x) * Mathf.Sin(2f * Mathf.PI * 2.5f * x), 40),
                stretchAmount = 0.5f,
                rotation = FromFunction(x => Mathf.Exp(-4f * x) * Mathf.Sin(2f * Mathf.PI * 3f * x), 40),
                rotationAmount = 14f
            },

            // ---- Objetos y puertas ----

            // Recoger un objeto: se aplasta de forma exagerada hasta quedar en una linea muy fina (y entonces se desvanece,
            // ver Collectible). stretchAmount 0.98 = queda al 2% de alto; maxCrossScale limita cuanto se ensancha.
            new ShotMotion
            {
                name = "PickupSquash", duration = 0.4f, axis = JuiceAxis.Vertical, preserveVolume = true,
                maxCrossScale = 1.8f,
                stretch = FromFunction(x =>
                {
                    float u = Mathf.Clamp01(x / 0.7f);
                    return -(1f - Mathf.Pow(1f - u, 3f));      // baja rapido y se asienta en la linea
                }, 40),
                stretchAmount = 0.98f
            },

            // Puerta que se abre: pop hacia arriba y asienta
            new ShotMotion
            {
                name = "DoorUnlock", duration = 0.4f, axis = JuiceAxis.Vertical, preserveVolume = true,
                stretch = FromFunction(x => Mathf.Sin(2f * Mathf.PI * x) * (1f - x)),
                stretchAmount = 0.28f
            },

            // Puerta que se cierra: golpe seco y rebote
            new ShotMotion
            {
                name = "DoorLock", duration = 0.35f, axis = JuiceAxis.Vertical, preserveVolume = true,
                stretch = FromFunction(x => -Mathf.Exp(-5f * x) * Mathf.Cos(2f * Mathf.PI * 1.5f * x), 32),
                stretchAmount = 0.3f
            },

            // ---- Ataque de los enemigos cuerpo a cuerpo ----

            // Carga: se inclina hacia atras con una vibracion corta que va a mas
            new ShotMotion
            {
                name = "LeanBack", duration = 0.3f, axis = JuiceAxis.Vertical, preserveVolume = true,
                mirrorRotationByDirection = true,
                stretch = FromFunction(x => -x),
                stretchAmount = 0.12f,
                rotation = FromFunction(x => Mathf.Pow(Mathf.Clamp01(x), 1.4f) + 0.1f * Mathf.Sin(2f * Mathf.PI * 10f * x) * x, 48),
                rotationAmount = 18f
            },

            // Golpe: se inclina hacia adelante de golpe y se estira en la direccion del ataque
            new ShotMotion
            {
                name = "LeanForward", duration = 0.22f, axis = JuiceAxis.FromDirection, preserveVolume = true,
                mirrorRotationByDirection = true,
                stretch = FromFunction(x => Mathf.Sin(Mathf.PI * Mathf.Clamp01(x))),
                stretchAmount = 0.3f,
                rotation = FromFunction(x => x < 0.35f
                    ? Mathf.Lerp(1f, -1.2f, Mathf.SmoothStep(0f, 1f, x / 0.35f))
                    : Mathf.Lerp(-1.2f, 0f, Mathf.SmoothStep(0f, 1f, (x - 0.35f) / 0.65f)), 40),
                rotationAmount = 18f
            }
        };
    }
}
