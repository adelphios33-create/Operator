// Figuren-Mod – Game Operator (Spielleiter): Ereignisse mit Status, Szenarien, Planung, Ereignisketten,
// Spielerwünsche und Weltzustand für den Operator im Leitungsfenster.
// Gespeichert im Spielstand als Figuren_Weltereignisse.xml. "Gespräch neu beginnen" löscht die Ereignisse NICHT.
// Grundsatz: Vorschläge und Planungen sind keine Tatsachen. Tatsache wird ein Ereignis erst, wenn der Spieler es
// auf "Aktiv" (läuft gerade) oder "Abgeschlossen" (eingetreten) setzt. Abgeschlossene Ereignisse ändern sich nie mehr.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using Figuren.Kern;

namespace Figuren.Spiel
{
    /// <summary>Ein Ereignis der Spielwelt (Vorschlag, Planung oder Tatsache).</summary>
    public class Weltereignis
    {
        [XmlAttribute] public int Id;
        [XmlAttribute] public string Status = GameOperator.Vorschlag;
        [XmlAttribute] public string Art = "";
        /// <summary>Name der Ereigniskette (leer = steht allein).</summary>
        [XmlAttribute] public string Kette = "";
        /// <summary>Nummer des Ereignisses, aus dem dieses folgt (0 = keins).</summary>
        [XmlAttribute] public int Vorgaenger;
        /// <summary>Spieltag, an dem es angelegt bzw. zuletzt geändert wurde.</summary>
        [XmlAttribute] public int Erstellt;
        [XmlAttribute] public int Geaendert;
        /// <summary>true = eigenständige Weltentwicklung (unabhängig vom Spieler).</summary>
        [XmlAttribute] public bool Hintergrund;
        public string Titel = "";
        public string Beschreibung = "";
        public string Warum = "";
        public string Beteiligte = "";
        public string Auswirkungen = "";
        public string Wahrscheinlichkeit = "";
        public string Folgen = "";
        /// <summary>Ausgearbeiteter Plan (Schritte), wird mit [PLAN #Nummer: ...] vom Operator gespeichert.</summary>
        public string Plan = "";
        /// <summary>Was tatsächlich passiert ist (beim Abschließen).</summary>
        public string Ergebnis = "";
        public List<string> Verlauf = new List<string>();
    }

    /// <summary>Langfristiger Wunsch des Spielers für die Entwicklung der Welt.</summary>
    public class Weltwunsch
    {
        [XmlAttribute] public int Tag;
        [XmlText] public string Text = "";
    }

    public class OperatorStand
    {
        public int NaechsteId = 1;
        /// <summary>Spieltag der letzten Simulation (-1 = noch nie).</summary>
        public int LetzteSimulation = -1;
        public List<Weltereignis> Ereignisse = new List<Weltereignis>();
        public List<Weltwunsch> Wuensche = new List<Weltwunsch>();
    }

    public static class GameOperator
    {
        public const string Dateiname = "Figuren_Weltereignisse.xml";
        static readonly XmlSerializer Serializer = new XmlSerializer(typeof(OperatorStand));

        // ---------- Status ----------
        public const string Vorschlag = "Vorschlag";
        public const string InPlanung = "In Planung";
        public const string Freigegeben = "Freigegeben";
        public const string Aktiv = "Aktiv";
        public const string Abgeschlossen = "Abgeschlossen";
        public const string Abgebrochen = "Abgebrochen";
        public const string Zurueckgestellt = "Zurückgestellt";
        public static readonly string[] AlleStatus = { Vorschlag, InPlanung, Freigegeben, Aktiv, Abgeschlossen, Abgebrochen, Zurueckgestellt };

        /// <summary>Ansichten im Fenster "Ereignisse".</summary>
        public static readonly string[] Filter = { "Offen", Vorschlag, InPlanung, Freigegeben, Aktiv, Abgeschlossen, Zurueckgestellt, Abgebrochen, "Ketten", "Wünsche" };

        public static OperatorStand Stand = new OperatorStand();

        // ---------- Laden und Speichern ----------

        public static void Laden(string ordner)
        {
            Stand = new OperatorStand();
            if (string.IsNullOrEmpty(ordner)) return;
            string pfad = Path.Combine(ordner, Dateiname);
            try
            {
                if (File.Exists(pfad))
                    using (var s = File.OpenRead(pfad)) Stand = (OperatorStand)Serializer.Deserialize(s);
                if (Stand == null) Stand = new OperatorStand();
                int hoechste = Stand.Ereignisse.Count == 0 ? 0 : Stand.Ereignisse.Max(w => w.Id);
                if (Stand.NaechsteId <= hoechste) Stand.NaechsteId = hoechste + 1;
                Log.Out("[Figuren] Operator: " + Stand.Ereignisse.Count + " Ereignisse, " + Stand.Wuensche.Count + " Wünsche geladen.");
            }
            catch (Exception e)
            {
                Log.Warning("[Figuren] Weltereignisse nicht lesbar, starte leer (Kopie als .defekt): " + e.Message);
                try { if (File.Exists(pfad)) File.Copy(pfad, pfad + ".defekt", true); } catch (Exception) { }
                Stand = new OperatorStand();
            }
        }

        public static void Speichern(string ordner)
        {
            if (string.IsNullOrEmpty(ordner) || Stand == null) return;
            try
            {
                string pfad = Path.Combine(ordner, Dateiname);
                using (var s = File.Create(pfad + ".tmp")) Serializer.Serialize(s, Stand);
                if (File.Exists(pfad)) File.Copy(pfad, pfad + ".bak", true);
                File.Copy(pfad + ".tmp", pfad, true);
                File.Delete(pfad + ".tmp");
            }
            catch (Exception e) { Log.Warning("[Figuren] Weltereignisse nicht gespeichert: " + e.Message); }
        }

        public static int Heute()
        {
            try
            {
                var w = GameManager.Instance == null ? null : GameManager.Instance.World;
                return w == null ? 0 : GameUtils.WorldTimeToDays(w.worldTime);
            }
            catch (Exception) { return 0; }
        }

        // ---------- Ereignisse verwalten ----------

        public static Weltereignis Holen(int id)
        {
            return Stand.Ereignisse.FirstOrDefault(w => w.Id == id);
        }

