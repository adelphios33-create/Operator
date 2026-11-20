// Figuren-Mod – Einstieg, Speichern, Aufstellen und Entfernen von Figuren
// Geprüft gegen 7 Days to Die V 1.4 (b8): IModApi, ModEvents, EntityClass.FromString, EntityFactory.CreateEntity,
// World.SpawnEntityInWorld, World.RemoveEntity, World.Entities, GameIO.GetSaveGameDir.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Figuren.Kern;
using UnityEngine;

namespace Figuren.Spiel
{
    public class Platz
    {
        public int Id;
        public string Rolle = "";
        public string Aussehen = "Joel";
        /// <summary>Aus welcher Vorlage die Figur stammt (leer = feste Rolle) und für welche Stadt.</summary>
        public string Vorlage = "";
        public string Stadt = "";
        public string Branche = "";
        /// <summary>Unternehmer: Firmenname (Freitext) und Firmengröße (Klein, Mittel, Gross).</summary>
        public string Firma = "";
        public string Groesse = "";
        /// <summary>true = Aussehen von Hand gewählt (Pfeile/Zufall im Fenster); Übernehmen behält es dann.</summary>
        public bool AussehenGewaehlt;
        /// <summary>Typ 3: Waffe (keine, Pistole, MP, AK, Schrotflinte) und Haltung (Wache; Feindlich nur bei Söldnern).</summary>
        public string Waffe = "keine";
        public string Haltung = "Wache";
        /// <summary>Typ 4: 0 = steht fest, sonst Umkreis in Blöcken, in dem die Figur tagsüber umherläuft.</summary>
        public int Umkreis;
        public float X, Y, Z, Drehung;
    }

    public class FigurenStand
    {
        public int Version = 1;
        public List<Platz> Figuren = new List<Platz>();
        public List<Immobilie> Immobilien = new List<Immobilie>();
    }

    public class FigurenMod : IModApi
    {
        public const string Dateiname = "Figuren.xml";
        static readonly XmlSerializer Serializer = new XmlSerializer(typeof(FigurenStand));

        public static FigurenStand Stand;
        static string ordner = "";
        static readonly List<Action> naechsterFrame = new List<Action>();

        public void InitMod(Mod _modInstance)
        {
            Rollen.Ordner = Path.Combine(_modInstance.Path, "Rollen");
            Rollen.NachLaden = InstanzenAufbauen;
            Stimmen.Ordner = Path.Combine(_modInstance.Path, "Stimmen");
            Log.Out("[Figuren] " + Rollen.Laden());
            Stimmen.ListeSchreiben();
            Ki.Datei = Path.Combine(_modInstance.Path, "KI.txt");
            Ki.HintergrundDatei = Path.Combine(_modInstance.Path, "Welt.txt");
            Ki.RegelnDatei = Path.Combine(_modInstance.Path, "Dialogregeln.txt");
            Waffen.LageDatei = Path.Combine(_modInstance.Path, "Waffenlage.txt");
            Waffen.LageLaden();
            Log.Out("[Figuren] " + Ki.EinstellungenLaden());
            ModEvents.GameStartDone.RegisterHandler(SpielGestartet);
            ModEvents.GameUpdate.RegisterHandler(Aktualisieren);
            ModEvents.WorldShuttingDown.RegisterHandler(SpielBeendet);
            Log.Out("[Figuren] Mod geladen.");
        }

        static void SpielGestartet()
        {
            try
            {
                ordner = GameIO.GetSaveGameDir();
                string pfad = Path.Combine(ordner, Dateiname);
                if (File.Exists(pfad))
                    using (var s = File.OpenRead(pfad)) Stand = (FigurenStand)Serializer.Deserialize(s);
                else Stand = new FigurenStand();
                Ki.VerlaufLaden(ordner);
                GameOperator.Laden(ordner);
                InstanzenAufbauen();
                Log.Out("[Figuren] " + Stand.Figuren.Count + " Figuren im Spielstand.");
            }
            catch (Exception e)
            {
                Log.Error("[Figuren] Laden fehlgeschlagen, starte leer: " + e.Message);
                try
                {
                    string pfad = Path.Combine(ordner, Dateiname);
                    if (File.Exists(pfad)) File.Copy(pfad, pfad + ".defekt", true);
                }
                catch (Exception) { }
                Stand = new FigurenStand();
            }
        }

