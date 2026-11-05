using UnityEngine;
using UnityEngine.UI;

/// Un objeto recogido en la UI: su sprite dentro de un marco con borde de color, semitransparente.
/// UIManager lo crea por codigo (marco + fondo + icono); tambien puedes hacer tu propio prefab con estas tres Image
/// y arrastrarlo a "Item Slot Prefab" para usar tu arte.
public class CollectedItemSlot : MonoBehaviour
{
    [SerializeField] Image frame;      // el borde: se pinta con el color del objeto
    [SerializeField] Image back;       // fondo oscuro dentro del borde
    [SerializeField] Image icon;       // el sprite del objeto
    [SerializeField] CanvasGroup group;
    [SerializeField] JuiceAnimator juice;

    /// Crea un slot sin necesidad de prefab.
    public static CollectedItemSlot Create(RectTransform parent, Vector2 size, float border, float padding, Color backColor)
    {
        var go = new GameObject("ItemSlot", typeof(RectTransform), typeof(CanvasGroup), typeof(LayoutElement));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.sizeDelta = size;

        var layout = go.GetComponent<LayoutElement>();
        layout.minWidth = layout.preferredWidth = size.x;
        layout.minHeight = layout.preferredHeight = size.y;

        var slot = go.AddComponent<CollectedItemSlot>();
        slot.group = go.GetComponent<CanvasGroup>();
        slot.frame = MakeImage("Frame", rt, 0f);
        slot.back = MakeImage("Back", rt, border);
        slot.icon = MakeImage("Icon", rt, padding);
        slot.back.color = backColor;
        slot.icon.preserveAspect = true;
        return slot;
    }

    static Image MakeImage(string objectName, RectTransform parent, float inset)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);

        var image = go.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    public void Setup(Sprite sprite, Color borderColor, float alpha)
    {
        if (frame != null) frame.color = borderColor;
        if (icon != null)
        {
            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }
        if (group != null) group.alpha = alpha;
    }

    /// Prepara el JuiceAnimator del slot (tiempo real, sin balanceo continuo).
    public void EnsureJuice(JuiceProfile profile)
    {
        if (juice == null) juice = GetComponent<JuiceAnimator>();
        if (juice == null) juice = gameObject.AddComponent<JuiceAnimator>();
        juice.Configure(profile, true, false);
    }

    /// Rebote al aparecer en la UI.
    public void Pop(float intensity = 1f)
    {
        if (juice != null) juice.Play("ItemPop", Vector2.zero, 0f, intensity);
    }
}