        static Weltereignis Anlegen(string titel)
        {
            var e = new Weltereignis { Id = Stand.NaechsteId++, Titel = Kurz(titel, 120), Erstellt = Heute() };
            e.Geaendert = e.Erstellt;
            e.Verlauf.Add("Tag " + e.Erstellt + ": als Vorschlag angelegt");
            Stand.Ereignisse.Add(e);
            return e;
        }

        public static bool IstTatsache(Weltereignis e) { return e.Status == Aktiv || e.Status == Abgeschlossen; }
        public static bool IstOffen(Weltereignis e) { return e.Status != Abgeschlossen && e.Status != Abgebrochen; }

        /// <summary>Status ändern (nur durch den Spieler). Abgeschlossene Ereignisse bleiben, wie sie sind.</summary>
        public static string StatusSetzen(int id, string status, string ergebnis = "")
        {
            var e = Holen(id);
            if (e == null) return "Ereignis #" + id + " gibt es nicht.";
            if (!AlleStatus.Contains(status)) return "Unbekannter Status.";
            if (e.Status == Abgeschlossen) return "#" + id + " ist abgeschlossen und gehört zur Geschichte. Abgeschlossenes wird nicht mehr geändert.";
            if (e.Status == status) return "#" + id + " ist schon " + status + ".";
            string alt = e.Status;
            e.Status = status;
            e.Geaendert = Heute();
            ergebnis = (ergebnis ?? "").Trim();
            if (status == Abgeschlossen)
                e.Ergebnis = ergebnis.Length > 0 ? Kurz(ergebnis, 600) : (e.Plan.Length > 0 ? "Eingetreten wie geplant." : "Eingetreten wie beschrieben.");
            e.Verlauf.Add("Tag " + e.Geaendert + ": " + alt + " -> " + status + (ergebnis.Length > 0 ? " (" + Kurz(ergebnis, 200) + ")" : ""));
            Ki.Protokoll(Ki.ErzaehlerName, "Ereignis", "#" + id + " " + e.Titel + ": " + alt + " -> " + status + (ergebnis.Length > 0 ? " | " + ergebnis : ""));
            string text = "#" + id + " " + e.Titel + ": jetzt " + status + ".";
            if (status == Aktiv) text += " Das Ereignis läuft jetzt in der Welt (Tatsache). Ist es vorbei, auf Fertig drücken.";
            else if (status == Abgeschlossen) text += " Es gehört jetzt zur Geschichte der Welt.";
            else if (status == Freigegeben) text += " Es soll eintreten, gilt aber erst als Tatsache, wenn du es auf Aktiv setzt.";
            else if (status == InPlanung) text += " Nur ein Plan, keine Tatsache.";
            return text;
        }

        public static string WunschAnlegen(string text)
        {
            text = (text ?? "").Replace('\n', ' ').Trim();
            if (text.Length == 0) return "Welchen Wunsch? Beispiel: /wunsch Springfield soll wirtschaftlich stärker wachsen.";
            if (Stand.Wuensche.Count >= 30) return "Schon 30 Wünsche. Lösche zuerst einen mit /wunschweg <Nummer>.";
            if (Stand.Wuensche.Any(w => string.Equals(w.Text, text, StringComparison.OrdinalIgnoreCase))) return "Diesen Wunsch gibt es schon.";
            Stand.Wuensche.Add(new Weltwunsch { Tag = Heute(), Text = Kurz(text, 300) });
            return "Wunsch " + Stand.Wuensche.Count + " gespeichert: " + text;
        }

        public static string WunschLoeschen(string nummer)
        {
            int n;
            if (!int.TryParse((nummer ?? "").Trim(), out n) || n < 1 || n > Stand.Wuensche.Count) return "Nummer fehlt oder stimmt nicht (Ansicht Wünsche).";
            string alt = Stand.Wuensche[n - 1].Text;
            Stand.Wuensche.RemoveAt(n - 1);
            return "Wunsch " + n + " gelöscht: " + alt;
        }

        // ---------- Auftrag an den Operator ----------

        /// <summary>Art der laufenden Operator-Anfrage (für das Auswerten der Antwort).</summary>
        class Auftrag
        {
            public string Art = "";
            public int Bezug;
        }
        static Auftrag laufend;

        /// <summary>Anfrage ist fehlgeschlagen: die nächste Antwort ist wieder ein normales Gespräch.</summary>
        public static void AuftragVerwerfen() { laufend = null; }

        /// <summary>Startet eine Operator-Anfrage. art: "szenarien", "welt", "folgen" (bezug = Ereignis) oder "plan" (bezug = Ereignis).
        /// Gibt eine Meldung zurück; fertig wird nach der Antwort im Spiel-Thread aufgerufen.</summary>
        public static string Starten(string art, string thema, int bezug, string spieler, Action<string> fertig)
        {
            if (Ki.Wartet(Ki.ErzaehlerName)) return "Der Operator arbeitet noch an der letzten Antwort.";
            thema = (thema ?? "").Trim();
            Weltereignis e = bezug > 0 ? Holen(bezug) : null;
            string frage, auftrag;
            int tokens;
            if (art == "plan")
            {
                if (e == null) return "Erst ein Ereignis auswählen.";
                frage = "Ich wähle #" + e.Id + " (" + e.Titel + "). Bitte arbeite das als Planung aus." + (thema.Length > 0 ? " " + thema : "");
                auftrag = PlanAuftrag(e);
                tokens = 900;
            }
            else if (art == "folgen")
            {
                if (e == null) return "Erst ein Ereignis auswählen.";
                frage = "Wie könnte es nach #" + e.Id + " (" + e.Titel + ") weitergehen?" + (thema.Length > 0 ? " " + thema : "");
                auftrag = FolgenAuftrag(e);
                tokens = 1500;
            }
            else if (art == "welt")
            {
                frage = "Wie entwickelt sich die Welt gerade ohne mich?" + (thema.Length > 0 ? " Schwerpunkt: " + thema : "");
                auftrag = WeltAuftrag(thema);
                tokens = 1300;
            }
            else
            {
                art = "szenarien";
                frage = "Neue Ereignisse simulieren." + (thema.Length > 0 ? " Thema: " + thema : "");
                auftrag = SzenarienAuftrag(thema);
                tokens = 1500;
            }
            laufend = new Auftrag { Art = art, Bezug = bezug };
            if (art != "plan") Stand.LetzteSimulation = Heute();
            Ki.Fragen(Ki.Erzaehler, spieler, frage, fertig, false, auftrag, tokens);
            if (!Ki.Wartet(Ki.ErzaehlerName)) { laufend = null; return "Konnte die Anfrage nicht starten."; }
            return art == "plan" ? "Der Operator arbeitet den Plan für #" + e.Id + " aus ..." : "Der Operator simuliert ...";
        }

