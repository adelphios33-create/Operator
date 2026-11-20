// Figuren-Mod – Typ 3: bewaffnete Figuren (Waffe in der Hand, schießen auf Zombies, auf Wunsch auf Spieler)
// Waffe und Haltung stehen pro Figur im Spielstand (Platz.Waffe, Platz.Haltung).
// Spiel-Teile, die per Reflection angesprochen werden (Signaturen ändern sich zwischen Versionen):
//   Inventory.SetItem(int, ItemValue, int, bool) bzw. SetItem(int, ItemStack), Inventory.SetHoldingItemIdx(int), ForceHoldingItemUpdate().
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Figuren.Spiel
{
    public static class Waffen
    {
        public class Art
        {
            public string Name, Item, Ton;
            public float Reichweite, Pause, Treffer;
            public int Schaden, Kugeln, HoldType;
            /// <summary>Lange Waffe (beide Hände am Schaft) oder Pistole (eine Hand, die zweite stützt).</summary>
            public bool Zweihand { get { return Name != "Pistole"; } }
            /// <summary>Mündungsfeuer aus dem Spiel (items.xml, Particles_muzzle_fire).</summary>
            public string Feuer
            {
                get
                {
                    switch (Name)
                    {
                        case "Pistole": return "gunfire_pistol";
                        case "MP": return "gunfire_SMG";
                        case "AK": return "gunfire_AK47";
                        case "Schrotflinte": return "gunfire_shotgun";
                        default: return "";
                    }
                }
            }
            public Art(string name, string item, string ton, float reichweite, float pause, int schaden, int kugeln, float treffer, int holdType)
            { Name = name; Item = item; Ton = ton; Reichweite = reichweite; Pause = pause; Schaden = schaden; Kugeln = kugeln; Treffer = treffer; HoldType = holdType; }
        }

        /// <summary>Waffen (Items aus dem Spiel, items.xml). Schaden pro Kugel, Pause zwischen Schüssen in Sekunden.</summary>
        public static readonly Art[] Arten =
        {
            new Art("keine", "", "", 0f, 0f, 0, 0, 0f, 0),
            new Art("Pistole", "gunHandgunT1Pistol", "Pistol_Fire", 20f, 0.6f, 30, 1, 0.75f, 1),
            new Art("MP", "gunHandgunT3SMG5", "smg_fire", 22f, 0.15f, 14, 1, 0.6f, 6),
            new Art("AK", "gunMGT1AK47", "ak47_fire", 30f, 0.2f, 28, 1, 0.7f, 48),
            new Art("Schrotflinte", "gunShotgunT2PumpShotgun", "pump_shotgun_fire", 12f, 1.1f, 12, 8, 0.85f, 5)
        };

        /// <summary>Wache = verteidigt sich gegen Zombies (alle bewaffneten Figuren). Feindlich = schießt auch auf Spieler (nur Söldner).</summary>
        public static readonly string[] Haltungen = { "Wache", "Feindlich" };

        /// <summary>Nur die feste Figur Söldner kann feindlich werden.</summary>
        public static bool IstSoeldner(Platz p)
        {
            return p != null && (string.Equals(p.Vorlage, "Söldner", StringComparison.OrdinalIgnoreCase) || string.Equals(p.Vorlage, "Soeldner", StringComparison.OrdinalIgnoreCase));
        }

        public static bool IstWachmann(Platz p)
        {
            return p != null && string.Equals(p.Vorlage, "Wachmann", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Nur Wachmänner und Söldner kämpfen gegeneinander (und können sich gegenseitig töten).</summary>
        public static bool KampfGruppe(Platz p) { return IstSoeldner(p) || IstWachmann(p); }

        /// <summary>Gegner unter Figuren: feindliche Söldner gegen Wachmänner und freundliche Söldner.</summary>
        public static bool Feinde(Platz a, Platz b)
        {
            if (!KampfGruppe(a) || !KampfGruppe(b)) return false;
            return (Wirksam(a) == "Feindlich") != (Wirksam(b) == "Feindlich");
        }

        /// <summary>Wirksame Haltung: Feindlich nur bei Söldnern, sonst immer Wache.</summary>
        public static string Wirksam(Platz p)
        {
            return IstSoeldner(p) && string.Equals(p.Haltung, "Feindlich", StringComparison.OrdinalIgnoreCase) ? "Feindlich" : "Wache";
        }

        public static Art ArtVon(string name)
        {
            return Arten.FirstOrDefault(a => string.Equals(a.Name, name ?? "", StringComparison.OrdinalIgnoreCase)) ?? Arten[0];
        }

        public static string HaltungVon(string name)
        {
            return Haltungen.FirstOrDefault(h => string.Equals(h, name ?? "", StringComparison.OrdinalIgnoreCase)) ?? "Wache";
        }

        // Laufzeit-Zustand pro Figur (nicht gespeichert)
        static readonly Dictionary<int, string> ausgeruestet = new Dictionary<int, string>();
        static readonly Dictionary<int, int> ziele = new Dictionary<int, int>();
        static readonly Dictionary<int, float> pause = new Dictionary<int, float>();
        static readonly Dictionary<int, float> ruhe = new Dictionary<int, float>();
        static readonly System.Random zufall = new System.Random();
        static float suchTakt;

        /// <summary>Waffe für die Figur setzen und speichern.</summary>
        public static string WaffeSetzen(EntityFigur f, string waffe)
        {
            var p = FigurenMod.PlatzVon(f);
            if (p == null) return "Figur hat keinen Platz im Spielstand.";
            p.Waffe = ArtVon(waffe).Name;
            ausgeruestet.Remove(f.entityId);
            lageGesetzt.Remove(f.entityId);
            grundLage.Remove(f.entityId);
            FigurenMod.Speichern();
            return "Waffe: " + p.Waffe + ".";
        }

        public static string HaltungSetzen(EntityFigur f, string haltung)
        {
            var p = FigurenMod.PlatzVon(f);
            if (p == null) return "Figur hat keinen Platz im Spielstand.";
            if (!IstSoeldner(p)) return "Nur Söldner können feindlich werden. Alle bewaffneten Figuren verteidigen sich gegen Zombies.";
            p.Haltung = HaltungVon(haltung);
            FigurenMod.Speichern();
            return "Haltung: " + p.Haltung + (ArtVon(p.Waffe).Item.Length == 0 ? " (ohne Waffe schießt er nicht)." : ".");
        }

        /// <summary>Alle Söldner auf einmal: feindlich (schießen auf Spieler) oder freundlich (nur Zombies). Konsole: npc f1 / npc f2.</summary>
        /// <summary>Reichweite von nf1 um den Spieler (Blöcke). nf2 gilt für alle Söldner der Karte.</summary>
        public const float BefehlReichweite = 400f;

        public static string AlleSoeldner(bool feindlich)
        {
            if (FigurenMod.Stand == null) return "Erst eine Welt laden.";
            Vector3? mitte = null;
            try
            {
                var sp = GameManager.Instance.World.GetPrimaryPlayer();
                if (sp != null) mitte = sp.position;
            }
            catch (Exception) { }
            int n = 0;
            foreach (var p in FigurenMod.Stand.Figuren)
            {
                if (!IstSoeldner(p)) continue;
                if (mitte.HasValue && (new Vector3(p.X, p.Y, p.Z) - mitte.Value).magnitude > BefehlReichweite) continue;
                p.Haltung = feindlich ? "Feindlich" : "Wache";
                leben.Remove(p.Id);
                n++;
            }
            FigurenMod.Speichern();
            return n + " Söldner " + (feindlich ? "im Umkreis von " + (int)BefehlReichweite + " Blöcken FEINDLICH: schießen jetzt auf Spieler." : "im Umkreis von " + (int)BefehlReichweite + " Blöcken freundlich: schießen nur noch auf Zombies.");
        }

        /// <summary>Gegenstand in die Hand geben (Slot 0 halten). Leerer Name = Hand leer.</summary>
        static bool Ausruesten(EntityAlive e, string item)
        {
            try
            {
                var inv = e.inventory;
                if (inv == null) return false;
                ItemValue iv = item.Length > 0 ? ItemClass.GetItem(item, false) : new ItemValue();
                if (item.Length > 0 && (iv == null || iv.type == 0)) { Log.Warning("[Figuren] Waffe " + item + " nicht gefunden."); return false; }
                var t = inv.GetType();
                var m = t.GetMethod("SetItem", new[] { typeof(int), typeof(ItemValue), typeof(int), typeof(bool) });
                if (m != null) m.Invoke(inv, new object[] { 0, iv, item.Length > 0 ? 1 : 0, true });
                else
                {
                    m = t.GetMethod("SetItem", new[] { typeof(int), typeof(ItemStack) });
                    if (m == null) { Log.Warning("[Figuren] Inventory.SetItem nicht gefunden."); return false; }
                    m.Invoke(inv, new object[] { 0, Activator.CreateInstance(typeof(ItemStack), iv, item.Length > 0 ? 1 : 0) });
                }
                var h = t.GetMethod("SetHoldingItemIdx", new[] { typeof(int) });
                if (h != null) h.Invoke(inv, new object[] { 0 });
                var u = t.GetMethod("ForceHoldingItemUpdate", Type.EmptyTypes);
                if (u != null) u.Invoke(inv, null);
                return true;
            }
            catch (Exception ex) { Log.Warning("[Figuren] Ausrüsten (" + item + "): " + ex.Message); return false; }
        }

        /// <summary>Animator-Parameter setzen, falls das Modell ihn hat (Trigger, Bool, Int, Float).</summary>
        static void Anim(EntityAlive e, string name, object wert)
        {
            try
            {
                foreach (var a in e.GetComponentsInChildren<Animator>())
                {
                    if (a == null || a.runtimeAnimatorController == null) continue;
                    foreach (var par in a.parameters)
                    {
                        if (par.name != name) continue;
                        if (par.type == AnimatorControllerParameterType.Trigger) a.SetTrigger(name);
                        else if (par.type == AnimatorControllerParameterType.Bool && wert is bool) a.SetBool(name, (bool)wert);
                        else if (par.type == AnimatorControllerParameterType.Int && wert is int) a.SetInteger(name, (int)wert);
                        else if (par.type == AnimatorControllerParameterType.Float && wert is int) a.SetFloat(name, (int)wert);
                    }
                }
            }
            catch (Exception) { }
        }

        /// <summary>Waffenhaltung im Animator: WeaponClass (Modelle von khzmusik) bzw. WeaponHoldType (Spiel) = HoldType der Waffe.</summary>
        static void Haltung(EntityAlive e, Art art)
        {
            Anim(e, "WeaponClass", art.HoldType);
            Anim(e, "WeaponHoldType", art.HoldType);
        }

        // ---------- Lage der Waffe in der Hand (pro Waffe, Datei Waffenlage.txt) ----------
        public static string LageDatei = "";
        static readonly Dictionary<string, float[]> lage = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<int, string> lageGesetzt = new Dictionary<int, string>();
        static readonly Dictionary<int, KeyValuePair<Vector3, Quaternion>> grundLage = new Dictionary<int, KeyValuePair<Vector3, Quaternion>>();

        /// <summary>Drehung der Waffe in der Hand, wie LageAnwenden sie gesetzt hat (für das Zielen in FigurKoerper).</summary>
        public static bool GrundDrehung(int id, out Quaternion q)
        {
            q = Quaternion.identity;
            KeyValuePair<Vector3, Quaternion> g;
            string name;
            if (!grundLage.TryGetValue(id, out g) || !lageGesetzt.TryGetValue(id, out name)) return false;
            float[] v;
            if (!lage.TryGetValue(name, out v)) v = new float[6];
            q = g.Value * Quaternion.Euler(v[3], v[4], v[5]);
            return true;
        }

        public static void LageLaden()
        {
            lage.Clear();
            try
            {
                if (string.IsNullOrEmpty(LageDatei) || !System.IO.File.Exists(LageDatei)) return;
                foreach (var z in System.IO.File.ReadAllLines(LageDatei))
                {
                    var t = z.Trim();
                    if (t.Length == 0 || t.StartsWith("#")) continue;
                    int dp = t.IndexOf(':');
                    if (dp <= 0) continue;
                    var w = t.Substring(dp + 1).Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    var v = new float[6];
                    for (int i = 0; i < 6 && i < w.Length; i++)
                        float.TryParse(w[i].Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v[i]);
                    lage[t.Substring(0, dp).Trim()] = v;
                }
            }
            catch (Exception ex) { Log.Warning("[Figuren] Waffenlage.txt: " + ex.Message); }
        }

        static void LageSpeichern()
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("# Lage der Waffe in der Hand der Figuren: Waffe: x y z (cm)  dreh-x dreh-y dreh-z (Grad)");
                sb.AppendLine("# Wird mit \"npc lage ...\" an der nächsten Figur eingestellt.");
                foreach (var kv in lage)
                    sb.AppendLine(kv.Key + ": " + string.Join(" ", kv.Value.Select(x => x.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)).ToArray()));
                System.IO.File.WriteAllText(LageDatei, sb.ToString());
            }
            catch (Exception ex) { Log.Warning("[Figuren] Waffenlage speichern: " + ex.Message); }
        }

        static Transform Griff(EntityAlive e)
        {
            foreach (var t in e.GetComponentsInChildren<Transform>(true))
                if (string.Equals(t.name, "Gunjoint", StringComparison.OrdinalIgnoreCase) && t.childCount > 0) return t.GetChild(0);
            return null;
        }

        /// <summary>Versatz der Waffe anwenden (relativ zur Lage, die das Spiel ihr gegeben hat). true = Waffe gefunden.</summary>
        static bool LageAnwenden(EntityAlive e, Art art)
        {
            var g = Griff(e);
            if (g == null) return false;
            KeyValuePair<Vector3, Quaternion> grund;
            if (!grundLage.TryGetValue(e.entityId, out grund) || lageGesetzt.ContainsKey(e.entityId) && lageGesetzt[e.entityId] != art.Name)
            {
                grund = new KeyValuePair<Vector3, Quaternion>(g.localPosition, g.localRotation);
                grundLage[e.entityId] = grund;
            }
            float[] v;
            if (!lage.TryGetValue(art.Name, out v)) v = new float[6];
            g.localPosition = grund.Key + new Vector3(v[0], v[1], v[2]) * 0.01f;
            g.localRotation = grund.Value * Quaternion.Euler(v[3], v[4], v[5]);
            lageGesetzt[e.entityId] = art.Name;
            return true;
        }

        /// <summary>Konsole: npc lage [x|y|z cm] [dreh x|y|z grad] [null] an der nächsten Figur. Gilt für diese Waffe bei allen Figuren.</summary>
        public static string LageBefehl(EntityFigur f, List<string> a)
        {
            var p = FigurenMod.PlatzVon(f);
            var art = ArtVon(p != null ? p.Waffe : "");
            if (art.Item.Length == 0) return "Die Figur hat keine Waffe.";
            float[] v;
            if (!lage.TryGetValue(art.Name, out v)) { v = new float[6]; lage[art.Name] = v; }
            if (a.Count >= 1 && a[0].ToLowerInvariant() == "null") { for (int i = 0; i < 6; i++) v[i] = 0f; }
            else if (a.Count >= 3 && a[0].ToLowerInvariant() == "dreh")
            {
                int achse = "xyz".IndexOf(a[1].ToLowerInvariant());
                float g;
                if (achse < 0 || !float.TryParse(a[2].Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out g)) return "npc lage dreh x|y|z <Grad>";
                v[3 + achse] += g;
            }
            else if (a.Count >= 2)
            {
                int achse = "xyz".IndexOf(a[0].ToLowerInvariant());
                float cm;
                if (achse < 0 || !float.TryParse(a[1].Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out cm)) return "npc lage x|y|z <cm>   oder   npc lage dreh x|y|z <Grad>   oder   npc lage null";
                v[achse] += cm;
            }
            LageSpeichern();
            // auf alle geladenen Figuren mit dieser Waffe anwenden
            var w = GameManager.Instance.World;
            foreach (var pl in FigurenMod.Stand.Figuren)
            {
                var x = w.GetEntity(pl.Id) as EntityFigur;
                if (x != null && ArtVon(pl.Waffe).Name == art.Name) LageAnwenden(x, art);
            }
            bool gefunden = Griff(f) != null;
            return art.Name + ": Versatz " + v[0] + " / " + v[1] + " / " + v[2] + " cm, Drehung " + v[3] + " / " + v[4] + " / " + v[5] + " Grad."
                + (gefunden ? "" : " (Kein Haltepunkt 'Gunjoint' im Modell gefunden.)");
        }

        /// <summary>Freie Schusslinie? Ignoriert die eigene Figur; andere Wesen blockieren nicht, Blöcke/Gelände schon.</summary>
        static bool Frei(EntityAlive von, EntityAlive ziel, Vector3 a, Vector3 b)
        {
            try
            {
                var d = b - a;
                float len = d.magnitude;
                if (len < 0.1f) return true;
                var treffer = Physics.RaycastAll(a, d / len, len);
                Array.Sort(treffer, (x, y) => x.distance.CompareTo(y.distance));
                foreach (var h in treffer)
                {
                    if (h.collider == null || h.collider.isTrigger) continue;
                    var ent = h.collider.GetComponentInParent<Entity>();
                    if (ent == von) continue;
                    if (ent == ziel) return true;
                    if (ent != null) continue;
                    return false;
                }
                return true;
            }
            catch (Exception) { return true; }
        }

        static EntityAlive Naechster(EntityFigur f, Art art, bool spieler)
        {
            var w = GameManager.Instance.World;
            var pf = FigurenMod.PlatzVon(f);
            Vector3 auge = f.position + Vector3.up * 1.5f;
            var kandidaten = new List<KeyValuePair<float, EntityAlive>>();
            foreach (var x in w.Entities.list)
            {
                var a = x as EntityAlive;
                if (a == null || a == f || a.IsDead()) continue;
                var af = a as EntityFigur;
                if (af != null)
                {
                    // andere Figur: nur Gegner aus Wachmann/Söldner (nicht beim Suchen nach Spielern), keine Leichen
                    if (spieler || pf == null || IstLeiche(af.entityId) || !Feinde(pf, FigurenMod.PlatzVon(af))) continue;
                }
                else if (spieler ? !(a is EntityPlayer) : !(a is EntityEnemy)) continue;
                float d = (a.position - f.position).sqrMagnitude;
                if (d <= art.Reichweite * art.Reichweite) kandidaten.Add(new KeyValuePair<float, EntityAlive>(d, a));
            }
            foreach (var k in kandidaten.OrderBy(k => k.Key))
                if (Frei(f, k.Value, auge, k.Value.position + Vector3.up * 1.0f)) return k.Value;
            return null;
        }

        /// <summary>Bei "Feindlich" zuerst Spieler, sonst (und danach) der nächste Gegner mit freier Schusslinie:
        /// Zombies, und für Wachmänner/Söldner auch die gegnerische Seite (feindliche Söldner gegen Wachmänner und freundliche Söldner).</summary>
        static EntityAlive ZielSuchen(EntityFigur f, Art art, bool spielerAuch)
        {
            if (spielerAuch)
            {
                var sp = Naechster(f, art, true);
                if (sp != null) return sp;
            }
            return Naechster(f, art, false);
        }

        /// <summary>Zielpunkt am Gegner: Brust (Kopfhöhe minus 35 cm), bei kleinen/kriechenden Gegnern tiefer.</summary>
        static Vector3 ZielPunkt(EntityAlive ziel)
        {
            Vector3 p = ziel.position + Vector3.up * 1.1f;
            try
            {
                Vector3 k = ziel.getHeadPosition();
                if (k.y > ziel.position.y + 0.2f) p = k - Vector3.up * 0.35f;
            }
            catch (Exception) { }
            return p;
        }

        static void Schiessen(EntityFigur f, EntityAlive ziel, Art art, FigurKoerper k)
        {
            try { Audio.Manager.BroadcastPlay(f.position, art.Ton); } catch (Exception) { }
            Anim(f, "WeaponFire", null);
            if (k != null)
            {
                k.Schuss();
                Partikel(art.Feuer, k.MuendungsOrt(), Quaternion.LookRotation(k.SchussRichtung));
            }
            float dist = Vector3.Distance(f.position, ziel.position);
            float chance = art.Treffer * (1f - 0.5f * Mathf.Clamp01(dist / art.Reichweite));
            int schaden = 0;
            for (int i = 0; i < art.Kugeln; i++) if (zufall.NextDouble() < chance) schaden += art.Schaden;
            if (schaden > 0)
                ziel.DamageEntity(new DamageSourceEntity(EnumDamageSource.External, EnumDamageTypes.Piercing, f.entityId), schaden, false, 1f);
        }

        /// <summary>Partikel aus dem Spiel abspielen (Mündungsfeuer, Blut), nur wenn es ihn gibt.</summary>
        public static void Partikel(string name, Vector3 ort, Quaternion dreh)
        {
            if (string.IsNullOrEmpty(name)) return;
            try
            {
                if (!ParticleEffect.IsAvailable(name)) return;
                var pe = new ParticleEffect(name, ort, dreh, 1f, Color.white, null, null);
                GameManager.Instance.SpawnParticleEffectClient(pe, -1, false, true);
            }
            catch (Exception) { }
        }

        /// <summary>Jeden Frame aus FigurenMod aufrufen.</summary>
        public static void Tick(float dt)
        {
            var w = GameManager.Instance == null ? null : GameManager.Instance.World;
            if (w == null || FigurenMod.Stand == null) return;
            LeichenWegraeumen(w, dt);
            Laufen.Tick(w, dt);
            Umher.Tick(w, dt);
            suchTakt -= dt;
            bool suchen = suchTakt <= 0f;
            if (suchen) suchTakt = 0.25f;
            foreach (var p in FigurenMod.Stand.Figuren)
            {
                var f = w.GetEntity(p.Id) as EntityFigur;
                if (f == null) continue;
                var art = ArtVon(p.Waffe);
                // Waffe in die Hand (einmal pro geladener Figur bzw. nach Wechsel)
                string soll = art.Item;
                string ist;
                if (!ausgeruestet.TryGetValue(f.entityId, out ist) || ist != soll)
                {
                    if (soll.Length > 0 || ausgeruestet.ContainsKey(f.entityId)) Ausruesten(f, soll);
                    ausgeruestet[f.entityId] = soll;
                    lageGesetzt.Remove(f.entityId);
                    grundLage.Remove(f.entityId);
                }
                FigurKoerper.Fuer(f);   // Blick, Atmen, Trefferreaktion auch für unbewaffnete Figuren
                Bodenhaftung(f, p, dt);
                if (soll.Length == 0) { ziele.Remove(f.entityId); continue; }
                // Waffe sitzt erst ein paar Frames nach dem Ausrüsten in der Hand: dann Lage und Haltung setzen
                string gesetzt;
                if (!lageGesetzt.TryGetValue(f.entityId, out gesetzt) || gesetzt != art.Name)
                {
                    if (LageAnwenden(f, art)) Haltung(f, art);
                }
                string haltung = Wirksam(p);
                var k = FigurKoerper.Fuer(f);
                int id = f.entityId;

                float t;
                pause.TryGetValue(id, out t);
                t = Mathf.Max(t - dt, -1f);
                pause[id] = t;

                // Ziel halten; alle 0,25 s neu suchen (nächster Gegner mit freier Schusslinie)
                EntityAlive ziel = null;
                int zid;
                if (ziele.TryGetValue(id, out zid))
                {
                    ziel = w.GetEntity(zid) as EntityAlive;
                    if (ziel == null || ziel.IsDead() || (ziel.position - f.position).sqrMagnitude > art.Reichweite * art.Reichweite * 1.3f) ziel = null;
                }
                if (suchen) ziel = ZielSuchen(f, art, haltung == "Feindlich");
                if (ziel == null) ziele.Remove(id); else ziele[id] = ziel.entityId;

                if (ziel != null)
                {
                    if (Laufen.Laeuft(id)) Laufen.Stoppen(f);   // nicht gleichzeitig laufen und zielen (beides dreht die Figur)
                    // zum Ziel drehen (nicht schlagartig), Waffe anlegen, erst schießen wenn sie oben ist und zeigt
                    float sollDreh = FigurenMod.DrehungZu(f.position, ziel.position);
                    float jetzt = Mathf.MoveTowardsAngle(f.rotation.y, sollDreh, 320f * dt);
                    try { f.SetRotationAndStopTurning(new Vector3(0f, jetzt, 0f)); } catch (Exception) { f.rotation = new Vector3(0f, jetzt, 0f); }
                    k.Zielen(ZielPunkt(ziel), art.Zweihand);
                    Haltung(f, art);
                    Anim(f, "IsAiming", true);
                    ruhe[id] = 3f;
                    bool bereit = k.Bereit || !k.HatKnochen;
                    if (t <= 0f && bereit && Mathf.Abs(Mathf.DeltaAngle(jetzt, sollDreh)) < 12f)
                    {
                        Schiessen(f, ziel, art, k);
                        pause[id] = art.Pause;
                    }
                }
                else
                {
                    // nach 3 s ohne Ziel Waffe senken und wieder in die alte Richtung schauen
                    float r;
                    if (ruhe.TryGetValue(id, out r))
                    {
                        r -= dt;
                        if (r <= 0f)
                        {
                            ruhe.Remove(id);
                            Anim(f, "IsAiming", false);
                            if (!Laufen.Laeuft(id)) try { f.SetRotationAndStopTurning(new Vector3(0f, p.Drehung, 0f)); } catch (Exception) { }
                        }
                        else ruhe[id] = r;
                    }
                }
            }
        }

        // ---------- Verwundbarkeit feindlicher Söldner ----------
        /// <summary>Lebenspunkte eines feindlichen Söldners (Kopftreffer zählen doppelt).</summary>
        public const int SoeldnerLeben = 150;
        static readonly Dictionary<int, int> leben = new Dictionary<int, int>();
        static readonly Dictionary<int, float> leichen = new Dictionary<int, float>();

        /// <summary>Normale Figuren (Bürger, Wachmann, Händler, Banker ...) halten mehr aus: man muss sie länger beschießen.</summary>
        public const int FigurLeben = 400;

        /// <summary>Wer stirbt, wenn man lange genug schießt: feindliche Söldner (150) und alle normalen Figuren (400).
        /// Haupt- und Nebenrollen (Gustavo, David, Orwell, Volkan, Frank, Churchill) bleiben unverwundbar.</summary>
        public static bool Verwundbar(EntityFigur f)
        {
            var p = FigurenMod.PlatzVon(f);
            if (p == null) return false;
            if (Wirksam(p) == "Feindlich") return true;
            var r = FigurenMod.RolleVon(f);
            if (r == null) return true;
            // Figuren aus einer Vorlage (Wachmann, Söldner, Bürger ...) sind normale Leute und können sterben.
            // (Ihre Rolle hat keine "Art:"-Zeile und galt deshalb fälschlich als Nebenrolle.)
            if (!string.IsNullOrEmpty(r.VorlageName) || r.Vorlage) return true;
            string art = r.Art;
            return art != "Hauptrolle" && art != "Nebenrolle";
        }

        static int StartLeben(EntityFigur f)
        {
            var p = FigurenMod.PlatzVon(f);
            return p != null && Wirksam(p) == "Feindlich" ? SoeldnerLeben : FigurLeben;
        }

        static readonly Dictionary<int, float> letzterTreffer = new Dictionary<int, float>();

        /// <summary>Schaden abziehen. true = tot. Pro Kugel wird höchstens MaxProTreffer gezählt, und Mehrfachmeldungen
        /// derselben Kugel (innerhalb von 0,08 s) zählen nur einmal - sonst starb ein Söldner schon am ersten Treffer.</summary>
        public const int MaxProTreffer = 40;
        public static bool Treffer(EntityFigur f, int schaden, bool kopf) { return Treffer(f, schaden, kopf, false); }

        /// <summary>explosion = starke Explosion: zählt voll (keine Obergrenze pro Treffer).</summary>
        public static bool Treffer(EntityFigur f, int schaden, bool kopf, bool explosion)
        {
            float jetzt = Time.time, zuletzt;
            if (letzterTreffer.TryGetValue(f.entityId, out zuletzt) && jetzt - zuletzt < 0.08f) return false;
            letzterTreffer[f.entityId] = jetzt;
            int hp;
            if (!leben.TryGetValue(f.entityId, out hp)) hp = StartLeben(f);
            if (schaden <= 0) return false;
            int zaehlt = explosion ? schaden : Mathf.Min(schaden, MaxProTreffer) * (kopf ? 2 : 1);
            Log.Out("[Figuren] Treffer Figur #" + f.entityId + ": gemeldet " + schaden + ", zählt " + zaehlt + ", Rest " + (hp - zaehlt));
            hp -= zaehlt;
            leben[f.entityId] = hp;
            if (hp > 0) { try { Audio.Manager.BroadcastPlay(f.position, "player1painsm"); } catch (Exception) { } }
            return hp <= 0;
        }

        static readonly Dictionary<int, float> luftZeit = new Dictionary<int, float>();
        static readonly Dictionary<int, float> schwebZeit = new Dictionary<int, float>();
        static readonly Dictionary<int, float> bodenPruefung = new Dictionary<int, float>();

        /// <summary>Figuren dürfen nicht klettern (Bäume, Wände), nicht springen und nicht in der Luft hängen bleiben.
        /// Gemessen wird gegen den Boden direkt unter der Figur (nicht gegen die Höhe ihres Platzes):
        ///  - länger als 1,5 s in der Luft oder 0,6 s mehr als 1,2 m über dem Boden -> auf den Boden darunter stellen
        ///  - fällt ohne Boden darunter (durch die Welt) oder mehr als 12 m unter dem Platz -> zurück an ihren Platz.
        /// Jede Korrektur steht im Log.</summary>
        static void Bodenhaftung(EntityFigur f, Platz p, float dt)
        {
            try
            {
                if (IstLeiche(f.entityId)) return;
                int id = f.entityId;
                f.bCanClimbVertical = false;
                f.bCanClimbLadders = false;
                if (f.Jumping && !f.isSwimming) f.Jumping = false;
                if (f.isSwimming) { luftZeit[id] = 0f; schwebZeit[id] = 0f; return; }

                float t;
                luftZeit.TryGetValue(id, out t);
                t = f.onGround ? 0f : t + dt;
                luftZeit[id] = t;

                // Abstand zum Boden: nur alle 0,2 s messen (Strahl nach unten)
                float takt;
                bodenPruefung.TryGetValue(id, out takt);
                takt -= dt;
                float schweb;
                schwebZeit.TryGetValue(id, out schweb);
                bool pruefen = takt <= 0f || t > 1.5f;
                if (!pruefen) { bodenPruefung[id] = takt; return; }
                bodenPruefung[id] = 0.2f;

                float bodenY;
                bool boden = Umher.BodenUnter(f.position + Vector3.up * 0.6f, 60f, out bodenY);
                float hoehe = boden ? f.position.y - bodenY : 99f;
                schweb = boden && hoehe > 1.2f ? schweb + 0.2f : 0f;
                schwebZeit[id] = schweb;

                float unterPlatz = p.Y - f.position.y;
                if ((!boden && t > 1.5f) || unterPlatz > 12f)
                {
                    Zurueck(f, p, !boden ? "fällt, kein Boden unter ihr" : (unterPlatz.ToString("0.0") + " m unter ihrem Platz"));
                }
                else if (boden && (t > 1.5f || schweb >= 0.6f))
                {
                    if (Laufen.Laeuft(id)) Laufen.Stoppen(f);
                    Umher.Pause(f, 8f);
                    f.motion = Vector3.zero;
                    f.SetPosition(new Vector3(f.position.x, bodenY + 0.05f, f.position.z), true);
                    luftZeit[id] = 0f; schwebZeit[id] = 0f;
                    Log.Out("[Figuren] Figur #" + id + " war " + hoehe.ToString("0.0") + " m über dem Boden - auf den Boden gestellt.");
                }
            }
            catch (Exception) { }
        }

        static void Zurueck(EntityFigur f, Platz p, string grund)
        {
            if (Laufen.Laeuft(f.entityId)) Laufen.Stoppen(f);
            Umher.Pause(f, 20f);
            f.motion = Vector3.zero;
            f.SetPosition(new Vector3(p.X, p.Y, p.Z), true);
            try { f.SetRotationAndStopTurning(new Vector3(0f, p.Drehung, 0f)); } catch (Exception) { }
            luftZeit[f.entityId] = 0f; schwebZeit[f.entityId] = 0f;
            Log.Out("[Figuren] Figur #" + f.entityId + ": " + grund + " - zurück an ihren Platz gestellt.");
        }

        /// <summary>Tote Söldner, die als Leiche liegen (bis sie nach 3 Minuten verschwinden).</summary>
        public static bool IstLeiche(int id) { return leichen.ContainsKey(id); }

        /// <summary>Bewaffnete Figur hat gerade ein Ziel (dann nicht umherlaufen).</summary>
        public static bool HatZiel(int id) { return ziele.ContainsKey(id); }

        /// <summary>Söldner ist tot: umfallen lassen (FigurKoerper), Blut, aus dem Spielstand nehmen, Leiche nach 3 Minuten entfernen.
        /// Bewusst KEIN Tod über das Spiel: der hat bei 1000 Schaden die Zombie-Zerstückelung ausgelöst.</summary>
        public static void Gestorben(EntityFigur f, Vector3 richtung)
        {
            try { Audio.Manager.BroadcastPlay(f.position, "player1death"); } catch (Exception) { }
            Partikel("blood_death", f.position + Vector3.up * 1.0f, Quaternion.identity);
            try { FigurKoerper.Fuer(f).Sterben(richtung); } catch (Exception) { }
            try { Ausruesten(f, ""); } catch (Exception) { }
            try { Anim(f, "IsAiming", false); } catch (Exception) { }
            var p = FigurenMod.PlatzVon(f);
            string name = p != null ? p.Rolle : "";
            if (FigurenMod.Stand != null) FigurenMod.Stand.Figuren.RemoveAll(x => x.Id == f.entityId);
            FigurenMod.Speichern();
            leben.Remove(f.entityId);
            Vergessen(f.entityId);
            leichen[f.entityId] = 180f;
            Log.Out("[Figuren] Figur " + name + " ist tot.");
        }

        static void LeichenWegraeumen(World w, float dt)
        {
            if (leichen.Count == 0) return;
            foreach (var id in leichen.Keys.ToList())
            {
                float t = leichen[id] - dt;
                if (t > 0f) { leichen[id] = t; continue; }
                leichen.Remove(id);
                try { if (w.GetEntity(id) != null) w.RemoveEntity(id, EnumRemoveEntityReason.Despawned); } catch (Exception) { }
            }
        }

        /// <summary>Nach dem Neuaufstellen (neue Nummer) muss die Waffe neu in die Hand.</summary>
        public static void Vergessen(int id) { luftZeit.Remove(id); schwebZeit.Remove(id); bodenPruefung.Remove(id); ziele.Remove(id); ausgeruestet.Remove(id); pause.Remove(id); ruhe.Remove(id); lageGesetzt.Remove(id); grundLage.Remove(id); }
    }
}
