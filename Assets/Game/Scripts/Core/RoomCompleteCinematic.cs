using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(1000)]
public class RoomCompleteCinematic : MonoBehaviour
{
    public static RoomCompleteCinematic Instance { get; private set; }

    [Header("Referencias (opcional)")]
    [Tooltip("Vacio = Camera.main.")]
    [SerializeField] Camera cam;
    [Tooltip("Vacio = se crea solo un Canvas con dos barras negras.")]
    [SerializeField] RectTransform topBar;
    [SerializeField] RectTransform bottomBar;

    [Header("Golpe al completar la sala (el mas fuerte)")]
    [Tooltip("Shake (0 a 1). GameFeel lo eleva al cuadrado. Debe ser mayor que el de los botones.")]
    [Range(0f, 1f)] [SerializeField] float completeShake = 0.7f;
    [SerializeField] float completeHitStop = 0.1f;
    [Tooltip("Segundos de invulnerabilidad (parpadeo blanco) que recibe el jugador al terminar la cinematica de sala " +
             "completada o de reja abierta. 0 = nada.")]
    [SerializeField, Min(0f)] float invulnerabilityAfter = 1.2f;

    [Header("Golpe al abrir una reja (GatedPuzzle)")]
    [Range(0f, 1f)] [SerializeField] float gateShake = 0.55f;
    [SerializeField] float gateHitStop = 0.07f;

    [Header("Barras negras")]
    [Tooltip("Alto de cada barra (fraccion de la pantalla).")]
    [SerializeField, Range(0f, 0.25f)] float barHeight = 0.08f;
    [SerializeField] float barsInTime = 0.3f;
    [SerializeField] float barsOutTime = 0.3f;

    [Header("Zoom a lo que se abre (puerta o reja)")]
    [Tooltip("Zoom al enfocar (0,6 = la vista se reduce al 60%: se ve mas cerca).")]
    [SerializeField, Range(0.3f, 1f)] float focusZoom = 0.6f;
    [Tooltip("Cuanto se centra en la puerta o reja (1 = centrada; la camara igual no sale de la sala).")]
    [SerializeField, Range(0f, 1f)] float focusAmount = 1f;
    [SerializeField] float focusTime = 0.4f;
    [Tooltip("Espera con la camara ya enfocada, antes de que se abra.")]
    [SerializeField] float beforeOpen = 0.15f;
    [Tooltip("Tiempo mirando como se abre.")]
    [SerializeField] float watchOpen = 0.55f;
    [Tooltip("Lo que tarda la camara en volver a mostrar toda la sala.")]
    [SerializeField] float returnTime = 0.45f;
    [SerializeField] AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Otras puertas y trampas")]
    [Tooltip("Segundos entre una puerta y la siguiente (las que no se enfocaron).")]
    [SerializeField] float doorStagger = 0.12f;
    [Tooltip("Segundos entre el puff de una trampa y la siguiente.")]
    [SerializeField] float trapStagger = 0.03f;
    [Tooltip("Lo que tarda cada trampa en encogerse hasta desaparecer.")]
    [SerializeField] float trapShrinkTime = 0.15f;
    [SerializeField] float trapPuffScale = 0.9f;
    [SerializeField] float pauseAfterTraps = 0.15f;

    bool running;
    bool driveCamera;
    Coroutine holdRoutine;
    Room clampRoom;
    Vector3 camPos;
    float camSize;
    float bars;          // 0 = sin barras, 1 = barras completas

    public bool IsPlaying => running;