        const string Format =
@"Schreibe jedes Szenario genau in diesem Format (ohne Markdown, ohne Sternchen):
[SZENARIO]
Titel: kurzer, aussagekräftiger Titel
Art: Wirtschaft, Politik, Gesellschaft, Konflikt, Persönlich, Geschäftsmöglichkeit, Sicherheit, Fraktionen, Unerwartet oder Machtverschiebung
Beschreibung: was sich entwickeln könnte (zwei bis drei Sätze)
Warum: warum es entstehen könnte, mit Bezug auf bekannte Fakten
Beteiligte: Personen, Firmen, Städte, Fraktionen
Auswirkungen: Auswirkungen auf die Spielwelt
Wahrscheinlichkeit: Sehr wahrscheinlich, Wahrscheinlich, Denkbar oder Eher unwahrscheinlich, dazu eine Schätzung (ca. 40 %)
Folgen für den Spieler: Chancen, Risiken und mögliche Entscheidungen
[/SZENARIO]
Vor den Szenarien höchstens ein kurzer Satz, danach nichts mehr. Die Prozentzahlen sind Simulationseinschätzungen, keine Berechnungen.";

        static string SzenarienAuftrag(string thema)
        {
            return "AUFTRAG FÜR DIESE ANTWORT: Neue Ereignisse simulieren" + (thema.Length > 0 ? " (Schwerpunkt des Spielers: " + thema + ")" : "") + ".\n"
                + "Analysiere Weltzustand, Chronik, aktive und geplante Ereignisse, Spielerwünsche und den bisherigen Verlauf, besonders die letzten Handlungen des Spielers. "
                + "Entwirf mindestens DREI deutlich unterschiedliche Szenarien, also nicht dasselbe Ereignis in drei Varianten. Mische die Arten (zum Beispiel ein wirtschaftliches, ein politisches oder gesellschaftliches und ein unerwartetes oder persönliches). "
                + "Greife offene Handlungsstränge und Ereignisketten auf, wenn es passt. Beteiligte nur aus dem Weltzustand und der Geschichte; neue Akteure kennzeichnest du mit (neu) und begründest sie. "
                + "Ein Szenario ist nur wahrscheinlich, wenn die Voraussetzungen (Kapital, Kontakte, Logistik, Macht) erkennbar da sind.\n" + Format;
        }

        static string WeltAuftrag(string thema)
        {
            return "AUFTRAG FÜR DIESE ANTWORT: Eigenständige Weltentwicklung" + (thema.Length > 0 ? " (Schwerpunkt: " + thema + ")" : "") + ".\n"
                + "Entwirf ZWEI bis DREI Entwicklungen, die unabhängig vom Spieler in der Welt entstehen könnten: Firmen, Städte, Fraktionen, Figuren, Handel, Konflikte. "
                + "Nichts Willkürliches: jede Entwicklung muss aus Weltzustand, Weltgeschichte und bisherigen Ereignissen folgen. Keine großen Umwälzungen ohne Vorgeschichte. "
                + "Bekannte Machtverhältnisse beachten; Charaktere handeln nach ihrem bekannten Profil.\n" + Format;
        }

        static string FolgenAuftrag(Weltereignis e)
        {
            return "AUFTRAG FÜR DIESE ANTWORT: Ereigniskette fortsetzen. Ausgangspunkt ist Ereignis #" + e.Id + " \"" + e.Titel + "\" (Status: " + e.Status + ").\n"
                + "Beschreibung: " + e.Beschreibung + (e.Plan.Length > 0 ? "\nPlan: " + e.Plan : "") + (e.Ergebnis.Length > 0 ? "\nTatsächliches Ergebnis: " + e.Ergebnis : "") + "\n"
                + (IstTatsache(e) ? "" : "Achtung: #" + e.Id + " ist noch KEINE Tatsache. Die Folgeereignisse setzen voraus, dass es eintritt, und sind ebenfalls nur Vorschläge.\n")
                + "Entwirf mindestens DREI unterschiedliche mögliche Folgeereignisse, die logisch daraus entstehen (Reaktionen anderer Akteure, Konkurrenz, Chancen, Konflikte, längerfristige Veränderungen). Frühere Ereignisse der Kette und abgeschlossene Ereignisse bleiben unverändert gültig.\n"
                + Format;
        }

        static string PlanAuftrag(Weltereignis e)
        {
            return "AUFTRAG FÜR DIESE ANTWORT: Der Spieler hat Ereignis #" + e.Id + " \"" + e.Titel + "\" ausgewählt. Es ist jetzt In Planung, also nur ein Plan, nichts davon ist schon passiert.\n"
                + "Beschreibung: " + e.Beschreibung + (e.Plan.Length > 0 ? "\nBisheriger Plan: " + e.Plan : "") + "\n"
                + "Arbeite daraus eine Ereigniskette in drei bis sechs kurzen, nummerierten Schritten aus (je ein Satz: was zuerst geschieht, was daraus folgt, wo der Spieler entscheidet). Halte es knapp. "
                + "Schreibe danach den Plan in eine eigene Zeile genau so: [PLAN #" + e.Id + ": 1. ... 2. ... 3. ...]\n"
                + "Frag am Ende kurz, ob der Spieler etwas ändern möchte. Ändert er später etwas, schreibst du den angepassten Plan wieder mit [PLAN #" + e.Id + ": ...].";
        }

        // ---------- Antwort des Operators auswerten ----------

