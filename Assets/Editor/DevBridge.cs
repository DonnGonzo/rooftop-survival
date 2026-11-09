using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

/// <summary>
/// Development bridge: lets an outside tool drive this Editor through files in <project>/DevBridge.
/// Write "id|command|argument" to DevBridge/request.txt; the answer appears in DevBridge/result_id.txt.
/// Commands: status, refresh, play, stop, shot (Game view PNG), camshot (main camera PNG),
/// invoke (calls a static method "Type.Method" with an optional string argument).
/// Everything the Console prints is also appended to DevBridge/console.log.
/// Editor only: none of this is in a build. Delete this file and the DevBridge folder to remove it.
/// </summary>
[InitializeOnLoad]
public static class DevBridge
{
    static readonly string Dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "DevBridge");
    static readonly string RequestPath = Path.Combine(Dir, "request.txt");
    static readonly string LogPath = Path.Combine(Dir, "console.log");
    static readonly object logLock = new object();
    static double nextPoll;

    static DevBridge()
    {
        Directory.CreateDirectory(Dir);
        Application.logMessageReceivedThreaded += OnLog;
        CompilationPipeline.assemblyCompilationFinished += OnCompiled;
        EditorApplication.update += Poll;
        Append("[bridge] loaded " + DateTime.Now.ToString("HH:mm:ss") + " playing=" + EditorApplication.isPlaying);
    }

    static void OnLog(string msg, string stack, LogType type)
    {
        if (type == LogType.Log || type == LogType.Warning) Append("[" + type + "] " + msg);
        else Append("[" + type + "] " + msg + "\n" + stack);
    }

    static void OnCompiled(string assembly, CompilerMessage[] messages)
    {
        foreach (var m in messages)
            if (m.type == CompilerMessageType.Error) Append("[CompileError] " + m.message);
        Append("[bridge] compiled " + Path.GetFileName(assembly));
    }

    static void Append(string line)
    {
        lock (logLock)
        {
            try { File.AppendAllText(LogPath, line + "\n"); } catch { }
        }
    }

    static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 0.1;
        string[] files;
        try { files = Directory.GetFiles(Dir, "req_*.txt"); } catch { return; }
        if (files.Length == 0) return;
        Array.Sort(files);
        string text;
        try { text = File.ReadAllText(files[0]).Trim(); File.Delete(files[0]); }
        catch { return; }
        if (text.Length == 0) return;

        var parts = text.Split(new[] { '|' }, 3);
        string id = parts[0];
        string cmd = parts.Length > 1 ? parts[1].Trim() : "";
        string arg = parts.Length > 2 ? parts[2] : "";
        string result;
        try { result = Run(cmd, arg); }
        catch (Exception e) { result = "ERROR " + (e.InnerException ?? e); }
        File.WriteAllText(Path.Combine(Dir, "result_" + id + ".txt"), result ?? "");
    }

    static string Run(string cmd, string arg)
    {
        switch (cmd)
        {
            case "status":
                return "playing=" + EditorApplication.isPlaying + " compiling=" + EditorApplication.isCompiling
                     + " paused=" + EditorApplication.isPaused + " frame=" + Time.frameCount + " time=" + Time.time.ToString("F2");
            case "refresh":
                AssetDatabase.Refresh();
                return "refresh requested";
            case "play":
                Application.runInBackground = true;
                EditorApplication.isPlaying = true;
                return "entering play mode";
            case "stop":
                EditorApplication.isPlaying = false;
                return "leaving play mode";
            case "shot":
                ScreenCapture.CaptureScreenshot(Path.Combine(Dir, arg.Length > 0 ? arg : "shot.png"));
                return "screenshot queued";
            case "camshot":
                return CamShot(arg.Length > 0 ? arg : "cam.png");
            case "invoke":
                return Invoke(arg);
        }
        return "ERROR unknown command " + cmd;
    }

    /// <summary>Renders the main camera to a PNG. Works even when the Game view is hidden (no HUD in it).</summary>
    static string CamShot(string file)
    {
        var cam = Camera.main;
        if (cam == null) return "ERROR no main camera";
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
        File.WriteAllBytes(Path.Combine(Dir, file), tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        rt.Release();
        UnityEngine.Object.DestroyImmediate(rt);
        return "saved " + file;
    }

    /// <summary>"Type.Method" or "Type.Method|argument". The method must be static and take nothing or one string.</summary>
    static string Invoke(string spec)
    {
        var p = spec.Split(new[] { '|' }, 2);
        int dot = p[0].LastIndexOf('.');
        if (dot < 0) return "ERROR expected Type.Method";
        string typeName = p[0].Substring(0, dot).Trim(), method = p[0].Substring(dot + 1).Trim();
        Type type = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            type = asm.GetType(typeName);
            if (type != null) break;
        }
        if (type == null) return "ERROR type not found: " + typeName;
        var mi = type.GetMethod(method, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (mi == null) return "ERROR method not found: " + method;
        object[] args = mi.GetParameters().Length == 0 ? null : new object[] { p.Length > 1 ? p[1] : "" };
        var r = mi.Invoke(null, args);
        return r != null ? r.ToString() : "ok";
    }
}
