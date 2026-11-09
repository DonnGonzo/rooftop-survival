using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>Editor-only test helpers called through DevBridge ("invoke Diag.Method|argument").</summary>
public static class Diag
{
    static float F(string s) => float.Parse(s.Trim(), CultureInfo.InvariantCulture);

    static readonly Queue<string> steps = new Queue<string>();
    static double waitUntil;
    static bool hooked;

    /// <summary>"Step;Step;..." run in order across frames. A step is "wait seconds" or "Method argument".</summary>
    public static string Seq(string script)
    {
        foreach (var s in script.Split(';')) if (s.Trim().Length > 0) steps.Enqueue(s.Trim());
        if (!hooked) { hooked = true; UnityEditor.EditorApplication.update += Tick; }
        return "queued " + steps.Count;
    }

    static void Tick()
    {
        while (steps.Count > 0 && UnityEditor.EditorApplication.timeSinceStartup >= waitUntil)
        {
            string s = steps.Dequeue();
            int sp = s.IndexOf(' ');
            string name = sp < 0 ? s : s.Substring(0, sp), arg = sp < 0 ? "" : s.Substring(sp + 1);
            if (name == "wait") { waitUntil = UnityEditor.EditorApplication.timeSinceStartup + F(arg); return; }
            try
            {
                var mi = typeof(Diag).GetMethod(name, BindingFlags.Static | BindingFlags.Public);
                mi.Invoke(null, mi.GetParameters().Length == 0 ? null : new object[] { arg });
            }
            catch (System.Exception e) { Debug.LogError("[Diag.Seq] " + s + ": " + (e.InnerException ?? e).Message); }
        }
    }

