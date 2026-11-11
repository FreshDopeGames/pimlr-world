using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using PIMLR.Analytics;
using UnityEditor;
using UnityEngine;

// PIMLR #ANL: editor helpers for the local analytics files.
//   PIMLR > Analytics > Open Analytics Folder     - the Editor's own play sessions
//   PIMLR > Analytics > Merge Folder to CSV...    - pick any folder of .jsonl (e.g. a playtester's export) and get
//                                                   one CSV per event type in <folder>/csv/, ready for a spreadsheet.
// Columns: the envelope fields, then every "d" field seen for that event (nested objects are flattened as a.b).
public static class AnalyticsTools
{
    [MenuItem("PIMLR/Analytics/Open Analytics Folder")]
    static void OpenFolder()
    {
        Directory.CreateDirectory(PimlrAnalytics.Folder);
        EditorUtility.RevealInFinder(PimlrAnalytics.Folder + Path.DirectorySeparatorChar);
    }

    [MenuItem("PIMLR/Analytics/Merge Folder to CSV...")]
    static void MergeToCsv()
    {
        string dir = EditorUtility.OpenFolderPanel("Folder of .jsonl analytics files", PimlrAnalytics.Folder, "");
        if (string.IsNullOrEmpty(dir)) return;
        var files = Directory.GetFiles(dir, "*.jsonl", SearchOption.AllDirectories);
        if (files.Length == 0) { EditorUtility.DisplayDialog("PIMLR Analytics", "No .jsonl files in that folder.", "OK"); return; }

        var byEvent = new Dictionary<string, List<Dictionary<string, string>>>();
        int bad = 0;
        foreach (var f in files)
        foreach (var line in File.ReadLines(f))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            Dictionary<string, object> obj;
            try { obj = MiniJson.Parse(line) as Dictionary<string, object>; }
            catch { bad++; continue; }
            if (obj == null) { bad++; continue; }

            var flat = new Dictionary<string, string>();
            Flatten("", obj, flat);
            string ev = flat.TryGetValue("ev", out var e) ? e : "unknown";
            if (!byEvent.TryGetValue(ev, out var list)) byEvent[ev] = list = new List<Dictionary<string, string>>();
            list.Add(flat);
        }

        string outDir = Path.Combine(dir, "csv");
        Directory.CreateDirectory(outDir);
        string[] envelope = { "v", "t", "st", "sid", "rid", "pid", "build", "plat", "input", "mode", "scene", "artist", "ev" };
        foreach (var kv in byEvent)
        {
            var cols = new List<string>(envelope);
            foreach (var row in kv.Value)
                foreach (var k in row.Keys)
                    if (!cols.Contains(k)) cols.Add(k);

            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", cols.Select(Csv)));
            foreach (var row in kv.Value)
                sb.AppendLine(string.Join(",", cols.Select(c => Csv(row.TryGetValue(c, out var v) ? v : ""))));
            File.WriteAllText(Path.Combine(outDir, kv.Key + ".csv"), sb.ToString());
        }

        EditorUtility.DisplayDialog("PIMLR Analytics",
            $"{files.Length} files, {byEvent.Sum(k => k.Value.Count)} events, {byEvent.Count} event types.\n" +
            (bad > 0 ? $"{bad} unreadable lines skipped.\n" : "") + $"CSVs written to:\n{outDir}", "OK");
        EditorUtility.RevealInFinder(outDir);
    }

    static void Flatten(string prefix, Dictionary<string, object> obj, Dictionary<string, string> into)
    {
        foreach (var kv in obj)
        {
            // "d" fields are written without the "d." prefix so CSV columns read naturally.
            string key = prefix.Length == 0 ? kv.Key : prefix + "." + kv.Key;
            if (prefix.Length == 0 && kv.Key == "d" && kv.Value is Dictionary<string, object> d) { Flatten("", d, into); continue; }
            if (kv.Value is Dictionary<string, object> child) Flatten(key, child, into);
            else into[key] = ToText(kv.Value);
        }
    }

    static string ToText(object v)
    {
        switch (v)
        {
            case null: return "";
            case double d: return d.ToString(CultureInfo.InvariantCulture);
            case bool b: return b ? "true" : "false";
            case List<object> l: return string.Join(";", l.Select(ToText));
            default: return v.ToString();
        }
    }

    static string Csv(string s)
    {
        if (s == null) return "";
        return s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }

    /// <summary>Minimal JSON reader: objects, arrays, strings, numbers (double), bools, null.</summary>
    static class MiniJson
    {
        public static object Parse(string s) { int i = 0; var v = Value(s, ref i); return v; }

        static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

        static object Value(string s, ref int i)
        {
            Ws(s, ref i);
            char c = s[i];
            if (c == '{') return Obj(s, ref i);
            if (c == '[') return Arr(s, ref i);
            if (c == '"') return Str(s, ref i);
            if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (s.Length - i >= 5 && string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
            if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            return double.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture);
        }

        static Dictionary<string, object> Obj(string s, ref int i)
        {
            var d = new Dictionary<string, object>();
            i++; Ws(s, ref i);
            if (s[i] == '}') { i++; return d; }
            while (true)
            {
                Ws(s, ref i);
                string k = Str(s, ref i);
                Ws(s, ref i); i++; // ':'
                d[k] = Value(s, ref i);
                Ws(s, ref i);
                if (s[i] == ',') { i++; continue; }
                i++; return d; // '}'
            }
        }

        static List<object> Arr(string s, ref int i)
        {
            var l = new List<object>();
            i++; Ws(s, ref i);
            if (s[i] == ']') { i++; return l; }
            while (true)
            {
                l.Add(Value(s, ref i));
                Ws(s, ref i);
                if (s[i] == ',') { i++; continue; }
                i++; return l;
            }
        }

        static string Str(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++; // opening quote
            while (s[i] != '"')
            {
                char c = s[i++];
                if (c != '\\') { sb.Append(c); continue; }
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u': sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber)); i += 4; break;
                    default: sb.Append(e); break;
                }
            }
            i++;
            return sb.ToString();
        }
    }
}
