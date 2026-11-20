// Wirtschaft-Mod – Schild-Sicherung
// Merkt sich den Text jedes beschriebenen Schilds in der Nähe des Spielers (Datei Wirtschaft_Schilder.xml im Spielstand).
// Wird ein Schild leer, obwohl es vorher Text hatte, schreibt die Sicherung den letzten Text automatisch zurück.
// Alte Fassungen eines Schilds bleiben als Verlauf erhalten (die letzten 10).
// Zugriff auf die Schilder des Spiels nur über Reflection: so baut der Mod auch dann, wenn sich Namen im Spiel ändern,
// und "wb schilder" zeigt an, welche Spielfunktionen gefunden wurden.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;
using Wirtschaft.Kern;

namespace Wirtschaft.Spiel
{
    public class SchildEintrag
    {
        [XmlAttribute] public int X;
        [XmlAttribute] public int Y;
        [XmlAttribute] public int Z;
        [XmlAttribute] public string Text = "";
        /// <summary>Wann der Text zuletzt so gesehen wurde (Ortszeit).</summary>
        [XmlAttribute] public string Gesehen = "";
        /// <summary>Kein Schild mehr an dieser Stelle (abgebaut oder ohne Daten). Text bleibt aufgehoben, wird aber nicht automatisch zurückgeschrieben.</summary>
        [XmlAttribute] public bool Entfernt;
        [XmlElement("Alt")] public List<string> Alt = new List<string>();
    }

    [XmlRoot("Schilder")]
    public class SchildArchiv
    {
        [XmlAttribute] public bool AutoWiederherstellen = true;
        [XmlElement("Schild")] public List<SchildEintrag> Liste = new List<SchildEintrag>();
    }

    public static class Schilder
    {
        public const string Dateiname = "Wirtschaft_Schilder.xml";
        /// <summary>So viele Chunks (je 16 Blöcke) um den Spieler werden geprüft.</summary>
        public const int Radius = 6;
        const int MaxAlt = 10;

        static SchildArchiv archiv;
        static string datei = "";
        static readonly Dictionary<string, SchildEintrag> index = new Dictionary<string, SchildEintrag>();
        static float naechster;
        static bool geaendert;
        static string api = "";
        static bool fehlerGemeldet;

        static string Key(int x, int y, int z) { return x + "," + y + "," + z; }
        static string Jetzt() { return DateTime.Now.ToString("yyyy-MM-dd HH:mm"); }

        // ---------- Datei ----------

        public static void Laden(string ordner)
        {
            datei = Path.Combine(ordner, Dateiname);
            archiv = null;
            try
            {
                if (File.Exists(datei))
                    using (var s = File.OpenRead(datei))
                        archiv = (SchildArchiv)new XmlSerializer(typeof(SchildArchiv)).Deserialize(s);
            }
            catch (Exception e)
            {
                Log.Error("[Wirtschaft] Schild-Sicherung nicht lesbar: " + e.Message);
                try { File.Copy(datei, datei + ".defekt", true); } catch (Exception) { }
            }
            if (archiv == null) archiv = new SchildArchiv();
            index.Clear();
            foreach (var e in archiv.Liste) index[Key(e.X, e.Y, e.Z)] = e;
            naechster = 0f;
            geaendert = false;
            fehlerGemeldet = false;
            Log.Out("[Wirtschaft] Schild-Sicherung: " + archiv.Liste.Count(w => w.Text.Length > 0) + " Schildtexte gesichert.");
        }

        public static void Speichern()
        {
            if (archiv == null || datei.Length == 0 || !geaendert) return;
            try
            {
                string neu = datei + ".neu";
                using (var s = File.Create(neu))
                    new XmlSerializer(typeof(SchildArchiv)).Serialize(s, archiv);
                if (File.Exists(datei)) File.Copy(datei, datei + ".bak", true);
                File.Copy(neu, datei, true);
                File.Delete(neu);
                geaendert = false;
            }
            catch (Exception e) { Log.Error("[Wirtschaft] Schild-Sicherung speichern fehlgeschlagen: " + e.Message); }
        }

        public static void Beenden()
        {
            Speichern();
            archiv = null;
            index.Clear();
        }

        // ---------- Regelmäßige Prüfung ----------

