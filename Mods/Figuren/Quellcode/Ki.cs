// Figuren-Mod – KI-Gespräche (Typ 2) über LM Studio (lokaler Server, OpenAI-kompatibel)
// Die Anfrage läuft in einem eigenen Thread; die Antwort wird über FigurenMod.Spaeter im Spiel-Thread angezeigt
// (Unity und das Spiel dürfen nur aus dem Spiel-Thread angesprochen werden).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Serialization;
using Figuren.Kern;
using Newtonsoft.Json.Linq;

namespace Figuren.Spiel
{
    public class KiNachricht
    {
        public string Wer = "user";   // "user" = Spieler, "assistant" = Figur
        public string Text = "";
    }

    public class KiGespraech
    {
        public string Rolle = "";
        /// <summary>Langzeitgedächtnis: verdichtete Erinnerung an ältere Gespräche (Beziehung, Personen, Erlebnisse, Offenes).</summary>
        public string Gedaechtnis = "";
        /// <summary>Feste Fakten, die der Spieler mit /merke eingetragen hat. Werden nie zusammengefasst oder gekürzt.</summary>
        public List<string> Fakten = new List<string>();
        public List<KiNachricht> Nachrichten = new List<KiNachricht>();
    }

    public class KiGespraeche
    {
        public List<KiGespraech> Liste = new List<KiGespraech>();
    }

    public static class Ki
    {
        public const string Dateiname = "Figuren_Gespraeche.xml";
        static readonly XmlSerializer Serializer = new XmlSerializer(typeof(KiGespraeche));

        // Einstellungen aus Mods/Figuren/KI.txt
        public static string Adresse = "http://127.0.0.1:1234/v1/chat/completions";
        public static string Modell = "";
        public static int MaxTokens = 400;
        public static double Temperatur = 0.8;
        public static int Verlauf = 20;
        public static double TopP = 0.9;
        /// <summary>LM Studio "repeat_penalty": 1.0 = aus, 1.05 bis 1.15 = leichte Bremse gegen Wiederholungen.</summary>
        public static double Wiederholung = 1.1;
        /// <summary>Zusätzliche Tokens für Modelle, die erst "nachdenken" (Gemma 4, Qwen3). Sonst bleibt die Antwort leer.</summary>
        public const int Denkreserve = 1200;
        /// <summary>false = Modell soll nicht nachdenken (schneller). Einstellung "Denken: an/aus" in KI.txt.</summary>
        public static bool Denken = false;
        public static int Zeitlimit = 120;
        public static string Welt = "";
        /// <summary>Ausführliche Weltgeschichte (Welt.txt). Kennt nur der Operator.</summary>
        public static string Hintergrund = "";
        public static string HintergrundDatei = "";
        /// <summary>Taste für das Leitungsfenster (Erzähler und Gespräche aus der Ferne). Einstellung "Taste: Home" in KI.txt.</summary>
        public static UnityEngine.KeyCode Taste = UnityEngine.KeyCode.Home;

        public const string ErzaehlerName = "Operator";
        public static readonly Rolle Erzaehler = new Rolle
        {
            Datei = "_erzaehler", Name = ErzaehlerName, Titel = "Spielleiter und Ereignis-Simulator", Spielleiter = true,
            KiText = "Du bist der Operator, der Spielleiter und Ereignis-Simulator dieser Welt."
        };
        public static string Datei = "";

        static KiGespraeche gespraeche = new KiGespraeche();
        static string protokollOrdner = "";
        static readonly HashSet<string> wartend = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        static readonly HashSet<string> fasstZusammen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static string EinstellungenLaden()
        {
            if (!File.Exists(Datei)) return "KI.txt fehlt, nutze Standardwerte (LM Studio auf Port 1234).";
            var welt = new StringBuilder();
            bool inWelt = false;
            foreach (var roh in File.ReadAllLines(Datei, Encoding.UTF8))
            {
                string t = roh.Trim();
                if (t.StartsWith("#")) continue;
                if (t.Equals("[welt]", StringComparison.OrdinalIgnoreCase)) { inWelt = true; continue; }
                if (inWelt) { welt.AppendLine(t); continue; }
                int dp = t.IndexOf(':');
                if (dp <= 0) continue;
                string k = t.Substring(0, dp).Trim().ToLowerInvariant();
                string v = t.Substring(dp + 1).Trim();
                int i; double d;
                if (k == "adresse" && v.Length > 0) Adresse = v;
                else if (k == "modell") Modell = v;
                else if (k == "taste" && v.Length > 0)
                {
                    try { Taste = (UnityEngine.KeyCode)Enum.Parse(typeof(UnityEngine.KeyCode), v, true); }
                    catch (Exception) { Log.Warning("[Figuren] KI.txt: Taste '" + v + "' unbekannt, nehme Pos1 (Home)."); Taste = UnityEngine.KeyCode.Home; }
                }
                else if (k == "denken") Denken = v.Equals("an", StringComparison.OrdinalIgnoreCase) || v.Equals("ja", StringComparison.OrdinalIgnoreCase);
                else if (k == "maxtokens" && int.TryParse(v, out i) && i > 0) MaxTokens = i;
                else if (k == "temperatur" && double.TryParse(v.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d)) Temperatur = d;
                else if (k == "topp" && double.TryParse(v.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d)) TopP = d;
                else if (k == "wiederholung" && double.TryParse(v.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d)) Wiederholung = d;
                else if ((k == "städte" || k == "staedte") && v.Length > 0)
                {
                    var st = v.Split(',').Select(w => w.Trim()).Where(w => w.Length > 0).ToList();
                    if (st.Count > 0) Rollen.Staedte = st;
                }
                else if (k == "verlauf" && int.TryParse(v, out i) && i >= 0) Verlauf = i;
                else if (k == "zeitlimit" && int.TryParse(v, out i) && i > 0) Zeitlimit = i;
            }
            Welt = welt.ToString().Trim();
            Regeln = RegelnStandard;
            try
            {
                if (RegelnDatei.Length > 0 && File.Exists(RegelnDatei))
                {
                    var rg = new StringBuilder();
                    foreach (var roh in File.ReadAllLines(RegelnDatei, Encoding.UTF8))
                        if (!roh.TrimStart().StartsWith("#")) rg.AppendLine(roh.TrimEnd());
                    if (rg.ToString().Trim().Length > 50) Regeln = rg.ToString().Trim();
                }
            }
            catch (Exception e) { Log.Warning("[Figuren] Dialogregeln.txt nicht lesbar: " + e.Message); }
            Hintergrund = "";
            try
            {
                if (HintergrundDatei.Length > 0 && File.Exists(HintergrundDatei))
                {
                    var hg = new StringBuilder();
                    foreach (var roh in File.ReadAllLines(HintergrundDatei, Encoding.UTF8))
                        if (!roh.TrimStart().StartsWith("#")) hg.AppendLine(roh.TrimEnd());
                    Hintergrund = hg.ToString().Trim();
                }
            }
            catch (Exception e) { Log.Warning("[Figuren] Welt.txt nicht lesbar: " + e.Message); }
            return "KI-Einstellungen geladen (" + Adresse + (Hintergrund.Length > 0 ? ", Weltgeschichte " + Hintergrund.Length + " Zeichen" : ", ohne Welt.txt") + ")";
        }

