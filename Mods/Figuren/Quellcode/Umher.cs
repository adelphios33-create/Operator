// Figuren-Mod – Typ 4, Stufe 2: Figuren laufen in einem Umkreis um ihren Platz umher (Belebung der Stadt).
// Einstellung pro Figur im Einrichten-Fenster ("Bewegung": Steht fest, 10, 25, 50, 100 Blöcke), gespeichert in Platz.Umkreis.
// Ablauf: kurz warten (6-20 s), dann ein Stück (4-20 m) zu einem zufälligen Punkt im Umkreis gehen, wieder warten ...
// Nachts gehen alle zurück an ihren Platz und bleiben dort. Hat eine bewaffnete Figur ein Ziel, bleibt sie stehen und kämpft.
// Spricht der Spieler sie an, bleibt sie 40 s stehen.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Figuren.Spiel
{
    public static class Umher
    {
        public static readonly int[] Stufen = { 0, 10, 25, 50, 100 };

        public static string Text(int umkreis)
        {
            return umkreis <= 0 ? "Steht fest" : "Umher, " + umkreis + " Blöcke";
        }

        class Zustand { public float Warten = 3f; }
        static readonly Dictionary<int, Zustand> zustand = new Dictionary<int, Zustand>();
        static readonly System.Random zufall = new System.Random();

        static Zustand Hole(int id)
        {
            Zustand z;
            if (!zustand.TryGetValue(id, out z)) { z = new Zustand(); z.Warten = 2f + (float)zufall.NextDouble() * 8f; zustand[id] = z; }
            return z;
        }

        /// <summary>Figur bleibt eine Weile stehen (z. B. wenn der Spieler sie anspricht).</summary>
        public static void Pause(EntityFigur f, float sekunden)
        {
            if (f == null) return;
            var z = Hole(f.entityId);
            z.Warten = Mathf.Max(z.Warten, sekunden);
            if (Laufen.Laeuft(f.entityId)) Laufen.Stoppen(f);
        }

        /// <summary>Im Fenster: nächste Stufe einstellen.</summary>
        public static string Setzen(EntityFigur f, Platz p, int umkreis)
        {
            if (p == null) return "Die Figur ist nicht mehr da.";
            p.Umkreis = Mathf.Max(0, umkreis);
            FigurenMod.Speichern();
            if (f != null)
            {
                if (Laufen.Laeuft(f.entityId)) Laufen.Stoppen(f);
                if (p.Umkreis == 0 && (f.position - new Vector3(p.X, p.Y, p.Z)).sqrMagnitude > 2.25f)
                    Laufen.Starten(f, new Vector3(p.X, p.Y, p.Z), 0.25f, true);
                Hole(f.entityId).Warten = 2f;
            }
            return p.Umkreis == 0 ? "Figur bleibt an ihrem Platz." : "Figur läuft tagsüber im Umkreis von " + p.Umkreis + " Blöcken umher.";
        }

        /// <summary>Jeden Frame aus Waffen.Tick.</summary>
        public static void Tick(World w, float dt)
        {
            if (FigurenMod.Stand == null) return;
            bool nacht = false;
            try { nacht = !w.IsDaytime(); } catch (Exception) { }
            foreach (var p in FigurenMod.Stand.Figuren)
            {
                if (p.Umkreis <= 0) continue;
                var f = w.GetEntity(p.Id) as EntityFigur;
                if (f == null || f.IsDead() || Waffen.IstLeiche(p.Id)) continue;
                var z = Hole(p.Id);

                // Kampf geht vor: stehen bleiben, solange ein Ziel da ist
                if (Waffen.HatZiel(p.Id))
                {
                    if (Laufen.Laeuft(p.Id)) Laufen.Stoppen(f);
                    z.Warten = Mathf.Max(z.Warten, 4f);
                    continue;
                }
                if (Laufen.Laeuft(p.Id)) continue;
                z.Warten -= dt;
                if (z.Warten > 0f) continue;

                Vector3 heim = new Vector3(p.X, p.Y, p.Z);
                if (nacht)
                {
                    if ((f.position - heim).sqrMagnitude > 2.25f) Laufen.Starten(f, heim, 0.25f, true);
                    else try { f.SetRotationAndStopTurning(new Vector3(0f, p.Drehung, 0f)); } catch (Exception) { }
                    z.Warten = 15f;
                    continue;
                }

                // Leine: zu weit vom Platz weg (z. B. nach einem abgebrochenen Weg) -> zu Fuß zurück, kein Teleport
                Vector3 weg = f.position - heim; weg.y = 0f;
                if (weg.magnitude > p.Umkreis + 5f)
                {
                    Laufen.Starten(f, heim, 0.25f, true);
                    z.Warten = 4f;
                    continue;
                }

                Vector3 ziel;
                if (Zielpunkt(f.position, heim, p.Umkreis, out ziel))
                    Laufen.Starten(f, ziel, 0.18f + (float)zufall.NextDouble() * 0.1f, true);
                z.Warten = 6f + (float)zufall.NextDouble() * 14f;   // zählt erst nach der Ankunft weiter
            }
        }

        /// <summary>Zufälliger begehbarer Punkt: 4-20 m vom jetzigen Ort, innerhalb des Umkreises, ungefähr auf der Höhe des Platzes.</summary>
        static bool Zielpunkt(Vector3 jetzt, Vector3 heim, int umkreis, out Vector3 ziel)
        {
            ziel = heim;
            for (int i = 0; i < 8; i++)
            {
                float winkel = (float)(zufall.NextDouble() * Math.PI * 2.0);
                float weit = 4f + (float)zufall.NextDouble() * 16f;
                Vector3 v = jetzt + new Vector3(Mathf.Sin(winkel), 0f, Mathf.Cos(winkel)) * weit;
                Vector3 vonHeim = v - heim; vonHeim.y = 0f;
                if (vonHeim.magnitude > umkreis)
                {
                    // zu weit draußen: Richtung Platz zurück
                    float r = (float)zufall.NextDouble() * Mathf.Min(umkreis, 10f);
                    v = heim + new Vector3(Mathf.Sin(winkel), 0f, Mathf.Cos(winkel)) * r;
                }
                // von Kopfhöhe nach unten: findet den Boden unter der Decke, nicht das Dach (Figuren/Fahrzeuge zählen nicht als Boden)
                float y; Vector3 normale;
                if (!BodenUnter(new Vector3(v.x, heim.y + 1.5f, v.z), 6f, out y, out normale)) continue;
                if (Mathf.Abs(y - heim.y) > 1.6f) continue;   // ungefähr gleiche Höhe: nicht auf Haufen, Dächer, Bäume, nicht in den Keller
                if (normale.y < 0.75f) continue;              // nur flacher Boden, keine Schrägen/Äste
                if (!Begehbar(new Vector3(v.x, y, v.z))) continue;
                ziel = new Vector3(v.x, y + 0.1f, v.z);
                return true;
            }
            return false;
        }

        /// <summary>Fester Block unter den Füßen, Platz für Füße und Kopf (2 Blöcke frei), kein Baum/Busch darunter.</summary>
        static bool Begehbar(Vector3 p)
        {
            try
            {
                var w = GameManager.Instance.World;
                int x = Mathf.FloorToInt(p.x), z = Mathf.FloorToInt(p.z), y = Mathf.FloorToInt(p.y + 0.05f);
                var unten = w.GetBlock(new Vector3i(x, y - 1, z));
                if (unten.isair || unten.Block == null || !unten.Block.IsCollideMovement) return false;
                string n = unten.Block.GetBlockName().ToLowerInvariant();
                if (n.Contains("tree") || n.Contains("plant") || n.Contains("bush") || n.Contains("rubble") || n.Contains("pile")) return false;
                for (int dy = 0; dy <= 1; dy++)
                {
                    var b = w.GetBlock(new Vector3i(x, y + dy, z));
                    if (!b.isair && b.Block != null && b.Block.IsCollideMovement) return false;
                }
                return true;
            }
            catch (Exception) { return false; }
        }

        public static void Vergessen(int id) { zustand.Remove(id); }

        /// <summary>Boden unter einem Punkt (Weltkoordinaten): nächster Treffer nach unten, der nicht zu einem Lebewesen
        /// oder Fahrzeug gehört (eigene Trefferzonen, andere Figuren, Spieler, Zombies, Fahrzeuge werden übersprungen).</summary>
        public static bool BodenUnter(Vector3 start, float weite, out float y)
        {
            Vector3 n;
            return BodenUnter(start, weite, out y, out n);
        }

        public static bool BodenUnter(Vector3 start, float weite, out float y, out Vector3 normale)
        {
            y = 0f; normale = Vector3.up;
            try
            {
                var treffer = Physics.RaycastAll(start - Origin.position, Vector3.down, weite, ~0, QueryTriggerInteraction.Ignore);
                float best = float.MaxValue;
                bool gefunden = false;
                foreach (var h in treffer)
                {
                    if (h.collider == null || h.collider is CharacterController) continue;
                    if (h.collider.GetComponentInParent<Entity>() != null) continue;
                    if (h.distance < best) { best = h.distance; y = h.point.y + Origin.position.y; normale = h.normal; gefunden = true; }
                }
                return gefunden;
            }
            catch (Exception) { return false; }
        }
    }
}
