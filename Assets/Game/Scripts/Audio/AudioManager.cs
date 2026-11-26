using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField] SoundLibrary library;

    [Header("Audio Mixer (recomendado)")]
    [Tooltip("Grupo para la musica.")]
    [SerializeField] AudioMixerGroup musicGroup;
    [Tooltip("Grupo para los efectos CON eco (ponle el efecto SFX Reverb en el Audio Mixer).")]
    [SerializeField] AudioMixerGroup sfxGroup;
    [Tooltip("Grupo para los efectos SIN eco (los que tienen 'reverb' desmarcado). Vacio = el de SFX.")]
    [SerializeField] AudioMixerGroup sfxDryGroup;
    [Tooltip("Grupo para el ambiente (goteos, viento...).")]
    [SerializeField] AudioMixerGroup ambienceGroup;

    [Header("Eco sin Audio Mixer")]
    [Tooltip("Si no hay grupo SFX asignado, cada efecto lleva este eco. Cave = cueva, StoneCorridor = pasillo de piedra.")]
    [SerializeField] AudioReverbPreset fallbackReverb = AudioReverbPreset.StoneCorridor;
    [Tooltip("Eco tambien en la musica (normalmente NO: la musica ya viene mezclada).")]
    [SerializeField] bool fallbackReverbOnMusic = false;
    [SerializeField] bool fallbackReverbOnAmbience = true;

    [Header("Efectos")]
    [SerializeField, Min(4)] int voices = 24;
    [Tooltip("Paneo izquierda/derecha segun donde suena respecto a la camara (0 = todo al centro).")]
    [SerializeField, Range(0f, 1f)] float stereoPan = 0.5f;

    [Header("Musica")]
    [Tooltip("Fundido por defecto al cambiar de cancion.")]
    [SerializeField] float defaultMusicFade = 1.2f;
    [Tooltip("Volumen de la musica con el menu de pausa abierto (fraccion).")]
    [SerializeField, Range(0f, 1f)] float pauseDuck = 0.45f;

    [Header("Volumen (se guarda entre partidas)")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float musicVolume = 1f;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    // ---------- Estado ----------
    readonly List<AudioSource> pool = new List<AudioSource>();
    readonly Dictionary<SfxId, float> lastPlayed = new Dictionary<SfxId, float>();
    readonly Dictionary<SfxId, int> lastClip = new Dictionary<SfxId, int>();
    readonly Dictionary<AudioSource, SfxId> playingId = new Dictionary<AudioSource, SfxId>();
    readonly Dictionary<AudioSource, Vector2> sliceEnd = new Dictionary<AudioSource, Vector2>();   // x = cuando para, y = volumen
    readonly List<AudioSource> sliceDone = new List<AudioSource>();
    readonly Dictionary<SfxId, int> lastSlice = new Dictionary<SfxId, int>();
    const float SliceFadeOut = 0.025f;      // fundido minimo al cortar: evita el "clic"
    int next;

    AudioSource musicA, musicB, current, ambienceSource;
    MusicId currentMusic = MusicId.None;
    SoundLibrary.Music currentEntry;
    float musicFade = 1f;           // 0..1 del fundido de la cancion actual
    float duck = 1f, duckTarget = 1f;
    Coroutine musicRoutine, ambienceRoutine;
    float ambienceBaseVolume, ambienceFade;
    AmbienceId currentAmbience = AmbienceId.None;

    public MusicId CurrentMusic => currentMusic;
    /// True si la musica tiene clip asignado en el SoundLibrary.
    public bool HasMusic(MusicId id) => library != null && library.Get(id) != null && library.Get(id).clip != null;
    public bool HasAmbience(AmbienceId id) => library != null && library.Get(id) != null && library.Get(id).clip != null;
    /// Segundo actual de la cancion que suena (para sincronizar animaciones con la musica).
    public float MusicTime => current != null && current.clip != null ? current.time : 0f;
    public bool IsMusicPlaying => current != null && current.isPlaying;

    // ---------- Creacion automatica ----------
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoCreate()
    {
        if (Instance != null) return;
        var prefab = Resources.Load<AudioManager>("AudioManager");
        if (prefab != null) Instantiate(prefab).name = "AudioManager";
        else
        {
            Debug.LogWarning("AudioManager: no hay prefab en Resources/AudioManager. El juego va sin sonido hasta que lo crees.");
            new GameObject("AudioManager").AddComponent<AudioManager>();
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        masterVolume = PlayerPrefs.GetFloat("vol_master", masterVolume);
        musicVolume = PlayerPrefs.GetFloat("vol_music", musicVolume);
        sfxVolume = PlayerPrefs.GetFloat("vol_sfx", sfxVolume);

        musicA = MakeSource("Music A", musicGroup, fallbackReverbOnMusic);
        musicB = MakeSource("Music B", musicGroup, fallbackReverbOnMusic);
        ambienceSource = MakeSource("Ambience", ambienceGroup, fallbackReverbOnAmbience);
        ambienceSource.loop = true;
        for (int i = 0; i < voices; i++) pool.Add(MakeSource("Sfx " + i, null, true));
    }

    AudioSource MakeSource(string label, AudioMixerGroup group, bool reverbIfNoMixer)
    {
        var go = new GameObject(label);
        go.transform.SetParent(transform, false);
        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.spatialBlend = 0f;
        s.outputAudioMixerGroup = group;
        if (reverbIfNoMixer && sfxGroup == null)
        {
            var r = go.AddComponent<AudioReverbFilter>();
            r.reverbPreset = fallbackReverb;
        }
        return s;
    }

    void Update()
    {
        UpdateSlices();
        duck = Mathf.MoveTowards(duck, duckTarget, Time.unscaledDeltaTime * 2f);
        if (current != null && currentEntry != null)
        {
            current.volume = currentEntry.volume * musicFade * duck * musicVolume * masterVolume;

            // Bucle con punto de regreso (por si la cancion tiene una intro que no se repite)
            if (currentEntry.loop && currentEntry.loopStart > 0f && current.clip != null &&
                current.time >= current.clip.length - 0.03f)
                current.time = currentEntry.loopStart;
        }
        if (ambienceSource != null)
            ambienceSource.volume = ambienceBaseVolume * ambienceFade * masterVolume * sfxVolume;
    }

    // =====================================================================
    // EFECTOS
    // =====================================================================
    /// Suena un efecto. 'position' sirve para el paneo izquierda/derecha (null = al centro).
    public void Play(SfxId id, Vector3? position = null, float volumeScale = 1f, float pitchScale = 1f)
    {
        if (id == SfxId.None || library == null) return;
        var e = library.Get(id);
        if (e == null || e.clips == null || e.clips.Length == 0) return;

        float now = Time.unscaledTime;
        if (lastPlayed.TryGetValue(id, out float last) && now - last < e.cooldown) return;
        if (CountPlaying(id) >= Mathf.Max(1, e.maxVoices)) return;

        AudioClip clip = PickClip(id, e.clips);
        if (clip == null) return;
        lastPlayed[id] = now;

        AudioSource s = TakeVoice();
        s.clip = clip;
        s.outputAudioMixerGroup = e.reverb ? sfxGroup : (sfxDryGroup != null ? sfxDryGroup : sfxGroup);
        var filter = s.GetComponent<AudioReverbFilter>();
        if (filter != null) filter.enabled = e.reverb;
        s.volume = e.volume * volumeScale * sfxVolume * masterVolume;
        s.pitch = Random.Range(e.pitch.x, e.pitch.y) * pitchScale;
        s.panStereo = position.HasValue ? PanFor(position.Value) : 0f;
        s.loop = false;
        sliceEnd.Remove(s);

        // Recorte: empieza en un segundo concreto y se corta a la duracion indicada
        if (e.slices != null && e.slices.Length > 0)
        {
            Vector2 slice = PickSlice(id, e.slices);
            float start = Mathf.Clamp(slice.x, 0f, Mathf.Max(0f, clip.length - 0.01f));
            float length = slice.y > 0f ? slice.y : clip.length - start;
            s.time = start;
            s.Play();
            sliceEnd[s] = new Vector2(Time.unscaledTime + length / Mathf.Max(0.05f, s.pitch), s.volume);
        }
        else
        {
            s.time = 0f;
            s.Play();
        }
        playingId[s] = id;
    }

    Vector2 PickSlice(SfxId id, Vector2[] slices)
    {
        if (slices.Length == 1) return slices[0];
        lastSlice.TryGetValue(id, out int prev);
        int i = Random.Range(0, slices.Length);
        if (i == prev) i = (i + 1) % slices.Length;
        lastSlice[id] = i;
        return slices[i];
    }

    // Corta los recortes a su duracion, con un fundido cortito
    void UpdateSlices()
    {
        if (sliceEnd.Count == 0) return;
        float now = Time.unscaledTime;
        sliceDone.Clear();
        foreach (var kv in sliceEnd)
        {
            AudioSource s = kv.Key;
            if (s == null || !s.isPlaying) { sliceDone.Add(s); continue; }
            float left = kv.Value.x - now;
            if (left <= 0f)
            {
                s.Stop();
                sliceDone.Add(s);
            }
            else if (left < SliceFadeOut) s.volume = kv.Value.y * (left / SliceFadeOut);
        }
        foreach (var s in sliceDone) sliceEnd.Remove(s);
    }

    AudioClip PickClip(SfxId id, AudioClip[] clips)
    {
        if (clips.Length == 1) return clips[0];
        lastClip.TryGetValue(id, out int prev);
        int i = Random.Range(0, clips.Length);
        if (i == prev) i = (i + 1) % clips.Length;     // no repetir el mismo dos veces seguidas
        lastClip[id] = i;
        return clips[i];
    }

    int CountPlaying(SfxId id)
    {
        int n = 0;
        foreach (var kv in playingId)
            if (kv.Value == id && kv.Key.isPlaying) n++;
        return n;
    }

    AudioSource TakeVoice()
    {
        for (int i = 0; i < pool.Count; i++)
        {
            var s = pool[(next + i) % pool.Count];
            if (!s.isPlaying)
            {
                next = (next + i + 1) % pool.Count;
                return s;
            }
        }
        var oldest = pool[next];               // todas ocupadas: se corta la mas vieja
        next = (next + 1) % pool.Count;
        oldest.Stop();
        return oldest;
    }

    float PanFor(Vector3 position)
    {
        var cam = Camera.main;
        if (cam == null || stereoPan <= 0f || !cam.orthographic) return 0f;
        float halfWidth = cam.orthographicSize * cam.aspect;
        return Mathf.Clamp((position.x - cam.transform.position.x) / Mathf.Max(0.01f, halfWidth), -1f, 1f) * stereoPan;
    }

    /// Corta todos los efectos que esten sonando (al mostrar el Game Over, por ejemplo).
    public void StopAllSfx()
    {
        foreach (var s in pool)
            if (s != null) s.Stop();
        playingId.Clear();
        sliceEnd.Clear();
    }

    // =====================================================================
    // MUSICA
    // =====================================================================
    /// Cambia de cancion con fundido cruzado. Si ya suena esa, no hace nada. fade < 0 = el de por defecto.
    /// startTime = segundo de la cancion desde el que empieza.
    public void PlayMusic(MusicId id, float fade = -1f, float startTime = 0f)
    {
        if (fade < 0f) fade = defaultMusicFade;
        if (id == MusicId.None)
        {
            StopMusic(fade);
            return;
        }
        if (id == currentMusic && current != null && current.isPlaying) return;

        var entry = library != null ? library.Get(id) : null;
        if (entry == null || entry.clip == null)
        {
            StopMusic(fade);
            currentMusic = id;
            return;
        }

        if (musicRoutine != null) StopCoroutine(musicRoutine);
        musicRoutine = StartCoroutine(CrossFade(entry, id, fade, startTime));
    }

    /// Para la musica con un fundido.
    public void StopMusic(float fade = -1f)
    {
        if (fade < 0f) fade = defaultMusicFade;
        if (musicRoutine != null) StopCoroutine(musicRoutine);
        musicRoutine = StartCoroutine(FadeOutRoutine(fade));
    }

    /// Baja la musica (menu de pausa) o la devuelve a su volumen.
    public void DuckMusic(bool ducked) => duckTarget = ducked ? pauseDuck : 1f;

    IEnumerator CrossFade(SoundLibrary.Music entry, MusicId id, float fade, float startTime)
    {
        AudioSource from = current;
        SoundLibrary.Music fromEntry = currentEntry;
        float fromFade = musicFade;
        AudioSource to = from == musicA ? musicB : musicA;

        to.clip = entry.clip;
        to.loop = entry.loop && entry.loopStart <= 0f;
        to.time = Mathf.Clamp(startTime, 0f, Mathf.Max(0f, entry.clip.length - 0.05f));
        to.volume = 0f;
        to.Play();

        current = to;
        currentEntry = entry;
        currentMusic = id;
        musicFade = 0f;

        float t = 0f;
        while (t < fade)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.01f, fade));
            musicFade = k;
            if (from != null && fromEntry != null)
                from.volume = fromEntry.volume * fromFade * (1f - k) * duck * musicVolume * masterVolume;
            yield return null;
        }
        musicFade = 1f;
        if (from != null) from.Stop();
        musicRoutine = null;
    }

    IEnumerator FadeOutRoutine(float fade)
    {
        float start = musicFade;
        float t = 0f;
        while (t < fade && current != null)
        {
            t += Time.unscaledDeltaTime;
            musicFade = start * (1f - Mathf.Clamp01(t / Mathf.Max(0.01f, fade)));
            yield return null;
        }
        musicFade = 0f;
        if (musicA != null) musicA.Stop();
        if (musicB != null) musicB.Stop();
        currentMusic = MusicId.None;
        currentEntry = null;
        current = null;
        musicRoutine = null;
    }

    // =====================================================================
    // AMBIENTE
    // =====================================================================
    public void PlayAmbience(AmbienceId id, float fade = 2f)
    {
        if (id == currentAmbience && ambienceSource.isPlaying) return;
        if (ambienceRoutine != null) StopCoroutine(ambienceRoutine);
        ambienceRoutine = StartCoroutine(AmbienceRoutine(id, fade));
    }

    IEnumerator AmbienceRoutine(AmbienceId id, float fade)
    {
        // Baja el que suena
        float start = ambienceFade;
        float t = 0f;
        while (ambienceSource.isPlaying && t < fade * 0.5f)
        {
            t += Time.unscaledDeltaTime;
            ambienceFade = start * (1f - t / Mathf.Max(0.01f, fade * 0.5f));
            yield return null;
        }
        ambienceSource.Stop();
        currentAmbience = id;

        var entry = library != null ? library.Get(id) : null;
        if (id == AmbienceId.None || entry == null || entry.clip == null)
        {
            ambienceRoutine = null;
            yield break;
        }

        // Sube el nuevo
        ambienceSource.clip = entry.clip;
        ambienceBaseVolume = entry.volume;
        ambienceFade = 0f;
        ambienceSource.Play();
        t = 0f;
        while (t < fade)
        {
            t += Time.unscaledDeltaTime;
            ambienceFade = Mathf.Clamp01(t / Mathf.Max(0.01f, fade));
            yield return null;
        }
        ambienceFade = 1f;
        ambienceRoutine = null;
    }

    // =====================================================================
    // VOLUMEN (para el menu de opciones)
    // =====================================================================
    public void SetMasterVolume(float v) { masterVolume = Mathf.Clamp01(v); PlayerPrefs.SetFloat("vol_master", masterVolume); }
    public void SetMusicVolume(float v) { musicVolume = Mathf.Clamp01(v); PlayerPrefs.SetFloat("vol_music", musicVolume); }
    public void SetSfxVolume(float v) { sfxVolume = Mathf.Clamp01(v); PlayerPrefs.SetFloat("vol_sfx", sfxVolume); }
}

/// Atajo: Sfx.Play(SfxId.DoorOpen, transform.position);
public static class Sfx
{
    public static void Play(SfxId id, Vector3 position, float volume = 1f, float pitch = 1f)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.Play(id, position, volume, pitch);
    }

    public static void Play(SfxId id, float volume = 1f, float pitch = 1f)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.Play(id, null, volume, pitch);
    }

    public static void Music(MusicId id, float fade = -1f)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayMusic(id, fade);
    }

    public static void StopMusic(float fade = -1f)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.StopMusic(fade);
    }

    public static void Ambience(AmbienceId id, float fade = 2f)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayAmbience(id, fade);
    }
}