        static int fokusId = -1;

        /// <summary>Blickstrahl des Spielers (Kopfposition + Blickrichtung), mit mehreren Rückfallwegen.</summary>
        static bool Blick(EntityPlayerLocal sp, out Vector3 start, out Vector3 richtung)
        {
            start = Vector3.zero; richtung = Vector3.forward;
            try
            {
                var strahl = sp.GetLookRay();
                start = strahl.origin; richtung = strahl.direction;
                if (richtung.sqrMagnitude > 0.0001f) return true;
            }
            catch (Exception) { }
            Transform kam = sp.playerCamera != null ? sp.playerCamera.transform : sp.cameraTransform;
            if (kam == null) return false;
            start = kam.position; richtung = kam.forward;
            return true;
        }

        /// <summary>Figur, auf die der Spieler schaut (oder die direkt vor ihm steht). Liefert auch Abstand und Winkel für den Test.</summary>
        static EntityFigur ZielFigur(EntityPlayerLocal sp, out float abstand, out float winkel, out int anzahlInWelt)
        {
            abstand = -1f; winkel = -1f; anzahlInWelt = 0;
            var welt = Welt;
            Vector3 start, richtung;
            if (welt == null || !Blick(sp, out start, out richtung)) return null;
            EntityFigur ziel = null;
            float bestWert = float.MaxValue;
            foreach (var e in welt.Entities.list)
            {
                var f = e as EntityFigur;
                if (f == null) continue;
                anzahlInWelt++;
                var d = (f.position + Vector3.up * 1.0f) - start;
                float dist = d.magnitude;
                float w = Vector3.Angle(richtung, d);
                bool imBlick = dist <= 4f && w <= 35f;
                bool direktDavor = dist <= 1.8f;
                if (!imBlick && !direktDavor) continue;
                float wert = dist + w * 0.05f;
                if (wert < bestWert) { bestWert = wert; ziel = f; abstand = dist; winkel = w; }
            }
            return ziel;
        }

        /// <summary>Taste E (auch Controller) bei einer Figur: Gespräch öffnen.
        /// Das Spiel macht nur seine eigenen Händler ansprechbar, deshalb fragen wir die Taste selbst ab.</summary>
        static void FokusPruefen()
        {
            if (Stand == null || Stand.Figuren.Count == 0) { fokusId = -1; return; }
            var welt = Welt;
            var sp = welt == null ? null : welt.GetPrimaryPlayer();
            if (sp == null || sp.PlayerUI == null || sp.IsDead()) { fokusId = -1; return; }
            if (sp.PlayerUI.windowManager.IsModalWindowOpen()) return;

            float abstand, winkel; int n;
            var ziel = ZielFigur(sp, out abstand, out winkel, out n);
            if (ziel == null) { fokusId = -1; return; }

            if (fokusId != ziel.entityId)
            {
                fokusId = ziel.entityId;
                var r = RolleVon(ziel);
                try { GameManager.ShowTooltip(sp, "[E] mit " + (r != null ? r.Name : "Figur") + " reden", false); }
                catch (Exception) { }
            }

            bool gedrueckt = false;
            try { gedrueckt = sp.playerInput != null && sp.playerInput.Activate.WasPressed; } catch (Exception) { }
            if (!gedrueckt)
            {
                try { gedrueckt = Input.GetKeyDown(KeyCode.E); } catch (Exception) { }
            }
            if (gedrueckt) Reden(sp, ziel.entityId);
        }