        static readonly Regex SzenarioMuster = new Regex(@"\[\s*SZENARIO\s*\](.*?)(?:\[\s*/\s*SZENARIO\s*\]|(?=\[\s*SZENARIO\s*\])|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        static readonly Regex PlanMuster = new Regex(@"\[\s*PLAN\s*#?\s*(\d+)\s*:\s*(.*?)\]", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        static readonly Regex VorschlagMuster = new Regex(@"\[\s*VORSCHLAG\s*:\s*(.*?)\]", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        static readonly Regex WunschMuster = new Regex(@"\[\s*WUNSCH\s*:\s*(.*?)\]", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        /// <summary>Liest Szenarien und Markierungen aus der Antwort, speichert sie und gibt den Text für den Verlauf zurück.</summary>
        public static string Auswerten(string antwort)
        {
            var a = laufend;
            laufend = null;
            if (string.IsNullOrEmpty(antwort)) return antwort ?? "";
            string text = antwort;
            var neue = new List<Weltereignis>();

            // 1) Szenarien
            var treffer = SzenarioMuster.Matches(text);
            if (treffer.Count > 0)
            {
                Weltereignis basis = a != null && a.Bezug > 0 && a.Art == "folgen" ? Holen(a.Bezug) : null;
                string kette = "";
                if (basis != null)
                {
                    if (basis.Kette.Length == 0) basis.Kette = "Kette " + basis.Id + ": " + Kurz(basis.Titel, 40);
                    kette = basis.Kette;
                }
                foreach (Match mt in treffer)
                {
                    var e = SzenarioLesen(mt.Groups[1].Value);
                    if (e == null) continue;
                    e.Kette = kette;
                    e.Vorgaenger = basis != null ? basis.Id : 0;
                    e.Hintergrund = a != null && a.Art == "welt";
                    neue.Add(e);
                }
                text = SzenarioMuster.Replace(text, "").Trim();
                text = Regex.Replace(text, @"\[\s*/\s*SZENARIO\s*\]", "", RegexOptions.IgnoreCase).Trim();
            }

            // 2) Plan zu einem bestehenden Ereignis
            bool planDa = false;
            text = PlanMuster.Replace(text, m =>
            {
                planDa = true;
                int id;
                if (!int.TryParse(m.Groups[1].Value, out id)) return "";
                var e = Holen(id);
                string plan = Einzeilig(m.Groups[2].Value);
                if (e == null || plan.Length == 0) return "";
                if (e.Status == Abgeschlossen || e.Status == Abgebrochen) return "(Plan für #" + id + " nicht gespeichert: das Ereignis ist " + e.Status + ".)";
                e.Plan = Kurz(plan, 1200);
                e.Geaendert = Heute();
                e.Verlauf.Add("Tag " + e.Geaendert + ": Plan " + (e.Verlauf.Any(v => v.Contains(": Plan ")) ? "angepasst" : "ausgearbeitet"));
                if (e.Status == Vorschlag || e.Status == Zurueckgestellt) e.Status = InPlanung;
                return "(Plan für #" + id + " gespeichert, Status: " + e.Status + ".)";
            });

            // 3) Einzelner Vorschlag aus dem Gespräch
            text = VorschlagMuster.Replace(text, m =>
            {
                var teile = m.Groups[1].Value.Split('|').Select(x => Einzeilig(x)).ToArray();
                if (teile.Length == 0 || teile[0].Length == 0) return "";
                var e = Anlegen(teile[0]);
                if (teile.Length > 1) e.Beschreibung = Kurz(teile[1], 800);
                if (teile.Length > 2) e.Wahrscheinlichkeit = Kurz(teile[2], 80);
                if (a != null && a.Bezug > 0) { var b = Holen(a.Bezug); if (b != null) { e.Vorgaenger = b.Id; e.Kette = b.Kette; } }
                return "(Als Vorschlag #" + e.Id + " vorgemerkt: " + e.Titel + ")";
            });

            // 4) Spielerwunsch
            text = WunschMuster.Replace(text, m =>
            {
                string w = Einzeilig(m.Groups[1].Value);
                if (w.Length == 0) return "";
                string r = WunschAnlegen(w);
                return r.StartsWith("Wunsch ") ? "(Wunsch für die Weltentwicklung gespeichert: " + Kurz(w, 120) + ")" : "";
            });

            text = Regex.Replace(text, @"\n{3,}", "\n\n").Trim();
            // Planung ohne Markierung: die Antwort selbst wird der Plan
            if (a != null && a.Art == "plan" && !planDa)
            {
                var pe = Holen(a.Bezug);
                if (pe != null && IstOffen(pe) && text.Length > 0)
                {
                    pe.Plan = Kurz(text, 1200);
                    pe.Geaendert = Heute();
                    pe.Verlauf.Add("Tag " + pe.Geaendert + ": Plan ausgearbeitet");
                    text += "\n(Plan für #" + pe.Id + " gespeichert.)";
                }
            }
            if (neue.Count > 0)
            {
                var sb = new StringBuilder();
                if (text.Length > 0 && text.Length < 300) sb.AppendLine(text).AppendLine();
                sb.Append(neue.Count == 1 ? "Neues Szenario" : neue.Count + " neue Szenarien").Append(a != null && a.Art == "welt" ? " (Weltentwicklung)" : "")
                  .AppendLine(", als Vorschlag gespeichert:");
                foreach (var e in neue)
                    sb.Append("#").Append(e.Id).Append(" ").Append(e.Titel)
                      .Append(e.Wahrscheinlichkeit.Length > 0 ? " (" + Kurz(e.Wahrscheinlichkeit, 40) + ")" : "")
                      .Append(": ").AppendLine(Kurz(e.Beschreibung, 200));
                sb.Append("Details, Auswahl und Planung: Knopf Ereignisse.");
                text = sb.ToString();
            }
            else if (a != null && (a.Art == "szenarien" || a.Art == "welt" || a.Art == "folgen"))
                text += "\n\n(Ich konnte in der Antwort keine Szenarien im verabredeten Format erkennen, es wurde nichts gespeichert. Einfach nochmal auf Szenarien drücken.)";
            if (neue.Count > 0) Log.Out("[Figuren] Operator: " + neue.Count + " Szenarien gespeichert (#" + neue[0].Id + " bis #" + neue[neue.Count - 1].Id + ").");
            return text.Trim();
        }

        /// <summary>Ein Block "Titel: ... / Beschreibung: ..." wird zu einem Ereignis (Status Vorschlag).</summary>
        static Weltereignis SzenarioLesen(string block)
        {
            var felder = new Dictionary<string, string>();
            string schluessel = null;
            foreach (var roh in (block ?? "").Replace("\r", "").Split('\n'))
            {
                string z = Regex.Replace(roh.Trim().TrimStart('-', '•', '*').Trim(), @"^\d+[.)]\s*", "");
                if (z.Length == 0) continue;
                var m = Regex.Match(z, @"^([A-Za-zÄÖÜäöüß ]{3,30})\s*:\s*(.*)$");
                string k = m.Success ? Feldname(m.Groups[1].Value) : null;
                if (k != null)
                {
                    schluessel = k;
                    felder[k] = (felder.ContainsKey(k) ? felder[k] + " " : "") + m.Groups[2].Value.Trim();
                }
                else if (schluessel != null) felder[schluessel] = felder[schluessel] + " " + z;
            }
            string titel;
            if (!felder.TryGetValue("titel", out titel) || titel.Trim().Length == 0) return null;
            var e = Anlegen(titel.Trim());
            string w;
            if (felder.TryGetValue("art", out w)) e.Art = Kurz(w, 60);
            if (felder.TryGetValue("beschreibung", out w)) e.Beschreibung = Kurz(w, 800);
            if (felder.TryGetValue("warum", out w)) e.Warum = Kurz(w, 600);
            if (felder.TryGetValue("beteiligte", out w)) e.Beteiligte = Kurz(w, 300);
            if (felder.TryGetValue("auswirkungen", out w)) e.Auswirkungen = Kurz(w, 600);
            if (felder.TryGetValue("wahrscheinlichkeit", out w)) e.Wahrscheinlichkeit = Kurz(w, 80);
            if (felder.TryGetValue("folgen", out w)) e.Folgen = Kurz(w, 600);
            return e;
        }

        static string Feldname(string roh)
        {
            string k = roh.Trim().ToLowerInvariant();
            if (k == "titel" || k == "name") return "titel";
            if (k == "art" || k == "typ" || k == "kategorie") return "art";
            if (k == "beschreibung" || k == "entwicklung") return "beschreibung";
            if (k == "warum" || k == "grund" || k == "ursache" || k.StartsWith("warum ")) return "warum";
            if (k == "beteiligte" || k.StartsWith("beteiligt")) return "beteiligte";
            if (k.StartsWith("auswirkung")) return "auswirkungen";
            if (k.StartsWith("wahrscheinlich")) return "wahrscheinlichkeit";
            if (k.StartsWith("folgen") || k.StartsWith("konsequenz")) return "folgen";
            return null;
        }

        static string Einzeilig(string t)
        {
            return Regex.Replace((t ?? "").Replace("\r", " ").Replace("\n", " "), @"\s{2,}", " ").Trim();
        }

        public static string Kurz(string t, int max)
        {
            t = Einzeilig(t);
            return t.Length <= max ? t : t.Substring(0, max).TrimEnd() + " ...";
        }

        // ---------- Kontext für den Operator ----------

        /// <summary>Ereignisse, Ketten und Wünsche für den Systemtext des Operators. Tatsachen und Pläne klar getrennt.</summary>
        public static string Ereignistext()
        {
            var sb = new StringBuilder();
            var l = Stand.Ereignisse;
            var fertig = l.Where(w => w.Status == Abgeschlossen).OrderBy(w => w.Geaendert).ThenBy(w => w.Id).ToList();
            if (fertig.Count > 0)
            {
                sb.AppendLine("CHRONIK - abgeschlossene Ereignisse (TATSACHEN, nie umdeuten):");
                if (fertig.Count > 20) sb.AppendLine("- (" + (fertig.Count - 20) + " ältere Ereignisse, gelten weiter)");
                foreach (var e in fertig.Skip(Math.Max(0, fertig.Count - 20)))
                    sb.Append("- #").Append(e.Id).Append(" Tag ").Append(e.Geaendert).Append(": ").Append(e.Titel).Append(". ").AppendLine(Kurz(e.Ergebnis.Length > 0 ? e.Ergebnis : e.Beschreibung, 220));
            }
            Gruppe(sb, l, Aktiv, "AKTIV - läuft gerade in der Welt (TATSACHE, Ausgang offen):", 10, true);
            Gruppe(sb, l, Freigegeben, "FREIGEGEBEN - soll demnächst eintreten, ist aber NOCH NICHT passiert:", 10, true);
            Gruppe(sb, l, InPlanung, "IN PLANUNG - nur Pläne, KEINE Tatsachen:", 10, true);
            Gruppe(sb, l, Vorschlag, "VORSCHLÄGE - nur Ideen, KEINE Tatsachen:", 12, false);
            Gruppe(sb, l, Zurueckgestellt, "ZURÜCKGESTELLT - vielleicht später, KEINE Tatsachen:", 6, false);
            var weg = l.Where(w => w.Status == Abgebrochen).ToList();
            if (weg.Count > 0)
                sb.Append("ABGEBROCHEN (tritt nicht ein): ").AppendLine(string.Join("; ", weg.Skip(Math.Max(0, weg.Count - 10)).Select(w => "#" + w.Id + " " + w.Titel).ToArray()));
            var ketten = Ketten();
            if (ketten.Count > 0)
            {
                sb.AppendLine("EREIGNISKETTEN:");
                foreach (var k in ketten.Take(8))
                    sb.Append("- ").Append(k.Key).Append(": ").AppendLine(string.Join(" -> ", k.Value.Select(w => "#" + w.Id + " (" + w.Status + ")").ToArray()));
            }
            if (Stand.Wuensche.Count > 0)
            {
                sb.AppendLine("WÜNSCHE DES SPIELERS für die Zukunft der Welt (bei Simulationen berücksichtigen, aber nur plausibel, nicht erzwingen):");
                foreach (var wu in Stand.Wuensche) sb.Append("- ").AppendLine(wu.Text);
            }
            return sb.ToString().Trim();
        }

        static void Gruppe(StringBuilder sb, List<Weltereignis> l, string status, string kopf, int max, bool mitPlan)
        {
            var g = l.Where(w => w.Status == status).OrderBy(w => w.Id).ToList();
            if (g.Count == 0) return;
            sb.AppendLine(kopf);
            foreach (var e in g.Skip(Math.Max(0, g.Count - max)))
            {
                sb.Append("- #").Append(e.Id).Append(" ").Append(e.Titel);
                if (e.Wahrscheinlichkeit.Length > 0 && !IstTatsache(e)) sb.Append(" (").Append(Kurz(e.Wahrscheinlichkeit, 40)).Append(")");
                sb.Append(": ").Append(Kurz(e.Beschreibung, 180));
                if (mitPlan && e.Plan.Length > 0) sb.Append(" | Plan: ").Append(Kurz(e.Plan, 300));
                if (e.Beteiligte.Length > 0) sb.Append(" | Beteiligte: ").Append(Kurz(e.Beteiligte, 100));
                sb.AppendLine();
            }
        }

        /// <summary>Ketten: Name -> Ereignisse der Kette in Reihenfolge der Nummern.</summary>
        public static List<KeyValuePair<string, List<Weltereignis>>> Ketten()
        {
            return Stand.Ereignisse.Where(w => w.Kette.Length > 0)
                .GroupBy(w => w.Kette)
                .Select(g => new KeyValuePair<string, List<Weltereignis>>(g.Key, g.OrderBy(w => w.Id).ToList()))
                .OrderByDescending(k => k.Value.Max(w => w.Geaendert))
                .ToList();
        }

        /// <summary>Für alle anderen KI-Figuren: nur Tatsachen (aktive und abgeschlossene Ereignisse), nie Pläne.</summary>
        public static string TatsachenFuerFiguren()
        {
            var t = Stand.Ereignisse.Where(w => IstTatsache(w)).OrderBy(w => w.Geaendert).ThenBy(w => w.Id).ToList();
            if (t.Count == 0) return "";
            var sb = new StringBuilder();
            foreach (var e in t.Skip(Math.Max(0, t.Count - 8)))
                sb.Append("- ").Append(e.Titel).Append(e.Status == Aktiv ? " (läuft gerade)" : "").Append(": ")
                  .AppendLine(Kurz(e.Status == Abgeschlossen && e.Ergebnis.Length > 0 ? e.Ergebnis : e.Beschreibung, 160));
            return sb.ToString().Trim();
        }

        /// <summary>Weltzustand direkt aus dem Spiel: Figuren und Rollen, Firmen, Banken, Regierungen, Personen.</summary>
        public static string Weltzustand()
        {
            var sb = new StringBuilder();
            sb.Append("Heute ist Spieltag ").Append(Heute()).AppendLine(".");
            try
            {
                var leute = Rollen.Personen().Where(r => r.IstKi).OrderByDescending(r => r.Rang).ThenBy(r => r.Name).ToList();
                var feste = Rollen.Personen().Where(r => !r.IstKi && !string.IsNullOrEmpty(r.VorlageName))
                    .GroupBy(r => r.VorlageName + (string.IsNullOrEmpty(r.Stadt) ? "" : " in " + r.Stadt))
                    .Select(g => g.Count() + " " + g.Key).ToList();
                if (leute.Count > 0)
                {
                    sb.AppendLine("Figuren und Rollen in der Welt:");
                    int n = 0;
                    foreach (var ro in leute)
                    {
                        if (++n > 40) { sb.AppendLine("- ... und " + (leute.Count - 40) + " weitere Figuren"); break; }
                        sb.Append("- ").Append(ro.Name);
                        if (ro.Titel.Length > 0) sb.Append(", ").Append(ro.Titel);
                        if (!string.IsNullOrEmpty(ro.Stadt) && ro.Titel.IndexOf(ro.Stadt, StringComparison.OrdinalIgnoreCase) < 0) sb.Append(" (").Append(ro.Stadt).Append(")");
                        if (!ro.Vorlage && string.IsNullOrEmpty(ro.VorlageName)) sb.Append(" [").Append(ro.Art).Append("]");
                        if (ro.FirmaFelder || !string.IsNullOrEmpty(ro.FirmaName))
                        {
                            string vn = ro.VorlageName;
                            var vorlage = Rollen.Liste.FirstOrDefault(v => v.Vorlage && string.Equals(v.Name, vn, StringComparison.OrdinalIgnoreCase));
                            sb.Append(" [Firma: ").Append(string.IsNullOrEmpty(ro.FirmaName) ? "Name noch offen" : ro.FirmaName)
                              .Append(", Branche ").Append(Rollen.BrancheText(vorlage, ro.Branche))
                              .Append(string.IsNullOrEmpty(ro.Groesse) ? "" : ", Größe " + Rollen.GroessenAnzeige[Rollen.GroesseIndex(ro.Groesse)]).Append("]");
                        }
                        sb.AppendLine();
                    }
                }
                if (feste.Count > 0) sb.Append("Dazu feste Figuren ohne KI: ").AppendLine(string.Join(", ", feste.ToArray()) + ".");
                // Was die Figuren mit dem Spieler erlebt haben (Kurzfassung ihres Gedächtnisses)
                var mit = leute.Where(r => r.IstKi).Select(r => new { r.Name, G = Ki.GedaechtnisVon(r.Name) }).Where(x => !string.IsNullOrEmpty(x.G)).Take(10).ToList();
                if (mit.Count > 0)
                {
                    sb.AppendLine("Aus den Erinnerungen der Figuren an den Spieler (deren Sicht, keine gesicherten Weltfakten):");
                    foreach (var xm in mit) sb.Append("- ").Append(xm.Name).Append(": ").AppendLine(Kurz(xm.G, 260));
                }
            }
            catch (Exception e) { Log.Warning("[Figuren] Operator-Weltzustand (Figuren): " + e.Message); }
            string wi = Wirtschaftslage();
            if (wi.Length > 0) sb.AppendLine(wi);
            return sb.ToString().Trim();
        }

        // ---------- Wirtschaft-Mod (per Reflection, ohne feste Abhängigkeit) ----------

        static object Wert(object o, string name)
        {
            if (o == null) return null;
            var t = o.GetType();
            var f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (f != null) return f.GetValue(o);
            var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            return p != null && p.GetIndexParameters().Length == 0 ? p.GetValue(o, null) : null;
        }

        static List<object> Reihe(object o)
        {
            var l = new List<object>();
            var e = o as IEnumerable;
            if (e == null || o is string) return l;
            foreach (var x in e) if (x != null) l.Add(x);
            return l;
        }

        static string Txt(object o, string name) { var v = Wert(o, name); return v == null ? "" : Convert.ToString(v, CultureInfo.InvariantCulture); }

        static decimal Zahl(object o, string name)
        {
            try { var v = Wert(o, name); return v == null ? 0m : Convert.ToDecimal(v, CultureInfo.InvariantCulture); }
            catch (Exception) { return 0m; }
        }

        static string Betrag(decimal d) { return Math.Round(d).ToString("N0", CultureInfo.InvariantCulture).Replace(",", ".") + " Coins"; }

        static string Wirtschaftslage()
        {
            object st = WirtschaftBruecke.StandHolen();
            if (st == null) return "";
            var sb = new StringBuilder();
            try
            {
                var banken = Reihe(Wert(st, "Banken"));
                var firmen = Reihe(Wert(st, "Unternehmen"));
                var parteien = Reihe(Wert(st, "Parteien"));
                if (banken.Count > 0)
                {
                    sb.AppendLine("Banken (je Stadt) und ihre Regierungen (Wirtschaft-Mod):");
                    foreach (var bk in banken)
                    {
                        string regName = Txt(bk, "Regierung");
                        string bankName = Txt(bk, "Name");
                        int fz = firmen.Count(f => string.Equals(Txt(f, "Bank"), bankName, StringComparison.OrdinalIgnoreCase));
                        sb.Append("- ").Append(bankName).Append(", Regierung: ").Append(regName.Length > 0 ? regName : "keine").Append(", ").Append(fz).AppendLine(" Firmen");
                    }
                }
                if (firmen.Count > 0)
                {
                    sb.AppendLine("Firmen (Bank, Branche, Besitzer, Umsatz pro Monat, Mitarbeiter, Guthaben):");
                    var sortiert = firmen.OrderByDescending(f => Zahl(f, "UmsatzMonat")).ToList();
                    int n = 0;
                    foreach (var fi in sortiert)
                    {
                        if (++n > 40) { sb.AppendLine("- ... und " + (sortiert.Count - 40) + " kleinere Firmen"); break; }
                        var laeuft = Wert(fi, "Laeuft");
                        sb.Append("- ").Append(Txt(fi, "Name")).Append(" (").Append(Txt(fi, "Bank")).Append(", ").Append(Txt(fi, "Branche"))
                          .Append(", Besitzer ").Append(Txt(fi, "Besitzer").Length > 0 ? Txt(fi, "Besitzer") : "-")
                          .Append(", Umsatz ").Append(Betrag(Zahl(fi, "UmsatzMonat"))).Append(", ").Append(Txt(fi, "Mitarbeiter")).Append(" MA, Guthaben ").Append(Betrag(Zahl(fi, "Konto")))
                          .Append(laeuft is bool && !(bool)laeuft ? ", ruht" : "").AppendLine(")");
                    }
                }
                if (parteien.Count > 0)
                {
                    var reg = new HashSet<string>(banken.Select(b => Txt(b, "Regierung")), StringComparer.OrdinalIgnoreCase);
                    var pers = parteien.Where(p => !reg.Contains(Txt(p, "Name"))).OrderByDescending(p => Zahl(p, "Konto")).Take(20).ToList();
                    if (pers.Count > 0)
                        sb.Append("Personen mit Konto: ").AppendLine(string.Join("; ", pers.Select(p => Txt(p, "Name") + " " + Betrag(Zahl(p, "Konto"))).ToArray()));
                }
            }
            catch (Exception e) { Log.Warning("[Figuren] Operator-Weltzustand (Wirtschaft): " + e.Message); }
            // Erweiterte Unternehmensverwaltung (Personal, Lieferanten, Verträge, Lager, Lieferungen, Projekte) mit Regeln
            string abm = WirtschaftBruecke.AbmBericht("");
            if (abm.Length > 0) sb.AppendLine(abm);
            return sb.ToString().Trim();
        }

        // ---------- Anzeige im Fenster "Ereignisse" ----------

        /// <summary>Ereignisse einer Ansicht (Filter-Index). Ketten und Wünsche haben eigene Ansichten.</summary>
        public static List<Weltereignis> Auswahl(int filter)
        {
            string f = Filter[Math.Max(0, Math.Min(Filter.Length - 1, filter))];
            if (f == "Offen") return Stand.Ereignisse.Where(w => w.Status != Abgeschlossen && w.Status != Abgebrochen).OrderBy(w => StatusRang(w.Status)).ThenByDescending(w => w.Id).ToList();
            if (f == "Ketten") return Ketten().SelectMany(k => k.Value).ToList();
            if (f == "Wünsche") return new List<Weltereignis>();
            return Stand.Ereignisse.Where(w => w.Status == f).OrderByDescending(w => w.Id).ToList();
        }

        static int StatusRang(string s)
        {
            int i = Array.IndexOf(new[] { Aktiv, Freigegeben, InPlanung, Vorschlag, Zurueckgestellt }, s);
            return i < 0 ? 9 : i;
        }

        /// <summary>Liste mit Markierung der Auswahl und darunter die Details des gewählten Ereignisses.</summary>
        public static string Ansicht(int filter, int wahl, out int anzahl, out int gewaehlteId)
        {
            gewaehlteId = 0;
            var sb = new StringBuilder();
            string f = Filter[Math.Max(0, Math.Min(Filter.Length - 1, filter))];
            if (f == "Wünsche")
            {
                anzahl = Stand.Wuensche.Count;
                sb.AppendLine("WÜNSCHE FÜR DIE WELTENTWICKLUNG (der Operator berücksichtigt sie bei Simulationen)");
                sb.AppendLine();
                if (anzahl == 0) sb.AppendLine("Noch keine. Im Eingabefeld: /wunsch Springfield soll wirtschaftlich stärker wachsen");
                for (int i = 0; i < anzahl; i++) sb.Append(i + 1).Append(". ").AppendLine(Sicher(Stand.Wuensche[i].Text));
                sb.AppendLine().Append("Löschen: /wunschweg Nummer");
                return sb.ToString();
            }
            var l = Auswahl(filter);
            anzahl = l.Count;
            sb.Append("ANSICHT: ").Append(f.ToUpperInvariant()).Append("   (").Append(anzahl).AppendLine(anzahl == 1 ? " Ereignis)" : " Ereignisse)");
            if (anzahl == 0)
            {
                sb.AppendLine().AppendLine(Stand.Ereignisse.Count == 0
                    ? "Noch keine Ereignisse. Im Gespräch auf Szenarien oder Welt entwickeln drücken."
                    : "Hier ist gerade nichts. Mit Filter die Ansicht wechseln.");
                return sb.ToString();
            }
            wahl = Math.Max(0, Math.Min(anzahl - 1, wahl));
            int von = Math.Max(0, Math.Min(wahl - 2, anzahl - 6)), bis = Math.Min(anzahl, von + 6);
            string letzteKette = null;
            for (int i = von; i < bis; i++)
            {
                var e = l[i];
                if (f == "Ketten" && e.Kette != letzteKette) { sb.Append("  ").AppendLine(Sicher(e.Kette)); letzteKette = e.Kette; }
                sb.Append(i == wahl ? "> " : "   ").Append("#").Append(e.Id).Append(" ").Append(Sicher(Kurz(e.Titel, 60))).Append("  - ").AppendLine(e.Status);
            }
            if (anzahl > 6) sb.Append("   (").Append(wahl + 1).Append(" von ").Append(anzahl).AppendLine(", mit < > blättern)");
            var g = l[wahl];
            gewaehlteId = g.Id;
            sb.AppendLine();
            sb.AppendLine(Details(g));
            return sb.ToString();
        }

        public static string Details(Weltereignis e)
        {
            var sb = new StringBuilder();
            sb.Append("#").Append(e.Id).Append(" ").Append(Sicher(e.Titel)).Append("   [").Append(e.Status).Append(IstTatsache(e) ? ", Tatsache" : ", keine Tatsache").Append("]");
            if (e.Art.Length > 0) sb.Append("  Art: ").Append(Sicher(e.Art));
            if (e.Hintergrund) sb.Append("  (Weltentwicklung)");
            sb.AppendLine();
            if (e.Kette.Length > 0) sb.Append("Kette: ").Append(Sicher(e.Kette)).Append(e.Vorgaenger > 0 ? ", folgt aus #" + e.Vorgaenger : "").AppendLine();
            Feld(sb, "", e.Beschreibung, 300);
            Feld(sb, "Warum: ", e.Warum, 180);
            Feld(sb, "Beteiligte: ", e.Beteiligte, 120);
            Feld(sb, "Auswirkungen: ", e.Auswirkungen, 180);
            Feld(sb, "Wahrscheinlichkeit: ", e.Wahrscheinlichkeit, 60);
            Feld(sb, "Für dich: ", e.Folgen, 180);
            Feld(sb, "Plan: ", e.Plan, 300);
            Feld(sb, "Ergebnis: ", e.Ergebnis, 200);
            return sb.ToString().TrimEnd();
        }

        static void Feld(StringBuilder sb, string kopf, string wert, int max)
        {
            if (string.IsNullOrEmpty(wert)) return;
            sb.Append(kopf).AppendLine(Sicher(Kurz(wert, max)));
        }

        /// <summary>Eckige Klammern würden im Fenster als Farbcodes gelesen.</summary>
        static string Sicher(string t) { return (t ?? "").Replace('[', '(').Replace(']', ')'); }

        /// <summary>Hinweis in der Statuszeile, wenn länger nichts simuliert wurde.</summary>
        public static string Erinnerung()
        {
            int heute = Heute();
            if (Stand.LetzteSimulation < 0) return Stand.Ereignisse.Count == 0 ? "Tipp: Szenarien drücken, dann schlägt der Operator mögliche Ereignisse vor." : "";
            int tage = heute - Stand.LetzteSimulation;
            return tage >= 7 ? "Seit " + tage + " Spieltagen keine Simulation. Szenarien oder Welt entwickeln?" : "";
        }

        // ---------- Befehle im Eingabefeld (nur beim Operator) ----------

        public const string Hilfe =
            "OPERATOR-BEFEHLE (ins Eingabefeld tippen):\n" +
            "/szenarien Thema   neue Ereignisse simulieren (Thema ist freiwillig)\n" +
            "/welt   eigenständige Weltentwicklung\n" +
            "/folgen 12   wie geht es nach Ereignis 12 weiter (Kette)\n" +
            "/plan 12   Ereignis 12 auswählen und als Plan ausarbeiten lassen\n" +
            "/freigeben 12   /aktiv 12   /spaeter 12   /abbruch 12\n" +
            "/fertig 12 Ergebnis   Ereignis 12 ist eingetreten (wird Geschichte)\n" +
            "/info 12   Details     /wunsch Text   /wunschweg 3\n" +
            "/merke Text   fester Weltfakt     /vergiss 2   Fakt löschen\n" +
            "Im Fenster Ereignisse geht das auch mit Knöpfen. Text, den du dort eintippst, bespricht das gewählte Ereignis mit dem Operator.";

        /// <summary>true = war ein Operator-Befehl. Startet eine Anfrage nur über "starten" (das Fenster gibt die Rückmeldung).</summary>
        public static bool Befehl(string text, out string meldung, out string starten, out string thema, out int bezug)
        {
            meldung = ""; starten = ""; thema = ""; bezug = 0;
            string t = (text ?? "").Trim();
            if (!t.StartsWith("/")) return false;
            string kopf = t.Split(' ')[0].ToLowerInvariant();
            string rest = t.Length > kopf.Length ? t.Substring(kopf.Length).Trim() : "";
            int id = 0;
            string nachId = rest;
            var m = Regex.Match(rest, @"^#?(\d+)\s*(.*)$", RegexOptions.Singleline);
            if (m.Success) { int.TryParse(m.Groups[1].Value, out id); nachId = m.Groups[2].Value.Trim(); }
            switch (kopf)
            {
                case "/hilfe": case "/befehle": meldung = Hilfe; return true;
                case "/szenarien": case "/szenario": case "/simulieren": starten = "szenarien"; thema = rest; return true;
                case "/welt": starten = "welt"; thema = rest; return true;
                case "/folgen": case "/weiter":
                    if (Holen(id) == null) { meldung = "Nummer fehlt oder stimmt nicht. Beispiel: /folgen 12"; return true; }
                    starten = "folgen"; bezug = id; thema = nachId; return true;
                case "/plan": case "/planen": case "/waehle": case "/wähle":
                    if (Holen(id) == null) { meldung = "Nummer fehlt oder stimmt nicht. Beispiel: /plan 12"; return true; }
                    meldung = StatusSetzen(id, InPlanung);
                    if (Holen(id).Status == InPlanung) { starten = "plan"; bezug = id; thema = nachId; }
                    return true;
                case "/freigeben": meldung = StatusSetzen(id, Freigegeben); return true;
                case "/aktiv": case "/aktivieren": meldung = StatusSetzen(id, Aktiv); return true;
                case "/fertig": case "/abschliessen": case "/abschließen": meldung = StatusSetzen(id, Abgeschlossen, nachId); return true;
                case "/abbruch": case "/abbrechen": meldung = StatusSetzen(id, Abgebrochen, nachId); return true;
                case "/spaeter": case "/später": case "/zurueckstellen": case "/zurückstellen": meldung = StatusSetzen(id, Zurueckgestellt); return true;
                case "/vorschlag": meldung = StatusSetzen(id, Vorschlag); return true;
                case "/info":
                    { var e = Holen(id); meldung = e == null ? "Nummer fehlt oder stimmt nicht. Beispiel: /info 12" : Details(e); return true; }
                case "/wunsch": meldung = WunschAnlegen(rest); return true;
                case "/wunschweg": case "/wunschloeschen": meldung = WunschLoeschen(rest); return true;
            }
            return false;
        }
    }
}
