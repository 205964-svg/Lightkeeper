using UnityEngine;

/// <summary>
/// Caminho fixo que os inimigos seguem: uma lista ordenada de pontos (waypoints).
/// O primeiro ponto é a ENTRADA e o último deve ficar sobre o FAROL.
/// Forma mais fácil de montar: crie objetos vazios como filhos deste, na ordem do trajeto
/// (Waypoint_0, Waypoint_1, ...). Se a lista "waypoints" estiver vazia, os filhos são usados.
/// </summary>
public class EnemyPath : MonoBehaviour
{
    [SerializeField] private Transform[] waypoints;
    [Tooltip("Opcional: desenha o caminho em jogo com um LineRenderer.")]
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private Color gizmoColor = new Color(1f, 0.6f, 0.1f);

    public int Count
    {
        get
        {
            EnsureWaypoints();
            return waypoints.Length;
        }
    }

    public Vector3 GetWaypoint(int index)
    {
        EnsureWaypoints();
        return waypoints[index].position;
    }

    private void Awake()
    {
        EnsureWaypoints();
        if (lineRenderer != null)
        {
            lineRenderer.positionCount = waypoints.Length;
            for (int i = 0; i < waypoints.Length; i++)
                lineRenderer.SetPosition(i, waypoints[i].position);
        }
    }

    private void EnsureWaypoints()
    {
        if (waypoints != null && waypoints.Length > 0) return;

        waypoints = new Transform[transform.childCount];
        for (int i = 0; i < transform.childCount; i++)
            waypoints[i] = transform.GetChild(i);
    }

    /// <summary>Desenha o caminho na janela Scene para facilitar a edição.</summary>
    private void OnDrawGizmos()
    {
        Transform[] points = (waypoints != null && waypoints.Length > 0) ? waypoints : null;
        int count = points != null ? points.Length : transform.childCount;
        if (count == 0) return;

        Gizmos.color = gizmoColor;
        Vector3 previous = Vector3.zero;
        for (int i = 0; i < count; i++)
        {
            Transform t = points != null ? points[i] : transform.GetChild(i);
            if (t == null) continue;
            Gizmos.DrawSphere(t.position, i == 0 ? 0.25f : 0.12f);
            if (i > 0) Gizmos.DrawLine(previous, t.position);
            previous = t.position;
        }
    }
}
