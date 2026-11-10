using UnityEngine;

/// <summary>Funções auxiliares de física 2D.</summary>
public static class PhysicsUtil
{
    /// <summary>
    /// Garante que o objeto tenha um Rigidbody2D cinemático (sem gravidade).
    /// Em 2D, eventos de trigger (OnTriggerEnter2D) só acontecem se pelo menos um dos dois
    /// objetos tiver Rigidbody2D. Cinemático = nós movemos pelo Transform, a física só detecta.
    /// </summary>
    public static Rigidbody2D EnsureKinematicBody(GameObject go)
    {
        if (!go.TryGetComponent(out Rigidbody2D body))
            body = go.AddComponent<Rigidbody2D>();

        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.useFullKinematicContacts = true;
        return body;
    }
}
