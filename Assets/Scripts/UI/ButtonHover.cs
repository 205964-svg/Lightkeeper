using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Efeito de hover nos botões: cresce um pouco quando o mouse passa por cima
/// (ou quando o botão é selecionado pelo teclado/controle) e volta ao normal ao sair.
/// Usa tempo "real" (unscaled) para funcionar também com o jogo pausado.
/// </summary>
public class ButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private float hoverScale = 1.05f;
    [SerializeField] private float speed = 12f;

    private Vector3 baseScale;
    private float target = 1f;

    private void Awake() => baseScale = transform.localScale;

    private void OnDisable()
    {
        target = 1f;
        transform.localScale = baseScale;
    }

    public void OnPointerEnter(PointerEventData eventData) => target = hoverScale;
    public void OnPointerExit(PointerEventData eventData) => target = 1f;
    public void OnSelect(BaseEventData eventData) => target = hoverScale;
    public void OnDeselect(BaseEventData eventData) => target = 1f;

    private void Update()
    {
        Vector3 desired = baseScale * target;
        transform.localScale = Vector3.Lerp(transform.localScale, desired, Time.unscaledDeltaTime * speed);
    }
}