        /// <summary>Taste für das Leitungsfenster (Standard Pos1 / Home): öffnet oder schließt es, auch ohne Figur in der Nähe.</summary>
        static void LeitungstastePruefen()
        {
            var welt = Welt;
            var sp = welt == null ? null : welt.GetPrimaryPlayer();
            if (sp == null || sp.PlayerUI == null || sp.IsDead()) return;
            bool gedrueckt = false;
            try { gedrueckt = Input.GetKeyDown(Ki.Taste); } catch (Exception) { }
            if (!gedrueckt) return;
            var wm = sp.PlayerUI.windowManager;
            if (wm.IsWindowOpen(XUiC_FigLeitung.Gruppe)) { wm.CloseIfOpen(XUiC_FigLeitung.Gruppe); return; }
            if (wm.IsModalWindowOpen()) return;
            XUiC_FigLeitung.Oeffnen(sp);
        }

        /// <summary>Was die Abfrage gerade sieht (für "npc test").</summary>
        public static string FokusBericht()
        {
            var welt = Welt;
            var sp = welt == null ? null : welt.GetPrimaryPlayer();
            var z = new List<string>();
            z.Add("Figuren im Spielstand: " + (Stand == null ? -1 : Stand.Figuren.Count));
            if (sp == null) { z.Add("Spieler: nicht gefunden"); return string.Join("\n", z.ToArray()); }
            int inWelt = 0;
            foreach (var e in welt.Entities.list) if (e is EntityFigur) inWelt++;
            z.Add("Figuren in der Welt: " + inWelt);
            z.Add("Fenster offen (modal): " + (sp.PlayerUI != null && sp.PlayerUI.windowManager.IsModalWindowOpen()));
            Vector3 st, ri;
            z.Add("Blickstrahl: " + (Blick(sp, out st, out ri) ? "ok" : "fehlt"));
            float ab, wi; int n;
            var f = ZielFigur(sp, out ab, out wi, out n);
            z.Add(f == null ? "Ziel: keine Figur im Blick (4 m, 35 Grad)" : "Ziel: #" + f.entityId + " Abstand " + ab.ToString("0.0") + " m, Winkel " + wi.ToString("0") + " Grad");
            foreach (var e in welt.Entities.list)
            {
                var g = e as EntityFigur;
                if (g == null) continue;
                z.Add("  #" + g.entityId + " " + g.EntityName + " bei " + (int)g.position.x + " " + (int)g.position.y + " " + (int)g.position.z + ", Abstand " + (g.position - sp.position).magnitude.ToString("0.0") + " m");
            }
            bool taste = false;
            try { taste = sp.playerInput != null && sp.playerInput.Activate.IsPressed; } catch (Exception) { }
            z.Add("Aktionstaste gerade gedrückt: " + taste);
            return string.Join("\n", z.ToArray());
        }

        static void Aktualisieren()
        {
            try { FokusPruefen(); }
            catch (Exception e) { Log.Warning("[Figuren] Fokus: " + e.Message); }
            try { LeitungstastePruefen(); }
            catch (Exception e) { Log.Warning("[Figuren] Taste: " + e.Message); }
            try { MarkierungenPruefen(); }
            catch (Exception e) { Log.Warning("[Figuren] Markierung: " + e.Message); }
            try { Waffen.Tick(Time.deltaTime); }
            catch (Exception e) { Log.Warning("[Figuren] Waffen: " + e.Message); }

            Action[] liste;
            lock (naechsterFrame)
            {
                if (naechsterFrame.Count == 0) return;
                liste = naechsterFrame.ToArray();
                naechsterFrame.Clear();
            }
            foreach (var a in liste)
            {
                try { a(); }
                catch (Exception e) { Log.Error("[Figuren] " + e.Message); }
            }
        }

        /// <summary>Etwas erst im nächsten Frame ausführen (z. B. ein Fenster nach dem Platzieren öffnen).</summary>
        public static void Spaeter(Action a) { lock (naechsterFrame) naechsterFrame.Add(a); }

        /// <summary>Gespräch öffnen: KI-Figuren (Rolle mit [ki]) bekommen das KI-Fenster, alle anderen den festen Dialog.</summary>
        public static bool Reden(EntityPlayerLocal sp, int id)
        {
            var f = FigurHolen(id);
            var r = f == null ? null : RolleVon(f);
            if (r != null && r.IstKi) return XUiC_FigKi.Oeffnen(sp, id);
            return XUiC_FigGespraech.Oeffnen(sp, id);
        }