        /// <summary>Vom Spiel jeden Frame aufgerufen, arbeitet alle 15 Sekunden.</summary>
        public static void Takt()
        {
            if (archiv == null) return;
            float jetzt = UnityEngine.Time.time;
            if (jetzt < naechster) return;
            naechster = jetzt + 15f;
            try { Pruefen(false); }
            catch (Exception e)
            {
                if (!fehlerGemeldet) { fehlerGemeldet = true; Log.Warning("[Wirtschaft] Schild-Sicherung: " + e.Message); }
            }
            Speichern();
        }

        class Ergebnis
        {
            public int Chunks, Gefunden, Neu, Geaendert, Wiederhergestellt, LeerOhneSicherung, NichtLesbar, Fehlgeschlagen, Entfernt;
            public List<string> Meldungen = new List<string>();
        }

        /// <summary>Prüft alle geladenen Schilder um den Spieler. erzwingen = auch ohne Auto und auch für "entfernte" Einträge zurückschreiben.</summary>
        static Ergebnis Pruefen(bool erzwingen)
        {
            var r = new Ergebnis();
            var welt = GameManager.Instance == null ? null : GameManager.Instance.World;
            if (welt == null) return r;
            var spieler = welt.GetPrimaryPlayer();
            if (spieler == null) return r;
            int cx = (int)Math.Floor(spieler.position.x / 16f), cz = (int)Math.Floor(spieler.position.z / 16f);

            for (int dx = -Radius; dx <= Radius; dx++)
                for (int dz = -Radius; dz <= Radius; dz++)
                {
                    int bx = (cx + dx) * 16, bz = (cz + dz) * 16;
                    var chunk = welt.GetChunkFromWorldPos(new Vector3i(bx, 0, bz)) as Chunk;
                    if (chunk == null) continue;
                    r.Chunks++;
                    var hier = new HashSet<string>();
                    var kacheln = Kacheln(chunk);
                    if (kacheln == null) { Api("Kacheln: nicht gefunden"); continue; }
                    bool sauber = true;
                    foreach (var te in kacheln)
                    {
                        if (te == null || te.GetType().Name.IndexOf("Sign", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        Vector3i p;
                        if (!WeltPos(te, out p)) { sauber = false; continue; }
                        string text;
                        if (!LeseText(te, out text)) { r.NichtLesbar++; sauber = false; continue; }
                        r.Gefunden++;
                        string k = Key(p.x, p.y, p.z);
                        hier.Add(k);
                        SchildEintrag e;
                        index.TryGetValue(k, out e);
                        if (text.Length > 0)
                        {
                            if (e == null)
                            {
                                e = new SchildEintrag { X = p.x, Y = p.y, Z = p.z, Text = text, Gesehen = Jetzt() };
                                archiv.Liste.Add(e);
                                index[k] = e;
                                r.Neu++;
                                geaendert = true;
                            }
                            else if (e.Text != text || e.Entfernt)
                            {
                                if (e.Text.Length > 0 && e.Text != text) MerkeAlt(e, e.Text);
                                e.Text = text;
                                e.Entfernt = false;
                                e.Gesehen = Jetzt();
                                r.Geaendert++;
                                geaendert = true;
                            }
                            continue;
                        }
                        // Schild ist leer
                        if (e == null || e.Text.Length == 0) continue;
                        if (e.Entfernt && !erzwingen) continue;   // neues Schild an alter Stelle: nicht ungefragt beschreiben
                        if (!archiv.AutoWiederherstellen && !erzwingen) { r.LeerOhneSicherung++; continue; }
                        if (SchreibeText(te, e.Text))
                        {
                            e.Entfernt = false;
                            e.Gesehen = Jetzt();
                            r.Wiederhergestellt++;
                            geaendert = true;
                            string m = "Schild bei " + k + " war leer, Text wiederhergestellt: " + Kurz(e.Text);
                            r.Meldungen.Add(m);
                            Log.Out("[Wirtschaft] " + m);
                        }
                        else r.Fehlgeschlagen++;
                    }
                    // Gesicherte Schilder in diesem Chunk, die es nicht mehr gibt: als entfernt markieren (Text bleibt aufgehoben).
                    if (!sauber) continue;
                    foreach (var w in archiv.Liste)
                    {
                        if (w.Entfernt || w.X < bx || w.X >= bx + 16 || w.Z < bz || w.Z >= bz + 16) continue;
                        if (hier.Contains(Key(w.X, w.Y, w.Z))) continue;
                        w.Entfernt = true;
                        r.Entfernt++;
                        geaendert = true;
                    }
                }

            if (r.Wiederhergestellt > 0)
            {
                try
                {
                    var sp = welt.GetPrimaryPlayer();
                    if (sp != null) GameManager.ShowTooltip(sp, r.Wiederhergestellt + " leere(s) Schild(er) wiederhergestellt (wb schilder)", false);
                }
                catch (Exception) { }
            }
            return r;
        }

        static void MerkeAlt(SchildEintrag e, string alt)
        {
            if (e.Alt.Count > 0 && e.Alt[e.Alt.Count - 1] == alt) return;
            e.Alt.Add(alt);
            while (e.Alt.Count > MaxAlt) e.Alt.RemoveAt(0);
        }

        static string Kurz(string t)
        {
            t = t.Replace("\r", "").Replace("\n", " / ");
            return t.Length > 60 ? t.Substring(0, 60) + "..." : t;
        }

        // ---------- Zugriff auf die Spielobjekte (Reflection) ----------

        /// <summary>Alle Kachel-Objekte (TileEntities) eines Chunks, oder null, wenn das Spiel sie nicht herausgibt.</summary>
        static List<object> Kacheln(Chunk chunk)
        {
            var res = new List<object>();
            object r = null;
            try
            {
                var m = chunk.GetType().GetMethod("GetTileEntities", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                if (m != null) r = m.Invoke(chunk, null);
            }
            catch (Exception) { }
            if (r == null)
            {
                try
                {
                    var f = chunk.GetType().GetField("tileEntities", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (f != null) r = f.GetValue(chunk);
                }
                catch (Exception) { }
            }
            if (r == null) return null;
            var lf = r.GetType().GetField("list");
            if (lf != null)
            {
                var l = lf.GetValue(r) as IEnumerable;
                if (l != null) { foreach (var o in l) if (o != null) res.Add(o); return res; }
            }
            var d = r as IDictionary;
            if (d != null) { foreach (var o in d.Values) if (o != null) res.Add(o); return res; }
            var en = r as IEnumerable;
            if (en != null)
                foreach (var o in en)
                {
                    if (o == null) continue;
                    var vp = o.GetType().GetProperty("Value");
                    res.Add(vp != null ? vp.GetValue(o, null) : o);
                }
            return res;
        }

        static bool WeltPos(object te, out Vector3i p)
        {
            p = Vector3i.zero;
            try
            {
                var m = te.GetType().GetMethod("ToWorldPos", Type.EmptyTypes);
                if (m != null) { p = (Vector3i)m.Invoke(te, null); return true; }
            }
            catch (Exception) { }
            return false;
        }

        static bool LeseText(object te, out string text)
        {
            text = "";
            var typ = te.GetType();
            foreach (var name in new[] { "GetAuthoredText", "GetText" })
            {
                var m = typ.GetMethod(name, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                if (m == null) continue;
                try { text = AlsText(m.Invoke(te, null)); Api("lesen: " + name + "()"); return true; }
                catch (Exception) { }
            }
            foreach (var f in typ.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (f.Name.IndexOf("text", StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (f.FieldType != typeof(string) && f.FieldType.Name.IndexOf("AuthoredText", StringComparison.Ordinal) < 0) continue;
                try { text = AlsText(f.GetValue(te)); Api("lesen: Feld " + f.Name); return true; }
                catch (Exception) { }
            }
            return false;
        }

        static string AlsText(object r)
        {
            if (r == null) return "";
            var s = r as string;
            if (s != null) return s;
            var typ = r.GetType();
            var f = typ.GetField("Text");
            if (f != null) return (f.GetValue(r) as string) ?? "";
            var p = typ.GetProperty("Text");
            if (p != null) return (p.GetValue(r, null) as string) ?? "";
            return "";
        }

        static object LokalerSpielerId(Assembly a)
        {
            try
            {
                var pm = a.GetType("PlatformManager");
                if (pm == null) return null;
                var p = pm.GetProperty("InternalLocalUserIdentifier", BindingFlags.Public | BindingFlags.Static);
                return p == null ? null : p.GetValue(null, null);
            }
            catch (Exception) { return null; }
        }

        static object Standard(ParameterInfo pi, object spielerId)
        {
            if (pi.ParameterType == typeof(bool)) return true;
            if (spielerId != null && pi.ParameterType.IsInstanceOfType(spielerId)) return spielerId;
            if (pi.IsOptional && !(pi.DefaultValue is DBNull)) return pi.DefaultValue;
            return pi.ParameterType.IsValueType ? Activator.CreateInstance(pi.ParameterType) : null;
        }

        static object MacheAutorText(Type t, string text, object spielerId)
        {
            foreach (var c in t.GetConstructors())
            {
                var ps = c.GetParameters();
                if (ps.Length == 0 || ps[0].ParameterType != typeof(string)) continue;
                var a = new object[ps.Length];
                a[0] = text;
                for (int i = 1; i < ps.Length; i++) a[i] = Standard(ps[i], spielerId);
                try { return c.Invoke(a); } catch (Exception) { }
            }
            try
            {
                var o = Activator.CreateInstance(t);
                var f = t.GetField("Text");
                if (f != null) { f.SetValue(o, text); return o; }
                var p = t.GetProperty("Text");
                if (p != null && p.CanWrite) { p.SetValue(o, text, null); return o; }
            }
            catch (Exception) { }
            return null;
        }

        static bool SchreibeText(object te, string text)
        {
            var typ = te.GetType();
            object spielerId = LokalerSpielerId(typ.Assembly);
            MethodInfo beste = null;
            foreach (var m in typ.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                if (m.Name != "SetText") continue;
                var ps = m.GetParameters();
                if (ps.Length == 0) continue;
                if (ps[0].ParameterType == typeof(string)) { beste = m; break; }
                if (beste == null) beste = m;
            }
            if (beste == null) { Api("schreiben: kein SetText gefunden"); return false; }
            var pars = beste.GetParameters();
            var args = new object[pars.Length];
            args[0] = pars[0].ParameterType == typeof(string) ? (object)text : MacheAutorText(pars[0].ParameterType, text, spielerId);
            if (args[0] == null) return false;
            for (int i = 1; i < pars.Length; i++) args[i] = Standard(pars[i], spielerId);
            try
            {
                beste.Invoke(te, args);
                var sm = typ.GetMethod("SetModified", Type.EmptyTypes);
                if (sm != null) sm.Invoke(te, null);
                Api("schreiben: SetText(" + string.Join(", ", pars.Select(x => x.ParameterType.Name).ToArray()) + ")");
            }
            catch (Exception e)
            {
                Log.Warning("[Wirtschaft] Schild beschreiben fehlgeschlagen: " + (e.InnerException != null ? e.InnerException.Message : e.Message));
                return false;
            }
            string nach;
            return LeseText(te, out nach) && nach == text;
        }

        static void Api(string s)
        {
            if (api.IndexOf(s, StringComparison.Ordinal) >= 0) return;
            api = api.Length == 0 ? s : api + " | " + s;
        }

        // ---------- Befehle: wb schilder ... / wb schild <x> <y> <z> ... ----------

        public static string Befehl(List<string> a)
        {
            if (archiv == null) return "Schild-Sicherung: erst eine Welt laden.";
            string was = a.Count > 1 ? a[1].ToLowerInvariant() : "";
            var s = new StringBuilder();

            if (a[0].ToLowerInvariant() == "schild")
            {
                int x, y, z;
                if (a.Count < 4 || !int.TryParse(a[1], out x) || !int.TryParse(a[2], out y) || !int.TryParse(a[3], out z))
                    return "wb schild <x> <y> <z> [leeren | zurueck]";
                SchildEintrag e;
                if (!index.TryGetValue(Key(x, y, z), out e)) return "Zu " + Key(x, y, z) + " ist kein Schildtext gesichert.";
                string op = a.Count > 4 ? a[4].ToLowerInvariant() : "";
                if (op == "leeren")
                {
                    if (e.Text.Length > 0) MerkeAlt(e, e.Text);
                    e.Text = "";
                    geaendert = true;
                    Speichern();
                    return "Gesicherter Text bei " + Key(x, y, z) + " gelöscht (steht noch im Verlauf). Jetzt das Schild im Spiel leeren, es wird nicht mehr zurückgeschrieben.";
                }
                if (op == "zurueck" || op == "zurück")
                {
                    if (e.Alt.Count == 0) return "Kein älterer Text vorhanden.";
                    string alt = e.Alt[e.Alt.Count - 1];
                    e.Alt.RemoveAt(e.Alt.Count - 1);
                    if (e.Text.Length > 0) e.Alt.Insert(0, e.Text);
                    e.Text = alt;
                    e.Entfernt = false;
                    geaendert = true;
                    var rz = Pruefen(false);
                    Speichern();
                    return "Älterer Text wird verwendet: " + Kurz(alt) + (rz.Wiederhergestellt > 0 ? "  (aufs Schild geschrieben)" : "  (wird geschrieben, sobald das Schild leer und geladen ist)");
                }
                s.AppendLine("Schild " + Key(x, y, z) + (e.Entfernt ? "  (zuletzt kein Schild an dieser Stelle)" : "") + ", gesehen " + e.Gesehen);
                s.AppendLine("Text:");
                s.AppendLine(e.Text.Length == 0 ? "  (leer)" : "  " + e.Text.Replace("\n", "\n  "));
                for (int i = e.Alt.Count - 1; i >= 0; i--) s.AppendLine("Früher: " + Kurz(e.Alt[i]));
                return s.ToString().TrimEnd();
            }

            if (was == "auto")
            {
                if (a.Count > 2) archiv.AutoWiederherstellen = a[2].ToLowerInvariant() == "an" || a[2] == "1" || a[2].ToLowerInvariant() == "ja";
                geaendert = true;
                Speichern();
                return "Automatisch wiederherstellen: " + (archiv.AutoWiederherstellen ? "AN" : "AUS");
            }
            if (was == "wiederherstellen")
            {
                var r = Pruefen(true);
                Speichern();
                return r.Wiederhergestellt + " Schild(er) wiederhergestellt, " + r.Fehlgeschlagen + " fehlgeschlagen. Geprüft: " + r.Gefunden + " Schilder in " + r.Chunks + " Chunks um dich."
                       + (r.Meldungen.Count > 0 ? "\n" + string.Join("\n", r.Meldungen.ToArray()) : "");
            }
            if (was == "liste")
            {
                var welt = GameManager.Instance == null ? null : GameManager.Instance.World;
                var sp = welt == null ? null : welt.GetPrimaryPlayer();
                float px = sp == null ? 0f : sp.position.x, pz = sp == null ? 0f : sp.position.z;
                var l = archiv.Liste.Where(w => w.Text.Length > 0)
                    .OrderBy(w => (w.X - px) * (w.X - px) + (w.Z - pz) * (w.Z - pz)).Take(40).ToList();
                s.AppendLine("Die " + l.Count + " nächsten gesicherten Schilder:");
                foreach (var g in l) s.AppendLine("  " + Key(g.X, g.Y, g.Z) + (g.Entfernt ? " [weg]" : "") + "  " + Kurz(g.Text));
                return s.ToString().TrimEnd();
            }

            var rr = Pruefen(false);
            Speichern();
            s.AppendLine("Schild-Sicherung (" + Dateiname + "): " + archiv.Liste.Count(w => w.Text.Length > 0) + " Texte gesichert, "
                         + archiv.Liste.Count(w => w.Entfernt) + " davon ohne Schild an der Stelle.");
            s.AppendLine("Automatisch wiederherstellen: " + (archiv.AutoWiederherstellen ? "AN" : "AUS"));
            s.AppendLine("Eben geprüft: " + rr.Chunks + " Chunks, " + rr.Gefunden + " Schilder, neu gesichert " + rr.Neu + ", geändert " + rr.Geaendert
                         + ", wiederhergestellt " + rr.Wiederhergestellt + (rr.NichtLesbar > 0 ? ", nicht lesbar " + rr.NichtLesbar : "")
                         + (rr.Fehlgeschlagen > 0 ? ", Schreiben fehlgeschlagen " + rr.Fehlgeschlagen : ""));
            s.AppendLine("Spielzugriff: " + (api.Length == 0 ? "noch kein Schild gelesen" : api));
            s.AppendLine("Befehle: wb schilder liste | wiederherstellen | auto an/aus,  wb schild <x> <y> <z> [leeren | zurueck]");
            return s.ToString().TrimEnd();
        }
    }
}
