using UnityEngine;

/// <summary>
/// First-person arms: two sleeves and hands fixed to the camera, so the player can see their own body.
/// Built from primitives, tinted through RoofGenerator.Tint (the same shader-safe path as the sky and roof),
/// with colliders removed so they can never block the CharacterController.
/// Attach to the camera. The weapon pivot (MeleeCombat) is parented to the same camera, so the club sits in view.
/// </summary>
public class FirstPersonArms : MonoBehaviour
{
    static readonly Color Sleeve = new Color(0.16f, 0.18f, 0.2f);   // dark jacket
    static readonly Color Skin = new Color(0.82f, 0.64f, 0.52f);

    void Start()
    {
        // This component sits on the camera, so the arms are built as its children.
        var cam = transform;
        Arm(cam, "ArmRight", new Vector3(0.26f, -0.30f, 0.42f), new Vector3(28f, -12f, -18f));
        Arm(cam, "ArmLeft", new Vector3(-0.26f, -0.32f, 0.42f), new Vector3(28f, 12f, 18f));
    }

    static void Arm(Transform cam, string name, Vector3 shoulder, Vector3 rotEuler)
    {
        var root = new GameObject(name).transform;
        root.SetParent(cam, false);
        root.localPosition = shoulder;
        root.localRotation = Quaternion.Euler(rotEuler);

        // Forearm (sleeve)
        var sleeve = Primitive(PrimitiveType.Capsule, root, "Sleeve", new Vector3(0f, 0f, 0.14f), new Vector3(0.09f, 0.2f, 0.09f), Sleeve);
        sleeve.localRotation = Quaternion.Euler(90f, 0f, 0f);

        // Hand
        Primitive(PrimitiveType.Sphere, root, "Hand", new Vector3(0f, 0f, 0.34f), Vector3.one * 0.1f, Skin);
    }

    static Transform Primitive(PrimitiveType type, Transform parent, string name, Vector3 localPos, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        RoofGenerator.Tint(go, color);
        var r = go.GetComponent<Renderer>();
        if (r != null)
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
        return go.transform;
    }
}
