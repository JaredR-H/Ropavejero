using System.Collections;
using TMPro;
using UnityEngine;

/// Solo muestra datos; la lógica vive en GameManager.
public class UIManager : MonoBehaviour
{
    [Header("HUD")]
    [SerializeField] TMP_Text deathsText;
    [SerializeField] TMP_Text timerText;
    [SerializeField] TMP_Text itemsText;
    [SerializeField] Color timerNormalColor = Color.white;
    [SerializeField] Color timerLowColor = new Color(1f, 0.3f, 0.3f);
    [SerializeField] float lowTimeThreshold = 10f;

    [Header("Mensaje central (opcional)")]
    [SerializeField] TMP_Text messageText;

    [Header("Paneles")]
    [SerializeField] GameObject gameOverPanel;
    [SerializeField] GameObject winPanel;
    [SerializeField] TMP_Text winStatsText;

    Coroutine messageRoutine;

    public void SetDeaths(int deaths)
    {
        if (deathsText != null) deathsText.text = "Muertes: " + deaths;
    }

    public void SetItems(int collected, int total)
    {
        if (itemsText != null) itemsText.text = "Objetos: " + collected + "/" + total;
    }

    public void SetTime(float secondsLeft)
    {
        if (timerText == null) return;
        timerText.text = FormatTime(secondsLeft);
        timerText.color = secondsLeft <= lowTimeThreshold ? timerLowColor : timerNormalColor;
    }

    /// Sala sin límite de tiempo (introducción).
    public void SetNoTime()
    {
        if (timerText == null) return;
        timerText.text = "--:--";
        timerText.color = timerNormalColor;
    }

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
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
    }

    public void ShowWin(int deaths, float totalTime, int items, int totalItems)
    {
        if (winPanel != null) winPanel.SetActive(true);
        if (winStatsText != null)
            winStatsText.text = "Muertes: " + deaths + "\nTiempo: " + FormatTime(totalTime) + "\nObjetos: " + items + "/" + totalItems;
    }

    public void HidePanels()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (winPanel != null) winPanel.SetActive(false);
        if (messageText != null && messageRoutine == null) messageText.gameObject.SetActive(false);
    }

    static string FormatTime(float seconds)
    {
        int s = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
    }
}