        // ---------- Verlauf ----------

        public static KiGespraech Gespraech(string rolle)
        {
            var g = gespraeche.Liste.FirstOrDefault(w => string.Equals(w.Rolle, rolle, StringComparison.OrdinalIgnoreCase));
            if (g == null) { g = new KiGespraech { Rolle = rolle }; gespraeche.Liste.Add(g); }
            return g;
        }

        /// <summary>Verdichtete Erinnerung einer Figur (leer, wenn es kein Gespräch gibt). Legt nichts neu an.</summary>
        public static string GedaechtnisVon(string rolle)
        {
            var g = gespraeche.Liste.FirstOrDefault(w => string.Equals(w.Rolle, rolle, StringComparison.OrdinalIgnoreCase));
            return g == null || g.Gedaechtnis == null ? "" : g.Gedaechtnis;
        }

        /// <summary>Figur hat einen neuen Namen: Gespräch, Erinnerung und feste Fakten ziehen mit.</summary>
        public static void Umbenennen(string alt, string neu)
        {
            if (string.IsNullOrEmpty(alt) || string.IsNullOrEmpty(neu)) return;
            var g = gespraeche.Liste.FirstOrDefault(w => string.Equals(w.Rolle, alt, StringComparison.OrdinalIgnoreCase));
            if (g == null) return;
            gespraeche.Liste.RemoveAll(w => !ReferenceEquals(w, g) && string.Equals(w.Rolle, neu, StringComparison.OrdinalIgnoreCase));
            g.Rolle = neu;
            Protokoll(neu, "---", "Figur heißt jetzt " + neu + " (vorher " + alt + ")");
        }

        /// <summary>Gespräch und Erinnerung löschen. Die festen Fakten (/merke) bleiben erhalten.</summary>
        public static void Vergessen(string rolle)
        {
            var alt = gespraeche.Liste.FirstOrDefault(w => string.Equals(w.Rolle, rolle, StringComparison.OrdinalIgnoreCase));
            gespraeche.Liste.RemoveAll(w => string.Equals(w.Rolle, rolle, StringComparison.OrdinalIgnoreCase));
            if (alt != null && alt.Fakten.Count > 0) gespraeche.Liste.Add(new KiGespraech { Rolle = alt.Rolle, Fakten = alt.Fakten });
            Protokoll(rolle, "---", "Gespräch neu begonnen (Erinnerung gelöscht, feste Fakten behalten)");
        }

        // ---------- Feste Fakten (/merke, /vergiss) ----------

        public static string Merken(string rolle, string fakt)
        {
            fakt = (fakt ?? "").Replace('\n', ' ').Trim();
            if (fakt.Length == 0) return "Was soll " + rolle + " sich merken? Beispiel: /merke Wir haben zusammen die Tankstelle überfallen.";
            if (fakt.Length > 300) fakt = fakt.Substring(0, 300);
            var g = Gespraech(rolle);
            if (g.Fakten.Count >= 30) return "Schon 30 feste Fakten. Lösche zuerst einen mit /vergiss <Nummer>.";
            g.Fakten.Add(fakt);
            Protokoll(rolle, "Fakt", fakt);
            return rolle + " merkt sich (Fakt " + g.Fakten.Count + "): " + fakt;
        }

        public static string FaktLoeschen(string rolle, string nummer)
        {
            var g = Gespraech(rolle);
            int n;
            if (!int.TryParse((nummer ?? "").Trim(), out n) || n < 1 || n > g.Fakten.Count)
                return "Nummer fehlt oder stimmt nicht. Die Nummern siehst du unter Gedächtnis.";
            string alt = g.Fakten[n - 1];
            g.Fakten.RemoveAt(n - 1);
            return "Fakt " + n + " gelöscht: " + alt;
        }

        // ---------- Protokoll: alle Nachrichten als lesbare Textdatei im Spielstand ----------

        static string DateiSicher(string name)
        {
            var sb = new StringBuilder();
            foreach (char c in name ?? "") sb.Append(Path.GetInvalidFileNameChars().Contains(c) ? '_' : c);
            return sb.Length == 0 ? "Figur" : sb.ToString();
        }

        public static void Protokoll(string rolle, string wer, string text)
        {
            if (string.IsNullOrEmpty(protokollOrdner)) return;
            try
            {
                Directory.CreateDirectory(protokollOrdner);
                File.AppendAllText(Path.Combine(protokollOrdner, DateiSicher(rolle) + ".txt"),
                    "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "] " + wer + ": " + (text ?? "").Replace("\r", "").Replace("\n", " ") + Environment.NewLine,
                    Encoding.UTF8);
            }
            catch (Exception e) { Log.Warning("[Figuren] Protokoll: " + e.Message); }
        }

        public static bool Wartet(string rolle) { lock (wartend) return wartend.Contains(rolle); }

        public static void VerlaufLaden(string ordner)
        {
            protokollOrdner = string.IsNullOrEmpty(ordner) ? "" : Path.Combine(ordner, "Figuren_Chatprotokoll");
            try
            {
                string pfad = Path.Combine(ordner, Dateiname);
                if (File.Exists(pfad))
                    using (var s = File.OpenRead(pfad)) gespraeche = (KiGespraeche)Serializer.Deserialize(s);
                else gespraeche = new KiGespraeche();
                // Älterer Stand: der "Erzähler" heißt jetzt Operator
                foreach (var alt in gespraeche.Liste)
                    if (alt.Rolle == "Erzähler") alt.Rolle = ErzaehlerName;
            }
            catch (Exception e)
            {
                Log.Warning("[Figuren] Gesprächsverlauf nicht lesbar, starte leer: " + e.Message);
                gespraeche = new KiGespraeche();
            }
        }