        // ---------- Symbol über dem Kopf (Nav-Objekte des Spiels) ----------
        static readonly Dictionary<int, string> markiert = new Dictionary<int, string>();
        static float naechsteMarkierung;

        static void MarkierungenPruefen()
        {
            if (Stand == null || Time.time < naechsteMarkierung) return;
            naechsteMarkierung = Time.time + 1f;
            var welt = Welt;
            var nav = NavObjectManager.Instance;
            if (welt == null || nav == null) return;
            var gesehen = new HashSet<int>();
            foreach (var e in welt.Entities.list)
            {
                var f = e as EntityFigur;
                if (f == null) continue;
                gesehen.Add(f.entityId);
                var r = RolleVon(f);
                string klasse = r != null && r.IstKi ? "figur_ki" : "figur_npc";
                string schluessel = klasse + "|" + (r != null ? r.Name : "");
                string alt;
                if (markiert.TryGetValue(f.entityId, out alt) && alt == schluessel) continue;
                if (alt != null) nav.UnRegisterNavObjectByEntityID(f.entityId);
                var no = nav.RegisterNavObject(klasse, f, null, false);
                if (no != null && r != null) no.localizedName = r.Name;
                markiert[f.entityId] = schluessel;
            }
            foreach (var id in markiert.Keys.Where(k => !gesehen.Contains(k)).ToList())
            {
                nav.UnRegisterNavObjectByEntityID(id);
                markiert.Remove(id);
            }
        }

        static void SpielBeendet()
        {
            Speichern();
            Stand = null;
            fokusId = -1;
            markiert.Clear();
            lock (naechsterFrame) naechsterFrame.Clear();
        }

        public static void Speichern()
        {
            if (Stand == null || string.IsNullOrEmpty(ordner)) return;
            try
            {
                Directory.CreateDirectory(ordner);
                string pfad = Path.Combine(ordner, Dateiname);
                string temp = pfad + ".tmp";
                using (var s = File.Create(temp)) Serializer.Serialize(s, Stand);
                if (File.Exists(pfad)) File.Copy(pfad, pfad + ".bak", true);
                File.Copy(temp, pfad, true);
                File.Delete(temp);
                Ki.VerlaufSpeichern(ordner);
                GameOperator.Speichern(ordner);
            }
            catch (Exception e) { Log.Error("[Figuren] Speichern fehlgeschlagen: " + e.Message); }
        }

        static World Welt { get { return GameManager.Instance == null ? null : GameManager.Instance.World; } }

        /// <summary>Eintrag zu einer Figur: zuerst über die Nummer, sonst über die Position (Figuren bewegen sich nicht).</summary>
        public static Platz PlatzVon(Entity e)
        {
            if (Stand == null || e == null) return null;
            var p = Stand.Figuren.FirstOrDefault(w => w.Id == e.entityId);
            if (p != null) return p;
            p = Stand.Figuren
                .Where(w => (new Vector3(w.X, w.Y, w.Z) - e.position).sqrMagnitude < 2.25f)
                .OrderBy(w => (new Vector3(w.X, w.Y, w.Z) - e.position).sqrMagnitude)
                .FirstOrDefault();
            if (p != null) p.Id = e.entityId;
            return p;
        }

        /// <summary>Stellt Figuren, die aus Vorlagen entstanden sind (Barkeeper, Bürgermeister ...), nach dem Laden wieder her.</summary>
        public static void InstanzenAufbauen()
        {
            if (Stand == null) return;
            foreach (var p in Stand.Figuren)
            {
                if (string.IsNullOrEmpty(p.Vorlage) || string.IsNullOrEmpty(p.Rolle) || Rollen.Instanzen.ContainsKey(p.Rolle)) continue;
                var v = Rollen.Liste.FirstOrDefault(w => w.Vorlage && string.Equals(w.Name, p.Vorlage, StringComparison.OrdinalIgnoreCase));
                if (v != null) Rollen.Instanz(v, p.Stadt, p.Rolle, p.Aussehen, p.Branche, p.Firma, p.Groesse);
            }
        }

