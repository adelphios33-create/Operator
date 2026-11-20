// Figuren-Mod – Aussehen-Tests (Analyse der Modelle, Farbe, Größe, Teile ein/aus) und Schießtest für Typ 3
// Alles hier ist ein TEST: Änderungen an Farbe/Größe/Teilen gelten nur bis zum Neuladen der Welt.
// Unity: Renderer, SkinnedMeshRenderer (bones, rootBone, sharedMesh), Material (HasProperty, SetColor, shader).
// 7DTD 1.4: Entity.emodel.GetModelTransform(), Audio.Manager.BroadcastPlay, DamageSourceEntity, EntityAlive.DamageEntity.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Figuren.Kern;
using UnityEngine;

namespace Figuren.Spiel
{
    public static class Optik
    {
        static readonly string[] FarbFelder = { "_Color", "_BaseColor", "_TintColor", "_AlbedoColor", "_MainColor" };

        static Transform Modell(Entity e)
        {
            Transform t = null;
            try { if (e.emodel != null) t = e.emodel.GetModelTransform(); } catch (Exception) { }
            return t != null ? t : e.transform;
        }

        static string Pfad(Transform t, Transform wurzel)
        {
            var teile = new List<string>();
            while (t != null && t != wurzel) { teile.Insert(0, t.name); t = t.parent; }
            return string.Join("/", teile.ToArray());
        }

        // ---------- Analyse ----------

        /// <summary>Schreibt Aufbau, Einzelteile, Materialien und Skelett eines Modells als Text.</summary>
        public static string Analyse(Entity e, string titel)
        {
            var sb = new StringBuilder();
            var wurzel = e.transform;
            var modell = Modell(e);
            sb.AppendLine("==================== " + titel + " ====================");
            sb.AppendLine("Modell-Wurzel: " + Pfad(modell, wurzel) + "  Größe " + modell.localScale);

            sb.AppendLine("--- Einzelteile (Renderer) ---");
            var rends = wurzel.GetComponentsInChildren<Renderer>(true);
            foreach (var r in rends)
            {
                string mesh = "-"; int ecken = 0;
                var smr = r as SkinnedMeshRenderer;
                var mf = r.GetComponent<MeshFilter>();
                if (smr != null && smr.sharedMesh != null) { mesh = smr.sharedMesh.name; ecken = smr.sharedMesh.vertexCount; }
                else if (mf != null && mf.sharedMesh != null) { mesh = mf.sharedMesh.name; ecken = mf.sharedMesh.vertexCount; }
                sb.AppendLine("* " + r.GetType().Name + "  " + Pfad(r.transform, wurzel) + (r.enabled ? "" : "  (aus)") + "  Mesh " + mesh + " (" + ecken + " Ecken)");
                if (smr != null)
                    sb.AppendLine("    Skelett: Wurzel " + (smr.rootBone != null ? smr.rootBone.name : "-") + ", " + (smr.bones != null ? smr.bones.Length : 0) + " Knochen");
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    var felder = FarbFelder.Where(f => m.HasProperty(f)).ToArray();
                    sb.AppendLine("    Material " + m.name + "  Shader " + (m.shader != null ? m.shader.name : "-")
                        + "  Textur " + (m.HasProperty("_MainTex") && m.mainTexture != null ? m.mainTexture.name : "-")
                        + "  Farbfelder " + (felder.Length > 0 ? string.Join(",", felder) : "keine"));
                }
            }

            sb.AppendLine("--- Halte-Punkte (Waffe/Hand/Kopf) ---");
            foreach (var t in wurzel.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (n.Contains("gun") || n.Contains("weapon") || n.Contains("hand") || n.Contains("head") || n.Contains("hat") || n.Contains("hair") || n.Contains("glass") || n.Contains("item"))
                    sb.AppendLine("  " + Pfad(t, wurzel));
            }

            // Skelett-Kennung: gleiche Knochennamen = Teile könnten zwischen den Modellen tauschbar sein
            var knochen = new SortedSet<string>();
            foreach (var smr in wurzel.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr.bones != null) foreach (var b in smr.bones) if (b != null) knochen.Add(b.name);
            sb.AppendLine("--- Skelett: " + knochen.Count + " Knochen, Kennung " + Kennung(knochen) + " ---");
            sb.AppendLine(string.Join(", ", knochen.ToArray()));
            sb.AppendLine();
            return sb.ToString();
        }

        static string Kennung(IEnumerable<string> namen)
        {
            unchecked
            {
                int h = 17;
                foreach (var n in namen) foreach (char c in n) h = h * 31 + c;
                return h.ToString("X8");
            }
        }

