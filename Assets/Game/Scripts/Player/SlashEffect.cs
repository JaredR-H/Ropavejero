using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SlashEffect : MonoBehaviour
{
    [Header("Animacion")]
    [SerializeField] private Animator animator;
    [Tooltip("Nombre EXACTO del estado de slash en el Animator del efecto")]
    [SerializeField] private string slashStateName = "Slash";
    [Tooltip("Duracion en segundos. Si es 0 usa la duracion del clip automaticamente")]
    [SerializeField] private float duration = 0f;

    [Header("Posicion y orientacion")]
    [Tooltip("Si esta marcado, el slash rota hacia la direccion del golpe. Dibuja el sprite mirando a la DERECHA")]
    [SerializeField] private bool rotateToDirection = true;
    [Tooltip("Ajuste de rotacion en grados si tu sprite no mira a la derecha")]
    [SerializeField] private float rotationOffset = 0f;

    private SpriteRenderer sr;
    private Coroutine running;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (animator == null) animator = GetComponent<Animator>();
        sr.enabled = false;
    }

    public void Play(Vector3 worldPosition, Vector2 direction)
    {
        transform.position = worldPosition;

        if (rotateToDirection && direction.sqrMagnitude > 0.001f)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle + rotationOffset);
        }

        if (running != null) StopCoroutine(running);
        running = StartCoroutine(PlayRoutine());
    }

    public void Stop()
    {
        if (running != null) StopCoroutine(running);
        running = null;
        if (sr != null) sr.enabled = false;
    }

    private IEnumerator PlayRoutine()
    {
        sr.enabled = true;

        float time = duration;
        if (animator != null)
        {
            animator.Play(slashStateName, 0, 0f);
            animator.Update(0f); // aplica el primer frame sin esperar

            if (time <= 0f)
            {
                var info = animator.GetCurrentAnimatorStateInfo(0);
                time = info.length > 0f ? info.length : 0.2f;
            }
        }
        else if (time <= 0f)
        {
            time = 0.2f;
        }

        yield return new WaitForSeconds(time);

        sr.enabled = false;
        running = null;
    }

    private void OnDisable()
    {
        if (sr != null) sr.enabled = false;
        running = null;
    }
}
