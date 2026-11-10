using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Área iluminada (mecânica de luz e sombra do GDD). Coloque no farol e nas torres.
/// Inimigos fora de qualquer LightSource andam mais rápido (Enemy.darkSpeedMultiplier).
/// Se o objeto também tiver uma Light2D (URP), ajuste o raio dela para combinar com "radius".
/// </summary>
public class LightSource : MonoBehaviour
{
    private static readonly List<LightSource> sources = new List<LightSource>();

    [SerializeField] private float radius = 3f;

    public float Radius
    {
        get => radius;
        set => radius = value;
    }

    private void OnEnable() => sources.Add(this);
    private void OnDisable() => sources.Remove(this);

    /// <summary>Retorna true se o ponto está dentro do raio de alguma fonte de luz.</summary>
    public static bool IsLit(Vector2 position)
    {
        for (int i = 0; i < sources.Count; i++)
        {
            Vector2 delta = position - (Vector2)sources[i].transform.position;
            if (delta.sqrMagnitude <= sources[i].radius * sources[i].radius) return true;
        }
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.9f, 0.4f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
