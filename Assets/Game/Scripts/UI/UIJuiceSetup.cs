using UnityEngine;
using UnityEngine.UI;

/// Pon este script en el Canvas: al empezar anade el efecto de squash & stretch (JuiceButton) a TODOS los botones
/// que cuelgan de el, incluso los que estan en paneles apagados (pausa, game over, victoria).
/// Sirve igual en el menu principal y en la escena de juego.
public class UIJuiceSetup : MonoBehaviour
{
    [Tooltip("Perfil de curvas de UI (Create > Ropavejero > Juice Profile). Si esta vacio se usa el de por defecto.")]
    [SerializeField] JuiceProfile uiProfile;

    void Awake()
    {
        foreach (var button in GetComponentsInChildren<Button>(true))
        {
            var juiceButton = button.GetComponent<JuiceButton>();
            if (juiceButton == null) juiceButton = button.gameObject.AddComponent<JuiceButton>();
            juiceButton.Configure(uiProfile);
        }
    }
}
