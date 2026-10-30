using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class WaveEntry
{
    public enum Choice { Random, Specific }

    [Tooltip("Random = un enemigo al azar de la lista 'Random Pool'. Specific = el prefab de abajo.")]
    public Choice choice = Choice.Random;
    [Tooltip("Solo si Choice = Specific.")]
    public Enemy prefab;
    [Min(1)] public int minCount = 1;
    [Tooltip("Si es mayor que Min Count, la cantidad es al azar entre los dos.")]
    [Min(1)] public int maxCount = 1;
}

[System.Serializable]
public class Wave
{
    public string name = "Oleada";
    [Tooltip("Segundos de espera antes de que aparezcan los enemigos de esta oleada.")]
    [Min(0f)] public float delayBefore = 0.8f;
    public WaveEntry[] entries;
}

/// Oleadas de enemigos de una sala. Va en un objeto dentro del Room (normalmente una sala de tipo Puzzle).
/// Flujo: el jugador entra -> se bloquean las puertas -> aparece la oleada -> se derrotan todos -> aparece la siguiente
/// -> sin mas oleadas -> la sala queda COMPLETADA y las puertas "Always Open" se vuelven a abrir.
/// Una sala completada no vuelve a generar enemigos al salir y volver a entrar.
/// Si el jugador muere antes de completarla, las oleadas empiezan de nuevo.
public class RoomEncounter : MonoBehaviour, IResettable
{
    [Header("Enemigos al azar")]
    [Tooltip("Lista de la que salen los enemigos con Choice = Random.")]
    [SerializeField] Enemy[] randomPool;

    [Header("Oleadas (en orden)")]
    [SerializeField] Wave[] waves;

    [Header("Donde aparecen")]
    [Tooltip("Puntos de aparicion (objetos vacios dentro de la sala). Si no hay ninguno, aparecen al azar dentro del area.")]
    [SerializeField] Transform[] spawnPoints;
    [Tooltip("Area alrededor del centro de la sala donde aparecen al azar (si no hay puntos).")]
    [SerializeField] Vector2 areaSize = new Vector2(12f, 6f);
    [SerializeField] float minDistanceFromPlayer = 3f;
    [Tooltip("Capa de las paredes: evita aparecer dentro de ellas.")]
    [SerializeField] LayerMask wallMask;
    [SerializeField] float spawnRadius = 0.5f;

    [Header("Efectos")]
    [Tooltip("Prefab opcional que aparece donde nace cada enemigo (debe destruirse solo).")]
    [SerializeField] GameObject spawnEffect;
    [Tooltip("Segundos entre un enemigo y el siguiente dentro de la oleada.")]
    [SerializeField] float spawnStagger = 0.12f;
    [Tooltip("Se ejecuta al completar todas las oleadas (por ejemplo, abrir un cofre).")]
    [SerializeField] UnityEvent onCompleted;

    Room room;
    PlayerHealth player;
    Coroutine running;
    readonly List<Enemy> spawned = new List<Enemy>();
    readonly HashSet<Transform> usedPoints = new HashSet<Transform>();

    public bool Completed { get; private set; }
    public bool IsRunning => running != null;

    static bool GameIsPlaying => GameManager.Instance == null || GameManager.Instance.IsPlaying;

    void Awake() => room = GetComponentInParent<Room>();

    /// La llama Room cuando el jugador entra (o reaparece en la sala).
    public void Begin()
    {
        if (Completed || running != null) return;

        if (waves == null || waves.Length == 0)
        {
            Complete();
            return;
        }

        if (room != null) room.SetEncounterDoorsLocked(true);
        running = StartCoroutine(RunWaves());
    }

    IEnumerator RunWaves()
    {
        for (int w = 0; w < waves.Length; w++)
        {
            Wave wave = waves[w];
            if (wave == null) continue;

            yield return WaitPlaying(wave.delayBefore);
            yield return SpawnWave(wave);

            // Hasta que no quede ninguno de la oleada, no hay siguiente.
            while (CountAlive() > 0) yield return null;
        }

        Complete();
    }