        public static Rolle RolleVon(Entity e)
        {
            var p = PlatzVon(e);
            return p == null ? null : Rollen.Holen(p.Rolle);
        }

        public static EntityFigur FigurHolen(int id)
        {
            var w = Welt;
            return w == null ? null : w.GetEntity(id) as EntityFigur;
        }

        /// <summary>Nächste Figur in der Nähe des Spielers (für Konsolenbefehle).</summary>
        public static EntityFigur NaechsteFigur(Entity spieler, float maxMeter)
        {
            var w = Welt;
            if (w == null || spieler == null) return null;
            EntityFigur beste = null;
            float bestAbstand = maxMeter * maxMeter;
            foreach (var e in w.Entities.list)
            {
                var f = e as EntityFigur;
                if (f == null) continue;
                float d = (f.position - spieler.position).sqrMagnitude;
                if (d <= bestAbstand) { bestAbstand = d; beste = f; }
            }
            return beste;
        }

        /// <summary>Stellt eine Figur auf. Rolle darf leer sein (wird dann im Einrichten-Fenster gewählt).</summary>
        /// <summary>Modelle, die sich in dieser Sitzung nicht laden ließen (werden beim Zufall übersprungen).</summary>
        public static readonly HashSet<string> Kaputt = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Erzeugt die Figur mit dem Wunsch-Modell. Klappt das nicht, zweite Variante ("…2"), sonst Joel. Wirft nie wegen eines Modells.</summary>
        static EntityFigur Erzeugen(string aussehen, Vector3 pos, float drehung, out string genutzt)
        {
            var w = Welt;
            var versuche = new List<KeyValuePair<string, string>>();
            if (!Kaputt.Contains(aussehen))
            {
                versuche.Add(new KeyValuePair<string, string>(aussehen, "figur" + aussehen));
                if (!Rollen.Modelle.Any(m => m.Gruppe == "Händler" && string.Equals(m.Name, aussehen, StringComparison.OrdinalIgnoreCase)))
                    versuche.Add(new KeyValuePair<string, string>(aussehen, "figur" + aussehen + "2"));
            }
            versuche.Add(new KeyValuePair<string, string>("Joel", "figurJoel"));
            foreach (var v in versuche)
            {
                EntityFigur e = null;
                try
                {
                    e = EntityFactory.CreateEntity(EntityClass.FromString(v.Value), pos, new Vector3(0f, drehung, 0f)) as EntityFigur;
                    if (e == null) throw new Exception("Klasse " + v.Value + " liefert keine Figur");
                    w.SpawnEntityInWorld(e);
                    genutzt = v.Key;
                    if (Rollen.IstSonder(v.Key)) AltModellRichten(e);
                    if (v.Value.Length > ("figur" + v.Key).Length) Log.Out("[Figuren] Modell " + v.Key + " lädt nur mit Variante 2 (" + v.Value + ").");
                    return e;
                }
                catch (Exception ex)
                {
                    Log.Warning("[Figuren] Modell " + v.Key + " (Klasse " + v.Value + ") nicht ladbar: " + ex);
                    try { if (e != null && w.GetEntity(e.entityId) != null) w.RemoveEntity(e.entityId, EnumRemoveEntityReason.Despawned); } catch (Exception) { }
                    Geister();
                    if (!v.Key.Equals("Joel", StringComparison.OrdinalIgnoreCase) && v.Value.Length > ("figur" + v.Key).Length) Kaputt.Add(v.Key);
                }
            }
            throw new Exception("Keine Figur erzeugbar (auch Joel nicht).");
        }

