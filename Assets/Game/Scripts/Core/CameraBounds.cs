using UnityEngine;

/// Mantiene la vista de la camara dentro de la sala actual, para que nunca se vea el fondo (azul) de fuera de la sala.
public static class CameraBounds
{
    /// Corrige 'pos' para que una camara ortografica de tamano 'orthoSize' no se salga de la sala.
    /// Si la vista es mas grande que la sala en un eje, la centra en ese eje.
    public static Vector3 Clamp(Vector3 pos, float orthoSize, float aspect, Room room)
    {
        if (room == null) return pos;
        Vector2 center = room.CameraPosition;
        Vector2 half = room.ViewSize * 0.5f;

        float halfH = orthoSize;
        float halfW = orthoSize * aspect;

        float maxX = half.x - halfW;
        float maxY = half.y - halfH;
        pos.x = maxX <= 0f ? center.x : Mathf.Clamp(pos.x, center.x - maxX, center.x + maxX);
        pos.y = maxY <= 0f ? center.y : Mathf.Clamp(pos.y, center.y - maxY, center.y + maxY);
        return pos;
    }
}