    IEnumerator SpawnWave(Wave wave)
    {
        usedPoints.Clear();
        if (wave.entries == null) yield break;

        foreach (var entry in wave.entries)
        {
            if (entry == null) continue;

            int count = Random.Range(entry.minCount, Mathf.Max(entry.minCount, entry.maxCount) + 1);
            for (int i = 0; i < count; i++)
            {
                Enemy prefab = ChoosePrefab(entry);
                if (prefab == null) continue;

                Vector2 position = PickPosition();
                if (spawnEffect != null) Destroy(Instantiate(spawnEffect, position, Quaternion.identity), 1.5f);

                Enemy enemy = Instantiate(prefab, position, Quaternion.identity, room != null ? room.transform : transform);
                spawned.Add(enemy);

                yield return WaitPlaying(spawnStagger);
            }
        }
    }

    Enemy ChoosePrefab(WaveEntry entry)
    {
        if (entry.choice == WaveEntry.Choice.Specific) return entry.prefab;

        if (randomPool == null || randomPool.Length == 0)
        {
            Debug.LogWarning("RoomEncounter '" + name + "': hay una entrada Random pero el Random Pool esta vacio.", this);
            return null;
        }
        return randomPool[Random.Range(0, randomPool.Length)];
    }

    Vector2 PickPosition()
    {
        // 1) Puntos de aparicion: uno distinto por enemigo mientras queden libres
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            var free = new List<Transform>();
            foreach (var p in spawnPoints)
                if (p != null && !usedPoints.Contains(p)) free.Add(p);

            if (free.Count == 0)
            {
                usedPoints.Clear();
                foreach (var p in spawnPoints)
                    if (p != null) free.Add(p);
            }

            if (free.Count > 0)
            {
                Transform chosen = free[Random.Range(0, free.Count)];
                usedPoints.Add(chosen);
                return chosen.position;
            }
        }

        // 2) Al azar dentro del area, lejos del jugador, de las paredes y de otros enemigos
        Vector2 center = room != null ? (Vector2)room.CameraPosition : (Vector2)transform.position;
        if (player == null) player = FindFirstObjectByType<PlayerHealth>();
        Vector2 playerPos = player != null ? (Vector2)player.transform.position : center;

        Vector2 candidate = center;
        for (int attempt = 0; attempt < 30; attempt++)
        {
            candidate = center + new Vector2(Random.Range(-areaSize.x * 0.5f, areaSize.x * 0.5f),
                                             Random.Range(-areaSize.y * 0.5f, areaSize.y * 0.5f));

            if (Vector2.Distance(candidate, playerPos) < minDistanceFromPlayer) continue;
            if (wallMask.value != 0 && Physics2D.OverlapCircle(candidate, spawnRadius, wallMask) != null) continue;
            if (TooCloseToOtherEnemy(candidate)) continue;
            return candidate;
        }
        return candidate;
    }

    bool TooCloseToOtherEnemy(Vector2 point)
    {
        foreach (var e in spawned)
            if (e != null && e.gameObject.activeSelf && Vector2.Distance(e.transform.position, point) < 1.2f)
                return true;
        return false;
    }

    // Un enemigo derrotado (o que ya exploto) queda desactivado.
    int CountAlive()
    {
        spawned.RemoveAll(e => e == null || !e.gameObject.activeSelf);
        return spawned.Count;
    }

    IEnumerator WaitPlaying(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            if (GameIsPlaying) t += Time.deltaTime;
            yield return null;
        }
    }

    void Complete()
    {
        Completed = true;
        running = null;
        DestroySpawned();
        if (room != null) room.SetEncounterDoorsLocked(false);
        onCompleted?.Invoke();
    }

    void DestroySpawned()
    {
        foreach (var e in spawned)
            if (e != null) Destroy(e.gameObject);
        spawned.Clear();
    }

    /// Al reiniciar la sala (morir, reintentar): si no estaba completada, las oleadas empiezan de cero.
    public void ResetState()
    {
        if (running != null) StopCoroutine(running);
        running = null;

        if (Completed) return;      // sala completada: no vuelven a salir enemigos

        DestroySpawned();
        if (room != null) room.SetEncounterDoorsLocked(false);
    }

    void OnDrawGizmosSelected()
    {
        var r = GetComponentInParent<Room>();
        Vector3 center = r != null ? r.CameraPosition : transform.position;
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.8f);
        Gizmos.DrawWireCube(center, new Vector3(areaSize.x, areaSize.y, 0f));

        if (spawnPoints == null) return;
        Gizmos.color = Color.red;
        foreach (var p in spawnPoints)
            if (p != null) Gizmos.DrawWireSphere(p.position, 0.4f);
    }
}