        /// <summary>Alte Modellpakete (Soldaten): Animator-Warnungen abschalten und kaputte Shader durch den Standard-Shader
        /// ersetzen (sonst wären die Figuren pink oder unsichtbar). Texturen bleiben erhalten.</summary>
        public static void AltModellRichten(Entity e)
        {
            try
            {
                if (e == null) return;
                foreach (var a in e.GetComponentsInChildren<Animator>(true)) if (a != null) a.logWarnings = false;
                Shader ersatz = null;
                int n = 0;
                foreach (var r in e.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null) continue;
                    foreach (var m in r.materials)
                    {
                        if (m == null) continue;
                        var sh = m.shader;
                        if (sh != null && sh.isSupported && sh.name != "Hidden/InternalErrorShader") continue;
                        if (ersatz == null) ersatz = Shader.Find("Standard");
                        if (ersatz == null) ersatz = Shader.Find("Legacy Shaders/Diffuse");
                        if (ersatz == null) return;
                        m.shader = ersatz;
                        n++;
                    }
                }
                if (n > 0) Log.Out("[Figuren] Altes Modell #" + e.entityId + ": " + n + " Material(ien) auf Ersatz-Shader gestellt.");
            }
            catch (Exception ex) { Log.Warning("[Figuren] AltModellRichten: " + ex.Message); }
        }

        /// <summary>Halb erzeugte Figuren (Modell ließ sich nicht laden) entfernen. Sonst laufen sie unsichtbar weiter
        /// und werfen nach dem Verlassen der Welt bei jedem Bild Fehler (EntityAlive.Update).</summary>
        public static void GeisterEntfernen() { Geister(); }

        static void Geister()
        {
            try
            {
                var w = Welt;
                foreach (var x in UnityEngine.Object.FindObjectsOfType<EntityFigur>())
                {
                    if (x == null) continue;
                    if (w != null && w.GetEntity(x.entityId) == x) continue;
                    Log.Out("[Figuren] Halb erzeugte Figur entfernt.");
                    UnityEngine.Object.Destroy(x.gameObject);
                }
            }
            catch (Exception ex) { Log.Warning("[Figuren] Aufräumen: " + ex.Message); }
        }

        public static EntityFigur Aufstellen(string rollenName, Vector3 pos, float drehung, string stadt = "", string aussehenWunsch = "", bool gewaehlt = false)
        {
            var w = Welt;
            if (w == null || Stand == null) throw new Exception("Erst eine Welt laden.");
            var r = Rollen.Holen(rollenName);
            if (r != null && r.Vorlage) r = Rollen.Instanz(r, stadt, null, string.IsNullOrEmpty(aussehenWunsch) ? null : aussehenWunsch);
            string aussehen = !string.IsNullOrEmpty(aussehenWunsch) ? Rollen.AussehenOderStandard(aussehenWunsch)
                : (r != null ? r.Aussehen : Rollen.ZufallsAussehen(null, "", ""));
            string genutzt;
            var e = Erzeugen(aussehen, pos, drehung, out genutzt);
            if (!string.Equals(genutzt, aussehen, StringComparison.OrdinalIgnoreCase) && r != null && r.Name.Length > 0 && Rollen.Instanzen.ContainsKey(r.Name))
                r.Aussehen = genutzt;
            Stand.Figuren.RemoveAll(p => p.Id == e.entityId);
            Stand.Figuren.Add(new Platz
            {
                Id = e.entityId, Rolle = r != null ? r.Name : "", Aussehen = genutzt, AussehenGewaehlt = gewaehlt,
                Vorlage = r != null ? r.VorlageName : "", Stadt = r != null ? r.Stadt : "", Branche = r != null ? r.Branche : "", Firma = r != null ? r.FirmaName : "", Groesse = r != null ? r.Groesse : "",
                X = pos.x, Y = pos.y, Z = pos.z, Drehung = drehung
            });
            Speichern();
            return e;
        }

