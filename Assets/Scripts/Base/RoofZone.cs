using UnityEngine;

/// <summary>
/// Trigger volume covering the roof. Tag your player "Player".
/// NightDirector checks PlayerOnRoof when night falls.
/// </summary>
[RequireComponent(typeof(Collider))]
public class RoofZone : MonoBehaviour
{
    public static bool PlayerOnRoof { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { PlayerOnRoof = false; }

    void Reset() { GetComponent<Collider>().isTrigger = true; }

    void OnTriggerEnter(Collider other) { if (other.CompareTag("Player")) PlayerOnRoof = true; }
    void OnTriggerExit(Collider other) { if (other.CompareTag("Player")) PlayerOnRoof = false; }
}
