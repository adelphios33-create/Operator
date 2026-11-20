// Figuren-Mod – Rollen aus Textdateien (Ordner "Rollen" im Mod)
// Eine Datei pro Charakter. Aufbau siehe Rollen/_ANLEITUNG.txt
// Eine Datei mit der Zeile "#aus" wird übersprungen.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Figuren.Kern
{
    public class Antwort
    {
        public string Text = "";
        public string Ziel = "";
    }

    public class Abschnitt
    {
        public string Id = "";
        public string Text = "";
        public List<Antwort> Antworten = new List<Antwort>();
    }

    public class Rolle
    {
        public string Datei = "";
        public string Name = "";
        public string Titel = "";
        public string Aussehen = "Joel";
        public string Start = "";
        /// <summary>Text aus dem Abschnitt [ki]: Persönlichkeit/Backstory für LM Studio. Gesetzt = Typ 2 (KI-Figur).</summary>
        public string KiText = "";
        public bool IstKi { get { return KiText.Length > 0; } }
        /// <summary>Nur der Erzähler im Leitungsfenster (keine Figur in der Welt).</summary>
        public bool Spielleiter = false;
        /// <summary>Erscheint im Leitungsfenster (Taste Pos1, im Spiel auch "Leitungsfenster"). Nur Hauptfiguren: Zeile "Leitung: ja" in der Rollendatei.</summary>
        public bool InLeitung = false;
        /// <summary>Vorlage: beim Aufstellen entsteht daraus eine eigene Figur mit Zufallsnamen (und Stadt).</summary>
        public bool Vorlage = false;
        public List<string> Namen = new List<string>();
        /// <summary>Zeile "Art: Hauptrolle" oder "Art: Nebenrolle" bei Einzelfiguren.</summary>
        public string ArtText = "";
        /// <summary>Vorgabe Typ 3 (Zeilen "Waffe:" und "Haltung:"), z. B. beim Wachmann.</summary>
        public string Waffe = "";
        public string Haltung = "";
        /// <summary>Grundrolle (KI, Zufallsname), Feste Figur (fester Klick-Dialog, Zufallsname), Hauptrolle, Nebenrolle.</summary>
        public string Art
        {
            get
            {
                if (Vorlage) return IstKi ? "Grundrolle" : "Feste Figur";
                return ArtText.Equals("Hauptrolle", StringComparison.OrdinalIgnoreCase) ? "Hauptrolle" : "Nebenrolle";
            }
        }
        public int Rang { get { var a = Art; return a == "Grundrolle" ? 0 : a == "Feste Figur" ? 1 : a == "Hauptrolle" ? 2 : 3; } }
        public List<string> Staedte = new List<string>();
        public List<string> AussehenListe = new List<string>();
        /// <summary>Namen für das Frauen-Modell Jen (Zeile "Frauennamen:"). Ohne diese Zeile wird Jen nie gewählt.</summary>
        public List<string> FrauenNamen = new List<string>();
        /// <summary>Nur bei diesen Branchen darf Jen vorkommen (Zeile "Frauen-Branchen:"). Leer = alle.</summary>
        public List<string> FrauenBranchen = new List<string>();
        /// <summary>Bei einer aufgestellten Figur: aus welcher Vorlage sie stammt und für welche Stadt.</summary>
        public string VorlageName = "";
        /// <summary>Bekommt die Immobilien-Angebote seiner Stadt als Spieldaten (Zeile "Immobilien: ja").</summary>
        public bool Immobilien = false;
        /// <summary>Bekommt Kreditregeln, Firmen- und Personendaten aus der Wirtschaft-Mod (Zeile "Finanzen: ja").</summary>
        public bool Finanzen = false;
        public string Stadt = "";
        /// <summary>Vorlage: wählbare Branchen (Zeile "Branchen: ..."). Figur: ihre Branche.</summary>
        public List<string> Branchen = new List<string>();
        public string Branche = "";
        /// <summary>Zeile "Firma: ja": Vorlage mit Firmenname, Branche und Firmengröße (Unternehmer). Figur: ihre Firma und Größe.</summary>
        public bool FirmaFelder = false;
        public string FirmaName = "";
        public string Groesse = "";
        /// <summary>Branchen-Schlüssel -> ausführlicher Name (Zeile "Branchen: Logistik=Logistik und Transport, ...") und alte Branche -> neue.</summary>
        public Dictionary<string, string> BrancheNamen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> BrancheAlt = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, Abschnitt> Abschnitte = new Dictionary<string, Abschnitt>(StringComparer.OrdinalIgnoreCase);

        public Abschnitt Holen(string id)
        {
            Abschnitt a;
            return id != null && Abschnitte.TryGetValue(id, out a) ? a : null;
        }
    }

    public static class Rollen
    {
        /// <summary>Ein Modell (Aussehen). Name = Endung der Entity-Klasse "figur" + Name (Config/entityclasses.xml).</summary>
        public class Modell
        {
            public string Name; public string Gruppe; public bool Frau;
            public Modell(string name, string gruppe, bool frau) { Name = name; Gruppe = gruppe; Frau = frau; }
        }

        /// <summary>Alle Modelle: 5 Händler aus dem Spiel, 8 Rogues und 15 Civilians (khzmusik, Resources/*.unity3d).</summary>
        public static readonly Modell[] Modelle =
        {
            new Modell("Joel", "Händler", false), new Modell("Bob", "Händler", false), new Modell("Hugh", "Händler", false),
            new Modell("Rekt", "Händler", false), new Modell("Jen", "Händler", true),
            new Modell("RogueBlonde", "Rogues", false), new Modell("RogueCowboy", "Rogues", false),
            new Modell("RogueYoung", "Rogues", false), new Modell("RogueAged", "Rogues", false),
            new Modell("RogueFrauAlt", "Rogues", true), new Modell("RogueFrauStark", "Rogues", true),
            new Modell("RogueFrauLeder1", "Rogues", true), new Modell("RogueFrauLeder2", "Rogues", true),
            new Modell("ZivilAlt", "Zivil", false), new Modell("ZivilAltKittel", "Zivil", false), new Modell("ZivilAltDunkel", "Zivil", false),
            new Modell("ZivilCowboy", "Zivil", false), new Modell("ZivilJerry", "Zivil", false),
            new Modell("ZivilJung1", "Zivil", false), new Modell("ZivilJung2", "Zivil", false), new Modell("ZivilJung3", "Zivil", false),
            new Modell("ZivilFrauAlt", "Zivil", true), new Modell("ZivilFrauKittel", "Zivil", true),
            new Modell("ZivilFrauJung1", "Zivil", true), new Modell("ZivilFrauJung2", "Zivil", true),
            new Modell("ZivilFrauJung3", "Zivil", true), new Modell("ZivilFrauJung4", "Zivil", true),
            new Modell("ZivilFrauJung5", "Zivil", true),
            // Soldaten: 8 Menschen-Soldaten (Guppycur, altes Paket von 2019 - Versuch) und der Soldaten-Zombie aus dem Spiel.
            // Gruppe "Soldaten" kommt nie durch "alle"/"Männer" in den Zufall, nur wenn die Rolle sie ausdrücklich nennt.
            new Modell("Soldat1", "Soldaten", false), new Modell("Soldat2", "Soldaten", false), new Modell("Soldat3", "Soldaten", false),
            new Modell("Soldat4", "Soldaten", false), new Modell("Soldat5", "Soldaten", false), new Modell("Soldat6", "Soldaten", false),
            new Modell("Soldat7", "Soldaten", false), new Modell("Soldat8", "Soldaten", false),
            new Modell("ZombieSoldat", "Zombie", false)
        };

        /// <summary>Sondermodelle (Soldaten, Zombie): nicht im allgemeinen Zufall, eigene Behandlung beim Erzeugen.</summary>
        public static bool IstSonder(string aussehen)
        {
            return Modelle.Any(m => (m.Gruppe == "Soldaten" || m.Gruppe == "Zombie") && string.Equals(m.Name, aussehen, StringComparison.OrdinalIgnoreCase));
        }

        public static readonly string[] Aussehen = Modelle.Select(m => m.Name).ToArray();

        /// <summary>Frauennamen, falls eine Rolle keine eigene Zeile "Frauennamen:" hat.</summary>
        public static readonly List<string> StandardFrauennamen = new List<string> { "Anna", "Maria", "Sabine", "Laura", "Julia", "Claudia", "Petra", "Nina", "Eva", "Sandra", "Lisa", "Vera" };

        public static bool IstFrau(string aussehen)
        {
            return Modelle.Any(m => m.Frau && string.Equals(m.Name, aussehen, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Liest eine Aussehen-Zeile: einzelne Modelle und Gruppen (Händler, Rogues, Zivil, Männer, Frauen, alle).</summary>
        public static List<string> AussehenAusListe(string text)
        {
            var erg = new List<string>();
            foreach (var roh in (text ?? "").Split(','))
            {
                string w = roh.Trim();
                if (w.Length == 0) continue;
                string k = w.ToLowerInvariant();
                IEnumerable<Modell> treffer;
                if (k == "alle") treffer = Modelle.Where(m => !IstSonder(m.Name));
                else if (k == "männer" || k == "maenner") treffer = Modelle.Where(m => !m.Frau && !IstSonder(m.Name));
                else if (k == "soldaten") treffer = Modelle.Where(m => m.Gruppe == "Soldaten");
                else if (k == "frauen") treffer = Modelle.Where(m => m.Frau);
                else if (k == "händler" || k == "haendler") treffer = Modelle.Where(m => m.Gruppe == "Händler" && !m.Frau);
                else if (k == "rogues") treffer = Modelle.Where(m => m.Gruppe == "Rogues" && !m.Frau);
                else if (k == "zivil") treffer = Modelle.Where(m => m.Gruppe == "Zivil" && !m.Frau);
                else if (k == "roguesfrauen") treffer = Modelle.Where(m => m.Gruppe == "Rogues" && m.Frau);
                else if (k == "zivilfrauen") treffer = Modelle.Where(m => m.Gruppe == "Zivil" && m.Frau);
                else treffer = Modelle.Where(m => string.Equals(m.Name, w, StringComparison.OrdinalIgnoreCase));
                foreach (var m in treffer) if (!erg.Contains(m.Name)) erg.Add(m.Name);
            }
            return erg;
        }

        /// <summary>Darf diese Rolle zufällig ein Frauenmodell bekommen? (nur mit Zeile "Frauennamen:" und passender Branche)</summary>
        public static bool FrauErlaubt(Rolle v, string branche)
        {
            return v != null && v.FrauenNamen.Count > 0
                && (v.FrauenBranchen.Count == 0 || v.FrauenBranchen.Any(b => b.Equals(branche ?? "", StringComparison.OrdinalIgnoreCase)));
        }

        /// <summary>Zufälliges Aussehen für eine Rolle (ohne das aktuelle). Ohne Liste: alle Männer.</summary>
        public static string ZufallsAussehen(Rolle v, string branche, string ausser)
        {
            var liste = v != null && v.AussehenListe.Count > 0 ? v.AussehenListe : AussehenAusListe("Männer");
            var wahl = liste.Where(a => (!IstFrau(a) || FrauErlaubt(v, branche)) && !Figuren.Spiel.FigurenMod.Kaputt.Contains(a)).ToList();
            if (wahl.Count > 1) wahl.RemoveAll(a => string.Equals(a, ausser, StringComparison.OrdinalIgnoreCase));
            return wahl.Count > 0 ? wahl[Zufall.Next(wahl.Count)] : (v != null ? v.Aussehen : "Joel");
        }

        public static List<Rolle> Liste = new List<Rolle>();
        /// <summary>Städte der Welt (KI.txt, Zeile "Städte: ..."). Gilt für alle Vorlagen ohne eigene Städte-Zeile.</summary>
        public static List<string> Staedte = new List<string> { "Springfield", "Berlin", "LZ", "WS" };

        public static List<string> StaedteFuer(Rolle r)
        {
            return r != null && r.Staedte.Count > 0 ? r.Staedte : Staedte;
        }
        public static string Ordner = "";

        public static string AussehenOderStandard(string wunsch)
        {
            var a = Aussehen.FirstOrDefault(w => string.Equals(w, (wunsch ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
            return a ?? "Joel";
        }

        /// <summary>Aufgestellte Figuren, die aus Vorlagen entstanden sind (Name -> Rolle).</summary>
        public static Dictionary<string, Rolle> Instanzen = new Dictionary<string, Rolle>(StringComparer.OrdinalIgnoreCase);
        /// <summary>Wird nach dem (Neu-)Laden der Rollen aufgerufen, damit Instanzen aus dem Spielstand neu entstehen.</summary>
        public static Action NachLaden;
        static readonly Random Zufall = new Random();

        public static Rolle Holen(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            Rolle i;
            if (Instanzen.TryGetValue(name, out i)) return i;
            return Liste.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase))
                ?? Liste.FirstOrDefault(r => string.Equals(r.Datei, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Alle echten Personen: feste Rollen (ohne Vorlagen) plus aufgestellte Vorlagen-Figuren.</summary>
        public static IEnumerable<Rolle> Personen()
        {
            return Liste.Where(r => !r.Vorlage).Concat(Instanzen.Values);
        }

        public class Wahl
        {
            public Rolle Rolle;
            public string Stadt = "";
            public string Text { get { return Rolle.Name + (Stadt.Length > 0 ? " (" + Stadt + ")" : "") + (Rolle.Titel.Length > 0 && Rolle.Vorlage == false ? " - " + Rolle.Titel : ""); } }
        }

        /// <summary>Fester Dialog: "Text A || Text B" = eine Variante zufällig; {Name} und {Stadt} werden ersetzt.</summary>
        public static string Variante(string text, Rolle r)
        {
            if (string.IsNullOrEmpty(text)) return text ?? "";
            var teile = text.Split(new[] { "||" }, StringSplitOptions.None);
            string t = teile[Zufall.Next(teile.Length)].Trim();
            if (r != null) t = t.Replace("{Name}", r.Name).Replace("{Stadt}", r.Stadt.Length > 0 ? r.Stadt : "der Stadt");
            return t;
        }

        /// <summary>Auswahlliste im Einrichten-Fenster: erst die Vorlagen (Grundrollen), dann die festen Figuren.</summary>
        public static List<Rolle> Auswahl()
        {
            return Liste.OrderBy(r => r.Rang).ThenBy(r => r.Name).ToList();
        }

        /// <summary>Firmengrößen: Schlüssel (für @-Zeilen), Anzeige im Fenster, Beschreibung für die KI.</summary>
        public static readonly string[] GroessenKey = { "Klein", "Mittel", "Gross" };
        public static readonly string[] GroessenAnzeige = { "Klein", "Mittel", "Groß" };
        public static readonly string[] GroessenText =
        {
            "Kleinunternehmen (kleiner Betrieb mit wenigen Mitarbeitern, überschaubarem Kundenkreis und begrenzten finanziellen Möglichkeiten)",
            "mittelständisches Unternehmen (etablierter Betrieb mit mehreren Mitarbeitern, regelmäßigen Geschäftsbeziehungen und größerem Handlungsspielraum)",
            "Großunternehmen (größerer Betrieb mit zahlreichen Mitarbeitern, mehreren Geschäftsbereichen oder Standorten und entsprechend größerem wirtschaftlichem Einfluss)"
        };
        public static int GroesseIndex(string g)
        {
            if (string.IsNullOrEmpty(g)) return 0;
            for (int i = 0; i < GroessenKey.Length; i++)
                if (string.Equals(GroessenKey[i], g, StringComparison.OrdinalIgnoreCase) || string.Equals(GroessenAnzeige[i], g, StringComparison.OrdinalIgnoreCase)) return i;
            return 0;
        }

        /// <summary>Ausführlicher Name einer Branche (oder die Branche selbst, wenn keiner hinterlegt ist).</summary>
        public static string BrancheText(Rolle v, string branche)
        {
            string s;
            return v != null && !string.IsNullOrEmpty(branche) && v.BrancheNamen.TryGetValue(branche, out s) ? s : (branche ?? "");
        }

        static string Ersetzen(string text, string name, string stadt, string branche, string brancheText = null, string firma = "", string groesse = "")
        {
            var sb = new StringBuilder();
            foreach (var roh in text.Replace("\r", "").Split('\n'))
            {
                string z = roh;
                if (z.StartsWith("@"))
                {
                    int sp = z.IndexOf(' ');
                    if (sp < 0) continue;
                    string ort = z.Substring(1, sp - 1);
                    if (!string.Equals(ort, stadt, StringComparison.OrdinalIgnoreCase) && !string.Equals(ort, branche, StringComparison.OrdinalIgnoreCase)
                        && !(groesse.Length > 0 && string.Equals(ort, groesse, StringComparison.OrdinalIgnoreCase))) continue;
                    z = z.Substring(sp + 1).TrimStart();
                }
                sb.AppendLine(z);
            }
            int gi = Math.Max(0, Array.FindIndex(GroessenKey, k => string.Equals(k, groesse, StringComparison.OrdinalIgnoreCase)));
            string firmaText = (firma ?? "").Length > 0 ? firma : "(der Name ist noch nicht festgelegt: denk dir beim ersten Gespräch einen passenden aus und behalte ihn bei)";
            return sb.ToString().Trim().Replace("{Name}", name).Replace("{Stadt}", stadt.Length > 0 ? stadt : "dieser Stadt")
                .Replace("{Branche}", branche.Length > 0 ? (string.IsNullOrEmpty(brancheText) ? branche : brancheText) : "Allgemein")
                .Replace("{Firma}", firmaText).Replace("{Groesse}", groesse.Length > 0 ? GroessenText[gi] : "Betrieb mittlerer Größe");
        }

        /// <summary>Erzeugt aus einer Vorlage eine eigene Figur. Name und Aussehen nur angeben, wenn sie schon feststehen (aus dem Spielstand).</summary>
        public static Rolle Instanz(Rolle v, string stadt, string name, string aussehen, string branche = "", string firma = "", string groesse = "")
        {
            stadt = stadt ?? "";
            branche = branche ?? "";
            firma = (firma ?? "").Trim();
            string alt;
            if (branche.Length > 0 && !v.Branchen.Any(w => string.Equals(w, branche, StringComparison.OrdinalIgnoreCase)) && v.BrancheAlt.TryGetValue(branche, out alt)) branche = alt;
            if (branche.Length == 0 && v.Branchen.Count > 0) branche = v.Branchen[0];
            groesse = v.FirmaFelder ? GroessenKey[GroesseIndex(groesse)] : "";
            string brancheText = BrancheText(v, branche);
            if (string.IsNullOrEmpty(aussehen)) aussehen = ZufallsAussehen(v, branche, "");
            bool frau = IstFrau(aussehen);
            var namen = frau ? (v.FrauenNamen.Count > 0 ? v.FrauenNamen : StandardFrauennamen) : v.Namen;
            if (string.IsNullOrEmpty(name))
            {
                var frei = namen.Where(n => Holen(n) == null).ToList();
                if (frei.Count > 0) name = frei[Zufall.Next(frei.Count)];
                else
                {
                    string basis = namen.Count > 0 ? namen[Zufall.Next(namen.Count)] : v.Name;
                    int n = 2;
                    while (Holen(basis + " " + n) != null) n++;
                    name = basis + " " + n;
                }
            }
            string titel = v.Titel.Contains("{Stadt}") ? v.Titel.Replace("{Stadt}", stadt.Length > 0 ? stadt : "") : v.Titel + (stadt.Length > 0 ? " in " + stadt : "");
            titel = titel.Replace("{Branche}", brancheText).Replace("{Firma}", firma.Length > 0 ? firma : v.Name).Replace("{Groesse}", groesse.Length > 0 ? GroessenAnzeige[GroesseIndex(groesse)] : "");
            var r = new Rolle
            {
                Datei = v.Datei, Name = name, Titel = titel.Trim(), Aussehen = AussehenOderStandard(aussehen),
                Start = v.Start, Abschnitte = v.Abschnitte, InLeitung = false, Vorlage = false,
                VorlageName = v.Name, Stadt = stadt, Immobilien = v.Immobilien, Finanzen = v.Finanzen, Branche = branche, FirmaName = firma, Groesse = groesse,
                Waffe = v.Waffe, Haltung = v.Haltung,
                KiText = v.KiText.Length > 0 ? (frau ? "Wichtig: " + name + " ist eine Frau. Sprich von dir in weiblicher Form; Angaben mit \"er\" in dieser Beschreibung gelten für dich als \"sie\".\n" : "") + Ersetzen(v.KiText, name, stadt, branche, brancheText, firma, groesse) : ""
            };
            Instanzen[name] = r;
            return r;
        }

        /// <summary>Liest alle .txt-Dateien neu ein (außer solchen, die mit _ beginnen). Gibt eine kurze Meldung zurück.</summary>
        public static string Laden()
        {
            var neu = new List<Rolle>();
            var fehler = new List<string>();
            if (!Directory.Exists(Ordner)) return "Ordner Rollen fehlt: " + Ordner;
            foreach (var pfad in Directory.GetFiles(Ordner, "*.txt").OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                string datei = Path.GetFileNameWithoutExtension(pfad);
                if (datei.StartsWith("_")) continue;
                try
                {
                    var zeilen = File.ReadAllLines(pfad, Encoding.UTF8);
                    if (zeilen.Any(w => w.Trim().Equals("#aus", StringComparison.OrdinalIgnoreCase))) continue;
                    var r = Lesen(zeilen, datei);
                    if (neu.Any(w => string.Equals(w.Name, r.Name, StringComparison.OrdinalIgnoreCase)))
                        fehler.Add(datei + ": Name '" + r.Name + "' gibt es doppelt");
                    else neu.Add(r);
                }
                catch (Exception e) { fehler.Add(datei + ": " + e.Message); }
            }
            Liste = neu;
            Instanzen.Clear();
            if (NachLaden != null) { try { NachLaden(); } catch (Exception) { } }
            string meldung = neu.Count + " Rollen geladen";
            if (fehler.Count > 0) meldung += ", Fehler: " + string.Join("; ", fehler.ToArray());
            return meldung;
        }

        public static Rolle Lesen(string[] zeilen, string datei)
        {
            var r = new Rolle { Datei = datei, Name = datei };
            Abschnitt akt = null, kiAbschnitt = null;
            foreach (var roh in zeilen)
            {
                string z = roh.TrimEnd();
                string t = z.Trim();
                if (t.Length == 0)
                {
                    if (akt != null && akt.Text.Length > 0 && akt.Antworten.Count == 0) akt.Text += "\n";
                    continue;
                }
                if (t.StartsWith("#")) continue;

                if (t.StartsWith("[") && t.EndsWith("]"))
                {
                    akt = new Abschnitt { Id = t.Substring(1, t.Length - 2).Trim() };
                    if (akt.Id.Length == 0) throw new Exception("Leerer Abschnitt []");
                    if (akt.Id.Equals("ki", StringComparison.OrdinalIgnoreCase)) { kiAbschnitt = akt; continue; }
                    r.Abschnitte[akt.Id] = akt;
                    if (r.Start.Length == 0) r.Start = akt.Id;
                    continue;
                }

                if (akt == null)
                {
                    int dp = t.IndexOf(':');
                    if (dp <= 0) continue;
                    string k = t.Substring(0, dp).Trim().ToLowerInvariant();
                    string v = t.Substring(dp + 1).Trim();
                    if (k == "name" && v.Length > 0) r.Name = v;
                    else if (k == "titel") r.Titel = v;
                    else if (k == "aussehen")
                    {
                        r.AussehenListe = AussehenAusListe(v);
                        r.Aussehen = AussehenOderStandard(r.AussehenListe.Count > 0 ? r.AussehenListe[0] : v);
                    }
                    else if (k == "vorlage") r.Vorlage = v.Equals("ja", StringComparison.OrdinalIgnoreCase) || v.Equals("an", StringComparison.OrdinalIgnoreCase);
                    else if (k == "branchen")
                    {
                        r.Branchen.Clear();
                        foreach (var w0 in v.Split(',').Select(w => w.Trim()).Where(w => w.Length > 0))
                        {
                            int gl = w0.IndexOf('=');
                            string key = gl > 0 ? w0.Substring(0, gl).Trim() : w0;
                            r.Branchen.Add(key);
                            if (gl > 0) r.BrancheNamen[key] = w0.Substring(gl + 1).Trim();
                        }
                    }
                    else if (k == "branchen-alt" || k == "branchenalt")
                    {
                        foreach (var w0 in v.Split(',').Select(w => w.Trim()).Where(w => w.Length > 0))
                        {
                            int gl = w0.IndexOf('=');
                            if (gl > 0) r.BrancheAlt[w0.Substring(0, gl).Trim()] = w0.Substring(gl + 1).Trim();
                        }
                    }
                    else if (k == "firma") r.FirmaFelder = v.Equals("ja", StringComparison.OrdinalIgnoreCase) || v.Equals("an", StringComparison.OrdinalIgnoreCase);
                    else if (k == "finanzen") r.Finanzen = v.Equals("ja", StringComparison.OrdinalIgnoreCase) || v.Equals("an", StringComparison.OrdinalIgnoreCase);
                    else if (k == "immobilien") r.Immobilien = v.Equals("ja", StringComparison.OrdinalIgnoreCase) || v.Equals("an", StringComparison.OrdinalIgnoreCase);
                    else if (k == "art") r.ArtText = v;
                    else if (k == "waffe") r.Waffe = v;
                    else if (k == "haltung") r.Haltung = v;
                    else if (k == "frauennamen") r.FrauenNamen = v.Split(',').Select(w => w.Trim()).Where(w => w.Length > 0).ToList();
                    else if (k == "frauen-branchen" || k == "frauenbranchen") r.FrauenBranchen = v.Split(',').Select(w => w.Trim()).Where(w => w.Length > 0).ToList();
                    else if (k == "namen") r.Namen = v.Split(',').Select(w => w.Trim()).Where(w => w.Length > 0).ToList();
                    else if (k == "städte" || k == "staedte") r.Staedte = v.Split(',').Select(w => w.Trim()).Where(w => w.Length > 0).ToList();
                    else if (k == "leitung") r.InLeitung = v.Equals("ja", StringComparison.OrdinalIgnoreCase) || v.Equals("an", StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (t.StartsWith(">"))
                {
                    string inhalt = t.Substring(1).Trim();
                    var a = new Antwort();
                    int pf = inhalt.LastIndexOf("->", StringComparison.Ordinal);
                    if (pf >= 0)
                    {
                        a.Text = inhalt.Substring(0, pf).Trim();
                        a.Ziel = inhalt.Substring(pf + 2).Trim();
                    }
                    else a.Text = inhalt;
                    if (a.Text.Length > 0) akt.Antworten.Add(a);
                    continue;
                }

                // Im Abschnitt [ki] bleiben Zeilen erhalten (Felder wie "Herkunft: ..." stehen dann je in einer Zeile).
                if (akt == kiAbschnitt) akt.Text = akt.Text.Length == 0 ? t : akt.Text.TrimEnd('\n') + "\n" + t;
                else akt.Text = akt.Text.Length == 0 ? t : akt.Text + (akt.Text.EndsWith("\n") ? "" : " ") + t;
            }
            foreach (var a in r.Abschnitte.Values) a.Text = a.Text.Trim();
            if (kiAbschnitt != null) r.KiText = kiAbschnitt.Text.Trim();
            if (r.Abschnitte.Count == 0 && !r.IstKi && !r.Vorlage) throw new Exception("kein Abschnitt wie [start] oder [ki] gefunden");
            return r;
        }
    }
}