        /// <summary>Gibt der Figur eine Rolle. Ein von Hand gewähltes Aussehen bleibt; sonst bekommt eine Grundrolle/feste Figur
        /// ein zufälliges Aussehen aus ihrer Liste (Figur wird dann an derselben Stelle neu aufgestellt). Liefert die (evtl. neue) Nummer.</summary>
        public static int RolleSetzen(EntityFigur e, Rolle r, string stadt = "", string branche = "", string firma = "", string groesse = "")
        {
            var p = PlatzVon(e);
            if (p == null)
            {
                p = new Platz { Id = e.entityId, X = e.position.x, Y = e.position.y, Z = e.position.z, Drehung = e.rotation.y };
                Stand.Figuren.Add(p);
            }
            string aussehen = p.Aussehen;
            bool neuWuerfeln = false;
            if (r.Vorlage)
            {
                bool gleich = string.Equals(p.Vorlage, r.Name, StringComparison.OrdinalIgnoreCase) && p.Rolle.Length > 0;
                if (!gleich && !p.AussehenGewaehlt)
                {
                    string z = Rollen.ZufallsAussehen(r, branche, "");
                    neuWuerfeln = !string.Equals(z, aussehen, StringComparison.OrdinalIgnoreCase);
                    aussehen = z;
                }
                r = Rollen.Instanz(r, stadt, gleich ? p.Rolle : null, aussehen, branche, firma, groesse);
            }
            if (neuWuerfeln)
            {
                // erst die neue Figur aufstellen, dann die alte entfernen - so verschwindet nie etwas
                var pos = new Vector3(p.X, p.Y, p.Z);
                var neu = Aufstellen(r.Name, pos, p.Drehung, r.Stadt, aussehen, false);
                Uebertragen(p, neu, r);
                Entfernen(e.entityId);
                return neu.entityId;
            }
            p.Rolle = r.Name;
            p.Vorlage = r.VorlageName;
            p.Stadt = r.Stadt;
            p.Branche = r.Branche;
            p.Firma = r.FirmaName;
            p.Groesse = r.Groesse;
            Bewaffnen(p, r);
            Speichern();
            return e.entityId;
        }

        /// <summary>Eigener Name für eine Figur, die aus einer Grundrolle stammt (Banker, Bürgermeister, Barkeeper, Unternehmer ...).
        /// Rolle, Stadt, Aussehen, Gedächtnis und Firma bleiben. Wirft einen Fehler mit Erklärung, wenn der Name nicht geht.</summary>
        public static string NameSetzen(Platz p, string neu)
        {
            neu = (neu ?? "").Trim();
            if (p == null || p.Vorlage.Length == 0) throw new Exception("Den Namen kann man nur bei Figuren ändern, die aus einer Grundrolle stammen (Banker, Bürgermeister, Barkeeper, Unternehmer ...).");
            if (neu.Length == 0) throw new Exception("Erst den neuen Namen im Feld 'Name' eintippen.");
            if (neu.Length > 40) throw new Exception("Der Name ist zu lang (höchstens 40 Zeichen).");
            if (neu.IndexOfAny(new[] { '|', '[', ']', '<', '>', '"' }) >= 0) throw new Exception("Im Namen sind die Zeichen | [ ] < > \" nicht erlaubt.");
            string alt = p.Rolle;
            bool nurGrossKlein = string.Equals(alt, neu, StringComparison.OrdinalIgnoreCase);
            if (string.Equals(alt, neu, StringComparison.Ordinal)) throw new Exception("Die Figur heißt schon so.");
            if (!nurGrossKlein && (Rollen.Holen(neu) != null || string.Equals(neu, Ki.ErzaehlerName, StringComparison.OrdinalIgnoreCase)))
                throw new Exception("Den Namen '" + neu + "' gibt es schon (andere Figur oder Rolle). Bitte einen anderen nehmen.");
            var v = Rollen.Liste.FirstOrDefault(w => w.Vorlage && string.Equals(w.Name, p.Vorlage, StringComparison.OrdinalIgnoreCase));
            if (v == null) throw new Exception("Grundrolle " + p.Vorlage + " nicht gefunden (Rollen neu laden).");
            Rollen.Instanz(v, p.Stadt, neu, p.Aussehen, p.Branche, p.Firma, p.Groesse);
            if (alt.Length > 0 && !nurGrossKlein) Rollen.Instanzen.Remove(alt);
            Ki.Umbenennen(alt, neu);
            KreditAngebote.Verwerfen(alt);
            p.Rolle = neu;
            Speichern();
            Log.Out("[Figuren] #" + p.Id + " heißt jetzt " + neu + " (vorher " + alt + ")");
            return alt + " heißt jetzt " + neu + ".";
        }

