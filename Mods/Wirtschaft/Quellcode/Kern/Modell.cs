// Wirtschaft-Mod – Datenmodell
// Reiner C#-Code ohne Spiel-Abhängigkeiten.
// Pro Firma: Umsatz und Ausgaben pro Monat, Steuersatz. Ein Monat = Stand.TageProMonat Spieltage.
using System.Collections.Generic;
using System.Xml.Serialization;

namespace Wirtschaft.Kern
{
    /// <summary>Eine Partei im Rollenspiel oder die Regierung.</summary>
    public class Partei
    {
        [XmlAttribute] public string Name = "";
        [XmlAttribute] public decimal Konto;
        [XmlAttribute] public bool IstRegierung;
        /// <summary>True = eigenständige Regierung (Staat). Personen, auch Hauptfiguren, sind nie eine Regierung.</summary>
        [XmlAttribute] public bool IstStaat;
        /// <summary>Position des Geldtresors (Banking), wenn diese Person einen hat.</summary>
        [XmlAttribute] public string Geldtresor = "";
        [XmlAttribute] public decimal GeldtresorBestand;
        // ---- nur für Regierungen ----
        /// <summary>Position des Firmentresors, mit dem diese Regierung verbunden ist. Leer = läuft automatisch.</summary>
        [XmlAttribute] public string RegTresor = "";
        /// <summary>True = Staatsausgaben sind abgeschaltet (Steuern kommen trotzdem weiter an).</summary>
        [XmlAttribute] public bool RegAus;
        /// <summary>Beschäftigte des Staates (Verwaltung, Polizei, Security ...).</summary>
        [XmlAttribute] public int RegMitarbeiter;
        /// <summary>Weitere Ausgaben pro Monat (Flughafen, Security, Polizei ...).</summary>
        [XmlAttribute] public decimal RegAusgaben;
        [XmlAttribute] public int RegZuletzt;
    }

    /// <summary>Wie viel Prozent einer Firma eine Partei hält.</summary>
    public class Anteil
    {
        [XmlAttribute] public string Partei = "";
        [XmlAttribute] public decimal Prozent;
    }

    /// <summary>Ein Eintrag im Verlauf.</summary>
    public class Buchung
    {
        [XmlAttribute] public int Tag;
        [XmlAttribute] public string Text = "";
        [XmlAttribute] public decimal Betrag;
    }

    /// <summary>Eine Bank = eine Stadt: eigene Firmen, eigene Regierung. Hauptbank-Blöcke werden ihr zugeordnet.</summary>
    public class Bankhaus
    {
        [XmlAttribute] public string Name = "";
        /// <summary>Name der Partei, die hier die Steuern bekommt.</summary>
        [XmlAttribute] public string Regierung = "";
        /// <summary>Positionen "x,y,z" der Hauptbank-Blöcke dieser Bank.</summary>
        public List<string> Standorte = new List<string>();
    }