    void Awake()
    {
        Instance = this;
        if (topBar == null || bottomBar == null) BuildBars();
        ApplyBars();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ================= API =================

    /// Sala completada. Devuelve false si ahora no se puede (y la sala se completa al instante).
    public bool TryPlay(Room room)
    {
        if (running || room == null) return false;
        var gm = GameManager.Instance;
        if (gm == null || !gm.BeginCutscene()) return false;     // solo si se estaba jugando
        StartCoroutine(RoomSequence(room));
        return true;
    }

    /// Enfoca un punto (por ejemplo, una reja), ejecuta 'reveal' (la abre) y vuelve. Devuelve false si no se pudo:
    /// en ese caso quien llama debe ejecutar 'reveal' el mismo.
    public bool TryPlayFocus(Vector3 target, Action reveal)
    {
        if (running) return false;
        var gm = GameManager.Instance;
        if (gm == null || gm.CurrentRoom == null || !gm.BeginCutscene()) return false;
        StartCoroutine(FocusSequence(gm.CurrentRoom, target, reveal));
        return true;
    }

    /// Enfoque que se QUEDA puesto (barras + zoom a 'target') hasta que alguien llame a EndFocus o EndFocusImmediate.
    /// Lo usa el jefe: al entrar a su sala y al morir. Devuelve false si ahora no se puede.
    public bool BeginFocus(Vector3 target, float zoom, float shake, float hitStop)
    {
        if (running) return false;
        var gm = GameManager.Instance;
        if (gm == null || gm.CurrentRoom == null || !gm.BeginCutscene()) return false;
        running = true;
        holdRoutine = StartCoroutine(HoldFocus(gm.CurrentRoom, target, zoom, shake, hitStop));
        return true;
    }

    IEnumerator HoldFocus(Room room, Vector3 target, float zoom, float shake, float hitStop)
    {
        yield return Impact(shake, hitStop);
        yield return FocusIn(room, target, zoom);
        holdRoutine = null;
    }

    /// Termina el enfoque: la camara vuelve a la sala, salen las barras y (si resumeGame) el jugador recupera el control.
    public Coroutine EndFocus(bool resumeGame = true)
    {
        if (!running) return null;
        if (holdRoutine != null) StopCoroutine(holdRoutine);
        holdRoutine = null;
        return StartCoroutine(EndFocusRoutine(resumeGame));
    }

    IEnumerator EndFocusRoutine(bool resumeGame)
    {
        Room room = clampRoom != null ? clampRoom : (GameManager.Instance != null ? GameManager.Instance.CurrentRoom : null);
        if (room != null) yield return ReturnToRoom(room, returnTime, true, null);
        Finish(resumeGame ? GameManager.Instance : null);
    }

    /// Quita el enfoque al instante, sin devolver el control (por ejemplo, con la pantalla en negro antes de la victoria).
    public void EndFocusImmediate()
    {
        StopAllCoroutines();
        holdRoutine = null;
        if (Time.timeScale == 0f) Time.timeScale = 1f;
        Finish(null);
    }

    // ================= Secuencias =================

    IEnumerator RoomSequence(Room room)
    {
        running = true;
        var gm = GameManager.Instance;

        yield return Impact(completeShake, completeHitStop);

        // La puerta a enfocar: la mas cercana al jugador (o la unica que se abre)
        List<RoomDoor> doors = room.DoorsThatWillOpen();
        var player = FindFirstObjectByType<PlayerController>();
        Vector3 playerPos = player != null ? player.transform.position : room.CameraPosition;
        RoomDoor focusDoor = Nearest(doors, playerPos);
        Vector3 focusPoint = focusDoor != null ? focusDoor.transform.position : playerPos;

        yield return FocusIn(room, focusPoint);
        yield return Wait(beforeOpen);

        if (focusDoor != null) focusDoor.SetEncounterLocked(false);     // squash & stretch + polvo de la puerta
        yield return Wait(watchOpen);

        // Vuelve a mostrar toda la sala mientras se abren las demas puertas
        doors.Remove(focusDoor);
        yield return ReturnToRoom(room, returnTime, false, doors);

        room.RaiseCompleted();      // jaulas que se abren solas, HUD sin tiempo

        // Las trampas desaparecen con un puff
        yield return PuffTraps(room);
        room.DeactivateTraps();
        yield return Wait(pauseAfterTraps);

        yield return BarsOut();
        GrantPlayerInvulnerability();
        Finish(gm);
    }

    void GrantPlayerInvulnerability()
    {
        if (invulnerabilityAfter <= 0f) return;
        var health = FindFirstObjectByType<PlayerHealth>();
        if (health != null) health.GrantInvulnerability(invulnerabilityAfter);
    }

    IEnumerator FocusSequence(Room room, Vector3 target, Action reveal)
    {
        running = true;
        var gm = GameManager.Instance;

        yield return Impact(gateShake, gateHitStop);
        yield return FocusIn(room, target);
        yield return Wait(beforeOpen);

        reveal?.Invoke();
        yield return Wait(watchOpen);

        yield return ReturnToRoom(room, returnTime, true, null);
        GrantPlayerInvulnerability();
        Finish(gm);
    }

    // ================= Pasos =================

    // Shake de GameFeel + hitstop propio (GameFeel anula el suyo fuera del estado Playing)
    IEnumerator Impact(float shake, float hitStop)
    {
        if (cam == null) cam = Camera.main;
        if (GameFeel.Instance != null && shake > 0f) GameFeel.Instance.AddShake(shake);
        if (hitStop > 0f)
        {
            Time.timeScale = 0f;
            yield return Wait(hitStop);
            Time.timeScale = 1f;
        }
    }

    IEnumerator FocusIn(Room room, Vector3 point, float zoomOverride = -1f)
    {
        clampRoom = room;
        Vector3 startPos = cam.transform.position;
        float startSize = cam.orthographicSize;
        baseSize = startSize;

        Vector3 roomPos = RoomCamPos(room);
        float targetSize = startSize * (zoomOverride > 0f ? zoomOverride : focusZoom);
        Vector3 wanted = Vector3.Lerp(roomPos, new Vector3(point.x, point.y, roomPos.z), focusAmount);
        Vector3 targetPos = CameraBounds.Clamp(wanted, targetSize, cam.aspect, room);

        driveCamera = true;
        camPos = startPos;
        camSize = startSize;
        float duration = Mathf.Max(focusTime, barsInTime);
        yield return Animate(duration, k =>
        {
            bars = Mathf.Clamp01(k * duration / Mathf.Max(0.01f, barsInTime));
            float e = ease.Evaluate(Mathf.Clamp01(k * duration / Mathf.Max(0.01f, focusTime)));
            camPos = Vector3.Lerp(startPos, targetPos, e);
            camSize = Mathf.Lerp(startSize, targetSize, e);
        });
    }

    float baseSize;

    // Vuelve a la vista completa de la sala. Si 'withBars', las barras salen a la vez. Abre 'doors' por el camino.
    IEnumerator ReturnToRoom(Room room, float duration, bool withBars, List<RoomDoor> doors)
    {
        Vector3 fromPos = camPos;
        float fromSize = camSize;
        Vector3 roomPos = RoomCamPos(room);
        duration = Mathf.Max(0.01f, withBars ? Mathf.Max(duration, barsOutTime) : duration);

        int doorIndex = 0;
        float doorTimer = 0f;
        float t = 0f;
        while (t < duration)
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;
            float k = Mathf.Clamp01(t / duration);
            float e = ease.Evaluate(k);
            camPos = Vector3.Lerp(fromPos, roomPos, e);
            camSize = Mathf.Lerp(fromSize, baseSize, e);
            if (withBars) bars = 1f - Mathf.Clamp01(t / Mathf.Max(0.01f, barsOutTime));

            if (doors != null && doorIndex < doors.Count)
            {
                doorTimer -= dt;
                if (doorTimer <= 0f)
                {
                    RoomDoor d = doors[doorIndex++];
                    if (d != null) d.SetEncounterLocked(false);
                    doorTimer = doorStagger;
                }
            }
            yield return null;
        }

        if (doors != null)
            while (doorIndex < doors.Count)
            {
                RoomDoor d = doors[doorIndex++];
                if (d != null) d.SetEncounterLocked(false);
            }

        camPos = roomPos;
        camSize = baseSize;
        if (withBars) bars = 0f;
    }

