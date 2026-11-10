using UnityEngine;

/// Barra de bloques del ataque cargado, bajo el personaje. Se crea sola por codigo (sin arte) y SOLO se ve cuando pasa algo:
///  1. Al usar el ataque cargado: aparece y se vacia por completo.
///  2. Al intentar cargar sin tenerla llena: aparece en rojo con vibracion y se apaga (fade in y fade out).
///  3. Al matar un enemigo: aparece, crece un bloque con efecto "pop" y se apaga.
/// Va en el Player raiz (con Sorting Group, la barra se ordena junto al personaje).
public class ChargeAttackMeter : MonoBehaviour
{
    [Header("Bloques")]
    [SerializeField, Min(1)] int blockCount = 5;
    [Tooltip("Bloques que se recuperan por cada enemigo derrotado.")]
    [SerializeField, Min(0)] int blocksPerKill = 1;
    [Tooltip("Segundos para recuperar un bloque con el tiempo. 0 = solo se recarga al matar enemigos.")]
    [SerializeField] float secondsPerBlockPassive = 0f;
    [Tooltip("Empieza llena (util para probar el ataque cargado).")]
    [SerializeField] bool startFull = true;

    [Header("Posicion y tamano (mundo)")]
    [SerializeField] Vector2 localOffset = new Vector2(0f, -0.55f);
    [SerializeField] Vector2 blockSize = new Vector2(0.2f, 0.1f);
    [SerializeField] float blockGap = 0.04f;
    [SerializeField] float padding = 0.04f;
    [Tooltip("Orden de dibujo respecto al sprite del personaje (alto = por encima).")]
    [SerializeField] int sortingOrderOffset = 20;