    public class Unternehmen
    {
        [XmlAttribute] public string Name = "";
        /// <summary>Name der Bank (Stadt), zu der die Firma gehört.</summary>
        [XmlAttribute] public string Bank = "";
        [XmlAttribute] public string Branche = "Sonstiges";
        [XmlAttribute] public string Besitzer = "";
        /// <summary>Nur laufende Firmen werden am Monatsende gebucht (die, die du gerade spielst).</summary>
        [XmlAttribute] public bool Laeuft = true;
        [XmlAttribute] public decimal UmsatzMonat;
        [XmlAttribute] public decimal AusgabenMonat;
        [XmlAttribute] public int Mitarbeiter;
        /// <summary>Wert aller Immobilien und Standorte der Firma, von Hand eingetragen.</summary>
        [XmlAttribute] public decimal Immobilienwert;
        /// <summary>Wert aller Fahrzeuge und Maschinen der Firma, von Hand eingetragen.</summary>
        [XmlAttribute] public decimal Fahrzeugwert;
        [XmlAttribute] public decimal Steuersatz = 20m;           // Prozent
        [XmlAttribute] public decimal Ausschuettungsquote = 100m; // Prozent, Rest bleibt in der Firma
        [XmlAttribute] public decimal Konto;                       // Guthaben der Firma (Einkommen sammelt sich hier)
        [XmlAttribute] public decimal Wert;                        // 0 = automatisch (Börse)
        [XmlAttribute] public int ZuletztAbgerechnet;              // Spieltag der letzten Monatsbuchung
        [XmlAttribute] public string Tresor = "";                  // Position "x,y,z" des Firmentresors
        [XmlAttribute] public string Geldtresor = "";              // Position "x,y,z" des Geldtresors (Banking)
        [XmlAttribute] public decimal GeldtresorBestand;           // Wert des Geldes, das zuletzt im Geldtresor lag
        /// <summary>Konkurs: Spieltag, an dem das Verfahren begann, und Tag des Urteils (0 = kein Verfahren).</summary>
        [XmlAttribute] public int KonkursSeit;
        [XmlAttribute] public int KonkursUrteil;
        /// <summary>Dynamische Sonderereignisse (pro Firma). Aus = keinerlei automatische Sonderbuchungen.</summary>
        [XmlAttribute] public bool SonderAn;
        /// <summary>Favorit: nur Favoriten bekommen die Ereignis-Meldungen im Spiel (wenn die Meldungen insgesamt an sind).</summary>
        [XmlAttribute] public bool Favorit;
        /// <summary>Liquiditätsgrenzen in Coins. 0 = automatischer Vorschlag aus den Firmendaten.</summary>
        [XmlAttribute] public decimal SonderMin;
        [XmlAttribute] public decimal SonderZielVon;
        [XmlAttribute] public decimal SonderZielBis;
        [XmlAttribute] public decimal SonderMax;
        /// <summary>0 schwach, 1 normal, 2 stark (Höhe der Beträge).</summary>
        [XmlAttribute] public int SonderIntensitaet = 1;
        /// <summary>0 selten, 1 normal, 2 häufig.</summary>
        [XmlAttribute] public int SonderHaeufigkeit = 1;
        [XmlAttribute] public bool SonderPositiv = true;
        [XmlAttribute] public bool SonderNegativ = true;
        /// <summary>Branchenprofil, "" = automatisch aus der Branche.</summary>
        [XmlAttribute] public string SonderProfil = "";
        /// <summary>Buchungsperiode (Tag der Monatsbuchung), die schon geprüft wurde: höchstens ein Sonderereignis pro Periode, auch nach Neustart.</summary>
        [XmlAttribute] public int SonderPeriode;
        /// <summary>Wie viele Perioden in Folge die Liquidität unter dem Ziel (Unten) bzw. darüber (Oben) lag.</summary>
        [XmlAttribute] public int SonderUnten;
        [XmlAttribute] public int SonderOben;

        // ---- Advanced Business Management (optional, siehe Abm.cs). Ausschalten löscht nichts. ----
        /// <summary>Erweiterte Verwaltung für diese Firma an.</summary>
        [XmlAttribute] public bool AbmAn;
        /// <summary>Gewählte Module, mit Komma getrennt: Personal, Lieferanten, Kunden, Produkte, Projekte, Logistik.</summary>
        [XmlAttribute] public string AbmModule = "";
        /// <summary>True = Löhne werden nicht mit der Monatsbuchung abgezogen, sondern per Knopf "Löhne zahlen".</summary>
        [XmlAttribute] public bool LohnManuell;
        /// <summary>Handbetrieb: so viele Monatslöhne sind noch nicht bezahlt.</summary>
        [XmlAttribute] public int LohnOffen;
        /// <summary>Laufende Nummer für ABM-Datensätze.</summary>
        [XmlAttribute] public int AbmNr;
        public List<AbmEintrag> Abm = new List<AbmEintrag>();

        public List<Anteil> Anteile = new List<Anteil>();
        public List<Buchung> Verlauf = new List<Buchung>();
        /// <summary>Protokoll der Sonderereignisse (automatisch und manuell).</summary>
        public List<SonderEreignis> Sonderprotokoll = new List<SonderEreignis>();
    }

    /// <summary>Ein gebuchtes Sonderereignis mit Liquidität davor und danach.</summary>
    public class SonderEreignis
    {
        [XmlAttribute] public int Tag;
        [XmlAttribute] public int Periode;
        [XmlAttribute] public string Titel = "";
        [XmlAttribute] public string Text = "";
        [XmlAttribute] public string Kategorie = "";
        [XmlAttribute] public decimal Betrag;
        [XmlAttribute] public decimal Vorher;
        [XmlAttribute] public decimal Nachher;
        [XmlAttribute] public bool Manuell;
    }

    public class Immobilie
    {
        [XmlAttribute] public string Name = "";
        [XmlAttribute] public string Bank = "";
        [XmlAttribute] public string Besitzer = "";
        [XmlAttribute] public decimal Wert;
        [XmlAttribute] public decimal MieteMonat;
        [XmlAttribute] public decimal KostenMonat;
        [XmlAttribute] public decimal Steuersatz = 20m;
        [XmlAttribute] public int ZuletztAbgerechnet;
    }