    public static string Mats(string who)
    {
        var sb = new StringBuilder();
        var go = GameObject.Find(who.Trim());
        if (go == null) return "not found";
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var smr = r as SkinnedMeshRenderer;
            Mesh mesh = smr != null ? smr.sharedMesh : (r.GetComponent<MeshFilter>() != null ? r.GetComponent<MeshFilter>().sharedMesh : null);
            sb.AppendLine(r.name + " verts=" + (mesh != null ? mesh.vertexCount : 0) + " uv=" + (mesh != null ? mesh.uv.Length : 0) + " normals=" + (mesh != null ? mesh.normals.Length : 0));
            foreach (var m in r.sharedMaterials)
            {
                if (m == null) { sb.AppendLine("  null material"); continue; }
                sb.Append("  " + m.name + " shader=" + m.shader.name + " kw=[" + string.Join(",", m.shaderKeywords) + "]");
                foreach (var prop in m.GetTexturePropertyNames())
                {
                    var t = m.GetTexture(prop);
                    if (t != null) sb.Append(" " + prop + "=" + t.name + "(" + t.width + ")");
                }
                foreach (var f in new[] { "metallicFactor", "roughnessFactor", "_Metallic", "_Glossiness", "_Smoothness", "specularFactor" })
                    if (m.HasProperty(f)) sb.Append(" " + f + "=" + m.GetFloat(f).ToString("F2"));
                sb.AppendLine();
            }
        }
        return sb.ToString();
    }

    public static string Pending() { return steps.Count.ToString(); }

    public static string Start()
    {
        GameState.MenuOpen = false; GameState.Playing = true; GameState.HasStarted = true; GameState.UiOpen = false;
        return "started";
    }

    public static string Player()
    {
        var sb = new StringBuilder();
        var body = Object.FindAnyObjectByType<PlayerBody>();
        if (body == null) return "no PlayerBody";
        var pc = PlayerController.Instance;
        sb.AppendLine("character=" + Characters.Current.modelKey + " frozen=" + GameState.Frozen + " pos=" + pc.transform.position + " speed=" + pc.HorizontalSpeed.ToString("F1") + " grounded=" + pc.Grounded);
        foreach (var a in body.GetComponentsInChildren<Animation>(true))
        {
            sb.Append("Animation enabled=" + a.enabled + " playing=" + a.isPlaying + " clips=" + a.GetClipCount() + ":");
            foreach (AnimationState s in a) sb.Append(" " + s.name + (s.enabled ? "*" + s.weight.ToString("F1") : ""));
            sb.AppendLine();
        }
        var m = body.GetComponent<MeleeCombat>();
        sb.AppendLine("melee armed=" + m.Armed + " swinging=" + m.IsSwinging + " Rw=" + m.RightWeight.ToString("F2") + " Lw=" + m.LeftWeight.ToString("F2"));
        return sb.ToString();
    }

    /// <summary>"x,y" movement (strafe, forward) and optional ",sprint". Empty = give control back to the keyboard.</summary>
    public static string Move(string a)
    {
        if (string.IsNullOrWhiteSpace(a)) { Inp.MoveOverride = null; Inp.SprintOverride = false; return "keyboard"; }
        var p = a.Split(',');
        Inp.MoveOverride = new Vector2(F(p[0]), F(p[1]));
        Inp.SprintOverride = p.Length > 2;
        return "moving " + Inp.MoveOverride;
    }

    public static string Swing(string a)
    {
        Object.FindAnyObjectByType<MeleeCombat>().Swing(a.Trim() == "heavy");
        return "swing " + a;
    }

    public static string View(string a)
    {
        Object.FindAnyObjectByType<PlayerBody>().SetThirdPerson(a.Trim() == "tp");
        return "view " + a;
    }

    public static string Pitch(string a) { PlayerController.Instance.SetPitch(F(a)); return "pitch " + a; }
    public static string Yaw(string a) { PlayerController.Instance.transform.rotation = Quaternion.Euler(0f, F(a), 0f); return "yaw " + a; }

    public static string Give(string a)
    {
        var p = a.Split(',');
        Items.Add(p[0].Trim(), p.Length > 1 ? int.Parse(p[1]) : 1);
        return "gave " + a;
    }

    public static string Hour(string a)
    {
        var c = GameClock.Instance;
        typeof(GameClock).GetProperty("Hour").SetValue(c, F(a));
        return "hour " + c.Hour;
    }

    public static string Tele(string a)
    {
        var p = a.Split(',');
        var pc = PlayerController.Instance;
        var cc = pc.GetComponent<CharacterController>();
        cc.enabled = false;
        pc.transform.position = new Vector3(F(p[0]), F(p[1]), F(p[2]));
        cc.enabled = true;
        return "at " + pc.transform.position;
    }

    public static string Scale(string a) { Time.timeScale = F(a); return "timeScale " + Time.timeScale; }

    /// <summary>"file,dx,dy,dz[,tx,ty,tz]": picture from a camera offset from the player (player space), looking at the player's chest or at the given world-offset target.</summary>
    public static string Snap(string a)
    {
        var p = a.Split(',');
        var pc = PlayerController.Instance.transform;
        Vector3 pos = pc.TransformPoint(new Vector3(F(p[1]), F(p[2]), F(p[3])));
        Vector3 look = p.Length > 6 ? pc.TransformPoint(new Vector3(F(p[4]), F(p[5]), F(p[6]))) : pc.position + Vector3.up * 1.1f;
        var go = new GameObject("DiagCam");
        var cam = go.AddComponent<Camera>();
        cam.transform.position = pos;
        cam.transform.LookAt(look);
        cam.nearClipPlane = 0.05f; cam.farClipPlane = 1200f; cam.fieldOfView = 55f;
        string r = Render(cam, p[0].Trim());
        Object.DestroyImmediate(go);
        return r;
    }

    /// <summary>"file,x,y,z,tx,ty,tz[,fov]": picture from a free camera at a world position looking at a world point.</summary>
    public static string Cam(string a)
    {
        var p = a.Split(',');
        var go = new GameObject("DiagCam");
        var cam = go.AddComponent<Camera>();
        cam.transform.position = new Vector3(F(p[1]), F(p[2]), F(p[3]));
        cam.transform.LookAt(new Vector3(F(p[4]), F(p[5]), F(p[6])));
        cam.nearClipPlane = 0.1f; cam.farClipPlane = 1500f; cam.fieldOfView = p.Length > 7 ? F(p[7]) : 60f;
        string r = Render(cam, p[0].Trim());
        Object.DestroyImmediate(go);
        return r;
    }

    /// <summary>Lists the animals: name, position, health.</summary>
    public static string Animals()
    {
        var sb = new StringBuilder();
        foreach (var a in Object.FindObjectsByType<AnimalAI>())
            sb.AppendLine(a.name + " " + a.transform.position.ToString("F0") + " scale=" + a.transform.localScale.x.ToString("F2"));
        return sb.ToString();
    }

    /// <summary>Game view with the HUD and menus (needs the Game view visible in the Editor).</summary>
    public static string Shot(string file)
    {
        string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "DevBridge");
        ScreenCapture.CaptureScreenshot(Path.Combine(dir, file.Trim()));
        return "queued";
    }

    /// <summary>Picture from the player's own camera.</summary>
    public static string Pov(string file) { return Render(Camera.main, file.Trim()); }

    static string Render(Camera cam, string file)
    {
        const int w = 960, h = 540;
        var rt = new RenderTexture(w, h, 24);
        var prev = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = prev;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "DevBridge");
        File.WriteAllBytes(Path.Combine(dir, file), tex.EncodeToJPG(80));
        Object.DestroyImmediate(tex);
        rt.Release();
        Object.DestroyImmediate(rt);
        return "saved " + file;
    }
}
