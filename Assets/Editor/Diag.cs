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

    public static string Linear()
    {
        UnityEditor.PlayerSettings.colorSpace = ColorSpace.Linear;
        return "colorSpace=" + UnityEditor.PlayerSettings.colorSpace;
    }

    public static string Raid(string a) { NightDirector.Instance.StartRaid(GameClock.Instance.Day, F(a)); return "raid started"; }
    public static string Xp(string a) { Progress.Gain((int)F(a)); return "level " + Progress.Level; }
    public static string Day(string a) { typeof(GameClock).GetProperty("Day").SetValue(GameClock.Instance, (int)F(a)); return "day " + GameClock.Instance.Day; }
    public static string NewRun() { RunManager.Instance.StartNewRun(); GameState.AutoStart = true; UnityEngine.SceneManagement.SceneManager.LoadScene(0); return "new run"; }

    /// <summary>Counts of what exists, and the dens.</summary>
    public static string World()
    {
        var sb = new StringBuilder();
        sb.AppendLine(StreetBuilder.Diag + " animals=" + AnimalAI.All.Count + " nodes=" + Object.FindObjectsByType<ResourceNode>().Length
            + " containers=" + Object.FindObjectsByType<Container>().Length + " renderers=" + Object.FindObjectsByType<Renderer>().Length
            + " fps=" + (1f / Time.smoothDeltaTime).ToString("F0"));
        foreach (var d in SpawnDirector.Dens) sb.AppendLine(d.label + " " + d.pos.ToString("F0") + " live=" + d.live.Count + (d.nightOnly ? " night" : ""));
        return sb.ToString();
    }

    /// <summary>World positions of the first few objects whose name starts with the text.</summary>
    public static string Find(string prefix)
    {
        var sb = new StringBuilder();
        int n = 0;
        foreach (var t in Object.FindObjectsByType<Transform>())
        {
            if (!t.name.StartsWith(prefix.Trim())) continue;
            sb.AppendLine(t.name + " " + t.position.ToString("F1") + " rotY=" + t.eulerAngles.y.ToString("F0"));
            if (++n >= 12) break;
        }
        return sb.ToString();
    }

    public static string Hold(string a) { Inp.HoldOverride = a.Trim() == "1"; return "hold " + Inp.HoldOverride; }
    public static string Place(string kit)
    {
        var pc = PlayerController.Instance.transform;
        Items.Add(kit.Trim(), 1);
        bool ok = Buildables.Place(kit.Trim(), new Vector3(pc.position.x, 0f, pc.position.z) + pc.forward * 2.4f, pc.eulerAngles.y);
        if (ok) Items.Remove(kit.Trim(), 1);
        return "placed " + ok;
    }
    public static string Equip(string id) { id = id.Trim(); if (id == "none") { Items.Equip(""); return "fists"; } Items.Add(id, 1); Items.Equip(id); return "equipped " + id; }
    public static string Inv()
    {
        var sb = new StringBuilder();
        foreach (var d in Items.Defs) { int n = Items.Count(d.id); if (n > 0) sb.Append(d.id + "=" + n + " "); }
        var pc = PlayerController.Instance;
        return sb + "| lvl " + Progress.Level + " xp " + Progress.Xp + " hp " + pc.health.ToString("F0") + " prompt='" + pc.Prompt + "' rope='" + pc.RopePrompt + "' hold=" + pc.HoldProgress.ToString("F2")
             + " day " + GameClock.Instance.Day + " h " + GameClock.Instance.Hour.ToString("F1") + " raid=" + NightDirector.RaidActive + ":" + NightDirector.RaidersLeft + " def=" + BaseStructure.TotalDefense();
    }
    public static string Order(string id)
    {
        var st = Object.FindAnyObjectByType<CraftStation>();
        if (st == null) return "no station";
        foreach (var r in Items.Recipes) if (r.output == id.Trim()) return "order " + st.PlaceOrder(r);
        return "no recipe";
    }
    public static string Map() { MapView.Toggle(); return "map " + MapView.Showing; }
    public static string SleepNow() { GameClock.Sleep(); return "sleeping"; }
    /// <summary>Moves the player next to the nearest object of a kind: tree, rock, wreck, container, animal.</summary>
    public static string Near(string what)
    {
        var pc = PlayerController.Instance;
        Transform best = null; float bd = 1e9f;
        System.Action<Transform> consider = t => { float d = (t.position - pc.transform.position).sqrMagnitude; if (d < bd) { bd = d; best = t; } };
        what = what.Trim();
        if (what == "container") { foreach (var c in Object.FindObjectsByType<Container>()) if (!c.Empty && c.transform.position.y < -50f) consider(c.transform); }
        else if (what == "animal") { foreach (var a in AnimalAI.All) consider(a.transform); }
        else foreach (var n in Object.FindObjectsByType<ResourceNode>())
            if (!n.Spent && n.kind.ToString().ToLower() == what) consider(n.transform);
        if (best == null) return "none";
        Vector3 dir = (pc.transform.position - best.position); dir.y = 0f; dir = dir.sqrMagnitude < 0.01f ? Vector3.forward : dir.normalized;
        Tele((best.position.x + dir.x * 1.6f).ToString(CultureInfo.InvariantCulture) + "," + (best.position.y + 0.4f).ToString(CultureInfo.InvariantCulture) + "," + (best.position.z + dir.z * 1.6f).ToString(CultureInfo.InvariantCulture));
        pc.transform.rotation = Quaternion.LookRotation(-dir);
        return "near " + best.name + " " + best.position.ToString("F0");
    }

    /// <summary>Native size of models (comma separated keys).</summary>
    public static string Bounds(string keys)
    {
        var sb = new StringBuilder();
        foreach (var k in keys.Split(','))
        {
            var go = ModelLibrary.Spawn(k.Trim(), null, new Vector3(0, 900, 0), 0f, keepAnimations: false);
            if (go == null) { sb.AppendLine(k + " missing"); continue; }
            var b = ModelLibrary.LocalBounds(go.transform);
            sb.AppendLine(k.Trim() + " size=" + b.size.ToString("F2") + " centre=" + b.center.ToString("F2"));
            Object.DestroyImmediate(go);
        }
        return sb.ToString();
    }

    /// <summary>"file,key[,height]": a studio picture of one model.</summary>
    public static string Preview(string a)
    {
        var p = a.Split(',');
        var go = ModelLibrary.Spawn(p[1].Trim(), null, new Vector3(0, 900, 0), 0f, fitHeight: p.Length > 2 ? F(p[2]) : 0f, keepAnimations: false);
        if (go == null) return "missing";
        var b = ModelLibrary.LocalBounds(go.transform);
        float s = go.transform.localScale.x;
        float r = Mathf.Max(b.size.x, b.size.y, b.size.z) * s;
        var cg = new GameObject("DiagCam");
        var cam = cg.AddComponent<Camera>();
        Vector3 c = go.transform.position + Vector3.up * b.size.y * s * 0.5f;
        cam.transform.position = c + new Vector3(r * 0.9f, r * 0.6f, r * 1.3f);
        cam.transform.LookAt(c);
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.5f, 0.55f, 0.6f);
        cam.nearClipPlane = 0.01f; cam.fieldOfView = 40f;
        string res = Render(cam, p[0].Trim());
        Object.DestroyImmediate(cg); Object.DestroyImmediate(go);
        return res + " size=" + (b.size * s).ToString("F2");
    }

    public static string Hands()
    {
        var body = Object.FindAnyObjectByType<PlayerBody>();
        var sb = new StringBuilder();
        foreach (string f in new[] { "rightHand", "leftHand" })
        {
            object h = typeof(PlayerBody).GetField(f, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(body);
            var t = h.GetType();
            Vector3 fd = (Vector3)t.GetMethod("FingerDir").Invoke(h, null), pd = (Vector3)t.GetMethod("PalmDir").Invoke(h, null);
            var hand = (Transform)t.GetField("hand").GetValue(h);
            var fingers = (System.Collections.Generic.List<Transform>)t.GetField("fingers").GetValue(h);
            var thumb = (System.Collections.Generic.List<Transform>)t.GetField("thumb").GetValue(h);
            sb.AppendLine(f + " finger=" + body.transform.InverseTransformDirection(fd).ToString("F2") + " palm=" + body.transform.InverseTransformDirection(pd).ToString("F2")
                + " forearm=" + body.transform.InverseTransformDirection((hand.position - hand.parent.position).normalized).ToString("F2") + " fingers=" + fingers.Count + " thumb=" + thumb.Count
                + " first=" + (fingers.Count > 0 ? fingers[0].name + "/" + fingers[1].name + "/" + fingers[2].name + "/" + fingers[3].name : ""));
        }
        return sb.ToString();
    }

    public static string Character(string i) { GameSettings.CharacterIndex = (int)F(i); return Characters.Current.name; }

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
        foreach (var a in AnimalAI.All)
            sb.AppendLine(a.name + " " + a.mode + " " + a.transform.position.ToString("F0"));
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