    /// <summary>Ein Kredit (Annuität): feste Monatsrate, die vom Konto des Kreditnehmers (Firma oder Partei) abgebucht wird.</summary>
    public class Kredit
    {
        [XmlAttribute] public int Nr;
        [XmlAttribute] public string Bank = "";
        [XmlAttribute] public string Nehmer = "";
        [XmlAttribute] public decimal Summe;
        [XmlAttribute] public decimal Zins;          // Prozent pro Jahr
        [XmlAttribute] public int Monate;
        [XmlAttribute] public decimal Rate;          // pro Monat
        [XmlAttribute] public decimal Rest;          // Restschuld
        [XmlAttribute] public int Bezahlt;           // bezahlte Monatsraten
        [XmlAttribute] public int Beginn;            // Spieltag
        [XmlAttribute] public int ZuletztAbgerechnet;
        [XmlAttribute] public bool Erledigt;
        [XmlAttribute] public string Zweck = "";
    }

    /// <summary>Ein Spiel-Gegenstand, in dem Guthaben ausgezahlt werden kann, mit Kurs in Dukes.</summary>
    public class Zahlungsmittel
    {
        [XmlAttribute] public string Item = "";
        [XmlAttribute] public string Name = "";
        [XmlAttribute] public decimal Kurs = 1m;
    }

    /// <summary>Der gesamte Wirtschaftsstand eines Spielstands.</summary>
    [XmlRoot("Wirtschaft")]
    public class Stand
    {
        [XmlAttribute] public int Version = 4;
        [XmlAttribute] public string AktivePartei = "";
        /// <summary>Ereignis-Meldungen im Spiel an/aus (nur für Favoriten).</summary>
        [XmlAttribute] public bool SonderBenachrichtigung = true;
        [XmlAttribute] public string Waehrung = "Coins";
        [XmlAttribute] public int TageProMonat = 7;
        /// <summary>Testmodus: ein "Tag" dauert so viele Ingame-Minuten (0 = aus, normale Spieltage).</summary>
        [XmlAttribute] public int TestMinuten;
        /// <summary>Börse: so viel ist ein Mitarbeiter im Firmenwert wert.</summary>
        [XmlAttribute] public decimal WertProMitarbeiter = 2000m;
        /// <summary>Kreditregeln: höchstens so viele Monate Laufzeit, Rate höchstens so viel Prozent vom Nettoeinkommen, Zins pro Jahr.</summary>
        /// <summary>Feste Obergrenzen: kein Befehl und keine Datei kann sie überschreiten (nur senken).</summary>
        public const int KreditLaufzeitGrenze = 36;
        public const decimal KreditQuoteGrenze = 40m;
        int kreditMaxMonate = KreditLaufzeitGrenze;
        decimal kreditMaxQuote = KreditQuoteGrenze;
        [XmlAttribute] public int KreditMaxMonate
        {
            get { return kreditMaxMonate > KreditLaufzeitGrenze ? KreditLaufzeitGrenze : (kreditMaxMonate < 1 ? 1 : kreditMaxMonate); }
            set { kreditMaxMonate = value; }
        }
        [XmlAttribute] public decimal KreditMaxQuote
        {
            get { return kreditMaxQuote > KreditQuoteGrenze ? KreditQuoteGrenze : (kreditMaxQuote < 0m ? 0m : kreditMaxQuote); }
            set { kreditMaxQuote = value; }
        }
        [XmlAttribute] public decimal KreditZins = 8m;
        /// <summary>1 = Kurse wurden auf Old Cash 20 / Silber 500 / Gold 5000 umgestellt.</summary>
        [XmlAttribute] public int KursStand;
        /// <summary>1 = Regierungen und Personen wurden getrennt (IstStaat).</summary>
        [XmlAttribute] public int StaatStand;

        public List<Bankhaus> Banken = new List<Bankhaus>();
        public List<Zahlungsmittel> Waehrungen = new List<Zahlungsmittel>();
        public List<Partei> Parteien = new List<Partei>();
        public List<Unternehmen> Unternehmen = new List<Unternehmen>();
        public List<Immobilie> Immobilien = new List<Immobilie>();
        public List<Buchung> Kassenbuch = new List<Buchung>();
        public List<Kredit> Kredite = new List<Kredit>();
    }
}