        /// <summary>Stellt jedes Modell kurz neben dem Spieler auf, analysiert es und entfernt es wieder. Ergebnis in Figuren_Teile.txt.</summary>
        public static string AlleAnalysieren(EntityPlayerLocal spieler, int halten = 0)
        {
            var w = GameManager.Instance.World;
            var ids = new List<KeyValuePair<string, int>>();
            float yaw = spieler.rotation.y * Mathf.Deg2Rad;
            var basis = spieler.position + new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw)) * 3f;
            int i = 0;
            var fehler = new List<string>();
            foreach (var a in Rollen.Aussehen)
            {
                try
                {
                    int klasse = EntityClass.FromString("figur" + a);
                    if (klasse < 0) { Log.Warning("[Figuren] Kein Modell figur" + a); continue; }
                    // Raster: 7 Figuren pro Reihe, Reihen nach hinten versetzt, alle schauen zum Spieler
                    var rechts = new Vector3(Mathf.Cos(yaw), 0f, -Mathf.Sin(yaw));
                    var vor = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
                    var pos = basis + rechts * ((i % 7) - 3) * 1.3f + vor * (i / 7) * 1.6f;
                    var e = EntityFactory.CreateEntity(klasse, pos, new Vector3(0f, FigurenMod.DrehungZu(pos, spieler.position), 0f));
                    i++;
                    if (e == null) continue;
                    w.SpawnEntityInWorld(e);
                    ids.Add(new KeyValuePair<string, int>(a, e.entityId));
                }
                catch (Exception ex) { fehler.Add(a + ": " + ex); Log.Warning("[Figuren] Modell " + a + " nicht geladen: " + ex); FigurenMod.GeisterEntfernen(); }
            }
            string datei = Path.Combine(GameIO.GetSaveGameDir(), "Figuren_Teile.txt");
            // ein paar Frames warten, bis die Modelle geladen sind
            Warten(10, () =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("Figuren – Analyse der Händler-Modelle, " + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
                sb.AppendLine();
                if (fehler.Count > 0)
                {
                    sb.AppendLine("==================== NICHT LADBAR (" + fehler.Count + ") ====================");
                    foreach (var f in fehler) sb.AppendLine(f).AppendLine();
                }
                foreach (var kv in ids)
                {
                    var e = w.GetEntity(kv.Value);
                    if (e == null) { sb.AppendLine("== " + kv.Key + ": nicht geladen =="); continue; }
                    try { sb.Append(Analyse(e, kv.Key)); }
                    catch (Exception ex) { sb.AppendLine("== " + kv.Key + ": Fehler " + ex.Message + " =="); }
                }
                // nach der Wartezeit alle Test-Figuren wieder entfernen
                Warten(halten, () =>
                {
                    foreach (var kv in ids) w.RemoveEntity(kv.Value, EnumRemoveEntityReason.Despawned);
                });
                File.WriteAllText(datei, sb.ToString(), Encoding.UTF8);
                Log.Out("[Figuren] Analyse gespeichert: " + datei);
            });
            return "Analysiere " + ids.Count + " Modelle (sie erscheinen vor dir" + (halten > 0 ? ", bleiben ca. " + (halten / 60) + " Sekunden" : "") + "). Ergebnis gleich in: " + datei;
        }

        static void Warten(int frames, Action a)
        {
            if (frames <= 0) { a(); return; }
            FigurenMod.Spaeter(() => Warten(frames - 1, a));
        }

        // ---------- Farbe / Größe / Teile ----------

        static readonly Dictionary<string, Color> Farben = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
        {
            { "rot", new Color(0.8f, 0.1f, 0.1f) }, { "grün", new Color(0.1f, 0.7f, 0.2f) }, { "gruen", new Color(0.1f, 0.7f, 0.2f) },
            { "blau", new Color(0.15f, 0.3f, 0.85f) }, { "gelb", new Color(0.95f, 0.85f, 0.15f) }, { "orange", new Color(1f, 0.5f, 0.1f) },
            { "lila", new Color(0.55f, 0.2f, 0.75f) }, { "pink", new Color(1f, 0.4f, 0.7f) }, { "schwarz", new Color(0.08f, 0.08f, 0.08f) },
            { "weiß", Color.white }, { "weiss", Color.white }, { "grau", new Color(0.5f, 0.5f, 0.5f) }, { "braun", new Color(0.45f, 0.28f, 0.12f) },
            { "blond", new Color(0.95f, 0.85f, 0.55f) }, { "normal", Color.white }
        };

        public static bool FarbeLesen(string s, out Color c)
        {
            if (Farben.TryGetValue(s, out c)) return true;
            return ColorUtility.TryParseHtmlString(s.StartsWith("#") ? s : "#" + s, out c);
        }

        static bool Passt(Renderer r, string teil)
        {
            if (teil.Equals("alle", StringComparison.OrdinalIgnoreCase)) return true;
            if (r.name.IndexOf(teil, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return r.sharedMaterials.Any(m => m != null && m.name.IndexOf(teil, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public static string Faerben(Entity e, string teil, Color c)
        {
            int n = 0; var felder = new HashSet<string>();
            foreach (var r in e.transform.GetComponentsInChildren<Renderer>(true))
            {
                if (!Passt(r, teil)) continue;
                foreach (var m in r.materials)
                    foreach (var f in FarbFelder)
                        if (m.HasProperty(f)) { m.SetColor(f, c); felder.Add(f); n++; }
            }
            return n == 0 ? "Kein Teil '" + teil + "' mit Farbfeld gefunden (Namen stehen in Figuren_Teile.txt)." : "Gefärbt: " + n + " Material(ien), Felder " + string.Join(",", felder.ToArray()) + ".";
        }

        public static string Schalten(Entity e, string teil, bool an)
        {
            int n = 0;
            foreach (var r in e.transform.GetComponentsInChildren<Renderer>(true))
                if (Passt(r, teil)) { r.enabled = an; n++; }
            return n == 0 ? "Kein Teil '" + teil + "' gefunden." : n + " Teil(e) " + (an ? "eingeblendet." : "ausgeblendet.");
        }

        public static string Groesse(Entity e, float faktor)
        {
            faktor = Mathf.Clamp(faktor, 0.7f, 1.4f);
            var t = Modell(e);
            t.localScale = Vector3.one * faktor;
            return "Größe " + faktor.ToString("0.00", CultureInfo.InvariantCulture) + " (Modell " + t.name + ").";
        }

        // ---------- Schießtest (Typ 3) ----------

        /// <summary>Nächster lebender Gegner (Zombie/Tier mit Feindstatus) im Umkreis.</summary>
        public static EntityAlive NaechsterGegner(Vector3 pos, float meter)
        {
            EntityAlive beste = null; float bd = meter * meter;
            foreach (var x in GameManager.Instance.World.Entities.list)
            {
                var a = x as EntityAlive;
                if (a == null || a is EntityFigur || a is EntityPlayer || a.IsDead()) continue;
                if (!(a is EntityEnemy)) continue;
                float d = (a.position - pos).sqrMagnitude;
                if (d < bd) { bd = d; beste = a; }
            }
            return beste;
        }

        public static string Schuss(EntityFigur f, EntityAlive ziel, int schaden, string ton)
        {
            if (ziel == null) return "Kein Ziel.";
            float yaw = FigurenMod.DrehungZu(f.position, ziel.position);
            try { f.SetRotationAndStopTurning(new Vector3(0f, yaw, 0f)); } catch (Exception) { f.rotation = new Vector3(0f, yaw, 0f); }
            try { Audio.Manager.BroadcastPlay(f.position, ton); } catch (Exception ex) { Log.Warning("[Figuren] Ton: " + ex.Message); }
            int vorher = ziel.Health;
            ziel.DamageEntity(new DamageSourceEntity(EnumDamageSource.External, EnumDamageTypes.Piercing, f.entityId), schaden, false, 1f);
            return "Schuss auf " + ziel.EntityName + " (" + Mathf.RoundToInt(Vector3.Distance(f.position, ziel.position)) + " m): Leben " + vorher + " -> " + ziel.Health + (ziel.IsDead() ? ", tot." : ".");
        }

        // einfache Wache zum Testen: schießt alle 1,5 s auf den nächsten Gegner bis 25 m
        static readonly Dictionary<int, float> wachen = new Dictionary<int, float>();

        public static string Wache(EntityFigur f, bool an)
        {
            if (an) wachen[f.entityId] = 0f; else wachen.Remove(f.entityId);
            return "Wache " + (an ? "an" : "aus") + " für Figur #" + f.entityId + (an ? " (schießt auf Zombies bis 25 m, gilt bis Neuladen)." : ".");
        }

        /// <summary>Jeden Frame aus FigurenMod aufrufen.</summary>
        public static void Tick(float dt)
        {
            if (wachen.Count == 0) return;
            var w = GameManager.Instance == null ? null : GameManager.Instance.World;
            if (w == null) return;
            foreach (var id in wachen.Keys.ToList())
            {
                var f = w.GetEntity(id) as EntityFigur;
                if (f == null) { wachen.Remove(id); continue; }
                float t = wachen[id] - dt;
                if (t <= 0f)
                {
                    var ziel = NaechsterGegner(f.position, 25f);
                    if (ziel != null) { Schuss(f, ziel, 35, "ak47_fire"); t = 1.5f; }
                    else t = 0.5f;
                }
                wachen[id] = t;
            }
        }
    }
}
