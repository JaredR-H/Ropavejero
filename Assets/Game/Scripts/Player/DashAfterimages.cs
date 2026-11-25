using System.Collections.Generic;
using UnityEngine;

/// Estela de "hologramas" del dash (estilo Mega Man X): mientras el jugador hace dash, deja copias de su sprite
/// que se quedan quietas donde estaban y se desvanecen. Reemplaza al TrailRenderer (deja vacio Dash Trail en PlayerController).
/// Va en el Player raiz, junto a PlayerController.
[RequireComponent(typeof(PlayerController))]
public class DashAfterimages : MonoBehaviour
{
    [Tooltip("El SpriteRenderer del personaje (el del hijo Visual).")]
    [SerializeField] SpriteRenderer source;
    [Tooltip("Segundos entre una copia y la siguiente durante el dash. Menor = mas copias.")]
    [SerializeField] float spawnInterval = 0.025f;
    [Tooltip("Cuanto dura cada copia hasta desaparecer.")]
    [SerializeField] float ghostLifetime = 0.22f;
    [Tooltip("Color de la copia durante su vida (izquierda = recien creada, derecha = al desaparecer). " +
             "Usa el alfa para que se desvanezca.")]
    [SerializeField] Gradient colorOverLife = DefaultGradient();
    [Tooltip("Opcional: material de las copias (por ejemplo, uno aditivo para que brillen). Vacio = el mismo del personaje.")]
    [SerializeField] Material ghostMaterial;
    [Tooltip("Cuantas copias puede haber a la vez.")]
    [SerializeField, Min(2)] int poolSize = 12;

    class Ghost
    {
        public SpriteRenderer sr;
        public float age;
        public bool alive;
    }

    PlayerController controller;
    readonly List<Ghost> ghosts = new List<Ghost>();
    Transform root;
    float timer;
    bool wasDashing;

    static Gradient DefaultGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(new Color(0.45f, 0.95f, 1f), 0f), new GradientColorKey(new Color(0.35f, 0.45f, 1f), 1f) },
            new[] { new GradientAlphaKey(0.65f, 0f), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    void Awake()
    {
        controller = GetComponent<PlayerController>();
        if (source == null)
        {
            Transform visual = transform.Find("Visual");
            source = visual != null ? visual.GetComponent<SpriteRenderer>() : GetComponentInChildren<SpriteRenderer>();
        }

        // Las copias viven fuera del jugador para quedarse quietas donde se crearon
        root = new GameObject(name + " DashGhosts").transform;
        for (int i = 0; i < poolSize; i++)
        {
            var go = new GameObject("Ghost");
            go.transform.SetParent(root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.enabled = false;
            ghosts.Add(new Ghost { sr = sr });
        }
    }

    void OnDestroy()
    {
        if (root != null) Destroy(root.gameObject);
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;

        if (controller.IsDashing)
        {
            if (!wasDashing)
            {
                timer = 0f;
                Spawn();          // la primera copia sale justo al empezar el dash
            }
            timer += dt;
            while (timer >= spawnInterval)
            {
                timer -= spawnInterval;
                Spawn();
            }
        }
        wasDashing = controller.IsDashing;

        foreach (var g in ghosts)
        {
            if (!g.alive) continue;
            g.age += dt;
            float k = g.age / Mathf.Max(0.01f, ghostLifetime);
            if (k >= 1f)
            {
                g.alive = false;
                g.sr.enabled = false;
                continue;
            }
            g.sr.color = colorOverLife.Evaluate(k);
        }
    }

    void Spawn()
    {
        if (source == null || source.sprite == null) return;

        // Una libre o, si no hay, la mas vieja
        Ghost ghost = null;
        float oldest = -1f;
        foreach (var g in ghosts)
        {
            if (!g.alive) { ghost = g; break; }
            if (g.age > oldest) { oldest = g.age; ghost = g; }
        }
        if (ghost == null) return;

        Transform s = source.transform;
        Transform t = ghost.sr.transform;
        t.SetPositionAndRotation(s.position, s.rotation);
        t.localScale = s.lossyScale;

        SpriteRenderer sr = ghost.sr;
        sr.sprite = source.sprite;
        sr.flipX = source.flipX;
        sr.flipY = source.flipY;
        sr.sharedMaterial = ghostMaterial != null ? ghostMaterial : source.sharedMaterial;
        sr.sortingLayerID = source.sortingLayerID;
        sr.sortingOrder = source.sortingOrder - 1;     // detras del personaje
        sr.color = colorOverLife.Evaluate(0f);
        sr.enabled = true;

        ghost.age = 0f;
        ghost.alive = true;
    }
}