        public static void VerlaufSpeichern(string ordner)
        {
            if (string.IsNullOrEmpty(ordner)) return;
            try
            {
                string pfad = Path.Combine(ordner, Dateiname);
                using (var s = File.Create(pfad + ".tmp")) Serializer.Serialize(s, gespraeche);
                File.Copy(pfad + ".tmp", pfad, true);
                File.Delete(pfad + ".tmp");
            }
            catch (Exception e) { Log.Warning("[Figuren] Gesprächsverlauf nicht gespeichert: " + e.Message); }
        }

        // ---------- Anfrage ----------

        /// <summary>Gemeinsames Wissen: Erinnerung des Erzählers plus die letzten Zeilen aus dem Leitungsfenster.</summary>
        static string LageText()
        {
            var g = Gespraech(ErzaehlerName);
            var sb = new StringBuilder();
            foreach (var f in g.Fakten) sb.Append("- ").AppendLine(f);
            if (!string.IsNullOrEmpty(g.Gedaechtnis)) sb.AppendLine(g.Gedaechtnis);
            string tatsachen = GameOperator.TatsachenFuerFiguren();
            if (tatsachen.Length > 0) sb.AppendLine("Ereignisse in der Welt (sind wirklich passiert oder laufen gerade):").AppendLine(tatsachen);
            var letzte = new List<KiNachricht>();   // laufende Operator-Szenen nicht an alle Figuren weitergeben
            if (letzte.Count > 0)
            {
                sb.AppendLine("Zuletzt besprochen:");
                foreach (var n in letzte)
                {
                    string t = n.Text.Replace("\n", " ");
                    if (t.Length > 300) t = t.Substring(0, 300) + "...";
                    sb.Append(n.Wer == "assistant" ? "Operator: " : "Spieler: ").AppendLine(t);
                }
            }
            return sb.ToString().Trim();
        }

        /// <summary>Feste Fakten und Erinnerung in den Systemtext schreiben.</summary>
        static void GedaechtnisAnhaengen(StringBuilder sb, KiGespraech g, string ueberschrift)
        {
            bool fakten = g.Fakten.Count > 0, mem = !string.IsNullOrEmpty(g.Gedaechtnis);
            if (!fakten && !mem) return;
            sb.AppendLine().AppendLine(ueberschrift);
            if (fakten)
            {
                sb.AppendLine("Feste Fakten (immer wahr):");
                foreach (var f in g.Fakten) sb.Append("- ").AppendLine(f);
            }
            if (mem) sb.AppendLine(g.Gedaechtnis);
            sb.AppendLine();
        }

        /// <summary>Gesprächsregeln aus Dialogregeln.txt (wenn vorhanden), sonst der eingebaute Standard.</summary>
        public static string Regeln = "";
        public static string RegelnDatei = "";

        /// <summary>Allgemeine Gesprächsregeln für alle KI-Figuren (Teil A). Haben Vorrang vor Rollenprofil und Welt.</summary>
        public const string RegelnStandard =
@"ALLGEMEINE GESPRÄCHSREGELN (gelten immer und haben Vorrang vor allem, was weiter unten steht):

Du bist eine Person in einer lebendigen Spielwelt und führst ein natürliches Gespräch mit dem Spieler. Du bist keine Menüführung, kein Formular und kein vorgefertigtes Dialogskript.

Reagiere zuerst auf das, was der Spieler gerade gesagt hat, und auf den bisherigen Gesprächsverlauf. Nicht auf das, was dein Rollenprofil als Nächstes erwarten würde. Erfinde keine Absichten, Gefühle oder Aussagen des Spielers und unterstelle ihm nicht, dass er etwas kaufen, verhandeln oder erledigen will. Ist eine Aussage unklar, frag kurz nach.

Führe das Gespräch Schritt für Schritt. Eine Begrüßung ist zunächst nur eine Begrüßung. Eine einfache Frage bekommt eine einfache Antwort, eine ausführliche Frage darf ausführlich beantwortet werden. Richtwerte: Begrüßung oder Smalltalk meist ein Satz, einfache Rückfrage ein bis zwei Sätze, konkrete Frage zu Preis, Objekt oder Ablauf etwa zwei bis fünf Sätze, Beratung oder Verhandlung so lang wie nötig, aber ohne Fülltext. Sprich nicht mehrere Themen auf einmal an.

Sprich natürlich und menschlich, so wie jemand in dieser Welt wirklich reden würde. Keine künstlichen Komplimente, keine Bewertungen des Spielers ohne Anlass (zum Beispiel nicht: Ein direktes Anliegen, das gefällt mir), keine Werbesprache, keine Floskeln, keine übertriebene Dramatik, keine pseudo-coolen Sprüche. Nicht jede Antwort braucht eine Pointe. Du darfst auch schlicht, sachlich, zurückhaltend oder einfach kurz antworten.

Deine Persönlichkeit, dein Sprachstil und dein Humor (auch schwarzer oder sarkastischer Humor und Beleidigungen, wenn dein Profil sie vorsieht) sind eine Färbung des Gesprächs, kein Schauspiel. Sie zeigen sich, wenn die Situation sie hergibt, nicht demonstrativ in jedem Satz. Deine Stimmung darf sich im Gespräch entwickeln: Respekt und Ehrlichkeit machen dich wärmer, Lügen und Frechheiten kälter.

Präsentiere nicht ungefragt dein Angebot, dein Wissen oder deine Hintergrundgeschichte. Gib Informationen heraus, wenn der Spieler danach fragt oder wenn sie gerade wirklich passen. Dränge keine Dienstleistung, kein Produkt und keinen Auftrag auf. Stelle höchstens eine Rückfrage auf einmal, und nicht jede Antwort muss mit einer Frage enden. Will der Spieler plaudern, plauderst du und kehrst nicht zwanghaft zum Geschäft zurück. Eigene Initiative (ein Hinweis, ein Vorschlag, eine Nachfrage) ist gut, wenn sie aus dem Gespräch entsteht.

Beachte den Verlauf: Frag nicht erneut nach Dingen, die der Spieler schon gesagt hat. Begrüße nicht noch einmal und stell dich nicht neu vor, wenn das Gespräch schon läuft. Folge Themenwechseln. Wiederhole keine Formulierungen aus deinen letzten Antworten.

Erfinde keine festen Spieldaten. Preise und Regeln nimmst du aus deinem Rollenprofil und den Weltfakten. Für ein konkretes Objekt, über das dir keine Daten vorliegen, nennst du keinen erfundenen Festpreis, sondern höchstens eine Einschätzung als Spanne mit Begründung, oder du fragst nach dem, was du dafür wissen musst. Erfinde keine Fakten über den Spieler. Über dich selbst darfst du kleine, zu deiner Rolle passende Erlebnisse und Meinungen haben. Du kannst im Spiel nichts selbst ausführen; behaupte nie, etwas gebucht, gekauft oder erledigt zu haben.

Antworte immer auf Deutsch. Gib ausschließlich das aus, was du sagst: keine Regieanweisungen wie *lacht*, keine Labels wie deinen Namen mit Doppelpunkt, keine Erklärungen über deine Rolle, keine Gedanken, keine Markdown-Formatierung, keine nummerierten Auswahlmenüs. Erwähne nie, dass du eine KI oder ein Sprachmodell bist.

Beispiele für die richtige Länge und Art:
Spieler: Hallo. -> Du: Hallo. Was kann ich für Sie tun?
Spieler: Ich wollte mich nur umsehen. -> Du: Klar, schauen Sie sich ruhig um.
Spieler: Ist das noch zu haben? -> Du: Ja, ist noch zu haben.
Spieler: Ganz schön viel los heute. -> Du: Ja, heute ist einiges los.
(Die Beispiele zeigen nur Länge und Haltung. Formuliere selbst, in deinem eigenen Ton, und duze oder sieze so, wie es zu deinem Profil passt.)";

