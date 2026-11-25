using System.Collections.Generic;
using UnityEngine;

/// Elige al azar uno de los mapas prefab (armados a mano) y lo crea en la escena.
/// Un mapa es un prefab con varias salas (Room) colocadas en una cuadricula: cada sala a un multiplo exacto
/// del tamano de sala. Dos salas pegadas quedan conectadas; con un hueco vacio entre ellas, no.
/// El MapLoader vive en la ESCENA, asi que puede apuntar a los prefabs; los prefabs no apuntan a la escena.
public class MapLoader : MonoBehaviour
{
    [Header("Mapas")]
    [Tooltip("Los mapas prefab armados a mano (Map_A, Map_B...). Se elige uno al azar al iniciar.")]
    [SerializeField] GameObject[] maps;
    [Tooltip("-1 = al azar. 0, 1, 2... = fuerza ese mapa de la lista (util para probar).")]
    [SerializeField] int forcedMapIndex = -1;
    [Tooltip("No repetir el mismo mapa dos veces seguidas (si hay mas de uno).")]
    [SerializeField] bool avoidRepeat = true;

    [Header("Cuadricula")]
    [Tooltip("Tamano EXACTO de una sala (lo que ve la camara, normalmente 16 x 9).")]
    [SerializeField] Vector2 roomSize = new Vector2(16f, 9f);
    [Tooltip("Objeto bajo el que se crea el mapa (opcional).")]
    [SerializeField] Transform parent;

    static int lastIndex = -1;

    public Room SpawnRoom { get; private set; }
    public string MapName { get; private set; }

    /// Crea el mapa y devuelve todas sus salas, ya conectadas entre si y con las puertas configuradas.
    public List<Room> Load()
    {
        var rooms = new List<Room>();
        SpawnRoom = null;

        if (maps == null || maps.Length == 0)
        {
            Debug.LogError("MapLoader: la lista 'Maps' esta vacia.", this);
            return rooms;
        }

        int index = ChooseIndex();
        GameObject prefab = maps[index];
        if (prefab == null)
        {
            Debug.LogError("MapLoader: el mapa " + index + " de la lista esta vacio.", this);
            return rooms;
        }

        GameObject map = Instantiate(prefab, Vector3.zero, Quaternion.identity, parent);
        map.name = prefab.name;
        MapName = prefab.name;
        Debug.Log("Mapa elegido: " + MapName + " (indice " + index + ")");

        // 0) Cada RoomBase trae TODAS las variantes de sala como hijas; solo cuenta la que esta ACTIVA.
        //    Las variantes desactivadas se borran para que nadie las cuente (objetos, celdas, profundidad).
        foreach (var r in map.GetComponentsInChildren<Room>(true))
            if (!r.gameObject.activeInHierarchy) DestroyImmediate(r.gameObject);

        // 1) Cada sala a su celda de la cuadricula
        var byCell = new Dictionary<Vector2Int, Room>();
        foreach (var room in map.GetComponentsInChildren<Room>(true))
        {
            Vector3 local = map.transform.InverseTransformPoint(room.transform.position);
            int cx = Mathf.RoundToInt(local.x / roomSize.x);
            int cy = Mathf.RoundToInt(local.y / roomSize.y);

            if (Mathf.Abs(local.x - cx * roomSize.x) > 0.05f || Mathf.Abs(local.y - cy * roomSize.y) > 0.05f)
                Debug.LogWarning("La sala '" + room.name + "' no esta en la cuadricula: su posicion (" + local.x + ", " + local.y +
                                 ") no es multiplo de " + roomSize.x + " x " + roomSize.y + ". Las puertas no encajaran.", room);

            var cell = new Vector2Int(cx, cy);
            if (byCell.ContainsKey(cell))
            {
                Debug.LogError("Dos salas ocupan la misma celda " + cell + ": '" + byCell[cell].name + "' y '" + room.name +
                               "'. Revisa que en ese RoomBase haya UNA sola variante activa.", room);
                continue;
            }

            byCell[cell] = room;
            room.SetGridCell(cell);
            rooms.Add(room);
        }

        // 2) Salas pegadas = conectadas, salvo que alguna de las dos tenga ese lado sellado (Sealed Sides)
        foreach (var pair in byCell)
        {
            for (int i = 0; i < 4; i++)
            {
                var side = (DoorSide)i;
                if (!byCell.TryGetValue(pair.Key + side.ToGridOffset(), out Room other)) continue;
                if (pair.Value.IsSealed(side) || other.IsSealed(side.Opposite())) continue;
                pair.Value.Link(side, other);
            }
        }

        // 3) Puertas donde hay vecina, pared donde no; y revisiones del mapa
        int spawnCount = 0;
        int exitCount = 0;
        foreach (var room in rooms)
        {
            room.ConfigureDoors();

            if (room.Type == RoomType.Spawn)
            {
                spawnCount++;
                if (SpawnRoom == null) SpawnRoom = room;
            }
            if (room.HasFinalExit()) exitCount++;
        }

        // Dificultad progresiva: cada sala sabe a cuantas salas esta del inicio
        AssignDepths(rooms);

        if (spawnCount != 1)
            Debug.LogError("El mapa '" + MapName + "' debe tener exactamente UNA sala de tipo Spawn y tiene " + spawnCount + ".", map);
        if (exitCount == 0 && map.GetComponentInChildren<BossController>(true) == null)
            Debug.LogWarning("El mapa '" + MapName + "' no tiene ninguna puerta FinalExit: no se podra ganar.", map);

        return rooms;
    }

    /// Recorre el mapa desde la sala Spawn: la distancia de cada sala (en salas) es su profundidad.
    /// Las salas mas lejanas son mas dificiles (puzzles, trampas y tiempo que escalan con la profundidad).
    void AssignDepths(List<Room> rooms)
    {
        if (SpawnRoom == null) return;

        var depth = new Dictionary<Room, int> { [SpawnRoom] = 0 };
        var queue = new Queue<Room>();
        queue.Enqueue(SpawnRoom);

        while (queue.Count > 0)
        {
            Room current = queue.Dequeue();
            for (int i = 0; i < 4; i++)
            {
                Room next = current.GetNeighbor((DoorSide)i);
                if (next != null && !depth.ContainsKey(next))
                {
                    depth[next] = depth[current] + 1;
                    queue.Enqueue(next);
                }
            }
        }

        int maxDepth = 0;
        foreach (var d in depth.Values) maxDepth = Mathf.Max(maxDepth, d);

        // Revision: con puertas selladas es facil dejar una sala aislada
        foreach (var room in rooms)
            if (!depth.ContainsKey(room))
                Debug.LogError("La sala '" + room.name + "' no se puede alcanzar desde el Spawn: revisa sus Sealed Sides.", room);

        foreach (var room in rooms)
        {
            room.SetDepth(depth.TryGetValue(room, out int d) ? d : maxDepth, maxDepth);
            room.ApplyDifficulty();
        }
    }

    int ChooseIndex()
    {
        if (forcedMapIndex >= 0) return Mathf.Clamp(forcedMapIndex, 0, maps.Length - 1);

        int index;
        if (avoidRepeat && maps.Length > 1 && lastIndex >= 0)
            index = (lastIndex + 1 + Random.Range(0, maps.Length - 1)) % maps.Length;   // siempre distinto al anterior
        else
            index = Random.Range(0, maps.Length);

        lastIndex = index;
        return index;
    }
}