    IEnumerator BarsOut()
    {
        yield return Animate(barsOutTime, k => bars = 1f - k);
        bars = 0f;
    }

    void Finish(GameManager gm)
    {
        driveCamera = false;
        clampRoom = null;
        if (cam != null) cam.orthographicSize = baseSize;
        bars = 0f;
        ApplyBars();
        running = false;
        if (gm != null) gm.EndCutscene();
    }

    IEnumerator PuffTraps(Room room)
    {
        var traps = new List<Transform>();
        foreach (var set in room.TrapSets)
        {
            if (set == null || !set.gameObject.activeInHierarchy) continue;
            foreach (Transform child in set.transform)
                if (child.gameObject.activeInHierarchy) traps.Add(child);
        }

        var baseScales = new Dictionary<Transform, Vector3>();
        foreach (var trap in traps)
        {
            if (trap == null) continue;
            baseScales[trap] = trap.localScale;
            Vfx.Play(VfxKind.Dust, trap.position, Vector2.up, trapPuffScale);
            StartCoroutine(Shrink(trap, trap.localScale));
            if (trapStagger > 0f) yield return Wait(trapStagger);
        }
        yield return Wait(trapShrinkTime);

        // Se devuelve el tamano original (la trampa ya esta apagada; asi queda bien si algun dia se reactiva)
        foreach (var pair in baseScales)
            if (pair.Key != null) pair.Key.localScale = pair.Value;
    }

