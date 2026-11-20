// Figuren-Mod – Typ 4, Stufe 1: Lauf-Test (Konsole "npc lauf").
// Die Figur geht zu einem Punkt. Drei Wege, der Reihe nach, bis einer funktioniert:
//   1. "Weg":    Wegfindung des Spiels (EntityAlive.FindPath), wie bei Zombies.
//   2. "Direkt": Bewegungshilfe des Spiels (EntityMoveHelper.SetMoveTo), gerade Linie mit Ausweichen/Springen.
//   3. "Schub":  selbst zum Ziel drehen und vorwärts gehen (SetMoveForward).
// Kommt die Figur 1,5 s lang nicht voran, wird der nächste Weg versucht. Das Ergebnis steht im Log
// ("[Figuren] Laufen ...") und in "npc lauf status". Die Beinbewegung macht FigurKoerper (Gang),
// falls das Modell selbst keine Gehanimation zeigt.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Figuren.Spiel
{
    public static class Laufen
    {
        class Auftrag
        {
            public Vector3 Ziel;
            public float Tempo;
            public int Modus;          // 1 Weg, 2 Direkt, 3 Schub
            public float Zeit, StillZeit, Gesamt;
            public Vector3 Letzte;
            public float Weg;          // zurückgelegte Strecke
            public string Ergebnis = "";
            public bool Leise;
            public float StartY;       // Klettern verhindern: nie höher als Start/Ziel + 1,4 m
            public float Vor = -1f;    // zuletzt gesetztes Vorwärts-Tempo (Weg 3) - nur bei Änderung neu setzen
            public float Takt;         // Weg 2: Ziel nur alle 0,5 s neu an die Bewegungshilfe geben
        }

        static readonly Dictionary<int, Auftrag> auftraege = new Dictionary<int, Auftrag>();
        static readonly Dictionary<int, string> letztesErgebnis = new Dictionary<int, string>();
        static readonly string[] modusName = { "", "Weg (Wegfindung des Spiels)", "Direkt (Bewegungshilfe)", "Schub (selbst gesteuert)" };

        public static bool Laeuft(int id) { return auftraege.ContainsKey(id); }

        public static void Vergessen(int id) { auftraege.Remove(id); letztesErgebnis.Remove(id); }

        public static string Starten(EntityFigur f, Vector3 ziel, float tempo) { return Starten(f, ziel, tempo, false); }

        public static string Starten(EntityFigur f, Vector3 ziel, float tempo, bool leise)
        {
            if (f == null) return "Keine Figur.";
            var a = new Auftrag { Ziel = ziel, Tempo = tempo, Letzte = f.position, Leise = leise, StartY = f.position.y };
            auftraege[f.entityId] = a;
            ModusStarten(f, a, 1);
            return "Figur #" + f.entityId + " läuft los (" + Mathf.RoundToInt(Vector3.Distance(f.position, ziel)) + " m, Tempo " + tempo + "). Ergebnis: npc lauf status";
        }

        public static string Stoppen(EntityFigur f)
        {
            if (f == null) return "Keine Figur.";
            Anhalten(f);
            auftraege.Remove(f.entityId);
            return "Figur #" + f.entityId + " angehalten.";
        }

        public static string Status(EntityFigur f)
        {
            if (f == null) return "Keine Figur.";
            Auftrag a;
            if (auftraege.TryGetValue(f.entityId, out a))
                return "Läuft: " + modusName[a.Modus] + ", noch " + Vector3.Distance(Flach(f.position), Flach(a.Ziel)).ToString("0.0") + " m, bisher " + a.Weg.ToString("0.0") + " m"
                       + "\n" + FigurKoerper.Fuer(f).GangBericht();
            string e;
            return (letztesErgebnis.TryGetValue(f.entityId, out e) ? e : "Kein Lauf-Auftrag.") + "\n" + FigurKoerper.Fuer(f).GangBericht();
        }

        static Vector3 Flach(Vector3 v) { v.y = 0f; return v; }

        static void ModusStarten(EntityFigur f, Auftrag a, int modus)
        {
            Anhalten(f);
            a.Modus = modus;
            a.Zeit = 0f;
            a.StillZeit = 0f;
            a.Letzte = f.position;
            a.Vor = -1f;
            a.Takt = 0f;
            try
            {
                if (modus == 1)
                {
                    f.FindPath(a.Ziel, a.Tempo, false, null);
                }
                else if (modus == 2)
                {
                    if (f.moveHelper == null) f.moveHelper = new EntityMoveHelper(f);
                    f.moveHelper.SetMoveTo(a.Ziel, false);
                }
            }
            catch (Exception e)
            {
                Log.Warning("[Figuren] Laufen, " + modusName[modus] + ": " + e.Message);
                a.StillZeit = 99f;   // sofort nächster Modus
            }
            if (!a.Leise) Log.Out("[Figuren] Laufen #" + f.entityId + ": versuche " + modusName[modus]);
        }

        static void Anhalten(EntityFigur f)
        {
            try { if (f.navigator != null) f.navigator.clearPath(); } catch (Exception) { }
            try { if (f.moveHelper != null) f.moveHelper.Stop(); } catch (Exception) { }
            try { f.SetMoveForward(0f); } catch (Exception) { }
        }

        /// <summary>Jeden Frame aus Waffen.Tick.</summary>
        public static void Tick(World w, float dt)
        {
            if (auftraege.Count == 0) return;
            var fertig = new List<int>();
            foreach (var kv in auftraege)
            {
                var f = w.GetEntity(kv.Key) as EntityFigur;
                var a = kv.Value;
                if (f == null || f.IsDead() || Waffen.IstLeiche(kv.Key)) { fertig.Add(kv.Key); continue; }
                a.Zeit += dt;
                a.Gesamt += dt;

                float schritt = Vector3.Distance(Flach(f.position), Flach(a.Letzte));
                a.Weg += schritt;
                a.Letzte = f.position;
                if (schritt / Mathf.Max(dt, 0.001f) < 0.15f) a.StillZeit += dt; else a.StillZeit = 0f;

                if (f.position.y > Mathf.Max(a.StartY, a.Ziel.y) + 1.4f)
                {
                    Anhalten(f);
                    letztesErgebnis[kv.Key] = "Abgebrochen: Figur wollte klettern/springen (zu hoch).";
                    if (!a.Leise) Log.Out("[Figuren] Laufen #" + kv.Key + ": " + letztesErgebnis[kv.Key]);
                    fertig.Add(kv.Key);
                    continue;
                }

                float rest = Vector3.Distance(Flach(f.position), Flach(a.Ziel));
                if (rest < 1.0f)
                {
                    Anhalten(f);
                    letztesErgebnis[kv.Key] = "Angekommen mit " + modusName[a.Modus] + " nach " + a.Gesamt.ToString("0.0") + " s.";
                    if (!a.Leise) Log.Out("[Figuren] Laufen #" + kv.Key + ": " + letztesErgebnis[kv.Key]);
                    fertig.Add(kv.Key);
                    continue;
                }

                try
                {
                    if (a.Modus == 2 && f.moveHelper != null)
                    {
                        // Die Bewegungshilfe rechnet das Spiel selbst (20x pro Sekunde). Hier nur ab und zu das Ziel auffrischen -
                        // früher wurde sie zusätzlich jeden Frame aufgerufen (doppelte Bewegung, abhängig von den FPS).
                        a.Takt -= dt;
                        if (a.Takt <= 0f) { a.Takt = 0.5f; f.moveHelper.SetMoveTo(a.Ziel, false); }
                    }
                    else if (a.Modus == 3)
                    {
                        float soll = FigurenMod.DrehungZu(f.position, a.Ziel);
                        float jetzt = Mathf.MoveTowardsAngle(f.rotation.y, soll, 180f * dt);
                        if (Mathf.Abs(Mathf.DeltaAngle(f.rotation.y, jetzt)) > 0.01f) f.SetRotationAndStopTurning(new Vector3(0f, jetzt, 0f));
                        // SetMoveForward setzt den Schwung jedes Mal auf null - deshalb nur bei Änderung aufrufen
                        float vor = Mathf.Abs(Mathf.DeltaAngle(jetzt, soll)) < 30f ? a.Tempo : 0f;
                        if (vor != a.Vor) { f.SetMoveForward(vor); a.Vor = vor; }
                    }
                }
                catch (Exception e) { Log.Warning("[Figuren] Laufen: " + e.Message); a.StillZeit = 99f; }

                if (a.StillZeit > 1.5f || a.Gesamt > 120f)
                {
                    // Umherlaufen (leise): ohne Modus 2 - die Bewegungshilfe des Spiels lässt Figuren springen und klettern
                    if (a.Modus < 3 && a.Gesamt <= 120f) ModusStarten(f, a, a.Leise && a.Modus == 1 ? 3 : a.Modus + 1);
                    else
                    {
                        Anhalten(f);
                        letztesErgebnis[kv.Key] = "Abgebrochen: keiner der drei Wege bewegt die Figur (zuletzt " + modusName[a.Modus] + ", " + a.Weg.ToString("0.0") + " m geschafft).";
                        if (!a.Leise) Log.Out("[Figuren] Laufen #" + kv.Key + ": " + letztesErgebnis[kv.Key]);
                        fertig.Add(kv.Key);
                    }
                }
            }
            foreach (var id in fertig) auftraege.Remove(id);
        }

        /// <summary>Punkt, auf den der Spieler schaut (bis 40 m), sonst 8 m vor ihm.</summary>
        public static Vector3 Blickpunkt(EntityPlayerLocal sp)
        {
            Vector3 start = sp.getHeadPosition();
            Vector3 dir = sp.GetLookVector();
            RaycastHit hit;
            if (Physics.Raycast(start - Origin.position, dir, out hit, 40f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point + Origin.position + Vector3.up * 0.1f;
            return sp.position + Flach(dir).normalized * 8f;
        }
    }
}
