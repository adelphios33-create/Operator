// Bauplan-Mod – Gebäude im laufenden Spiel kopieren, als Prefab speichern und wieder einfügen.
// Geprüft gegen die Assembly-CSharp.dll von 7 Days to Die V 1.4 (b8):
//   Prefab: .ctor(), copyFromWorld(World, Vector3i, Vector3i), Save(AbstractedLocation, bool), LocationForNewPrefab(string, string),
//           Load(AbstractedLocation, bool, bool, bool, bool), Clone(bool), RotateY(bool, int), CopyIntoRPC(GameManager, Vector3i, bool),
//           Felder size, yOffset, location.
//   PathAbstractions.PrefabsSearchPaths.GetAvailablePathsList(Regex, string, string, bool), PathAbstractions.InvalidateCaches().
//   SelectionBoxManager.Instance.GetCategory(string).AddBox/RemoveBox, SelectionBox.SetPositionAndSize/SetCaption/SetAllFacesColor.
//   Voxel.Raycast(World, Ray, float, int, float) + Voxel.voxelRayHitInfo, EntityAlive.GetLookRay().
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Bauplan
{
    public enum Modus { Markieren, Platzieren }

    public static class Plan
    {
        // ---------- Grenzen ----------
        /// <summary>Größte Kante einer Markierung in Blöcken.</summary>
        public const int MaxKante = 256;
        /// <summary>So weit reicht das Werkzeug (Blöcke).</summary>
        public const float Reichweite = 100f;

        // ---------- Zustand ----------
        public static Modus Modus = Modus.Markieren;
        public static bool HatEcke1, HatEcke2;
        public static Vector3i Ecke1, Ecke2;

        public static Prefab Gewaehlt;          // so wie geladen
        public static Prefab Platz;             // gedreht, wird eingefügt
        public static string GewaehltName = "";
        /// <summary>Datei des gewählten Gebäudes ohne Endung (für das Vorschaubild).</summary>
        public static string GewaehltPfad = "";

        /// <summary>Vorschaubild: .jpg neben dem Gebäude (Spiel-Gebäude haben eins, eigene Kopien meist nicht).</summary>
        public static string GewaehltBildPfad()
        {
            if (string.IsNullOrEmpty(GewaehltPfad)) return "";
            foreach (var endung in new[] { ".jpg", ".png" })
                if (System.IO.File.Exists(GewaehltPfad + endung)) return GewaehltPfad + endung;
            return "";
        }
        public static bool GewaehltEigen;
        public static int Drehung;              // 0..3 (×90° im Uhrzeigersinn)
        public static bool MitLuft = true;      // Luftblöcke einfügen = Fläche wird frei geräumt
        public static bool MitKeller = true;
        public static bool OhneZombies = true;  // Schläfer-Zonen (Zombies) des Gebäudes beim Einfügen entfernen    // yOffset der Spiel-Prefabs anwenden (Keller im Boden)

        public static bool HatZiel;
        public static Vector3i Ziel;            // linke untere Ecke beim Einfügen
        static Vector3i bestaetigtZiel;
        static float bestaetigtBis;

        static Prefab rueck;                    // Zustand vor dem letzten Einfügen
        static Vector3i rueckPos;
        public static bool KannRueckgaengig { get { return rueck != null; } }

        public static string Meldung = "";

        // ---------- Hilfen ----------

        static World Welt { get { return GameManager.Instance == null ? null : GameManager.Instance.World; } }

        public static string Pos(Vector3i p) { return p.x + " " + p.y + " " + p.z; }

        public static void Melden(EntityPlayerLocal sp, string text)
        {
            Meldung = text;
            try { if (sp != null) GameManager.ShowTooltip(sp, text, false); } catch (Exception) { }
            if (XUiC_Bauplan.Instanz != null) XUiC_Bauplan.Instanz.Neu();
        }

        public static void Min(out Vector3i min, out Vector3i max)
        {
            min = new Vector3i(Math.Min(Ecke1.x, Ecke2.x), Math.Min(Ecke1.y, Ecke2.y), Math.Min(Ecke1.z, Ecke2.z));
            max = new Vector3i(Math.Max(Ecke1.x, Ecke2.x), Math.Max(Ecke1.y, Ecke2.y), Math.Max(Ecke1.z, Ecke2.z));
        }

        public static Vector3i Groesse()
        {
            Vector3i a, b;
            Min(out a, out b);
            return new Vector3i(b.x - a.x + 1, b.y - a.y + 1, b.z - a.z + 1);
        }

        public static string MarkierungText()
        {
            if (!HatEcke1 && !HatEcke2) return "Noch nichts markiert. Werkzeug: Linksklick = Ecke 1, Rechtsklick = Ecke 2.";
            string s = "Ecke 1: " + (HatEcke1 ? Pos(Ecke1) : "-") + "    Ecke 2: " + (HatEcke2 ? Pos(Ecke2) : "-");
            if (HatEcke1 && HatEcke2)
            {
                var g = Groesse();
                s += "    Größe " + g.x + " × " + g.z + " Grundfläche, " + g.y + " hoch (" + ((long)g.x * g.y * g.z).ToString("N0") + " Blöcke)";
            }
            return s;
        }

        /// <summary>Block, auf den der Spieler schaut. danach = true: der freie Block davor (zum Daraufsetzen).</summary>
        public static bool Blick(EntityPlayerLocal sp, out Vector3i block, bool davor)
        {
            block = Vector3i.zero;
            var w = Welt;
            if (w == null || sp == null) return false;
            try
            {
                if (!Voxel.Raycast(w, sp.GetLookRay(), Reichweite, Voxel.HM_Melee, 0f)) return false;
                var h = Voxel.voxelRayHitInfo;
                if (h == null || !h.bHitValid) return false;
                block = davor ? h.lastBlockPos : h.hit.blockPos;
                return true;
            }
            catch (Exception) { return false; }
        }

        static bool Geladen(Vector3i a, Vector3i b)
        {
            var w = Welt;
            if (w == null) return false;
            for (int x = (int)Math.Floor(a.x / 16f) * 16; x <= b.x; x += 16)
                for (int z = (int)Math.Floor(a.z / 16f) * 16; z <= b.z; z += 16)
                    if (w.GetChunkFromWorldPos(x, 0, z) == null) return false;
            return true;
        }

        // ---------- Markieren ----------

        public static void SetzeEcke(EntityPlayerLocal sp, int nr)
        {
            Vector3i p;
            if (!Blick(sp, out p, false)) { Melden(sp, "Kein Block im Blick (bis " + (int)Reichweite + " Blöcke)."); return; }
            if (nr == 1) { Ecke1 = p; HatEcke1 = true; } else { Ecke2 = p; HatEcke2 = true; }
            Vorschau.Markierung();
            if (HatEcke1 && HatEcke2)
            {
                var g = Groesse();
                Melden(sp, "Ecke " + nr + ": " + Pos(p) + "   Markiert: " + g.x + " × " + g.z + ", " + g.y + " hoch. Rechtsklick in die Luft öffnet das Fenster.");
            }
            else Melden(sp, "Ecke " + nr + " gesetzt: " + Pos(p) + ". Jetzt die " + (nr == 1 ? "zweite (Rechtsklick)" : "erste (Linksklick)") + " Ecke.");
        }

        public static void Erweitern(int unten, int oben)
        {
            if (!HatEcke1 || !HatEcke2) return;
            Vector3i a, b;
            Min(out a, out b);
            Ecke1 = new Vector3i(a.x, Math.Max(0, a.y - unten), a.z);
            Ecke2 = new Vector3i(b.x, Math.Min(254, b.y + oben), b.z);
            Vorschau.Markierung();
        }

        // ---------- Kopieren ----------

        public static string SauberName(string n)
        {
            n = (n ?? "").Trim();
            var sb = new StringBuilder();
            foreach (char c in n)
            {
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-') sb.Append(c);
                else if (c == ' ') sb.Append('_');
            }
            string s = sb.ToString().Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("Ä", "Ae").Replace("Ö", "Oe").Replace("Ü", "Ue").Replace("ß", "ss");
            return s.Length > 48 ? s.Substring(0, 48) : s;
        }

        /// <summary>Kopiert die Markierung und speichert sie als eigenes Prefab. Gibt die Meldung zurück.</summary>
        public static string Kopieren(string name, bool ueberschreiben)
        {
            var w = Welt;
            if (w == null) return "Keine Welt geladen.";
            if (!HatEcke1 || !HatEcke2) return "Erst zwei Ecken markieren (Linksklick und Rechtsklick mit dem Werkzeug).";
            string n = SauberName(name);
            if (n.Length == 0) return "Bitte einen Namen eingeben (Buchstaben, Ziffern, _ und -).";
            var g = Groesse();
            if (g.x > MaxKante || g.y > MaxKante || g.z > MaxKante) return "Zu groß: jede Kante höchstens " + MaxKante + " Blöcke.";
            Vector3i a, b;
            Min(out a, out b);
            if (!Geladen(a, b)) return "Ein Teil der Fläche ist nicht geladen. Geh näher heran und versuch es noch einmal.";
            try
            {
                var ort = Prefab.LocationForNewPrefab(n, null);
                if (ort.Exists() && !ueberschreiben) return "Den Namen '" + n + "' gibt es schon. Anderen Namen wählen oder nochmal drücken zum Überschreiben.";
                var p = new Prefab();
                p.copyFromWorld(w, a, b);
                p.bCopyAirBlocks = true;
                if (!p.Save(ort, true)) return "Speichern fehlgeschlagen (" + ort.FullPath + ").";
                PathAbstractions.InvalidateCaches();
                Liste.Neu();
                InZwischenablage(p);
                Log.Out("[Bauplan] Gespeichert: " + n + " (" + g.x + "x" + g.y + "x" + g.z + ") nach " + ort.FullPath);
                return "Gespeichert als '" + n + "' (" + g.x + " × " + g.z + ", " + g.y + " hoch). Es steht unter 'Eigene' und liegt in der Zwischenablage (Strg+V).";
            }
            catch (Exception e)
            {
                Log.Error("[Bauplan] Kopieren fehlgeschlagen: " + e);
                return "Kopieren fehlgeschlagen: " + e.Message;
            }
        }

        // ---------- Auswählen und Einfügen ----------

        public static string Waehlen(Eintrag e)
        {
            if (e == null) return "Nichts ausgewählt.";
            try
            {
                var p = new Prefab();
                if (e.Laden == null || !e.Laden(p)) return "Konnte '" + e.Name + "' nicht laden.";
                Gewaehlt = p;
                GewaehltName = e.Name;
                GewaehltEigen = e.Eigen;
                GewaehltPfad = e.Pfad ?? "";
                Drehung = 0;
                NeuDrehen();
                return "Ausgewählt: " + e.Name + " (" + p.size.x + " × " + p.size.z + ", " + p.size.y + " hoch).";
            }
            catch (Exception ex)
            {
                Log.Error("[Bauplan] Laden fehlgeschlagen: " + ex);
                return "Laden fehlgeschlagen: " + ex.Message;
            }
        }

        static void NeuDrehen()
        {
            if (Gewaehlt == null) { Platz = null; return; }
            Platz = Gewaehlt.Clone(false);
            if (Drehung > 0) Platz.RotateY(false, Drehung);
        }

        public static void Drehen()
        {
            Drehung = (Drehung + 1) % 4;
            NeuDrehen();
            if (Modus == Modus.Platzieren) InZwischenablage(Platz);
            bestaetigtBis = 0f;
            Vorschau.Ziel();
        }

        /// <summary>Legt ein Prefab in die Zwischenablage des Editors: danach fügt Strg+V es an der Rahmen-Position ein (Strg+Z = rückgängig).</summary>
        public static bool InZwischenablage(Prefab p)
        {
            var t = BlockToolSelection.Instance;
            if (t == null || p == null) return false;
            try { t.LoadPrefabIntoClipboard(p.Clone(false)); return true; }
            catch (Exception e) { Editor.Fehler("Zwischenablage", e); return false; }
        }

        public static string Platzieren(EntityPlayerLocal sp)
        {
            if (Platz == null) return "Erst unten ein Gebäude auswählen.";
            InZwischenablage(Platz);
            Modus = Modus.Platzieren;
            HatZiel = false;
            bestaetigtBis = 0f;
            return "Platzieren: auf den Boden zielen, der Rahmen folgt. Linksklick zweimal oder Strg+V = einfügen, Rechtsklick = drehen, Rechtsklick in die Luft = Fenster.";
        }

        public static void Abbrechen()
        {
            Modus = Modus.Markieren;
            HatZiel = false;
            Vorschau.Weg();
            Vorschau.Markierung();
        }

        /// <summary>Jeden Frame beim Halten des Werkzeugs im Platzieren-Modus: Ziel unter dem Blick berechnen.</summary>
        public static void ZielAktualisieren(EntityPlayerLocal sp)
        {
            if (Modus != Modus.Platzieren || Platz == null) return;
            Vector3i boden;
            if (!Blick(sp, out boden, false)) { HatZiel = false; Vorschau.Weg(); return; }
            var s = Platz.size;
            int y = boden.y + 1 + (MitKeller && !GewaehltEigen ? Platz.yOffset : 0);
            var z = new Vector3i(boden.x - s.x / 2, Math.Max(1, y), boden.z - s.z / 2);
            if (!HatZiel || z.x != Ziel.x || z.y != Ziel.y || z.z != Ziel.z)
            {
                Ziel = z;
                HatZiel = true;
                Vorschau.Ziel();
            }
        }

        /// <summary>Linksklick im Platzieren-Modus: erst bestätigen lassen, beim zweiten Klick einfügen.</summary>
        public static void EinfuegenKlick(EntityPlayerLocal sp)
        {
            ZielAktualisieren(sp);
            if (Platz == null || !HatZiel) { Melden(sp, "Kein Ziel im Blick."); return; }
            float jetzt = Time.time;
            if (jetzt > bestaetigtBis || bestaetigtZiel.x != Ziel.x || bestaetigtZiel.y != Ziel.y || bestaetigtZiel.z != Ziel.z)
            {
                bestaetigtZiel = Ziel;
                bestaetigtBis = jetzt + 5f;
                Melden(sp, "Hier einfügen? " + GewaehltName + " bei " + Pos(Ziel) + ". Zum Bestätigen innerhalb von 5 Sekunden nochmal Linksklick.");
                return;
            }
            bestaetigtBis = 0f;
            Melden(sp, Einfuegen(Ziel));
        }

        public static string Einfuegen(Vector3i ziel)
        {
            var w = Welt;
            if (w == null || Platz == null) return "Nichts zum Einfügen ausgewählt.";
            var s = Platz.size;
            var ende = new Vector3i(ziel.x + s.x - 1, ziel.y + s.y - 1, ziel.z + s.z - 1);
            if (ende.y > 254) return "Zu hoch: das Gebäude ragt über die Welt hinaus.";
            if (!Geladen(ziel, ende)) return "Ein Teil der Zielfläche ist nicht geladen. Geh näher heran.";
            try
            {
                var alt = new Prefab();
                alt.copyFromWorld(w, ziel, ende);
                rueck = alt;
                rueckPos = ziel;
                int zombies = 0;
                if (OhneZombies)
                {
                    // Schläfer-Zonen und ihre Auslöser entfernen: im eingefügten Gebäude wachen keine Zombies auf
                    zombies = Platz.SleeperVolumes.Count;
                    Platz.SleeperVolumes.Clear();
                    Platz.TriggerVolumes.Clear();
                }
                int fueller = FuellerErsetzen(Platz, alt);
                Platz.CopyIntoRPC(GameManager.Instance, ziel, MitLuft);
                letztesEinfuegen = ziel; letzteGroesse = Platz.size; hatLetztes = true;
                if (fueller > 0) Log.Out("[Bauplan] " + fueller + " terrainFiller-Blöcke durch Boden ersetzt.");
                if (zombies > 0) Log.Out("[Bauplan] " + zombies + " Zombie-Zonen von " + GewaehltName + " entfernt.");
                Log.Out("[Bauplan] Eingefügt: " + GewaehltName + " bei " + Pos(ziel) + ", Drehung " + Drehung * 90 + "°");
                return GewaehltName + " eingefügt bei " + Pos(ziel) + ". Rückgängig im Fenster oder mit 'bau rueckgaengig'.";
            }
            catch (Exception e)
            {
                Log.Error("[Bauplan] Einfügen fehlgeschlagen: " + e);
                return "Einfügen fehlgeschlagen: " + e.Message;
            }
        }

        // ---------- Platzhalter "terrainFiller" ----------
        // Spiel-Gebäude enthalten "terrainFiller" = "hier gehört Gelände hin". Beim Erzeugen der Welt ersetzt das Spiel
        // ihn durch Boden; beim Einfügen muss das der Bauplan tun, sonst hat die Erde darüber keinen Halt und rieselt nach.

        static Vector3i letztesEinfuegen, letzteGroesse;
        static bool hatLetztes;

        static bool IstFueller(BlockValue b)
        {
            if (b.isair || b.Block == null) return false;
            string n = b.Block.GetBlockName() ?? "";
            return n.StartsWith("terrainFiller", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Ersetzt im Prefab alle terrainFiller durch den Block, der vorher in der Welt war; war dort nichts, durch Erde.</summary>
        static int FuellerErsetzen(Prefab p, Prefab welt)
        {
            int n = 0;
            BlockValue erde = Block.GetBlockValue("terrDirt");
            var s = p.size;
            for (int x = 0; x < s.x; x++)
                for (int y = 0; y < s.y; y++)
                    for (int z = 0; z < s.z; z++)
                    {
                        if (!IstFueller(p.GetBlock(x, y, z))) continue;
                        BlockValue alt = welt != null ? welt.GetBlock(x, y, z) : BlockValue.Air;
                        if (alt.isair || IstFueller(alt)) { p.SetBlock(x, y, z, erde); p.SetDensity(x, y, z, MarchingCubes.DensityTerrain); }
                        else { p.SetBlock(x, y, z, alt); p.SetDensity(x, y, z, welt.GetDensity(x, y, z)); }
                        n++;
                    }
            return n;
        }

        /// <summary>Repariert schon eingefügte Gebäude: terrainFiller im letzten Einfügebereich (oder in der Markierung) werden zu Erde.</summary>
        public static string BodenReparieren()
        {
            var w = Welt;
            if (w == null) return "Keine Welt.";
            Vector3i a, b;
            if (HatEcke1 && HatEcke2) Min(out a, out b);
            else if (hatLetztes) { a = letztesEinfuegen; b = new Vector3i(a.x + letzteGroesse.x - 1, a.y + letzteGroesse.y - 1, a.z + letzteGroesse.z - 1); }
            else return "Erst das Gebäude mit dem Werkzeug markieren (Linksklick / Rechtsklick auf zwei Ecken), dann nochmal.";
            long vol = (long)(b.x - a.x + 1) * (b.y - a.y + 1) * (b.z - a.z + 1);
            if (vol > MaxLeeren) return "Bereich zu groß (" + vol + " Blöcke).";
            if (!Geladen(a, b)) return "Ein Teil des Bereichs ist nicht geladen. Geh näher heran.";
            BlockValue erde = Block.GetBlockValue("terrDirt");
            var liste = new List<BlockChangeInfo>();
            int n = 0;
            for (int x = a.x; x <= b.x; x++)
                for (int y = a.y; y <= b.y; y++)
                    for (int z = a.z; z <= b.z; z++)
                    {
                        var q = new Vector3i(x, y, z);
                        if (!IstFueller(w.GetBlock(q))) continue;
                        liste.Add(new BlockChangeInfo(0, q, erde, MarchingCubes.DensityTerrain));
                        n++;
                        if (liste.Count >= 20000) { w.SetBlocksRPC(liste); liste = new List<BlockChangeInfo>(); }
                    }
            if (liste.Count > 0) w.SetBlocksRPC(liste);
            Log.Out("[Bauplan] Boden repariert: " + n + " terrainFiller in " + Pos(a) + " bis " + Pos(b));
            return n == 0 ? "Keine Platzhalter gefunden, der Boden ist in Ordnung." : n + " Platzhalter durch Erde ersetzt. Das Nachrutschen sollte aufhören.";
        }

        static float fuellenBis;

        /// <summary>Füllt in der Markierung alle Löcher (Luft und Platzhalter) bis zur Höhe der oberen Ecke mit Erde. Zwei Klicks, Rückgängig geht.</summary>
        public static string BodenAuffuellen()
        {
            var w = Welt;
            if (w == null) return "Keine Welt geladen.";
            if (!HatEcke1 || !HatEcke2) return "Erst die Grube markieren: eine Ecke unten in der Grube, die gegenüberliegende Ecke oben auf Straßenhöhe.";
            Vector3i a, b;
            Min(out a, out b);
            if (a.y < 1) a = new Vector3i(a.x, 1, a.z);
            if (b.y > 254) b = new Vector3i(b.x, 254, b.z);
            long volumen = (long)(b.x - a.x + 1) * (b.y - a.y + 1) * (b.z - a.z + 1);
            if (volumen > MaxLeeren) return "Zu groß: höchstens " + MaxLeeren.ToString("N0") + " Blöcke auf einmal (markiert: " + volumen.ToString("N0") + "). Bitte in Teilen auffüllen.";
            if (!Geladen(a, b)) return "Ein Teil der Fläche ist nicht geladen. Geh näher heran und versuch es noch einmal.";
            float jetzt = Time.time;
            if (jetzt > fuellenBis)
            {
                fuellenBis = jetzt + 5f;
                return "Alle Löcher im Rahmen bis Höhe " + b.y + " mit Erde füllen? Zum Bestätigen innerhalb von 5 Sekunden nochmal drücken. Danach geht 'Rückgängig'.";
            }
            fuellenBis = 0f;
            try
            {
                var alt = new Prefab();
                alt.copyFromWorld(w, a, b);
                alt.bCopyAirBlocks = true;
                rueck = alt;
                rueckPos = a;

                BlockValue erde = Block.GetBlockValue("terrDirt");
                var liste = new List<BlockChangeInfo>();
                int n = 0;
                for (int x = a.x; x <= b.x; x++)
                    for (int z = a.z; z <= b.z; z++)
                        for (int y = a.y; y <= b.y; y++)
                        {
                            var q = new Vector3i(x, y, z);
                            BlockValue bv = w.GetBlock(q);
                            if (!bv.isair && !IstFueller(bv)) continue;
                            liste.Add(new BlockChangeInfo(0, q, erde, MarchingCubes.DensityTerrain));
                            n++;
                            if (liste.Count >= 20000) { w.SetBlocksRPC(liste); liste = new List<BlockChangeInfo>(); }
                        }
                if (liste.Count > 0) w.SetBlocksRPC(liste);
                Log.Out("[Bauplan] Boden aufgefüllt: " + n + " Blöcke in " + Pos(a) + " bis " + Pos(b));
                if (n == 0) { rueck = null; return "Keine Löcher gefunden."; }
                return n.ToString("N0") + " Blöcke Erde aufgefüllt bis Höhe " + b.y + ". 'Rückgängig' nimmt es zurück.";
            }
            catch (Exception e)
            {
                Log.Error("[Bauplan] Auffüllen fehlgeschlagen: " + e);
                return "Auffüllen fehlgeschlagen: " + e.Message;
            }
        }

        // ---------- Fläche leeren (Gebäude entfernen) ----------

        /// <summary>Höchstens so viele Blöcke (Volumen) auf einmal leeren.</summary>
        public const long MaxLeeren = 4000000;
        static float leerenBis;

        static long Schluessel(int x, int y, int z) { return ((long)(x + 100000) * 300 + y) * 300000 + (z + 100000); }

        /// <summary>Entfernt alles in der Markierung. mitBoden = false: Erde/Stein (Gelände) bleibt. Zwei Klicks, danach Rückgängig möglich.</summary>
        public static string FlaecheLeeren(bool mitBoden)
        {
            var w = Welt;
            if (w == null) return "Keine Welt geladen.";
            if (!HatEcke1 || !HatEcke2) return "Erst zwei Ecken markieren (Linksklick und Rechtsklick mit dem Werkzeug).";
            Vector3i a, b;
            Min(out a, out b);
            if (a.y < 1) a = new Vector3i(a.x, 1, a.z);
            if (b.y > 254) b = new Vector3i(b.x, 254, b.z);
            long volumen = (long)(b.x - a.x + 1) * (b.y - a.y + 1) * (b.z - a.z + 1);
            if (volumen > MaxLeeren) return "Zu groß zum Leeren: höchstens " + MaxLeeren.ToString("N0") + " Blöcke auf einmal (markiert: " + volumen.ToString("N0") + "). Bitte in Teilen leeren.";
            if (!Geladen(a, b)) return "Ein Teil der Fläche ist nicht geladen. Geh näher heran und versuch es noch einmal.";
            float jetzt = Time.time;
            if (jetzt > leerenBis)
            {
                leerenBis = jetzt + 5f;
                return (mitBoden ? "ALLES im Rahmen entfernen (auch Erde und Stein)?" : "Gebäude im Rahmen entfernen (Erde und Stein bleiben)?") + " Zum Bestätigen innerhalb von 5 Sekunden nochmal drücken. Danach geht 'Rückgängig'.";
            }
            leerenBis = 0f;
            try
            {
                var alt = new Prefab();
                alt.copyFromWorld(w, a, b);
                alt.bCopyAirBlocks = true;
                rueck = alt;
                rueckPos = a;

                var liste = new List<BlockChangeInfo>();
                var erledigt = new HashSet<long>();
                int anzahl = 0;
                for (int x = a.x; x <= b.x; x++)
                    for (int y = a.y; y <= b.y; y++)
                        for (int z = a.z; z <= b.z; z++)
                        {
                            var p = new Vector3i(x, y, z);
                            BlockValue bv = w.GetBlock(p);
                            if (bv.isair) continue;
                            Block blk = bv.Block;
                            if (!mitBoden && blk != null && blk.shape != null && blk.shape.IsTerrain()) continue;
                            Vector3i q = p;
                            if (bv.ischild && blk != null && blk.multiBlockPos != null)
                            {
                                try { q = blk.multiBlockPos.GetParentPos(p, bv); } catch (Exception) { q = p; }
                            }
                            if (!erledigt.Add(Schluessel(q.x, q.y, q.z))) continue;
                            liste.Add(new BlockChangeInfo(0, q, BlockValue.Air, MarchingCubes.DensityAir));
                            anzahl++;
                            if (liste.Count >= 20000) { w.SetBlocksRPC(liste); liste = new List<BlockChangeInfo>(); }
                        }
                if (liste.Count > 0) w.SetBlocksRPC(liste);
                Log.Out("[Bauplan] Fläche geleert" + (mitBoden ? " (mit Boden)" : "") + ": " + Pos(a) + " bis " + Pos(b) + ", " + anzahl + " Blöcke.");
                if (anzahl == 0) { rueck = null; return mitBoden ? "Im Rahmen ist nichts." : "Im Rahmen ist nichts außer Gelände."; }
                return anzahl.ToString("N0") + " Blöcke entfernt. 'Rückgängig' holt alles zurück (Kisteninhalte nicht sicher).";
            }
            catch (Exception e)
            {
                Log.Error("[Bauplan] Fläche leeren fehlgeschlagen: " + e);
                return "Leeren fehlgeschlagen: " + e.Message;
            }
        }

        public static string Rueckgaengig()
        {
            if (rueck == null) return "Es gibt nichts rückgängig zu machen.";
            try
            {
                rueck.CopyIntoRPC(GameManager.Instance, rueckPos, true);
                rueck = null;
                return "Rückgängig gemacht (letztes Einfügen bzw. Leeren).";
            }
            catch (Exception e) { return "Rückgängig fehlgeschlagen: " + e.Message; }
        }

        public static string Loeschen(Eintrag e)
        {
            if (e == null || !e.Eigen) return "Nur eigene Kopien lassen sich löschen.";
            try
            {
                string pfad = e.Pfad;
                int n = 0;
                foreach (var endung in new[] { ".tts", ".xml", ".ins", ".mesh", ".jpg" })
                    if (File.Exists(pfad + endung)) { File.Delete(pfad + endung); n++; }
                PathAbstractions.InvalidateCaches();
                Liste.Neu();
                if (GewaehltName == e.Name && GewaehltEigen) { Gewaehlt = null; Platz = null; GewaehltName = ""; }
                return n > 0 ? "'" + e.Name + "' gelöscht." : "Keine Dateien zu '" + e.Name + "' gefunden.";
            }
            catch (Exception ex) { return "Löschen fehlgeschlagen: " + ex.Message; }
        }
    }

    // ---------- Liste der Gebäude ----------

    public class Eintrag
    {
        public string Name;
        public bool Eigen;
        public string Pfad = "";
        public Func<Prefab, bool> Laden;   // lädt dieses Gebäude in ein Prefab (Ort-Typ wird nicht direkt benannt)
    }

    public static class Liste
    {
        static List<Eintrag> alle;

        public static void Neu() { alle = null; }

        public static List<Eintrag> Alle()
        {
            if (alle != null) return alle;
            alle = new List<Eintrag>();
            try
            {
                var orte = PathAbstractions.PrefabsSearchPaths.GetAvailablePathsList((Regex)null, null, null, true);
                foreach (var o in orte)
                {
                    var ort = o;
                    string art = ort.Type.ToString();
                    bool eigen = art == "UserDataPath" || art == "LocalSave" || art == "HostSave";
                    alle.Add(new Eintrag { Name = ort.Name, Eigen = eigen, Pfad = ort.FullPathNoExtension, Laden = pf => pf.Load(ort, true, true, true, false) });
                }
                alle = alle.OrderBy(e => e.Eigen ? 0 : 1).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
            }
            catch (Exception e) { Log.Error("[Bauplan] Prefab-Liste nicht lesbar: " + e.Message); }
            return alle;
        }

        /// <summary>filter: 0 alle, 1 eigene, 2 Spiel.</summary>
        public static List<Eintrag> Gefiltert(int filter, string suche)
        {
            string q = (suche ?? "").Trim().ToLowerInvariant();
            return Alle().Where(e => (filter == 0 || (filter == 1) == e.Eigen) && (q.Length == 0 || e.Name.ToLowerInvariant().Contains(q))).ToList();
        }
    }

    // ---------- Rahmen in der Welt: der originale Auswahlrahmen des Prefab-Editors ----------
    // BlockToolSelection.Instance: SelectionStart/SelectionEnd/SelectionActive (V 1.4).
    // So zeigt das Spiel denselben Rahmen wie im Editor, und Strg+C / Strg+V arbeiten genau mit dieser Fläche.

    public static class Vorschau
    {
        static void Zeigen(Vector3i pos, Vector3i groesse)
        {
            var t = BlockToolSelection.Instance;
            if (t == null) return;
            try
            {
                t.SelectionStart = pos;
                t.SelectionEnd = new Vector3i(pos.x + groesse.x - 1, pos.y + groesse.y - 1, pos.z + groesse.z - 1);
                if (!t.SelectionActive) t.SelectionActive = true;
            }
            catch (Exception e) { Editor.Fehler("Rahmen", e); }
        }

        public static void Weg()
        {
            var t = BlockToolSelection.Instance;
            if (t == null) return;
            try { if (t.SelectionActive) t.SelectionActive = false; } catch (Exception) { }
        }

        public static void Markierung()
        {
            if (Plan.Modus != Modus.Markieren) return;
            if (!Plan.HatEcke1 && !Plan.HatEcke2) { Weg(); return; }
            Vector3i a, b;
            if (Plan.HatEcke1 && Plan.HatEcke2) Plan.Min(out a, out b);
            else { a = Plan.HatEcke1 ? Plan.Ecke1 : Plan.Ecke2; b = a; }
            Zeigen(a, new Vector3i(b.x - a.x + 1, b.y - a.y + 1, b.z - a.z + 1));
        }

        public static void Ziel()
        {
            if (Plan.Modus != Modus.Platzieren || Plan.Platz == null || !Plan.HatZiel) return;
            Zeigen(Plan.Ziel, Plan.Platz.size);
        }
    }
}