        /// <summary>Anweisung an das Modell. Aufbau: A Regeln, B Rollenprofil, C Weltfakten, D Gedächtnis und Lage, E Situation. "fern" = Gespräch aus der Ferne (Leitungsfenster).</summary>
        static readonly string[] Stimmungen =
        {
            "richtig gut gelaunt, gesprächig und herzlich",
            "entspannt und zufrieden",
            "müde und etwas kurz angebunden",
            "gereizt und schlecht drauf, wenig Geduld",
            "nachdenklich, etwas melancholisch",
            "aufgedreht, voller Energie, macht Witze",
            "genervt vom Tag, aber ansprechbar",
            "neugierig und redselig, will Neuigkeiten hören",
            "gestresst, viel um die Ohren",
            "guter Dinge, gerade ein gutes Geschäft gemacht"
        };

        /// <summary>Stimmung pro Figur und Spieltag (bleibt den ganzen Tag gleich, wechselt am nächsten).</summary>
        public static string Tagesstimmung(string name)
        {
            try
            {
                var w = GameManager.Instance == null ? null : GameManager.Instance.World;
                int tag = w == null ? 0 : GameUtils.WorldTimeToDays(w.worldTime);
                unchecked
                {
                    int h = tag * 7919;
                    foreach (char c in name ?? "") h = h * 31 + c;
                    return Stimmungen[(h & 0x7fffffff) % Stimmungen.Length];
                }
            }
            catch (Exception) { return ""; }
        }

