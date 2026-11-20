// Figuren-Mod – Sprachausgabe für feste Dialoge (Piper, offline)
// Ablauf: Beim Laden der Rollen schreibt die Mod alle Sätze, für die noch keine Audiodatei existiert, in
//   Stimmen/maenner.jsonl und Stimmen/frauen.jsonl. STIMMEN.bat lässt Piper daraus WAV-Dateien erzeugen
//   (Stimmen/<kennung>_m.wav bzw. _f.wav). Im Spiel spielt die Figur die passende Datei als 3D-Ton ab.
// Sätze mit {Name} oder {Stadt} bekommen keine Stimme (der Text ist erst im Spiel bekannt).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Figuren.Kern;
using UnityEngine;

namespace Figuren.Spiel
{
    public static class Stimmen
    {
        public static string Ordner = "";
        static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
        static AudioSource letzte;

        /// <summary>Stabile Kennung eines Satzes (FNV-1a über UTF-8).</summary>
        public static string Kennung(string text)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (byte b in Encoding.UTF8.GetBytes((text ?? "").Trim())) { h ^= b; h *= 16777619; }
                return h.ToString("x8");
            }
        }

        static string Datei(string text, bool frau) { return Path.Combine(Ordner, Kennung(text) + (frau ? "_f" : "_m") + ".wav"); }

        static string Json(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s)
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c < ' ') sb.Append(' ');
                else sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>Liste der fehlenden Sätze für Piper schreiben. Liefert die Anzahl.</summary>
        public static int ListeSchreiben()
        {
            if (string.IsNullOrEmpty(Ordner)) return 0;
            try
            {
                Directory.CreateDirectory(Ordner);
                var m = new List<string>();
                var f = new List<string>();
                var gesehen = new HashSet<string>();
                foreach (var r in Rollen.Liste)
                {
                    if (r.IstKi) continue;
                    bool frauMoeglich = r.AussehenListe.Any(Rollen.IstFrau) || Rollen.IstFrau(r.Aussehen);
                    foreach (var a in r.Abschnitte.Values)
                    {
                        foreach (var roh in (a.Text ?? "").Split(new[] { "||" }, StringSplitOptions.None))
                        {
                            string t = roh.Trim();
                            if (t.Length == 0 || t.Contains("{")) continue;
                            if (!File.Exists(Datei(t, false)) && gesehen.Add("m" + t))
                                m.Add("{\"text\": \"" + Json(t) + "\", \"output_file\": \"" + Json(Datei(t, false)) + "\"}");
                            if (frauMoeglich && !File.Exists(Datei(t, true)) && gesehen.Add("f" + t))
                                f.Add("{\"text\": \"" + Json(t) + "\", \"output_file\": \"" + Json(Datei(t, true)) + "\"}");
                        }
                    }
                }
                var utf8 = new UTF8Encoding(false);
                File.WriteAllLines(Path.Combine(Ordner, "maenner.jsonl"), m.ToArray(), utf8);
                File.WriteAllLines(Path.Combine(Ordner, "frauen.jsonl"), f.ToArray(), utf8);
                if (m.Count + f.Count > 0) Log.Out("[Figuren] Stimmen: " + (m.Count + f.Count) + " Sätze ohne Audio - STIMMEN.bat im Mod-Ordner ausführen.");
                return m.Count + f.Count;
            }
            catch (Exception e) { Log.Warning("[Figuren] Stimmen-Liste: " + e.Message); return 0; }
        }

        /// <summary>WAV (PCM 16 Bit) laden, wie Piper sie schreibt.</summary>
        static AudioClip Laden(string pfad)
        {
            AudioClip c;
            if (cache.TryGetValue(pfad, out c)) return c;
            c = null;
            try
            {
                if (File.Exists(pfad))
                {
                    var d = File.ReadAllBytes(pfad);
                    int kanaele = BitConverter.ToInt16(d, 22);
                    int rate = BitConverter.ToInt32(d, 24);
                    int bits = BitConverter.ToInt16(d, 34);
                    int p = 12, start = -1, laenge = 0;
                    while (p + 8 <= d.Length)
                    {
                        string id = Encoding.ASCII.GetString(d, p, 4);
                        int sz = BitConverter.ToInt32(d, p + 4);
                        if (id == "data") { start = p + 8; laenge = Math.Min(sz, d.Length - start); break; }
                        p += 8 + sz + (sz & 1);
                    }
                    if (start > 0 && bits == 16 && kanaele > 0)
                    {
                        int n = laenge / 2;
                        var werte = new float[n];
                        for (int i = 0; i < n; i++) werte[i] = BitConverter.ToInt16(d, start + i * 2) / 32768f;
                        c = AudioClip.Create(Path.GetFileNameWithoutExtension(pfad), n / kanaele, kanaele, rate, false);
                        c.SetData(werte, 0);
                    }
                }
            }
            catch (Exception e) { Log.Warning("[Figuren] Stimme laden: " + e.Message); }
            if (c != null) cache[pfad] = c;   // fehlende Dateien nicht merken: STIMMEN.bat kann sie später erzeugen
            return c;
        }

        /// <summary>Satz von der Figur sprechen lassen (falls eine Audiodatei da ist).</summary>
        public static void Sprechen(Entity figur, string text)
        {
            try
            {
                if (figur == null || string.IsNullOrEmpty(text) || string.IsNullOrEmpty(Ordner)) return;
                var p = FigurenMod.PlatzVon(figur as EntityFigur);
                bool frau = p != null && Rollen.IstFrau(p.Aussehen);
                var clip = Laden(Datei(text, frau));
                if (clip == null && frau) clip = Laden(Datei(text, false));
                if (clip == null) return;
                if (letzte != null) letzte.Stop();
                var q = figur.gameObject.GetComponent<AudioSource>();
                if (q == null)
                {
                    q = figur.gameObject.AddComponent<AudioSource>();
                    q.spatialBlend = 1f;
                    q.minDistance = 2f;
                    q.maxDistance = 25f;
                    q.rolloffMode = AudioRolloffMode.Linear;
                }
                q.volume = 1f;
                // jede Figur klingt etwas anders: Tonhöhe 0,88-1,12, fest nach Rolle und Platz
                q.pitch = 1f;
                if (p != null)
                {
                    uint h = Convert.ToUInt32(Kennung(p.Rolle + "|" + (int)p.X + "|" + (int)p.Z), 16);
                    q.pitch = 0.88f + (h % 25) / 100f;
                }
                q.clip = clip;
                q.Play();
                letzte = q;
            }
            catch (Exception e) { Log.Warning("[Figuren] Sprechen: " + e.Message); }
        }

        public static void Stoppen()
        {
            try { if (letzte != null) letzte.Stop(); } catch (Exception) { }
        }
    }
}