        /// <summary>Gibt der Figur ein anderes Aussehen (neu aufstellen an derselben Stelle). Rolle, Name, Stadt bleiben;
        /// wechselt Mann/Frau bei einer Grundrolle/festen Figur, gibt es einen passenden neuen Namen. Liefert die neue Nummer.</summary>
        public static int AussehenSetzen(EntityFigur e, string aussehen)
        {
            aussehen = Rollen.AussehenOderStandard(aussehen);
            var p = PlatzVon(e);
            var pos = p != null ? new Vector3(p.X, p.Y, p.Z) : e.position;
            float dreh = p != null ? p.Drehung : e.rotation.y;
            string rolle = p != null ? p.Rolle : "";
            string stadt = p != null ? p.Stadt : "";
            if (p != null && p.Vorlage.Length > 0)
            {
                var v = Rollen.Liste.FirstOrDefault(x => x.Vorlage && string.Equals(x.Name, p.Vorlage, StringComparison.OrdinalIgnoreCase));
                if (v != null)
                {
                    bool gleich = Rollen.IstFrau(p.Aussehen) == Rollen.IstFrau(aussehen);
                    if (!gleich && rolle.Length > 0) Rollen.Instanzen.Remove(rolle);
                    var r = Rollen.Instanz(v, stadt, gleich && rolle.Length > 0 ? rolle : null, aussehen, p.Branche, p.Firma, p.Groesse);
                    rolle = r.Name;
                }
            }
            var neu = Aufstellen(rolle, pos, dreh, stadt, aussehen, true);
            Uebertragen(p, neu, null);
            Entfernen(e.entityId);
            return neu.entityId;
        }

        /// <summary>Waffe/Haltung vom alten Platz auf die neu aufgestellte Figur übertragen (plus Vorgabe der Rolle).</summary>
        static void Uebertragen(Platz alt, EntityFigur neu, Rolle r)
        {
            var pn = PlatzVon(neu);
            if (pn == null) return;
            if (alt != null) { pn.Waffe = alt.Waffe; pn.Haltung = alt.Haltung; pn.Umkreis = alt.Umkreis; }
            if (r != null) Bewaffnen(pn, r);
            Waffen.Vergessen(neu.entityId);
            Speichern();
        }

        /// <summary>Rolle mit Zeile "Waffe:"/"Haltung:" (z. B. Wachmann): Vorgabe übernehmen, wenn die Figur noch unbewaffnet ist.</summary>
        static void Bewaffnen(Platz p, Rolle r)
        {
            if (r == null || p == null) return;
            if (r.Waffe.Length > 0 && (p.Waffe.Length == 0 || p.Waffe == "keine")) p.Waffe = Waffen.ArtVon(r.Waffe).Name;
            // Feindlich nie automatisch (nur per Befehl nf1), damit ein neuer Söldner nicht sofort auf den Spieler schießt
            if (r.Haltung.Length > 0 && (p.Haltung.Length == 0 || p.Haltung == "Friedlich" || p.Haltung == "Wache"))
            {
                string h = Waffen.HaltungVon(r.Haltung);
                p.Haltung = h == "Feindlich" ? "Wache" : h;
            }
        }

        public static void Entfernen(int id)
        {
            var w = Welt;
            if (w != null && w.GetEntity(id) != null) w.RemoveEntity(id, EnumRemoveEntityReason.Despawned);
            if (Stand != null) Stand.Figuren.RemoveAll(p => p.Id == id);
            Speichern();
        }

        /// <summary>Blickrichtung (Grad), damit eine Figur an "von" zum Punkt "zu" schaut.</summary>
        public static float DrehungZu(Vector3 von, Vector3 zu)
        {
            var d = zu - von;
            if (Mathf.Abs(d.x) < 0.001f && Mathf.Abs(d.z) < 0.001f) return 0f;
            return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }
    }
}