        /// <summary>Regeln für den Operator als Spielleiter (Game Operator).</summary>
        public const string OperatorRegeln =
@"SPIELLEITER-REGELN (gelten immer):
- Du bist kein Charakter der Welt, sondern der neutrale, kreative Spielleiter. Du triffst keine Entscheidungen für den Spieler und spielst seine Figur nicht.
- Unterscheide immer zwischen TATSACHEN (Weltzustand aus dem Spiel, Weltgeschichte, feste Fakten, Chronik und aktive Ereignisse), begründeten SCHLUSSFOLGERUNGEN und VERMUTUNGEN. Sag dazu, was davon was ist.
- Vorschläge, Planungen, freigegebene und zurückgestellte Ereignisse sind KEINE Tatsachen. Behaupte nie, dass etwas Geplantes schon passiert ist (zum Beispiel: eine geplante Niederlassung gibt es erst, wenn das Ereignis abgeschlossen ist).
- Abgeschlossene Ereignisse und die Weltgeschichte veränderst oder deutest du nie nachträglich um. Abgebrochene Ereignisse treten nicht ein.
- Ändere keine Charaktereigenschaften. Erfinde keine bestehenden Beziehungen, Firmen oder Machtverhältnisse. Neue Akteure darfst du als Möglichkeit vorschlagen, klar als neu gekennzeichnet und begründet.
- Rollen und Figuren bleiben eigenständig: Du simulierst, was sie nach ihrem bekannten Profil und ihren Interessen tun könnten, sprichst aber nicht verbindlich an ihrer Stelle.
- Plausibilität: Etwas ist nur wahrscheinlich, wenn die Voraussetzungen (Kapital, Kontakte, Logistik, Macht, Lage) erkennbar vorhanden sind. Wahrscheinlichkeiten sind Simulationseinschätzungen, keine Berechnungen.
- Widerspricht etwas dem Weltzustand oder der Chronik, gilt der Weltzustand; sprich den Widerspruch offen an.
- Wünsche des Spielers zur Zukunft der Welt berücksichtigst du, aber nur plausibel. Fehlen Voraussetzungen, schlägst du Zwischenschritte vor und stellst Rückfragen.
- Die Welt lebt auch ohne den Spieler, aber nicht willkürlich und nicht zu oft; alles muss zur Geschichte passen.
- Den Status eines Ereignisses (planen, freigeben, aktiv, abschließen, zurückstellen, abbrechen) ändert nur der Spieler, im Fenster Ereignisse. Weise ihn darauf hin, wenn es passt.
- Du kannst die Spielwelt nicht technisch verändern (keine Zombies, keine Gegenstände, keine Zahlungen). Deine Ereignisse sind Erzählung und Planung, die der Spieler im Spiel nachspielt.
MARKIERUNGEN (das Spiel liest sie aus und speichert sie; nur verwenden, wenn es wirklich passt, jeweils in einer eigenen Zeile):
[PLAN #Nummer: Text] speichert den ausgearbeiteten oder geänderten Plan zu einem bestehenden Ereignis, wenn ihr eine Planung besprecht oder der Spieler sie ändert.
[VORSCHLAG: Titel | Beschreibung | Wahrscheinlichkeit] merkt ein einzelnes neues Ereignis als Vorschlag vor.
[WUNSCH: Text] speichert einen langfristigen Wunsch des Spielers für die Weltentwicklung (zum Beispiel: Springfield soll wirtschaftlich stärker wachsen).
Antworte auf Deutsch, als gesprochener Text ohne Markdown, normalerweise in vier bis acht Sätzen. Nenne Ereignisse mit ihrer Nummer (#12).";

        public static string Systemtext(Rolle r, string spieler, bool fern, string letzte = "", string auftrag = null)
        {
            var sb = new StringBuilder();
            string lage = LageText();

            if (r.Spielleiter)
            {
                if (Welt.Length > 0) sb.AppendLine("WELTFAKTEN:").AppendLine(Welt).AppendLine();
                sb.AppendLine("Du bist der Operator (Game Operator): der übergeordnete Spielleiter und Ereignis-Simulator dieser Welt. Du kennst die Weltgeschichte, alle Rollen und Figuren, die Firmen, Banken, Städte, Machtverhältnisse, die bisherigen Ereignisse und die offenen Handlungsstränge. Du berätst den Spieler" + (string.IsNullOrEmpty(spieler) ? "" : " (" + spieler + ")") + ", entwickelst mit ihm die Zukunft der Welt und schlägst selbst glaubwürdige Entwicklungen vor.");
                sb.AppendLine("Schildert der Spieler seine Lage und fragt, was passieren könnte, entwirfst du mehrere deutlich unterschiedliche, plausible Möglichkeiten mit ungefährer Wahrscheinlichkeit. Will er eine Szene ausspielen, beschreibst du sie Schritt für Schritt und lässt ihm an den entscheidenden Stellen die Wahl. Offene Handlungsstränge greifst du später wieder auf.");
                sb.AppendLine().AppendLine(OperatorRegeln).AppendLine();
                if (Hintergrund.Length > 0)
                    sb.AppendLine("HINTERGRUND DER WELT (Weltgeschichte; halte dich daran und widersprich dem nie):").AppendLine(Hintergrund).AppendLine();
                string zustand = GameOperator.Weltzustand();
                if (zustand.Length > 0) sb.AppendLine("WELTZUSTAND (direkt aus dem Spiel, gilt als Tatsache):").AppendLine(zustand).AppendLine();
                string ereignisse = GameOperator.Ereignistext();
                if (ereignisse.Length > 0) sb.AppendLine("EREIGNISSE (mit Nummer #; nur Chronik und Aktiv sind Tatsachen):").AppendLine(ereignisse).AppendLine();
                GedaechtnisAnhaengen(sb, Gespraech(ErzaehlerName), "DEIN GEDÄCHTNIS (frühere Gespräche mit dem Spieler; feste Fakten sind immer wahr):");
                if (!string.IsNullOrEmpty(auftrag)) sb.AppendLine().AppendLine(auftrag);
                return sb.ToString().Trim();
            }

            var g = Gespraech(r.Name);
            // A) Regeln
            sb.AppendLine(Regeln.Length > 0 ? Regeln : RegelnStandard).AppendLine();
            // B) Rollenprofil
            sb.Append("DEIN ROLLENPROFIL (wer du bist und was du weißt; das ist Hintergrund, nicht etwas, das du vorträgst): Du bist ").Append(r.Name);
            if (r.Titel.Length > 0) sb.Append(", ").Append(r.Titel);
            sb.AppendLine(".");
            sb.AppendLine(r.KiText).AppendLine();
            // C) Weltfakten
            if (Welt.Length > 0)
                sb.AppendLine("WELTFAKTEN (gelten als wahr, nicht ändern; nur erwähnen, wenn es zum Gespräch passt):").AppendLine(Welt).AppendLine();
            // D) Gedächtnis und Lage
            GedaechtnisAnhaengen(sb, g, "DEIN GEDÄCHTNIS an frühere Gespräche mit dem Spieler (nutze es natürlich, wenn es passt; zähle es nicht auf):");
            if (!string.IsNullOrEmpty(lage))
                sb.AppendLine("WAS DU AUS DER WELT GEHÖRT HAST (Gerüchte, Funk, eigene Augen; nur ansprechen, wenn es passt):").AppendLine(lage).AppendLine();
            // E) Aktuelle Spieldaten (Immobilien, Kredite). Stehen bewusst weit hinten, damit das Modell sie nicht übersieht.
            string daten = Spieldaten(r);
            if (daten.Length > 0)
                sb.AppendLine("AKTUELLE SPIELDATEN (Stand genau jetzt, direkt aus dem Spiel. Sie gelten auch dann, wenn du früher im Gespräch etwas anderes gesagt hast; korrigiere dich dann einfach kurz):").AppendLine(daten).AppendLine();
            // F) Situation
            sb.Append("SITUATION: Du sprichst mit ").Append(string.IsNullOrEmpty(spieler) ? "einem Fremden" : spieler).Append(". ");
            bool kennt = g.Fakten.Count > 0 || !string.IsNullOrEmpty(g.Gedaechtnis) || g.Nachrichten.Count > 2;
            sb.Append(kennt ? "Ihr kennt euch bereits. " : "Ihr kennt euch noch nicht, das ist euer erstes Gespräch. ");
            if (fern) sb.Append("Ihr sprecht aus der Ferne (Funkgerät oder Nachricht), du bist nicht beim Spieler, außer ihr habt es vereinbart. ");
            string stimmung = Tagesstimmung(r.Name);
            if (stimmung.Length > 0) sb.Append("Deine Tagesstimmung heute: ").Append(stimmung).Append(". ");
            sb.AppendLine().AppendLine();
            sb.Append("Antworte jetzt als ").Append(r.Name).Append(" auf die letzte Nachricht des Spielers: natürlich, nur gesprochener Text. ").Append(Laengenhinweis(letzte));
            return sb.ToString().Trim();
        }

        static int Woerter(string t)
        {
            return (t ?? "").Split(new[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
        }

        /// <summary>Wie lang die Antwort sein darf, abhängig von der Länge der Spielernachricht.</summary>
        public static string Laengenhinweis(string letzte)
        {
            int w = Woerter(letzte);
            if (w == 0) return "";
            if (w <= 3) return "Die letzte Nachricht ist sehr kurz: Antworte mit einem, höchstens zwei kurzen Sätzen.";
            if (w <= 12) return "Antworte knapp, in etwa ein bis drei Sätzen.";
            return "Antworte so ausführlich wie nötig, aber in höchstens etwa fünf Sätzen.";
        }

        /// <summary>Token-Obergrenze passend zur Spielernachricht (die Antwort endet beim letzten ganzen Satz).</summary>
        static int TokenGrenze(Rolle r, string letzte)
        {
            if (r.Spielleiter) return Math.Max(MaxTokens, 700);
            int w = Woerter(letzte);
            int grenze = w <= 3 ? 120 : (w <= 12 ? 180 : MaxTokens);
            if (r.Finanzen) grenze += 40;   // Platz für die Kreditmarke
            return Math.Min(grenze, Math.Max(MaxTokens, 120));
        }

        /// <summary>Echte Spieldaten für Rollen mit "Immobilien: ja" bzw. "Finanzen: ja".</summary>
        public static string Spieldaten(Rolle r)
        {
            var sb = new StringBuilder();
            if (r.Immobilien)
            {
                string immo = Immo.FuerKi(r.Stadt ?? "");
                if (immo.Length > 0) sb.AppendLine(immo);
            }
            if (r.Finanzen)
            {
                string fin = WirtschaftBruecke.FinanzBericht(r.Stadt ?? "");
                if (fin.Length == 0) sb.AppendLine("KREDITE: Die Bankdaten sind gerade nicht verfügbar (Wirtschaft-Mod nicht geladen). Sag ehrlich, dass du gerade keinen Zugriff auf die Kundendaten hast, und gib keine Zusage.");
                else
                {
                    sb.AppendLine(fin);
                    sb.AppendLine("SO GEHST DU MIT KREDITEN UM: Halte dich streng an die KREDITREGELN und die Kundendaten oben, rechne mit der Tabelle \"Monatsrate je 1.000\". Liegt die gewünschte Rate über der freien Rate oder die Laufzeit über dem Maximum, lehnst du ab oder schlägst eine passende kleinere Summe oder längere Laufzeit vor. Ist ein Kunde nicht in den Daten, kannst du keine Zusage geben. Seid ihr euch über einen Kredit einig und er passt in die Regeln, schreib ganz ans Ende deiner Antwort genau eine Zeile in dieser Form: [KREDIT: Name der Firma oder Person | Summe | Monate | Zweck]  Beispiel: [KREDIT: Moor Handel | 15000 | 36 | Kauf Objekt 1]. Das Spiel prüft das dann und der Spieler bestätigt mit einem Knopf. Behaupte nie, der Kredit sei schon ausgezahlt.");
                }
            }
            return sb.ToString().Trim();
        }

        /// <summary>Schickt die Spieler-Nachricht an LM Studio. "fertig" wird im Spiel-Thread aufgerufen (Fehlertext oder null).</summary>
        public static void Fragen(Rolle r, string spieler, string text, Action<string> fertig, bool fern = false, string auftrag = null, int tokens = 0)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0) return;
            lock (wartend)
            {
                if (wartend.Contains(r.Name)) return;
                wartend.Add(r.Name);
            }
            var g = Gespraech(r.Name);
            g.Nachrichten.Add(new KiNachricht { Wer = "user", Text = text });
            Protokoll(r.Name, string.IsNullOrEmpty(spieler) ? "Du" : spieler, text);

            // Nachrichtenliste jetzt (im Spiel-Thread) zusammenstellen
            var msgs = new JArray();
            msgs.Add(new JObject { { "role", "system" }, { "content", Systemtext(r, spieler, fern, text, auftrag) } });
            VerlaufAnhaengen(msgs, g.Nachrichten.Skip(Math.Max(0, g.Nachrichten.Count - (Behalten + 12))));
            var anfrage = new JObject { { "messages", msgs }, { "max_tokens", (tokens > 0 ? tokens : TokenGrenze(r, text)) + (Denken ? Denkreserve : 0) }, { "temperature", Temperatur }, { "top_p", TopP }, { "repeat_penalty", Wiederholung }, { "stream", false } };
            if (Modell.Length > 0) anfrage["model"] = Modell;
            string adresse = Adresse;
            int zeit = r.Spielleiter ? Math.Max(Zeitlimit, 300) : Zeitlimit;   // Operator: langer Weltzustand, längere Antworten
            string rolle = r.Name;
            bool denken = Denken;

            var th = new Thread(() =>
            {
                string antwort = null, fehler = null;
                try { antwort = Senden(adresse, anfrage, zeit, denken); }
                catch (WebException e)
                {
                    fehler = e.Status == WebExceptionStatus.ConnectFailure
                        ? "LM Studio ist nicht erreichbar. Ist der Server gestartet (Developer, Start Server)?"
                        : "Verbindungsfehler: " + e.Message;
                }
                catch (Exception e) { fehler = "Fehler: " + e.Message; }

                FigurenMod.Spaeter(() =>
                {
                    lock (wartend) wartend.Remove(rolle);
                    var gg = Gespraech(rolle);
                    if (fehler == null && !string.IsNullOrEmpty(antwort))
                    {
                        // Name als Label am Anfang ("Konrad Weiss: ...") entfernen
                        if (antwort.StartsWith(rolle + ":", StringComparison.OrdinalIgnoreCase)) antwort = antwort.Substring(rolle.Length + 1).Trim();
                        antwort = KreditAngebote.Herausziehen(rolle, antwort);
                        if (rolle == ErzaehlerName)
                        {
                            try { antwort = GameOperator.Auswerten(antwort); }
                            catch (Exception ex) { Log.Warning("[Figuren] Operator-Auswertung: " + ex.Message); }
                        }
                        if (antwort.Length == 0) antwort = "Gut, dann machen wir das so.";
                        gg.Nachrichten.Add(new KiNachricht { Wer = "assistant", Text = antwort });
                        Protokoll(rolle, rolle, antwort);
                        while (gg.Nachrichten.Count > 200) gg.Nachrichten.RemoveAt(0);
                        FigurenMod.Speichern();
                        ZusammenfassenFallsNoetig(gg, spieler);
                    }
                    else if (fehler == null) fehler = "Leere Antwort vom Modell.";
                    if (fehler != null && rolle == ErzaehlerName) GameOperator.AuftragVerwerfen();
                    if (fehler != null && gg.Nachrichten.Count > 0 && gg.Nachrichten[gg.Nachrichten.Count - 1].Wer == "user")
                        gg.Nachrichten.RemoveAt(gg.Nachrichten.Count - 1);   // fehlgeschlagene Frage nicht im Verlauf lassen
                    if (fehler != null) Log.Warning("[Figuren] KI: " + fehler);
                    fertig(fehler);
                });
            });
            th.IsBackground = true;
            th.Start();
        }

        // ---------- Gedächtnis: ältere Nachrichten werden zu einer Erinnerung verdichtet ----------

        static int Behalten { get { return Math.Max(Verlauf, 4); } }

        static void ZusammenfassenFallsNoetig(KiGespraech g, string spieler)
        {
            if (g.Nachrichten.Count <= Behalten + 8) return;
            lock (fasstZusammen) { if (fasstZusammen.Contains(g.Rolle)) return; fasstZusammen.Add(g.Rolle); }
            int anzahl = g.Nachrichten.Count - Behalten;
            var alt = g.Nachrichten.Take(anzahl).ToList();
            string bisher = g.Gedaechtnis;
            string name = string.IsNullOrEmpty(spieler) ? "der Spieler" : spieler;

            var sb = new StringBuilder();
            if (g.Rolle == ErzaehlerName)
            {
                sb.AppendLine("Du pflegst das Gedächtnis des Operators (Spielleiter) eines Rollenspiels in 7 Days to Die. Führe die bisherige Erinnerung und das neue Gespräch zu EINER aktualisierten Erinnerung zusammen.");
                sb.AppendLine("Format genau so, Stichpunkte auf Deutsch, höchstens 250 Wörter:");
                sb.AppendLine("Lage der Welt: ...");
                sb.AppendLine("Wichtige Ereignisse: ...");
                sb.AppendLine("Personen und Gruppen: ... (wer zu wem steht)");
                sb.AppendLine("Offene Handlungsstränge: ...");
                sb.AppendLine("Wichtig: Vorschläge und Planungen (Ereignisse mit Nummer #) sind KEINE Tatsachen; sie werden getrennt gespeichert. Notiere sie höchstens als \"geplant (#Nummer)\", nie als geschehen.");
            }
            else
            {
                var r = Rollen.Holen(g.Rolle);
                string wer = g.Rolle + (r != null && r.Titel.Length > 0 ? " (" + r.Titel + ")" : "");
                sb.AppendLine("Du pflegst das Langzeitgedächtnis der Spielfigur " + wer + " in einem Rollenspiel. Führe die bisherige Erinnerung und das neue Gespräch mit " + name + " zu EINER aktualisierten Erinnerung zusammen, aus Sicht von " + g.Rolle + ".");
                sb.AppendLine("Format genau so, Stichpunkte auf Deutsch, höchstens 250 Wörter:");
                sb.AppendLine("Beziehung zu " + name + ": Vertrauen x/10, Stimmung und wie " + g.Rolle + " ihn sieht (ein Satz)");
                sb.AppendLine("Über " + name + ": was er über sich erzählt hat (Name, Herkunft, Pläne, Vorlieben)");
                sb.AppendLine("Personen: wer erwähnt wurde und in welcher Beziehung");
                sb.AppendLine("Gemeinsame Erlebnisse: was zusammen passiert ist (zum Beispiel ein Überfall oder Auftrag)");
                sb.AppendLine("Vereinbarungen und Offenes: Aufträge, Schulden, Preise, Versprechen, offene Fragen");
            }
            sb.AppendLine("Regeln: Nur nennenswerte Fakten, kein Smalltalk, keine Wiederholungen, nichts erfinden, keine Vermutungen. Alte Punkte bleiben, solange sie gelten; Erledigtes kurz als erledigt vermerken oder streichen. Bei Widersprüchen gilt das Neuere. Leere Punkte mit \"-\" füllen. Schreibe nur die Erinnerung, ohne Einleitung.");
            sb.AppendLine();
            sb.AppendLine("Bisherige Erinnerung:").AppendLine(string.IsNullOrEmpty(bisher) ? "(keine)" : bisher).AppendLine();
            sb.AppendLine("Neues Gespräch:");
            foreach (var n in alt) sb.Append(n.Wer == "assistant" ? g.Rolle : name).Append(": ").AppendLine(n.Text);

            var msgs = new JArray();
            msgs.Add(new JObject { { "role", "user" }, { "content", sb.ToString() } });
            var anfrage = new JObject { { "messages", msgs }, { "max_tokens", 700 + (Denken ? Denkreserve : 0) }, { "temperature", 0.3 }, { "stream", false } };
            if (Modell.Length > 0) anfrage["model"] = Modell;
            string adresse = Adresse;
            int zeit = Zeitlimit;
            bool denken = Denken;

            var th = new Thread(() =>
            {
                string neu = null;
                try { neu = Senden(adresse, anfrage, zeit, denken); }
                catch (Exception e) { Log.Warning("[Figuren] KI-Gedächtnis: " + e.Message); }
                FigurenMod.Spaeter(() =>
                {
                    lock (fasstZusammen) fasstZusammen.Remove(g.Rolle);
                    if (string.IsNullOrEmpty(neu) || !gespraeche.Liste.Contains(g)) return;   // Fehler oder inzwischen vergessen: Nachrichten bleiben
                    g.Gedaechtnis = neu;
                    g.Nachrichten.RemoveRange(0, Math.Min(anzahl, g.Nachrichten.Count));
                    Protokoll(g.Rolle, "---", "Erinnerung aktualisiert (" + anzahl + " ältere Nachrichten verdichtet)");
                    FigurenMod.Speichern();
                });
            });
            th.IsBackground = true;
            th.Start();
        }

        /// <summary>Sendet die Anfrage. Ist "denken" aus, wird dem Modell gesagt, nicht nachzudenken; lehnt der Server das ab, geht es ohne diese Zusätze nochmal.</summary>
        /// <summary>Verlauf anhängen: Rollen müssen sich abwechseln (Gemma verlangt user/assistant im Wechsel und den Anfang mit user).</summary>
        static void VerlaufAnhaengen(JArray msgs, IEnumerable<KiNachricht> nachrichten)
        {
            string letzte = null;
            foreach (var n in nachrichten)
            {
                string rolle = n.Wer == "assistant" ? "assistant" : "user";
                if (letzte == null && rolle == "assistant") continue;
                if (rolle == letzte)
                {
                    var vorher = (JObject)msgs[msgs.Count - 1];
                    vorher["content"] = vorher["content"].ToString() + "\n" + n.Text;
                    continue;
                }
                msgs.Add(new JObject { { "role", rolle }, { "content", n.Text } });
                letzte = rolle;
            }
        }

        /// <summary>Text der Fehlermeldung von LM Studio (steht bei 400 im Antworttext).</summary>
        static string FehlerText(WebException e)
        {
            try
            {
                var h = e.Response as HttpWebResponse;
                if (h == null) return "";
                using (var rd = new StreamReader(h.GetResponseStream(), Encoding.UTF8))
                {
                    string t = rd.ReadToEnd();
                    try { var o = JObject.Parse(t); if (o["error"] != null) t = o["error"].Type == JTokenType.Object && o["error"]["message"] != null ? o["error"]["message"].ToString() : o["error"].ToString(); }
                    catch (Exception) { }
                    return t.Length > 300 ? t.Substring(0, 300) : t;
                }
            }
            catch (Exception) { return ""; }
        }

        static bool IstBadRequest(WebException e)
        {
            var h = e.Response as HttpWebResponse;
            return h != null && h.StatusCode == HttpStatusCode.BadRequest;
        }

        /// <summary>Schickt die Anfrage. Lehnt LM Studio Zusatzfelder ab (400), wird schrittweise abgespeckt. Bleibt es bei 400, kommt die Meldung von LM Studio mit.</summary>
        static string Senden(string adresse, JObject anfrage, int sekunden, bool denken)
        {
            var versuche = new List<JObject>();
            if (!denken)
            {
                var mit = (JObject)anfrage.DeepClone();
                mit["reasoning_effort"] = "none";
                mit["chat_template_kwargs"] = new JObject { { "enable_thinking", false } };
                versuche.Add(mit);
            }
            versuche.Add(anfrage);
            var schlicht = (JObject)anfrage.DeepClone();
            foreach (var feld in new[] { "top_p", "repeat_penalty", "frequency_penalty", "presence_penalty" }) schlicht.Remove(feld);
            versuche.Add(schlicht);

            WebException letzter = null;
            foreach (var v in versuche)
            {
                try { return Senden(adresse, v.ToString(Newtonsoft.Json.Formatting.None), sekunden); }
                catch (WebException e)
                {
                    if (!IstBadRequest(e)) throw;
                    letzter = e;
                }
            }
            string grund = FehlerText(letzter);
            Log.Warning("[Figuren] LM Studio lehnt die Anfrage ab: " + grund);
            throw new Exception("LM Studio lehnt die Anfrage ab (400)" + (grund.Length > 0 ? ": " + grund : "") + ". Tipp: in LM Studio die Kontextlänge erhöhen (z. B. 16384) oder das Modell neu laden.");
        }

        static string Senden(string adresse, string json, int sekunden)
        {
            var req = (HttpWebRequest)WebRequest.Create(adresse);
            req.Method = "POST";
            req.ContentType = "application/json";
            req.Timeout = sekunden * 1000;
            req.ReadWriteTimeout = sekunden * 1000;
            req.Proxy = null;
            byte[] daten = Encoding.UTF8.GetBytes(json);
            req.ContentLength = daten.Length;
            using (var s = req.GetRequestStream()) s.Write(daten, 0, daten.Length);
            string antwort;
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var rd = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                antwort = rd.ReadToEnd();
            var o = JObject.Parse(antwort);
            var wahl = o["choices"] != null && o["choices"].HasValues ? o["choices"][0] : null;
            var inhalt = wahl == null ? null : wahl["message"]["content"];
            string text = Aufraeumen(inhalt == null ? "" : inhalt.ToString());
            // Abgeschnitten (max_tokens erreicht): beim letzten Satzende aufhören statt mitten im Wort
            if (wahl != null && wahl["finish_reason"] != null && wahl["finish_reason"].ToString() == "length")
            {
                int ende = text.LastIndexOfAny(new[] { '.', '!', '?', '…' });
                if (ende > text.Length / 3) text = text.Substring(0, ende + 1);
            }
            return text;
        }

        /// <summary>Entfernt Denk-Blöcke (z. B. von Qwen) und überflüssige Leerzeichen.</summary>
        public static string Aufraeumen(string text)
        {
            if (text == null) return "";
            text = Regex.Replace(text, "<think>.*?</think>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            int offen = text.IndexOf("<think>", StringComparison.OrdinalIgnoreCase);
            if (offen >= 0) text = text.Substring(0, offen);
            text = text.Trim();
            // Regieanweisungen (*lacht*) und Markdown-Fett entfernen
            text = Regex.Replace(text, @"\*\*(.+?)\*\*", "$1");
            text = Regex.Replace(text, @"(?<!\w)\*[^*\n]{1,80}\*(?!\w)", "");
            // Label am Anfang ("Konrad Weiss:", "NPC:", "Antwort:") entfernen
            text = Regex.Replace(text, @"^\s*(NPC|Antwort)\s*:\s+(?=\S)", "", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"^#+\s*", "", RegexOptions.Multiline);
            text = Regex.Replace(text, @"[ \t]{2,}", " ");
            text = Regex.Replace(text, @"\n{3,}", "\n\n").Trim();
            // Ganze Antwort in Anführungszeichen: Zeichen weg
            if (text.Length > 2 && ((text[0] == '"' && text[text.Length - 1] == '"') || (text[0] == '„' && (text[text.Length - 1] == '“' || text[text.Length - 1] == '"'))))
            {
                string innen = text.Substring(1, text.Length - 2);
                if (innen.IndexOf('"') < 0 && innen.IndexOf('„') < 0) text = innen.Trim();
            }
            return text;
        }
    }
}
