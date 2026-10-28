using System.Collections.Generic;
using UnityEngine;

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
                Debug.LogError("Dos salas ocupan la misma celda " + cell + ": '" + byCell[cell].name + "' y '" + room.name + "'.", room);
                continue;
            }

            byCell[cell] = room;
            room.SetGridCell(cell);
            rooms.Add(room);
        }

        // 2) Salas pegadas = conectadas
        foreach (var pair in byCell)
        {
            for (int i = 0; i < 4; i++)
            {
                var side = (DoorSide)i;
                if (byCell.TryGetValue(pair.Key + side.ToGridOffset(), out Room other))
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

        if (spawnCount != 1)
            Debug.LogError("El mapa '" + MapName + "' debe tener exactamente UNA sala de tipo Spawn y tiene " + spawnCount + ".", map);
        if (exitCount == 0)
            Debug.LogWarning("El mapa '" + MapName + "' no tiene ninguna puerta FinalExit: no se podra ganar.", map);

        return rooms;
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