    IEnumerator Shrink(Transform trap, Vector3 from)
    {
        float t = 0f;
        while (t < trapShrinkTime && trap != null)
        {
            t += Time.unscaledDeltaTime;
            trap.localScale = from * (1f - ease.Evaluate(Mathf.Clamp01(t / Mathf.Max(0.01f, trapShrinkTime))));
            yield return null;
        }
    }

    // ================= Camara =================

    Vector3 RoomCamPos(Room room)
    {
        Vector3 p = room.CameraPosition;
        return new Vector3(p.x, p.y, cam.transform.position.z);
    }

    static RoomDoor Nearest(List<RoomDoor> doors, Vector3 point)
    {
        RoomDoor best = null;
        float bestDist = float.PositiveInfinity;
        foreach (var d in doors)
        {
            if (d == null) continue;
            float dist = ((Vector2)(d.transform.position - point)).sqrMagnitude;
            if (dist < bestDist)
            {
                bestDist = dist;
                best = d;
            }
        }
        return best;
    }

    void LateUpdate()
    {
        ApplyBars();
        if (cam == null) return;

        // Durante el golpe inicial (antes de mover la camara) GameFeel no limita la camara: lo hacemos aqui
        if (running && !driveCamera)
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.CurrentRoom != null)
                cam.transform.position = CameraBounds.Clamp(cam.transform.position, cam.orthographicSize, cam.aspect, gm.CurrentRoom);
            return;
        }
        if (!driveCamera) return;

        // GameFeel ya puso la camara en su base + shake: se conserva el shake encima de nuestra posicion
        Vector3 shakeOffset = Vector3.zero;
        if (GameFeel.Instance != null)
        {
            shakeOffset = cam.transform.position - GameFeel.Instance.CameraBase;
            shakeOffset.z = 0f;
            if (shakeOffset.sqrMagnitude > 1f) shakeOffset = Vector3.zero;
        }

        Vector3 final = camPos + shakeOffset;
        if (clampRoom != null) final = CameraBounds.Clamp(final, camSize, cam.aspect, clampRoom);   // nunca fuera de la sala
        cam.transform.position = final;
        cam.orthographicSize = camSize;
    }

    // ================= Barras =================

    void BuildBars()
    {
        var canvasGo = new GameObject("CinematicBars", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        topBar = MakeBar("TopBar", canvasGo.transform);
        bottomBar = MakeBar("BottomBar", canvasGo.transform);
    }

    static RectTransform MakeBar(string barName, Transform parent)
    {
        var go = new GameObject(barName, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = false;
        return rt;
    }

    void ApplyBars()
    {
        float h = barHeight * bars;
        if (topBar != null)
        {
            topBar.anchorMin = new Vector2(0f, 1f - h);
            topBar.anchorMax = new Vector2(1f, 1f + 0.001f);
            topBar.offsetMin = topBar.offsetMax = Vector2.zero;
            topBar.gameObject.SetActive(h > 0.0001f);
        }
        if (bottomBar != null)
        {
            bottomBar.anchorMin = new Vector2(0f, -0.001f);
            bottomBar.anchorMax = new Vector2(1f, h);
            bottomBar.offsetMin = bottomBar.offsetMax = Vector2.zero;
            bottomBar.gameObject.SetActive(h > 0.0001f);
        }
    }

    // ================= Tiempo real (no le afecta el hitstop) =================

    static IEnumerator Wait(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    static IEnumerator Animate(float duration, Action<float> step)
    {
        float t = 0f;
        duration = Mathf.Max(0.01f, duration);
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            step(Mathf.Clamp01(t / duration));
            yield return null;
        }
        step(1f);
    }
}