    [Header("Colores")]
    [SerializeField] Color plateColor = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] Color emptyColor = new Color(1f, 1f, 1f, 0.15f);
    [SerializeField] Color fillColor = new Color(0.4f, 0.9f, 1f, 1f);
    [Tooltip("Color del aviso cuando no esta cargada (semitransparente).")]
    [SerializeField] Color deniedColor = new Color(1f, 0.15f, 0.15f, 0.55f);

    [Header("Animacion")]
    [Tooltip("Cuanto tiempo se queda visible tras ganar un bloque.")]
    [SerializeField] float showTime = 1.1f;
    [Tooltip("Velocidad del fade in y fade out.")]
    [SerializeField] float fadeSpeed = 8f;
    [Tooltip("Lo que tarda en vaciarse al usar el ataque cargado.")]
    [SerializeField] float drainTime = 0.4f;
    [Tooltip("Efecto pop al ganar un bloque: tamano (eje Y) en el tiempo (eje X, 0 a 1).")]
    [SerializeField] AnimationCurve popCurve = DefaultPopCurve();
    [SerializeField] float popDuration = 0.35f;
    [Tooltip("Duracion del aviso rojo.")]
    [SerializeField] float deniedDuration = 0.9f;
    [SerializeField] float deniedShake = 0.06f;
    [SerializeField] float deniedShakeFrequency = 45f;

    Transform root;
    SpriteRenderer plate;
    SpriteRenderer deniedOverlay;
    SpriteRenderer[] blocks;
    float[] popStart;

    int current;
    float displayed;
    bool draining;
    float alpha;
    float showUntil;
    float deniedUntil;
    float passiveTimer;

    public bool IsFull => current >= blockCount;
    public int Blocks => current;

    static AnimationCurve DefaultPopCurve()
    {
        var curve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.55f, 1.3f), new Keyframe(1f, 1f));
        for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
        return curve;
    }

    void Awake()
    {
        BuildVisuals();
        current = startFull ? blockCount : 0;
        displayed = current;
    }

    void OnEnable() => Enemy.AnyDefeated += OnEnemyDefeated;
    void OnDisable() => Enemy.AnyDefeated -= OnEnemyDefeated;

    void OnEnemyDefeated(Enemy enemy)
    {
        if (blocksPerKill > 0) AddBlocks(blocksPerKill);
    }

    // ---------- API ----------
    /// Recupera bloques: aparece la barra y el bloque nuevo hace "pop".
    public void AddBlocks(int amount)
    {
        if (amount <= 0 || current >= blockCount) return;

        float now = Time.unscaledTime;
        for (int i = 0; i < amount && current < blockCount; i++)
        {
            popStart[current] = now;
            current++;
        }
        if (!draining) displayed = current - 0.001f;     // los bloques nuevos aparecen con su pop
        showUntil = Mathf.Max(showUntil, now + showTime);
    }

    /// Se uso el ataque cargado: la barra aparece y se vacia por completo.
    public void ConsumeAll()
    {
        if (current <= 0) return;
        displayed = current;
        draining = true;
        current = 0;
        showUntil = Mathf.Max(showUntil, Time.unscaledTime + drainTime + 0.5f);
    }

    /// Se intento cargar sin tenerla llena: aviso rojo con vibracion.
    public void ShowDenied()
    {
        float now = Time.unscaledTime;
        deniedUntil = now + deniedDuration;
        showUntil = Mathf.Max(showUntil, now + deniedDuration + 0.15f);
    }

    // ---------- Visuales ----------
    void BuildVisuals()
    {
        // Mismo orden de dibujo que el sprite del personaje
        int layerId = 0, baseOrder = 0;
        var anySprite = GetComponentInChildren<SpriteRenderer>(true);
        if (anySprite != null)
        {
            layerId = anySprite.sortingLayerID;
            baseOrder = anySprite.sortingOrder;
        }
        baseOrder += sortingOrderOffset;

        root = new GameObject("ChargeBar").transform;
        root.SetParent(transform, false);
        root.localPosition = localOffset;

        float innerWidth = blockCount * blockSize.x + (blockCount - 1) * blockGap;
        Vector2 plateSize = new Vector2(innerWidth + padding * 2f, blockSize.y + padding * 2f);

        plate = MakeRenderer("Plate", plateSize, layerId, baseOrder);
        plate.color = plateColor;

        deniedOverlay = MakeRenderer("Denied", plateSize, layerId, baseOrder + 2);
        deniedOverlay.color = new Color(deniedColor.r, deniedColor.g, deniedColor.b, 0f);

        blocks = new SpriteRenderer[blockCount];
        popStart = new float[blockCount];
        for (int i = 0; i < blockCount; i++)
        {
            blocks[i] = MakeRenderer("Block" + i, blockSize, layerId, baseOrder + 1);
            float x = -innerWidth * 0.5f + blockSize.x * 0.5f + i * (blockSize.x + blockGap);
            blocks[i].transform.localPosition = new Vector3(x, 0f, 0f);
            popStart[i] = -1f;
        }
    }

    SpriteRenderer MakeRenderer(string objectName, Vector2 size, int layerId, int order)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(root, false);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = ProceduralSprites.Square;
        sr.sortingLayerID = layerId;
        sr.sortingOrder = order;
        return sr;
    }

    void Update()
    {
        float now = Time.unscaledTime;
        float dt = Time.unscaledDeltaTime;

        // Recarga lenta con el tiempo (opcional)
        if (secondsPerBlockPassive > 0f && current < blockCount)
        {
            passiveTimer += Time.deltaTime;
            if (passiveTimer >= secondsPerBlockPassive)
            {
                passiveTimer = 0f;
                AddBlocks(1);
            }
        }
        else passiveTimer = 0f;

        // Aparece y desaparece con fade
        alpha = Mathf.MoveTowards(alpha, now < showUntil ? 1f : 0f, fadeSpeed * dt);

        // Vaciado animado
        if (draining)
        {
            displayed = Mathf.MoveTowards(displayed, 0f, (blockCount / Mathf.Max(0.05f, drainTime)) * dt);
            if (displayed <= 0f) draining = false;
        }
        else
        {
            displayed = current;
        }

        // Aviso rojo: entra y sale suave, con vibracion
        Vector3 shake = Vector3.zero;
        float deniedEnvelope = 0f;
        if (now < deniedUntil)
        {
            float t = 1f - (deniedUntil - now) / Mathf.Max(0.05f, deniedDuration);
            deniedEnvelope = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t));
            shake = new Vector3(Mathf.Sin(now * deniedShakeFrequency) * deniedShake * deniedEnvelope, 0f, 0f);
        }
        root.localPosition = (Vector3)localOffset + shake;

        plate.color = new Color(plateColor.r, plateColor.g, plateColor.b, plateColor.a * alpha);
        deniedOverlay.color = new Color(deniedColor.r, deniedColor.g, deniedColor.b, deniedColor.a * deniedEnvelope * alpha);

        for (int i = 0; i < blockCount; i++)
        {
            bool filled = i < displayed;
            Color c = filled ? fillColor : emptyColor;
            c.a *= alpha;
            blocks[i].color = c;

            float scale = 1f;
            if (popStart[i] >= 0f)
            {
                float k = (now - popStart[i]) / Mathf.Max(0.05f, popDuration);
                if (k >= 1f) popStart[i] = -1f;
                else if (filled) scale = popCurve.Evaluate(k);
            }
            blocks[i].transform.localScale = new Vector3(blockSize.x * scale, blockSize.y * scale, 1f);
        }
    }
}
