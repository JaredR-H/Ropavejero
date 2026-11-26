using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Ropavejero/Sound Library", fileName = "SoundLibrary")]
public class SoundLibrary : ScriptableObject
{
    [System.Serializable]
    public class Sfx
    {
        public SfxId id;
        [Tooltip("Uno o varios clips: si hay varios se elige uno al azar (sin repetir el anterior).")]
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume = 0.8f;
        [Tooltip("Variacion de tono al azar (min, max). (0.95, 1.05) evita que suene repetitivo.")]
        public Vector2 pitch = new Vector2(0.95f, 1.05f);
        [Tooltip("Tiempo minimo entre dos veces el mismo sonido (evita que se amontonen).")]
        public float cooldown = 0.03f;
        [Tooltip("Cuantas copias de este sonido pueden sonar a la vez como maximo.")]
        public int maxVoices = 4;
        [Tooltip("Marcado: pasa por el eco de la mazmorra. Desmarcalo para sonidos 'secos' (p. ej. la UI).")]
        public bool reverb = true;
        [Tooltip("RECORTES de un audio largo: X = segundo donde empieza, Y = cuanto dura (segundos). " +
                 "Si hay varios, se elige uno al azar cada vez (p. ej. 4 pasos distintos del mismo archivo). " +
                 "Vacio = suena el clip completo.")]
        public Vector2[] slices;
    }

    [System.Serializable]
    public class Music
    {
        public MusicId id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 0.7f;
        public bool loop = true;
        [Tooltip("Segundo del clip al que vuelve al repetir (0 = desde el principio). Util si la cancion tiene intro.")]
        public float loopStart = 0f;
    }

    [System.Serializable]
    public class Ambience
    {
        public AmbienceId id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 0.4f;
    }

    public List<Sfx> sounds = new List<Sfx>();
    public List<Music> music = new List<Music>();
    public List<Ambience> ambience = new List<Ambience>();

    Dictionary<SfxId, Sfx> sfxMap;

    public Sfx Get(SfxId id)
    {
        if (sfxMap == null || sfxMap.Count != sounds.Count)
        {
            sfxMap = new Dictionary<SfxId, Sfx>();
            foreach (var s in sounds)
                if (s != null && !sfxMap.ContainsKey(s.id)) sfxMap.Add(s.id, s);
        }
        return sfxMap.TryGetValue(id, out var e) ? e : null;
    }

    public Music Get(MusicId id)
    {
        foreach (var m in music) if (m != null && m.id == id) return m;
        return null;
    }

    public Ambience Get(AmbienceId id)
    {
        foreach (var a in ambience) if (a != null && a.id == id) return a;
        return null;
    }

#if UNITY_EDITOR
    /// Agrega a la lista todos los SfxId que falten (clic derecho en el titulo del componente > Fill Missing).
    [ContextMenu("Fill Missing Ids")]
    void FillMissing()
    {
        foreach (SfxId id in System.Enum.GetValues(typeof(SfxId)))
        {
            if (id == SfxId.None || sounds.Exists(s => s != null && s.id == id)) continue;
            sounds.Add(new Sfx { id = id });
        }
        sounds.Sort((a, b) => ((int)a.id).CompareTo((int)b.id));
        foreach (MusicId id in System.Enum.GetValues(typeof(MusicId)))
            if (id != MusicId.None && !music.Exists(m => m != null && m.id == id)) music.Add(new Music { id = id });
        foreach (AmbienceId id in System.Enum.GetValues(typeof(AmbienceId)))
            if (id != AmbienceId.None && !ambience.Exists(a => a != null && a.id == id)) ambience.Add(new Ambience { id = id });
        sfxMap = null;
        UnityEditor.EditorUtility.SetDirty(this);
    }

    void OnValidate() => sfxMap = null;
#endif
}
