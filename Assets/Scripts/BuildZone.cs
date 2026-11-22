using UnityEngine;

public enum BuildZoneType
{
    Land,
    Water
}

[RequireComponent(typeof(Collider2D))]
public class BuildZone : MonoBehaviour
{
    [Header("Tipo de área")]
    public BuildZoneType zoneType;
}