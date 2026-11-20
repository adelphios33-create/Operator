// Wirtschaft-Mod – Advanced Business Management (ABM): optionale Erweiterung des Firmentresors.
// Reiner C#-Code ohne Spiel-Abhängigkeiten.
//
// Grundsätze:
//  - Das bestehende System bleibt die Grundlage. Ohne ABM rechnet jede Firma genau wie vorher.
//  - Es gibt keine zweite Abrechnung: Löhne und Vertragsumsätze laufen durch dieselbe Monatsbuchung (Bank.FirmaAbrechnen),
//    also durch dieselbe Steuerrechnung. "Umsatz / Monat" und "Ausgaben / Monat" im Firmentresor sind ab jetzt
//    "sonstiger Umsatz" und "sonstige Ausgaben"; Verträge und Löhne kommen dazu.
//  - Schutz vor Doppelbuchung: Beim ersten Einschalten der Personalverwaltung wird die bisherige Mitarbeiterzahl als Gruppe
//    "Bisherige Mitarbeiter" mit "Lohn schon in Ausgaben = Ja" übernommen. Solche Gruppen zählen als Köpfe, kosten aber nichts extra.
//  - Ausschalten löscht nichts. Die Daten bleiben gespeichert und wirken nur nicht mehr.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace Wirtschaft.Kern
{
    public class AbmFeld
    {
        [XmlAttribute] public string K = "";
        [XmlAttribute] public string V = "";
    }

    /// <summary>Ein Datensatz der erweiterten Verwaltung (Mitarbeiter, Gruppe, Lieferant, Vertrag, Produkt, Projekt, Lieferung).</summary>
    public class AbmEintrag
    {
        [XmlAttribute] public string Art = "";
        [XmlAttribute] public int Nr;
        [XmlAttribute] public int Angelegt;
        public List<AbmFeld> Felder = new List<AbmFeld>();

        public string Hol(string k)
        {
            var f = Felder.FirstOrDefault(w => w.K == k);
            return f == null ? "" : (f.V ?? "");
        }

        public void Setz(string k, string v)
        {
            var f = Felder.FirstOrDefault(w => w.K == k);
            if (f == null) { f = new AbmFeld { K = k }; Felder.Add(f); }
            f.V = (v ?? "").Trim();
        }

        public decimal Zahl(string k)
        {
            decimal d;
            return Befehle.VersucheZahl(Hol(k), out d) ? d : 0m;
        }

        public bool Ist(string k, string wert) { return string.Equals(Hol(k), wert, StringComparison.OrdinalIgnoreCase); }

        [XmlIgnore] public string Name { get { return Hol("Name"); } }
    }

    public class AbmFeldDef
    {
        public string Key = "", Label = "";
        public string[] Werte;
        public bool IstZahl;
        /// <summary>Wird vom Spiel gesetzt (z. B. Verknüpfung mit einer Firma), nicht von Hand.</summary>
        public bool NurLesen;
    }

    public class AbmArt
    {
        public string Art = "", Modul = "", Titel = "";
        public AbmFeldDef[] Felder;
    }

    public static class Abm
    {
        public const string Personal = "Personal", Lieferanten = "Lieferanten", Kunden = "Kunden", Produkte = "Produkte", Projekte = "Projekte", Logistik = "Logistik";
        public static readonly string[] Module = { Personal, Lieferanten, Kunden, Produkte, Projekte, Logistik };
        public static readonly string[] ModulText = { "Personal", "Lieferanten", "Kunden & Verträge", "Produkte & Lager", "Projekte", "Lieferungen" };

        public const int MaxFelder = 14;

        static AbmFeldDef T(string k, string l) { return new AbmFeldDef { Key = k, Label = l }; }
        static AbmFeldDef Z(string k, string l) { return new AbmFeldDef { Key = k, Label = l, IstZahl = true }; }
        static AbmFeldDef W(string k, string l, params string[] w) { return new AbmFeldDef { Key = k, Label = l, Werte = w }; }
        static AbmFeldDef L(string k, string l) { return new AbmFeldDef { Key = k, Label = l, NurLesen = true }; }

        public static readonly AbmArt[] Arten =
        {
            new AbmArt { Art = "Mitarbeiter", Modul = Personal, Titel = "Mitarbeiter", Felder = new[] {
                T("Name", "Name"), T("Konto", "Person (Lohnkonto)"), T("Position", "Position"), T("Taetigkeit", "Tätigkeit"),
                T("Abteilung", "Abteilung"), Z("Lohn", "Lohn / Monat"), W("Status", "Status", "Aktiv", "Beurlaubt", "Deaktiviert"),
                Z("Eingestellt", "Eingestellt (Spieltag)"), Z("Bonus", "Bonus / Monat"), Z("Provision", "Provision % vom Umsatz"), T("Notiz", "Notiz") } },
            new AbmArt { Art = "Gruppe", Modul = Personal, Titel = "Mitarbeitergruppe", Felder = new[] {
                T("Name", "Bezeichnung"), Z("Anzahl", "Anzahl"), T("Taetigkeit", "Tätigkeit"), Z("Lohn", "Lohn pro Kopf / Monat"),
                T("Abteilung", "Abteilung"), W("Status", "Status", "Aktiv", "Deaktiviert"),
                W("InAusgaben", "Lohn schon in Ausgaben?", "Nein", "Ja"), T("Notiz", "Notiz") } },
            new AbmArt { Art = "Lieferant", Modul = Lieferanten, Titel = "Lieferant", Felder = new[] {
                T("Name", "Firmenname"), L("Firma", "Firma im Spiel"), T("Branche", "Branche"), T("Standort", "Standort"),
                T("Ansprechpartner", "Ansprechpartner"), T("Produkte", "Produkte / Leistungen"), T("Preise", "Einkaufspreise"),
                T("Bedingungen", "Lieferbedingungen"), T("Lieferzeit", "Lieferzeit"), T("Mindestmenge", "Mindestbestellung"),
                T("Zahlung", "Zahlungsbedingungen"), W("Zuverlaessigkeit", "Zuverlässigkeit", "Unbekannt", "Niedrig", "Mittel", "Hoch"),
                W("Beziehung", "Geschäftsbeziehung", "Kontakt", "Verhandlung", "Aktiv", "Bevorzugt", "Pausiert", "Beendet"),
                T("Vereinbarung", "Vereinbarungen") } },
            new AbmArt { Art = "Vertrag", Modul = Kunden, Titel = "Kundenvertrag", Felder = new[] {
                T("Kunde", "Kunde"), W("Kundenart", "Kundenart", "Firma", "Person", "Organisation"), T("Name", "Vertragsbezeichnung"),
                T("Leistung", "Leistung"), Z("Wert", "Wert / Monat"), Z("Beginn", "Beginn (Spieltag)"), Z("Laufzeit", "Laufzeit Monate (0 = offen)"),
                W("Intervall", "Abrechnung", "Monatlich", "Quartal", "Jährlich"), W("Status", "Status", "Entwurf", "Aktiv", "Pausiert", "Beendet"),
                Z("Kuendigung", "Kündigungsfrist Monate"), T("Notiz", "Notiz") } },
            new AbmArt { Art = "Produkt", Modul = Produkte, Titel = "Produkt", Felder = new[] {
                T("Name", "Produktname"), T("Beschreibung", "Beschreibung"), T("Kategorie", "Kategorie"), Z("EK", "Einkaufspreis"),
                Z("VK", "Verkaufspreis"), Z("Bestand", "Lagerbestand"), Z("Mindest", "Mindestbestand"), T("Lieferanten", "Lieferanten"), T("Notiz", "Notiz") } },
            new AbmArt { Art = "Projekt", Modul = Projekte, Titel = "Projekt", Felder = new[] {
                T("Name", "Projekttitel"), T("Beschreibung", "Beschreibung"), T("Verantwortlich", "Verantwortlich"), T("Beteiligte", "Beteiligte Firmen"),
                Z("Kosten", "Geschätzte Kosten"), T("Zeitraum", "Geplanter Zeitraum"),
                W("Status", "Status", "Idee", "In Planung", "Aktiv", "Pausiert", "Abgeschlossen", "Abgebrochen"), T("Notiz", "Notizen") } },
            new AbmArt { Art = "Lieferung", Modul = Logistik, Titel = "Lieferauftrag", Felder = new[] {
                T("Name", "Lieferauftrag"), T("Lieferant", "Lieferant"), T("Produkt", "Produkt"), Z("Menge", "Menge"), T("Termin", "Liefertermin"),
                W("Status", "Status", "Angefragt", "Bestätigt", "Unterwegs", "Geliefert", "Verzögert", "Storniert"),
                Z("Transport", "Transportkosten"), T("Notiz", "Notiz") } },
        };

        public static AbmArt ArtDef(string art) { return Arten.FirstOrDefault(a => a.Art == art); }
        public static List<AbmArt> ArtenVon(string modul) { return Arten.Where(a => a.Modul == modul).ToList(); }
        public static int ModulIndex(string modul) { return Array.IndexOf(Module, modul); }

        static bool Gleich(string a, string b) { return string.Equals((a ?? "").Trim(), (b ?? "").Trim(), StringComparison.OrdinalIgnoreCase); }

        // ---------- Ein- und Ausschalten ----------

        static List<string> ModulListe(Unternehmen u)
        {
            return (u.AbmModule ?? "").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        }

        /// <summary>Modul ausgewählt (unabhängig davon, ob ABM gerade an ist).</summary>
        public static bool ModulGewaehlt(Unternehmen u, string modul) { return ModulListe(u).Contains(modul); }

        /// <summary>Modul wirkt: ABM an und Modul gewählt.</summary>
        public static bool ModulAn(Unternehmen u, string modul) { return u != null && u.AbmAn && ModulGewaehlt(u, modul); }

        public static string AnSetzen(Unternehmen u, bool an, int heute)
        {
            u.AbmAn = an;
            if (an) BestandUebernehmen(u, heute);
            Abgleichen(u);
            return an ? "Erweiterte Verwaltung für " + u.Name + " eingeschaltet." : "Erweiterte Verwaltung für " + u.Name + " ausgeschaltet. Alle Daten bleiben gespeichert.";
        }

        public static string ModulSetzen(Unternehmen u, string modul, bool an, int heute)
        {
            var l = ModulListe(u);
            l.Remove(modul);
            if (an) l.Add(modul);
            u.AbmModule = string.Join(",", Module.Where(m => l.Contains(m)).ToArray());
            if (an) BestandUebernehmen(u, heute);
            Abgleichen(u);
            int i = ModulIndex(modul);
            return (i >= 0 ? ModulText[i] : modul) + (an ? " eingeschaltet." : " ausgeschaltet (Daten bleiben).");
        }

        /// <summary>Beim ersten Einschalten der Personalverwaltung: bisherige Mitarbeiterzahl als Gruppe übernehmen.
        /// Ihr Lohn steckt schon in den Ausgaben, darum "Lohn schon in Ausgaben = Ja" (keine Doppelbuchung).</summary>
        static void BestandUebernehmen(Unternehmen u, int heute)
        {
            if (!ModulAn(u, Personal) || u.Mitarbeiter <= 0) return;
            if (u.Abm.Any(e => e.Art == "Mitarbeiter" || e.Art == "Gruppe")) return;
            var g = Neu(u, "Gruppe", heute);
            g.Setz("Name", "Bisherige Mitarbeiter");
            g.Setz("Anzahl", u.Mitarbeiter.ToString());
            g.Setz("Lohn", "0");
            g.Setz("InAusgaben", "Ja");
            g.Setz("Bestand", "ja");
            g.Setz("Notiz", "Aus der alten Mitarbeiterzahl übernommen. Ihr Lohn steckt schon in den Ausgaben.");
        }

        /// <summary>Bei aktiver Personalverwaltung ist die Mitarbeiterzahl der Firma die Summe aus Personal (für Firmenwert und Börse).</summary>
        public static void Abgleichen(Unternehmen u)
        {
            if (ModulAn(u, Personal)) u.Mitarbeiter = Kopfzahl(u);
        }

        public static int Kopfzahl(Unternehmen u)
        {
            int n = 0;
            foreach (var e in u.Abm)
            {
                if (e.Art == "Mitarbeiter" && !e.Ist("Status", "Deaktiviert")) n++;
                else if (e.Art == "Gruppe" && !e.Ist("Status", "Deaktiviert")) n += (int)Math.Max(0m, e.Zahl("Anzahl"));
            }
            return n;
        }

        // ---------- Datensätze ----------

        public static AbmEintrag Neu(Unternehmen u, string art, int heute)
        {
            var def = ArtDef(art);
            if (def == null) throw new WirtschaftsFehler("Unbekannte Art: " + art);
            var e = new AbmEintrag { Art = art, Nr = ++u.AbmNr, Angelegt = heute };
            foreach (var f in def.Felder) e.Setz(f.Key, f.Werte != null ? f.Werte[0] : "");
            if (art == "Mitarbeiter") { e.Setz("Status", "Aktiv"); e.Setz("Eingestellt", heute.ToString()); }
            if (art == "Vertrag") { e.Setz("Beginn", heute.ToString()); e.Setz("Laufzeit", "0"); e.Setz("Status", "Entwurf"); }
            if (art == "Gruppe") e.Setz("Anzahl", "1");
            if (art == "Lieferung") e.Setz("Status", "Angefragt");
            u.Abm.Add(e);
            return e;
        }

        public static List<AbmEintrag> Liste(Unternehmen u, string modul)
        {
            return u.Abm.Where(e => { var d = ArtDef(e.Art); return d != null && d.Modul == modul; }).OrderBy(e => e.Art).ThenBy(e => e.Nr).ToList();
        }

        /// <summary>Prüft einen Datensatz nach dem Bearbeiten. Liefert null oder einen Fehlertext.</summary>
        public static string Pruefen(Unternehmen u, AbmEintrag e)
        {
            var def = ArtDef(e.Art);
            if (def == null) return "Unbekannte Art.";
            if (e.Name.Length == 0) return "'" + def.Felder[0].Label + "' darf nicht leer sein.";
            foreach (var f in def.Felder)
            {
                string v = e.Hol(f.Key);
                if (f.IstZahl && v.Length > 0)
                {
                    decimal d;
                    if (!Befehle.VersucheZahl(v, out d)) return f.Label + ": '" + v + "' ist keine Zahl.";
                    if (d < 0) return f.Label + " darf nicht negativ sein.";
                }
                if (f.Werte != null && !f.Werte.Any(w => Gleich(w, v)))
                    return f.Label + ": nur " + string.Join(", ", f.Werte) + " (mit dem Knopf » durchschalten).";
                if (f.Werte != null) e.Setz(f.Key, f.Werte.First(w => Gleich(w, v)));
            }
            if (e.Art == "Mitarbeiter" && e.Zahl("Provision") > 100m) return "Die Provision darf höchstens 100 % sein.";
            if (e.Art == "Lieferant")
            {
                var doppelt = u.Abm.FirstOrDefault(w => w != e && w.Art == "Lieferant" && Gleich(w.Name, e.Name));
                if (doppelt != null) return "Den Lieferanten " + e.Name + " gibt es bei dieser Firma schon (Nr. " + doppelt.Nr + ").";
            }
            if (e.Art == "Mitarbeiter" && e.Hol("Konto").Length > 0 && Gleich(e.Hol("Konto"), u.Name)) return "Die Firma kann nicht ihr eigener Mitarbeiter sein.";
            return null;
        }

        // ---------- Rechnen: Löhne und Verträge ----------

        /// <summary>Lohn eines einzelnen Mitarbeiters pro Monat (Lohn + Bonus + Provision vom Umsatz). Beurlaubt und deaktiviert: 0.</summary>
        public static decimal LohnEinzeln(AbmEintrag m, decimal umsatzMonat)
        {
            if (!m.Ist("Status", "Aktiv")) return 0m;
            decimal p = m.Zahl("Provision");
            return Bank.Runden(m.Zahl("Lohn") + m.Zahl("Bonus") + (p > 0 && umsatzMonat > 0 ? umsatzMonat * p / 100m : 0m));
        }

        /// <summary>Lohnkosten einer Gruppe pro Monat. Gruppen mit "Lohn schon in Ausgaben = Ja" kosten nichts extra.</summary>
        public static decimal LohnGruppe(AbmEintrag g)
        {
            if (!g.Ist("Status", "Aktiv") || g.Ist("InAusgaben", "Ja")) return 0m;
            return Bank.Runden(Math.Max(0m, g.Zahl("Anzahl")) * g.Zahl("Lohn"));
        }

        /// <summary>Gesamte zusätzliche Personalkosten pro Monat (ohne die, die schon in den Ausgaben stecken).</summary>
        public static decimal LohnMonat(Unternehmen u, decimal umsatzMonat)
        {
            if (!ModulAn(u, Personal)) return 0m;
            decimal s = 0m;
            foreach (var e in u.Abm)
            {
                if (e.Art == "Mitarbeiter") s += LohnEinzeln(e, umsatzMonat);
                else if (e.Art == "Gruppe") s += LohnGruppe(e);
            }
            return s;
        }

        public static bool LohnAutomatisch(Unternehmen u) { return ModulAn(u, Personal) && !u.LohnManuell; }

        /// <summary>Löhne, die auf das Konto einer Person im Spiel gehen (Mitarbeiter mit "Person (Lohnkonto)").</summary>
        public static List<KeyValuePair<string, decimal>> Lohnempfaenger(Unternehmen u, decimal umsatzMonat)
        {
            var l = new List<KeyValuePair<string, decimal>>();
            if (!ModulAn(u, Personal)) return l;
            foreach (var e in u.Abm.Where(w => w.Art == "Mitarbeiter"))
            {
                string k = e.Hol("Konto");
                decimal b = LohnEinzeln(e, umsatzMonat);
                if (k.Length > 0 && b > 0) l.Add(new KeyValuePair<string, decimal>(k, b));
            }
            return l;
        }

        /// <summary>Läuft der Vertrag in dem Monat, der an diesem Spieltag endet?</summary>
        public static bool VertragLaeuft(AbmEintrag v, int tag, int tageProMonat)
        {
            if (v.Art != "Vertrag" || !v.Ist("Status", "Aktiv")) return false;
            int beginn = (int)v.Zahl("Beginn");
            if (beginn >= tag) return false;
            int laufzeit = (int)v.Zahl("Laufzeit");
            return laufzeit <= 0 || tag <= beginn + laufzeit * tageProMonat;
        }

        /// <summary>Umsatz aus Kundenverträgen für den Monat, der an diesem Spieltag endet.</summary>
        public static decimal VertragsUmsatzAm(Unternehmen u, int tag, int tageProMonat)
        {
            if (!ModulAn(u, Kunden)) return 0m;
            return u.Abm.Where(v => VertragLaeuft(v, tag, tageProMonat)).Sum(v => v.Zahl("Wert"));
        }

        /// <summary>Für die Vorschau: Verträge, die jetzt aktiv sind (begonnen und nicht abgelaufen).</summary>
        public static decimal VertragsUmsatzJetzt(Unternehmen u, int heute, int tageProMonat)
        {
            if (!ModulAn(u, Kunden)) return 0m;
            return u.Abm.Where(v => v.Art == "Vertrag" && v.Ist("Status", "Aktiv") && (int)v.Zahl("Beginn") <= heute
                                    && ((int)v.Zahl("Laufzeit") <= 0 || heute <= (int)v.Zahl("Beginn") + (int)v.Zahl("Laufzeit") * tageProMonat))
                        .Sum(v => v.Zahl("Wert"));
        }

        /// <summary>Verträge mit fester Laufzeit, die vorbei sind, auf "Beendet" setzen. Liefert die Namen.</summary>
        public static List<string> VertraegeAuslaufen(Unternehmen u, int heute, int tageProMonat)
        {
            var l = new List<string>();
            foreach (var v in u.Abm.Where(w => w.Art == "Vertrag" && w.Ist("Status", "Aktiv")))
            {
                int laufzeit = (int)v.Zahl("Laufzeit");
                if (laufzeit > 0 && heute >= (int)v.Zahl("Beginn") + laufzeit * tageProMonat)
                {
                    v.Setz("Status", "Beendet");
                    v.Setz("Notiz", (v.Hol("Notiz") + " | ausgelaufen an Tag " + heute).Trim(' ', '|'));
                    l.Add(v.Hol("Kunde") + ": " + v.Name);
                }
            }
            return l;
        }

        /// <summary>Lieferung auf "Geliefert": Menge einmalig beim Produkt gleichen Namens einbuchen (nur Lagerbestand, kein Geld).</summary>
        public static string LieferungEinbuchen(Unternehmen u, AbmEintrag l)
        {
            if (l.Art != "Lieferung" || !l.Ist("Status", "Geliefert") || l.Hol("Eingebucht") == "ja") return "";
            var p = u.Abm.FirstOrDefault(w => w.Art == "Produkt" && Gleich(w.Name, l.Hol("Produkt")));
            if (p == null || !ModulAn(u, Produkte)) return "";
            decimal menge = l.Zahl("Menge");
            if (menge <= 0) return "";
            p.Setz("Bestand", Befehle.Betrag(p.Zahl("Bestand") + menge));
            l.Setz("Eingebucht", "ja");
            return " Lagerbestand von " + p.Name + " um " + Befehle.Betrag(menge) + " erhöht.";
        }

        // ---------- Lieferantensuche ----------

        public class Treffer
        {
            public string Name = "", Branche = "", Standort = "", Herkunft = "";
            public bool IstFirma;
        }

        /// <summary>Sucht Firmen der Spielwelt und bekannte Lieferanten anderer Firmen (Name, Branche oder Produkte enthalten den Suchtext).</summary>
        public static List<Treffer> Suchen(Stand st, Unternehmen fuer, string text)
        {
            text = (text ?? "").Trim();
            var l = new List<Treffer>();
            Func<string, bool> passt = s => text.Length == 0 || (s ?? "").IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
            foreach (var f in st.Unternehmen)
            {
                if (f == fuer) continue;
                if (passt(f.Name) || passt(f.Branche))
                    l.Add(new Treffer { Name = f.Name, Branche = f.Branche, Standort = f.Bank, Herkunft = "Firma", IstFirma = true });
            }
            foreach (var f in st.Unternehmen)
                foreach (var e in f.Abm.Where(w => w.Art == "Lieferant" && w.Hol("Firma").Length == 0))
                {
                    if (!(passt(e.Name) || passt(e.Hol("Branche")) || passt(e.Hol("Produkte")))) continue;
                    if (l.Any(t => Gleich(t.Name, e.Name))) continue;
                    l.Add(new Treffer { Name = e.Name, Branche = e.Hol("Branche"), Standort = e.Hol("Standort"), Herkunft = "bekannt bei " + f.Name });
                }
            return l;
        }

        /// <summary>Legt einen Lieferanten an. Gibt es den Namen schon als Firma, wird er mit ihr verknüpft (nie doppelt angelegt).</summary>
        public static AbmEintrag LieferantAnlegen(Stand st, Unternehmen u, string name, int heute, out string hinweis)
        {
            name = (name ?? "").Trim();
            hinweis = "";
            if (name.Length == 0) throw new WirtschaftsFehler("Erst einen Namen ins Suchfeld schreiben.");
            if (Gleich(name, u.Name)) throw new WirtschaftsFehler("Eine Firma kann nicht ihr eigener Lieferant sein.");
            var schon = u.Abm.FirstOrDefault(w => w.Art == "Lieferant" && (Gleich(w.Name, name) || Gleich(w.Hol("Firma"), name)));
            if (schon != null) throw new WirtschaftsFehler(name + " ist schon Lieferant dieser Firma (Nr. " + schon.Nr + ").");
            var firma = st.Unternehmen.FirstOrDefault(f => Gleich(f.Name, name));
            var e = Neu(u, "Lieferant", heute);
            if (firma != null)
            {
                e.Setz("Name", firma.Name); e.Setz("Firma", firma.Name); e.Setz("Branche", firma.Branche); e.Setz("Standort", firma.Bank);
                hinweis = firma.Name + " gibt es schon im Spiel: als Lieferant verknüpft (nicht neu angelegt).";
            }
            else
            {
                var bekannt = st.Unternehmen.SelectMany(f => f.Abm).FirstOrDefault(w => w.Art == "Lieferant" && w != e && Gleich(w.Name, name));
                e.Setz("Name", bekannt != null ? bekannt.Name : name);
                if (bekannt != null)
                {
                    foreach (var k in new[] { "Branche", "Standort", "Ansprechpartner", "Produkte" }) e.Setz(k, bekannt.Hol(k));
                    hinweis = "Daten von " + bekannt.Name + " aus einer anderen Firma übernommen. Preise und Vereinbarungen gelten pro Firma.";
                }
                else hinweis = "Neuer Geschäftspartner " + name + " angelegt. Rechts die Daten ergänzen.";
            }
            e.Setz("Beziehung", "Kontakt");
            return e;
        }

        // ---------- Manuelle Lohnabrechnung ----------

        /// <summary>Offene Lohnmonate (manuelle Lohnabrechnung) jetzt bezahlen: vom Firmenkonto an die Mitarbeiter.</summary>
        public static string LoehneZahlen(Bank bank, Unternehmen u, int heute)
        {
            if (!ModulAn(u, Personal)) throw new WirtschaftsFehler("Die Personalverwaltung ist für diese Firma nicht an.");
            if (!u.LohnManuell) throw new WirtschaftsFehler("Die Löhne laufen automatisch mit der Monatsbuchung. Für Handbetrieb erst auf 'Manuell' stellen.");
            if (u.LohnOffen <= 0) throw new WirtschaftsFehler("Es sind keine Löhne offen. Offene Monate entstehen bei jeder Monatsbuchung.");
            int monate = u.LohnOffen;
            decimal umsatz = u.UmsatzMonat + VertragsUmsatzJetzt(u, heute, bank.Stand.TageProMonat);
            decimal summe = LohnMonat(u, umsatz) * monate;
            u.Konto -= summe;
            var empf = Lohnempfaenger(u, umsatz);
            bank.LoehneAuszahlen(u, empf, monate, heute);
            u.LohnOffen = 0;
            bank.VerlaufBuchen(u, heute, "Löhne bezahlt (" + monate + " Monat/e, Handbetrieb)", -summe);
            return "Löhne für " + monate + " Monat/e bezahlt: " + Befehle.Betrag(summe) + (u.Konto < 0 ? ". Achtung: Firmenkonto ist jetzt im Minus." : ".");
        }

        // ---------- Bericht für den Game Operator und KI-Figuren ----------

        static string Kurz(string s, int max)
        {
            s = (s ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
            return s.Length <= max ? s : s.Substring(0, max - 1) + "…";
        }

        static void Zeilen(StringBuilder sb, string titel, IEnumerable<string> zeilen, int max)
        {
            var l = zeilen.ToList();
            if (l.Count == 0) return;
            sb.Append("  ").Append(titel).Append(": ").Append(string.Join("; ", l.Take(max).ToArray()));
            if (l.Count > max) sb.Append("; ... und ").Append(l.Count - max).Append(" weitere");
            sb.AppendLine();
        }

        public static void FirmaBeschreiben(Bank bank, Unternehmen u, StringBuilder sb, int heute)
        {
            var st = bank.Stand;
            int t = st.TageProMonat;
            decimal vert = VertragsUmsatzJetzt(u, heute, t);
            decimal lohn = LohnMonat(u, u.UmsatzMonat + vert);
            sb.Append("- ").Append(u.Name).Append(" (").Append(u.Branche).Append(", ").Append(u.Bank).Append("): sonstiger Umsatz ").Append(Befehle.Betrag(u.UmsatzMonat))
              .Append(", Vertragsumsatz ").Append(Befehle.Betrag(vert)).Append(", sonstige Ausgaben ").Append(Befehle.Betrag(u.AusgabenMonat))
              .Append(", Personalkosten ").Append(Befehle.Betrag(lohn)).Append(u.LohnManuell ? " (Handbetrieb, " + u.LohnOffen + " Monat/e offen)" : "")
              .Append(", Firmenkonto ").Append(Befehle.Betrag(u.Konto)).AppendLine();
            if (ModulAn(u, Personal))
            {
                Zeilen(sb, "Mitarbeiter (" + Kopfzahl(u) + " Köpfe)", u.Abm.Where(e => e.Art == "Mitarbeiter").Select(e =>
                    e.Name + " (" + Kurz(e.Hol("Position"), 30) + (e.Hol("Abteilung").Length > 0 ? ", " + Kurz(e.Hol("Abteilung"), 20) : "") + ", Lohn " + Befehle.Betrag(LohnEinzeln(e, u.UmsatzMonat + vert))
                    + (e.Ist("Status", "Aktiv") ? "" : ", " + e.Hol("Status")) + ")"), 12);
                Zeilen(sb, "Gruppen", u.Abm.Where(e => e.Art == "Gruppe").Select(e =>
                    Befehle.Betrag(e.Zahl("Anzahl")) + "x " + e.Name + (e.Hol("Abteilung").Length > 0 ? " (" + Kurz(e.Hol("Abteilung"), 20) + ")" : "")
                    + (e.Ist("InAusgaben", "Ja") ? ", Lohn in den Ausgaben" : ", je " + Befehle.Betrag(e.Zahl("Lohn"))) + (e.Ist("Status", "Aktiv") ? "" : ", deaktiviert")), 8);
            }
            if (ModulAn(u, Lieferanten))
                Zeilen(sb, "Lieferanten", u.Abm.Where(e => e.Art == "Lieferant").Select(e =>
                    e.Name + " (" + (e.Hol("Produkte").Length > 0 ? Kurz(e.Hol("Produkte"), 40) + ", " : "") + (e.Hol("Preise").Length > 0 ? "Preise: " + Kurz(e.Hol("Preise"), 40) + ", " : "")
                    + e.Hol("Beziehung") + ", Zuverlässigkeit " + e.Hol("Zuverlaessigkeit")
                    + (e.Hol("Vereinbarung").Length > 0 ? ", Vereinbart: " + Kurz(e.Hol("Vereinbarung"), 60) : "") + ")"), 8);
            if (ModulAn(u, Kunden))
                Zeilen(sb, "Kundenverträge", u.Abm.Where(e => e.Art == "Vertrag").Select(e =>
                    e.Hol("Kunde") + " (" + e.Hol("Kundenart") + "): " + e.Name + ", " + Befehle.Betrag(e.Zahl("Wert")) + "/Monat, " + e.Hol("Status")
                    + ((int)e.Zahl("Laufzeit") > 0 ? ", läuft bis Tag " + ((int)e.Zahl("Beginn") + (int)e.Zahl("Laufzeit") * t) : ", unbefristet")
                    + (e.Zahl("Kuendigung") > 0 ? ", Kündigungsfrist " + Befehle.Betrag(e.Zahl("Kuendigung")) + " M" : "")), 10);
            if (ModulAn(u, Produkte))
                Zeilen(sb, "Produkte", u.Abm.Where(e => e.Art == "Produkt").Select(e =>
                    e.Name + " (Bestand " + Befehle.Betrag(e.Zahl("Bestand")) + (e.Zahl("Bestand") < e.Zahl("Mindest") ? " UNTER MINDESTBESTAND " + Befehle.Betrag(e.Zahl("Mindest")) : "")
                    + ", EK " + Befehle.Betrag(e.Zahl("EK")) + ", VK " + Befehle.Betrag(e.Zahl("VK")) + ")"), 10);
            if (ModulAn(u, Logistik))
                Zeilen(sb, "Lieferungen", u.Abm.Where(e => e.Art == "Lieferung" && !e.Ist("Status", "Geliefert") && !e.Ist("Status", "Storniert")).Select(e =>
                    e.Name + " (" + e.Hol("Lieferant") + ", " + e.Hol("Produkt") + " x" + Befehle.Betrag(e.Zahl("Menge")) + ", " + e.Hol("Status") + (e.Hol("Termin").Length > 0 ? ", Termin " + e.Hol("Termin") : "") + ")"), 8);
            if (ModulAn(u, Projekte))
                Zeilen(sb, "Projekte", u.Abm.Where(e => e.Art == "Projekt" && !e.Ist("Status", "Abgeschlossen") && !e.Ist("Status", "Abgebrochen")).Select(e =>
                    e.Name + " (" + e.Hol("Status") + ", geschätzt " + Befehle.Betrag(e.Zahl("Kosten")) + (e.Hol("Verantwortlich").Length > 0 ? ", verantwortlich " + e.Hol("Verantwortlich") : "") + ")"), 8);
        }

        public const string RegelnOperator =
            "REGELN ZU DIESEN UNTERNEHMENSDATEN: Leite selbstständig glaubwürdige wirtschaftliche Entwicklungen ab (z. B. ein Lieferant erhöht Preise oder fällt aus, ein Kunde will kündigen, "
          + "nachverhandeln oder erweitern, ein Mitarbeiter will mehr Gehalt oder eine andere Position, ein Projekt wird teurer, ein Unternehmen aus einer anderen Stadt sucht einen Lieferanten). "
          + "Sie müssen zur Branche passen, die vorhandenen Lieferanten, Kunden und Verträge berücksichtigen, nachvollziehbare Zahlen haben und dürfen sich nicht widersprechen. "
          + "Unterscheide immer klar zwischen Vorschlag und Tatsache. Du änderst selbst keine Verträge, Löhne, Bestände, Konten oder Zahlungen und führst keine Investitionen aus. "
          + "Echte Änderungen trägt der Spieler im Firmentresor unter 'Erweitert' ein; erst dann gelten sie. Mündliche Absprachen verändern kein Geld.";

        /// <summary>Alle Firmen mit erweiterter Verwaltung (für den Operator). Leer, wenn keine Firma sie nutzt.</summary>
        public static string Bericht(Bank bank, int heute)
        {
            var firmen = bank.Stand.Unternehmen.Where(u => u.AbmAn).ToList();
            if (firmen.Count == 0) return "";
            var sb = new StringBuilder();
            sb.AppendLine("ERWEITERTE UNTERNEHMENSDATEN (Advanced Business Management; echte Spieldaten, Beträge in " + bank.Stand.Waehrung + " pro Monat):");
            foreach (var u in firmen.Take(25)) FirmaBeschreiben(bank, u, sb, heute);
            if (firmen.Count > 25) sb.AppendLine("- ... und " + (firmen.Count - 25) + " weitere Firmen");
            sb.AppendLine(RegelnOperator);
            return sb.ToString().Trim();
        }

        /// <summary>Für eine KI-Figur, die eine Firma führt (Unternehmer): eigene erweiterte Daten und wo sie Lieferant oder Kunde ist.</summary>
        public static string PartnerBericht(Bank bank, string firma, int heute)
        {
            firma = (firma ?? "").Trim();
            if (firma.Length == 0) return "";
            var sb = new StringBuilder();
            var eigen = bank.Stand.Unternehmen.FirstOrDefault(u => Gleich(u.Name, firma));
            if (eigen != null && eigen.AbmAn)
            {
                sb.AppendLine("DEINE FIRMA IM WIRTSCHAFTSSYSTEM (echte Daten):");
                FirmaBeschreiben(bank, eigen, sb, heute);
            }
            var lief = new List<string>();
            var kund = new List<string>();
            foreach (var u in bank.Stand.Unternehmen)
            {
                if (u == eigen) continue;
                foreach (var e in u.Abm)
                {
                    if (e.Art == "Lieferant" && (Gleich(e.Name, firma) || Gleich(e.Hol("Firma"), firma)))
                        lief.Add(u.Name + " kauft bei dir" + (e.Hol("Produkte").Length > 0 ? ": " + Kurz(e.Hol("Produkte"), 60) : " (Produkte noch nicht festgelegt)") + (e.Hol("Preise").Length > 0 ? ", Preise " + Kurz(e.Hol("Preise"), 50) : "")
                                 + (e.Hol("Bedingungen").Length > 0 ? ", Bedingungen " + Kurz(e.Hol("Bedingungen"), 50) : "") + (e.Hol("Zahlung").Length > 0 ? ", Zahlung " + Kurz(e.Hol("Zahlung"), 40) : "")
                                 + ", Beziehung " + e.Hol("Beziehung") + (e.Hol("Vereinbarung").Length > 0 ? ", bisher vereinbart: " + Kurz(e.Hol("Vereinbarung"), 80) : ""));
                    if (e.Art == "Vertrag" && Gleich(e.Hol("Kunde"), firma))
                        kund.Add("du bist Kunde von " + u.Name + ": " + e.Name + ", " + Befehle.Betrag(e.Zahl("Wert")) + " " + bank.Stand.Waehrung + "/Monat, " + e.Hol("Status"));
                }
            }
            if (lief.Count > 0 || kund.Count > 0)
            {
                sb.AppendLine("DEINE GESCHÄFTSBEZIEHUNGEN (echte Daten):");
                foreach (var z in lief.Take(10)) sb.Append("- ").AppendLine(z);
                foreach (var z in kund.Take(10)) sb.Append("- ").AppendLine(z);
            }
            if (sb.Length == 0) return "";
            sb.AppendLine("SO VERHANDELST DU: Du hast eigene wirtschaftliche Interessen. Nimm Angebote nicht einfach an: mach Gegenangebote, verlange begründet höhere Preise, "
                        + "biete Mengenrabatte nur gegen feste Abnahme, lehne ab, wenn es sich für dich nicht lohnt. Eine Einigung ist nur eine Absprache: sag dem Spieler, dass er sie "
                        + "im Firmentresor unter 'Erweitert' eintragen muss (z. B. Lieferanten > Vereinbarungen). Durch das Gespräch fließt kein Geld und es ändert sich nichts automatisch.");
            return sb.ToString().Trim();
        }
    }
}
