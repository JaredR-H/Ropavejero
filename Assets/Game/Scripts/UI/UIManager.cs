using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("HUD")]
    [SerializeField] TMP_Text deathsText;
    [SerializeField] TMP_Text timerText;
    [Tooltip("OBSOLETO: el texto de objetos se reemplazo por los iconos. Puedes dejarlo vacio y borrar ese objeto.")]
    [SerializeField] TMP_Text itemsText;
    [SerializeField] Color timerNormalColor = Color.white;
    [SerializeField] Color timerLowColor = new Color(1f, 0.3f, 0.3f);
    [Tooltip("Con estos segundos o menos, el timer se pone rojo, crece y rebota mas fuerte.")]
    [SerializeField] float lowTimeThreshold = 10f;
    [Tooltip("Cuanto crece el timer con poco tiempo (1 = nada).")]
    [SerializeField] float lowTimeScale = 1.18f;
    [SerializeField] string deathsFormat = "Muertes: {0}";

    [Header("Animacion")]
    [Tooltip("Perfil de curvas de UI (Create > Ropavejero > Juice Profile). Si esta vacio se usa el de por defecto.")]
    [SerializeField] JuiceProfile uiProfile;

    [Header("Objetos recogidos (iconos)")]
    [Tooltip("Contenedor de los iconos (con un Vertical Layout Group). Si esta vacio se crea solo en la parte izquierda.")]
    [SerializeField] RectTransform itemsContainer;
    [Tooltip("Opcional: tu propio prefab de slot (con CollectedItemSlot). Si esta vacio se crea por codigo.")]
    [SerializeField] CollectedItemSlot itemSlotPrefab;
    [SerializeField] Vector2 slotSize = new Vector2(72f, 72f);
    [Tooltip("Grosor del borde de color.")]
    [SerializeField, Min(0f)] float borderThickness = 4f;
    [Tooltip("Margen entre el borde y el icono.")]
    [SerializeField, Min(0f)] float iconPadding = 10f;
    [SerializeField] Color slotBackColor = new Color(0f, 0f, 0f, 0.55f);
    [Tooltip("Transparencia de cada slot (1 = opaco).")]
    [SerializeField, Range(0f, 1f)] float slotAlpha = 0.7f;
    [SerializeField] float slotSpacing = 10f;
    [Tooltip("Solo si el contenedor se crea solo: posicion desde la esquina superior izquierda.")]
    [SerializeField] Vector2 autoContainerOffset = new Vector2(30f, -150f);

    [Header("Mensaje central (opcional)")]
    [SerializeField] TMP_Text messageText;

    [Header("Paneles")]
    [SerializeField] GameObject gameOverPanel;
    [SerializeField] GameObject winPanel;
    [SerializeField] TMP_Text winStatsText;

    [Header("Pausa (lo controla PauseMenu: teclas P y Esc)")]
    [Tooltip("Fondo oscuro a pantalla completa del menu de pausa. Se activa y desactiva.")]
    [SerializeField] GameObject pausePanel;
    [Tooltip("Hijo del panel con el titulo y los botones (pivot 0.5, 0.5). Es lo que hace el pop; NO el fondo oscuro, porque al estirarse dejaria bordes.")]
    [SerializeField] RectTransform pauseWindow;
    [Tooltip("Animacion de un disparo del perfil de UI que se usa al abrir la pausa.")]
    [SerializeField] string pauseShot = "Pop";

    Coroutine messageRoutine;
    BounceNumber deathsBounce;
    BounceNumber timerBounce;
    JuiceAnimator pauseJuice;
    readonly Dictionary<Collectible, CollectedItemSlot> slots = new Dictionary<Collectible, CollectedItemSlot>();

    void Awake()
    {
        deathsBounce = SetupNumber(deathsText, deathsFormat, deathsText != null ? deathsText.color : Color.white,
                                   deathsText != null ? deathsText.color : Color.white, 1f);
        timerBounce = SetupNumber(timerText, "{0}", timerNormalColor, timerLowColor, lowTimeScale);
    }

    /// Anade (si faltan) el BounceNumber y el JuiceAnimator al texto, ya preparados para UI.
    BounceNumber SetupNumber(TMP_Text label, string format, Color normal, Color urgent, float urgentScale)
    {
        if (label == null) return null;

        var juice = label.GetComponent<JuiceAnimator>();
        if (juice == null) juice = label.gameObject.AddComponent<JuiceAnimator>();
        juice.Configure(uiProfile, true, false);      // tiempo real, sin balanceo continuo

        var bounce = label.GetComponent<BounceNumber>();
        if (bounce == null)
        {
            bounce = label.gameObject.AddComponent<BounceNumber>();
            bounce.Configure(format, normal, urgent, urgentScale);
        }
        return bounce;
    }

    // ---------- HUD ----------
    public void SetDeaths(int deaths)
    {
        if (deathsBounce != null) deathsBounce.SetValue(deaths);
        else if (deathsText != null) deathsText.text = string.Format(deathsFormat, deaths);
    }

    /// Solo para el texto obsoleto; los iconos de objetos los gestionan AddCollectedItem / RemoveCollectedItem.
    public void SetItems(int collected, int total)
    {
        if (itemsText != null) itemsText.text = "Objetos: " + collected + "/" + total;
    }

    /// Solo segundos (60, 59, 58...). Rebota en cada cambio; con poco tiempo, rojo + mas grande + rebote mas fuerte.
    public void SetTime(float secondsLeft)
    {
        if (timerText == null) return;

        int seconds = Mathf.CeilToInt(Mathf.Max(0f, secondsLeft));
        bool low = secondsLeft <= lowTimeThreshold;

        if (timerBounce != null)
        {
            timerBounce.SetUrgent(low);     // primero el modo, para que el rebote de este cambio ya sea el fuerte
            timerBounce.SetValue(seconds);
        }
        else
        {
            timerText.text = seconds.ToString();
            timerText.color = low ? timerLowColor : timerNormalColor;
        }
    }

    /// Sala sin limite de tiempo.
    public void SetNoTime()
    {
        if (timerText == null) return;

        if (timerBounce != null)
        {
            timerBounce.SetUrgent(false);
            timerBounce.SetText("--");
        }
        else
        {
            timerText.text = "--";
            timerText.color = timerNormalColor;
        }
    }

    // ---------- Objetos recogidos ----------
    public void AddCollectedItem(Collectible item)
    {
        if (item == null || slots.ContainsKey(item)) return;

        RectTransform container = EnsureItemsContainer();
        CollectedItemSlot slot = itemSlotPrefab != null
            ? Instantiate(itemSlotPrefab, container)
            : CollectedItemSlot.Create(container, slotSize, borderThickness, iconPadding, slotBackColor);

        slot.EnsureJuice(uiProfile);
        slot.Setup(item.UiIcon, item.UiBorderColor, slotAlpha);
        slot.Pop();
        slots[item] = slot;
    }

    public void RemoveCollectedItem(Collectible item)
    {
        if (item == null) return;
        if (slots.TryGetValue(item, out CollectedItemSlot slot))
        {
            if (slot != null) Destroy(slot.gameObject);
            slots.Remove(item);
        }
    }

    public void ClearCollectedItems()
    {
        foreach (var pair in slots)
            if (pair.Value != null) Destroy(pair.Value.gameObject);
        slots.Clear();
    }

    RectTransform EnsureItemsContainer()
    {
        if (itemsContainer != null) return itemsContainer;

        var go = new GameObject("CollectedItems", typeof(RectTransform), typeof(VerticalLayoutGroup));
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);
        rt.SetAsFirstSibling();                       // detras de los paneles y de las transiciones
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = autoContainerOffset;
        rt.sizeDelta = Vector2.zero;

        var layout = go.GetComponent<VerticalLayoutGroup>();
        layout.spacing = slotSpacing;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        itemsContainer = rt;
        return rt;
    }

    // ---------- Mensajes y paneles ----------
    /// Texto grande temporal (por ejemplo: "¡Escapa!").
    public void ShowMessage(string text, float seconds)
    {
        if (messageText == null) return;
        if (messageRoutine != null) StopCoroutine(messageRoutine);
        messageRoutine = StartCoroutine(MessageRoutine(text, seconds));
    }

    IEnumerator MessageRoutine(string text, float seconds)
    {
        messageText.text = text;
        messageText.gameObject.SetActive(true);
        yield return new WaitForSeconds(seconds);
        messageText.gameObject.SetActive(false);
        messageRoutine = null;
    }

    public void ShowGameOver()
    {
        HidePause();
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
    }

    public void ShowWin(int deaths, float totalTime, int items, int totalItems)
    {
        HidePause();
        if (winPanel != null) winPanel.SetActive(true);
        if (winStatsText != null)
            winStatsText.text = "Muertes: " + deaths + "\nTiempo: " + FormatTime(totalTime) + "\nObjetos: " + items + "/" + totalItems;
    }

    public void HidePanels()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (winPanel != null) winPanel.SetActive(false);
        HidePause();
        if (messageText != null && messageRoutine == null) messageText.gameObject.SetActive(false);
    }

    // ---------- Pausa ----------
    public bool IsPauseOpen => pausePanel != null && pausePanel.activeSelf;

    /// Abre el menu de pausa con el mismo pop de squash & stretch que el resto de la UI.
    public void ShowPause()
    {
        if (pausePanel == null) return;

        // Primero se activa: asi el JuiceAnimator hace su Awake antes de configurarlo y no pisa el perfil.
        pausePanel.SetActive(true);
        if (pauseWindow == null) return;

        if (pauseJuice == null)
        {
            pauseJuice = pauseWindow.GetComponent<JuiceAnimator>();
            if (pauseJuice == null) pauseJuice = pauseWindow.gameObject.AddComponent<JuiceAnimator>();
            pauseJuice.Configure(uiProfile, true, false);   // tiempo real (el juego esta en pausa), sin balanceo continuo
        }

        pauseJuice.StopAll();
        pauseJuice.Play(pauseShot);
    }

    public void HidePause()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
    }

    static string FormatTime(float seconds)
    {
        int s = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
    }
}