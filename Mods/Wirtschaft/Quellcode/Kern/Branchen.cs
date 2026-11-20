// Wirtschaft-Mod – Branchenprofile für die dynamischen Sonderereignisse
// Reiner C#-Code ohne Spiel-Abhängigkeiten.
// Erweitern: einfach ein neues Profil in Alle() anhängen (Name, Suchwörter für die Branche, Ereignisse).
// Ereignis: E(positiv, Titel, Beschreibung, FaktorVon, FaktorBis). Der Faktor wirkt auf die Grundgröße der Firma
// (siehe Ereignisse.Grundgroesse): 1,0 = ein typisches Ereignis, 0,4 = klein, 1,6 = groß.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Wirtschaft.Kern
{
    public class SonderVorlage
    {
        public bool Positiv;
        public string Titel = "";
        public string Text = "";
        public decimal FaktorVon = 0.6m;
        public decimal FaktorBis = 1.2m;
    }

    public class Branchenprofil
    {
        public string Name = "";
        public string[] Suchwoerter = new string[0];
        /// <summary>Firmenwert: so oft zählt der Monatsumsatz (mindestens 2).</summary>
        public decimal UmsatzFaktor = 2m;
        /// <summary>Übliche Umsatzrendite der Branche vor Steuer, in Prozent (von–bis). Nur zum Vergleich.</summary>
        public decimal MargeVon = 3m, MargeBis = 8m;
        /// <summary>Wie stark die Sonderereignisse der Branche schwanken (1 = normal).</summary>
        public decimal Schwankung = 1m;
        public List<SonderVorlage> Ereignisse = new List<SonderVorlage>();

        public List<SonderVorlage> Liste(bool positiv) { return Ereignisse.Where(e => e.Positiv == positiv).ToList(); }
    }

    public static class Branchen
    {
        static List<Branchenprofil> alle;

        static SonderVorlage E(bool pos, string titel, string text, decimal von, decimal bis)
        {
            return new SonderVorlage { Positiv = pos, Titel = titel, Text = text, FaktorVon = von, FaktorBis = bis };
        }

        static Branchenprofil P(string name, string woerter, decimal faktor, decimal margeVon, decimal margeBis, decimal schwankung, params SonderVorlage[] e)
        {
            return new Branchenprofil { Name = name, Suchwoerter = woerter.Split(','), UmsatzFaktor = faktor, MargeVon = margeVon, MargeBis = margeBis, Schwankung = schwankung, Ereignisse = new List<SonderVorlage>(e) };
        }

        public static List<Branchenprofil> Alle()
        {
            if (alle != null) return alle;
            const bool Plus = true, Minus = false;
            alle = new List<Branchenprofil>
            {
                P("KFZ-Handel", "kfz,auto,fahrzeug,wagen,garage,werkstatt", 2.0m, 3m, 8m, 1.0m,
                    E(Plus, "Erfolgreicher Verkaufsmonat", "Mehrere Fahrzeuge wurden schneller als erwartet verkauft.", 0.7m, 1.2m),
                    E(Plus, "Wertvolles Fahrzeug verkauft", "Ein besonders gut erhaltenes Fahrzeug ging zu einem Spitzenpreis weg.", 1.0m, 1.6m),
                    E(Plus, "Größerer Kundenauftrag", "Ein Kunde hat gleich mehrere Fahrzeuge für seinen Betrieb gekauft.", 0.8m, 1.4m),
                    E(Plus, "Unerwartet hohe Nachfrage", "Die Nachfrage nach fahrbereiten Autos ist gestiegen.", 0.5m, 1.0m),
                    E(Minus, "Schwacher Gebrauchtwagenmarkt", "Die Nachfrage ist zurückgegangen. Mehrere Fahrzeuge blieben stehen, die Standzeiten kosten zusätzlich.", 0.8m, 1.4m),
                    E(Minus, "Weniger Fahrzeugverkäufe", "Es wurden deutlich weniger Fahrzeuge verkauft als geplant.", 0.6m, 1.1m),
                    E(Minus, "Unerwartete Betriebskosten", "Strom, Treibstoff und Ersatzteile waren teurer als üblich.", 0.4m, 0.8m),
                    E(Minus, "Reparaturkosten", "Mehrere Fahrzeuge mussten vor dem Verkauf aufwendig repariert werden.", 0.5m, 1.0m),
                    E(Minus, "Verzögerte Kundenzahlungen", "Ein Käufer zahlt verspätet, das Geld fehlt vorerst in der Kasse.", 0.5m, 1.0m),
                    E(Minus, "Lange Standzeiten", "Fahrzeuge stehen zu lange auf dem Hof und verursachen laufende Kosten.", 0.4m, 0.8m)),

                P("Transport und Spedition", "transport,spedition,logistik,liefer,kurier,zug,bahn,taxi", 2.0m, 4m, 10m, 1.0m,
                    E(Plus, "Größerer Transportauftrag", "Ein großer Auftrag für mehrere Fahrten ist eingegangen.", 0.9m, 1.5m),
                    E(Plus, "Zusätzliche Lieferaufträge", "Kurzfristig kamen mehrere Lieferungen dazu.", 0.5m, 1.0m),
                    E(Plus, "Erfolgreicher Geschäftsmonat", "Die Fahrzeuge waren den ganzen Monat ausgelastet.", 0.6m, 1.1m),
                    E(Plus, "Lukrativer Kundenauftrag", "Ein Kunde zahlt einen Aufschlag für eine eilige Sonderfahrt.", 0.8m, 1.4m),
                    E(Minus, "Auftragsrückgang", "Mehrere Stammkunden haben weniger Fahrten bestellt.", 0.6m, 1.1m),
                    E(Minus, "Treibstoffkosten steigen", "Der Treibstoff ist knapp und deutlich teurer geworden.", 0.5m, 1.0m),
                    E(Minus, "Reparaturkosten", "Ein Fahrzeug fiel aus und musste repariert werden.", 0.5m, 1.1m),
                    E(Minus, "Verzögerte Lieferungen", "Blockierte Straßen haben Lieferungen verzögert, es gab Vertragsstrafen.", 0.4m, 0.9m),
                    E(Minus, "Vertragsstornierung", "Ein Kunde hat einen fest eingeplanten Auftrag storniert.", 0.7m, 1.2m),
                    E(Minus, "Unerwartete Betriebskosten", "Versicherung, Maut und Wartung waren höher als geplant.", 0.4m, 0.8m)),

                P("Industrie und Produktion", "industrie,produktion,fabrik,werk,herstell,schmiede,chemie,mine,bergbau", 2.4m, 6m, 14m, 0.9m,
                    E(Plus, "Größerer Produktionsauftrag", "Ein Abnehmer hat eine große Menge bestellt.", 0.9m, 1.5m),
                    E(Plus, "Erfolgreicher Absatzmonat", "Die Produktion wurde vollständig verkauft.", 0.6m, 1.1m),
                    E(Plus, "Zusätzliche Kundenbestellungen", "Mehrere Kunden haben nachbestellt.", 0.5m, 1.0m),
                    E(Plus, "Einmaliger Großauftrag", "Die Stadt hat einen einmaligen Großauftrag vergeben.", 1.0m, 1.6m),
                    E(Minus, "Steigende Energiekosten", "Strom und Brennstoff für die Anlagen sind teurer geworden.", 0.5m, 1.0m),
                    E(Minus, "Höhere Materialkosten", "Rohstoffe waren knapp und mussten teuer eingekauft werden.", 0.6m, 1.1m),
                    E(Minus, "Wartung und Reparatur", "Eine Maschine ist ausgefallen und musste instand gesetzt werden.", 0.6m, 1.2m),
                    E(Minus, "Produktionsverzögerung", "Ein Ausfall hat die Produktion tagelang aufgehalten.", 0.5m, 1.0m),
                    E(Minus, "Auftragsrückgang", "Ein wichtiger Abnehmer bestellt vorerst weniger.", 0.6m, 1.1m),
                    E(Minus, "Unerwartete Betriebskosten", "Sicherheit und Instandhaltung des Geländes kosteten mehr als geplant.", 0.4m, 0.8m)),

                P("Landwirtschaft", "landwirt,farm,bauernhof,agrar,acker,ernte,vieh,garten,gärtnerei,gaertnerei", 2.0m, 5m, 15m, 1.2m,
                    E(Plus, "Gute Ernte", "Die Ernte ist besser ausgefallen als erwartet.", 0.7m, 1.3m),
                    E(Plus, "Höhere Nachfrage", "Frische Lebensmittel sind gefragt, die Preise ziehen an.", 0.5m, 1.0m),
                    E(Plus, "Erfolgreicher Verkauf", "Ein großer Posten ging auf einmal an einen Händler.", 0.6m, 1.1m),
                    E(Plus, "Zusätzlicher Abnahmevertrag", "Ein neuer Abnehmer hat einen festen Vertrag abgeschlossen.", 0.8m, 1.4m),
                    E(Minus, "Schlechte Ernte", "Trockenheit und Schädlinge haben die Ernte geschmälert.", 0.7m, 1.3m),
                    E(Minus, "Niedrige Verkaufspreise", "Das Angebot war groß, die Preise sind gefallen.", 0.5m, 1.0m),
                    E(Minus, "Höhere Betriebskosten", "Saatgut, Dünger und Wasser waren teurer.", 0.4m, 0.8m),
                    E(Minus, "Reparaturkosten", "Zäune und Geräte mussten nach einem Angriff repariert werden.", 0.5m, 1.0m),
                    E(Minus, "Schwache Nachfrage", "Die Abnehmer kaufen derzeit weniger.", 0.5m, 0.9m)),

                P("Bauunternehmen", "bau,handwerk,zimmerei,dach,maurer,abriss,säuberung,saeuberung", 2.0m, 4m, 10m, 1.1m,
                    E(Plus, "Neuer Bauauftrag", "Ein neues Bauprojekt wurde vergeben.", 0.8m, 1.4m),
                    E(Plus, "Größerer Kundenauftrag", "Ein Kunde lässt gleich mehrere Gebäude instand setzen.", 0.9m, 1.5m),
                    E(Plus, "Erfolgreicher Projektabschluss", "Ein Projekt wurde früher fertig, die Schlusszahlung ist eingegangen.", 0.7m, 1.3m),
                    E(Plus, "Zusätzliche Aufträge", "Kleinere Zusatzarbeiten kamen kurzfristig dazu.", 0.4m, 0.9m),
                    E(Minus, "Materialkosten steigen", "Beton, Stahl und Holz sind knapp und teurer geworden.", 0.6m, 1.1m),
                    E(Minus, "Projektverzögerung", "Ein Bau liegt hinter dem Plan, es fallen Mehrkosten an.", 0.5m, 1.0m),
                    E(Minus, "Unerwartete Reparaturkosten", "Werkzeug und Maschinen mussten ersetzt werden.", 0.5m, 1.0m),
                    E(Minus, "Auftragsstornierung", "Ein Bauherr hat einen Auftrag zurückgezogen.", 0.7m, 1.2m),
                    E(Minus, "Höhere Betriebskosten", "Sicherung der Baustellen gegen Zombies kostete mehr als geplant.", 0.4m, 0.8m)),

                P("Gastronomie und Hotellerie", "gastro,hotel,restaurant,bar,kneipe,café,cafe,imbiss,club,pension,taverne,saloon", 2.0m, 5m, 12m, 1.0m,
                    E(Plus, "Erfolgreicher Monat", "Das Haus war den ganzen Monat gut besucht.", 0.6m, 1.1m),
                    E(Plus, "Größere Veranstaltung", "Eine große Feier wurde ausgerichtet.", 0.8m, 1.4m),
                    E(Plus, "Hohe Auslastung", "Zimmer und Tische waren durchgehend belegt.", 0.6m, 1.2m),
                    E(Plus, "Zusätzliche Buchungen", "Reisende haben kurzfristig gebucht.", 0.4m, 0.9m),
                    E(Minus, "Schwache Besucherzahlen", "Es kamen deutlich weniger Gäste als üblich.", 0.5m, 1.0m),
                    E(Minus, "Steigende Lebensmittelkosten", "Lebensmittel und Getränke sind teurer geworden.", 0.4m, 0.9m),
                    E(Minus, "Unerwartete Reparaturen", "Küche und Einrichtung mussten repariert werden.", 0.5m, 1.0m),
                    E(Minus, "Saisonbedingter Umsatzrückgang", "Die ruhige Jahreszeit drückt den Umsatz.", 0.5m, 1.0m),
                    E(Minus, "Höhere Betriebskosten", "Strom, Wasser und Personal kosteten mehr.", 0.4m, 0.8m)),

                P("IT und Dienstleistung", "it,software,computer,technik,dienstleist,beratung,service,elektro,funk,medien", 3.0m, 15m, 30m, 0.8m,
                    E(Plus, "Größerer Kundenauftrag", "Ein Kunde hat einen umfangreichen Auftrag erteilt.", 0.8m, 1.4m),
                    E(Plus, "Neues Projekt", "Ein neues Projekt ist angelaufen, die Anzahlung kam sofort.", 0.6m, 1.2m),
                    E(Plus, "Langfristiger Servicevertrag", "Ein Kunde hat einen Wartungsvertrag unterschrieben, die erste Zahlung ist da.", 0.6m, 1.1m),
                    E(Plus, "Zusätzlicher Beratungsauftrag", "Ein kurzer Zusatzauftrag kam herein.", 0.4m, 0.8m),
                    E(Minus, "Projektverzögerung", "Ein Projekt verzögert sich, die Kosten laufen weiter.", 0.5m, 1.0m),
                    E(Minus, "Kundenverlust", "Ein Stammkunde ist abgesprungen.", 0.6m, 1.1m),
                    E(Minus, "Auftragsrückgang", "Es kamen weniger neue Aufträge herein.", 0.5m, 1.0m),
                    E(Minus, "Höhere Betriebskosten", "Geräte und Ersatzteile waren teurer.", 0.4m, 0.8m),
                    E(Minus, "Verzögerte Kundenzahlungen", "Mehrere Rechnungen sind noch offen.", 0.5m, 1.0m)),

                P("Waffen und Sicherheit", "waffe,munition,sicherheit,söldner,soeldner,militär,militaer,schutz,wache", 2.5m, 10m, 20m, 1.1m,
                    E(Plus, "Großauftrag Ausrüstung", "Eine Stadtwache hat Ausrüstung in großer Menge bestellt.", 0.9m, 1.5m),
                    E(Plus, "Erfolgreicher Verkaufsmonat", "Nach einer Horde war die Nachfrage hoch.", 0.6m, 1.2m),
                    E(Plus, "Säuberungsauftrag", "Ein Gebäude wurde gesäubert, die Prämie ist bezahlt.", 0.7m, 1.3m),
                    E(Plus, "Zusätzliche Bestellungen", "Mehrere Kunden haben nachbestellt.", 0.4m, 0.9m),
                    E(Minus, "Schwache Nachfrage", "Ruhige Wochen, kaum jemand kauft.", 0.5m, 1.0m),
                    E(Minus, "Höhere Einkaufspreise", "Teile und Munition sind knapp und teuer.", 0.5m, 1.0m),
                    E(Minus, "Ausrüstung verloren", "Bei einem Einsatz ging Ausrüstung verloren und muss ersetzt werden.", 0.6m, 1.1m),
                    E(Minus, "Unerwartete Betriebskosten", "Lager und Sicherung kosteten mehr als geplant.", 0.4m, 0.8m)),

                P("Kampfsport", "kampf,boxen,box,ufc,mma,ring,arena,kickbox,ringen,dojo,fight,käfig,kaefig,turnier", 2.2m, 8m, 20m, 1.3m,
                    E(Plus, "Ausverkaufter Kampfabend", "Die Arena war bis auf den letzten Platz gefüllt.", 0.8m, 1.4m),
                    E(Plus, "Titelkampf ausgerichtet", "Ein Titelkampf brachte hohe Eintritts- und Wetteinnahmen.", 1.0m, 1.6m),
                    E(Plus, "Sponsor gewonnen", "Ein Händler zahlt für Werbung am Ring.", 0.6m, 1.1m),
                    E(Plus, "Neue Mitglieder im Training", "Viele neue Kämpfer haben sich für das Training angemeldet.", 0.4m, 0.9m),
                    E(Minus, "Kampf abgesagt", "Ein Hauptkämpfer ist verletzt, Eintrittsgelder mussten zurückgezahlt werden.", 0.7m, 1.3m),
                    E(Minus, "Verletzungen und Arztkosten", "Mehrere Kämpfer mussten behandelt werden.", 0.4m, 0.9m),
                    E(Minus, "Schwacher Kampfabend", "Wenig Zuschauer, die Kosten liefen trotzdem.", 0.5m, 1.0m),
                    E(Minus, "Ring und Halle beschädigt", "Nach einem Tumult mussten Ring und Tribüne repariert werden.", 0.5m, 1.0m),
                    E(Minus, "Hohe Kampfbörsen", "Bekannte Kämpfer verlangten höhere Gagen.", 0.5m, 1.0m)),

                P("Theater und Unterhaltung", "theater,bühne,buehne,kino,zirkus,show,unterhaltung,belustigung,varieté,variete,musik,konzert,casino,kasino,spielhalle,bordell,tanz", 2.0m, 5m, 15m, 1.2m,
                    E(Plus, "Ausverkaufte Vorstellung", "Alle Plätze waren mehrere Abende hintereinander belegt.", 0.7m, 1.3m),
                    E(Plus, "Erfolgreiche Premiere", "Die neue Show ist der Renner der Stadt.", 0.8m, 1.4m),
                    E(Plus, "Private Feier gebucht", "Eine reiche Gruppe hat den Saal für einen Abend gemietet.", 0.6m, 1.2m),
                    E(Plus, "Gastspiel in der Nachbarstadt", "Die Truppe trat erfolgreich auswärts auf.", 0.5m, 1.0m),
                    E(Minus, "Vorstellung abgesagt", "Wegen einer Horde blieb das Haus geschlossen.", 0.5m, 1.0m),
                    E(Minus, "Wenig Publikum", "Die Leute sparen, die Reihen blieben leer.", 0.5m, 1.0m),
                    E(Minus, "Technik ausgefallen", "Licht und Tonanlage mussten ersetzt werden.", 0.5m, 1.0m),
                    E(Minus, "Kulissen und Kostüme erneuert", "Ausstattung war verschlissen und musste neu her.", 0.4m, 0.9m),
                    E(Minus, "Höhere Gagen", "Die Künstler verlangten mehr Geld.", 0.4m, 0.8m)),

                P("Luftfahrt und Flughafen", "flughafen,airport,luftfahrt,flug,airline,hubschrauber,helikopter,gyro", 2.2m, 3m, 10m, 1.2m,
                    E(Plus, "Zusätzliche Flüge", "Mehrere Sonderflüge wurden kurzfristig gebucht.", 0.6m, 1.2m),
                    E(Plus, "Frachtauftrag per Luft", "Eilige Fracht wurde zu einem hohen Preis geflogen.", 0.8m, 1.4m),
                    E(Plus, "Volle Maschinen", "Die Flüge waren den ganzen Monat ausgebucht.", 0.6m, 1.1m),
                    E(Plus, "Landegebühren kassiert", "Fremde Maschinen haben den Platz genutzt und Gebühren bezahlt.", 0.4m, 0.9m),
                    E(Minus, "Treibstoff teurer", "Flugbenzin ist knapp und deutlich teurer geworden.", 0.6m, 1.2m),
                    E(Minus, "Maschine am Boden", "Eine Maschine musste wegen eines Schadens stehen bleiben.", 0.7m, 1.3m),
                    E(Minus, "Schlechtes Wetter", "Stürme haben mehrere Flüge ausfallen lassen.", 0.5m, 1.0m),
                    E(Minus, "Wartung der Landebahn", "Die Landebahn musste ausgebessert und gesichert werden.", 0.5m, 1.0m),
                    E(Minus, "Sicherheitsauflagen", "Zusätzliche Wachen und Kontrollen kosteten mehr als geplant.", 0.4m, 0.8m)),

                P("Gesundheit und Medizin", "arzt,klinik,krankenhaus,apotheke,medizin,gesundheit,pharma,pflege,sanit,labor,praxis", 2.8m, 12m, 25m, 0.7m,
                    E(Plus, "Viele Patienten", "Die Praxis war den ganzen Monat voll.", 0.5m, 1.0m),
                    E(Plus, "Medikamente gut verkauft", "Antibiotika und Verbandszeug waren stark gefragt.", 0.6m, 1.2m),
                    E(Plus, "Versorgungsvertrag", "Die Stadt zahlt für die Versorgung ihrer Wachen.", 0.8m, 1.4m),
                    E(Plus, "Impfaktion", "Eine Impfaktion brachte zusätzliche Einnahmen.", 0.4m, 0.9m),
                    E(Minus, "Medikamente knapp", "Nachschub war nur zu hohen Preisen zu bekommen.", 0.5m, 1.0m),
                    E(Minus, "Geräte ausgefallen", "Ein Behandlungsgerät musste ersetzt werden.", 0.6m, 1.2m),
                    E(Minus, "Unbezahlte Behandlungen", "Mehrere Patienten konnten nicht zahlen.", 0.4m, 0.9m),
                    E(Minus, "Seuchenschutz", "Zusätzliche Schutzmaßnahmen nach einem Infektionsfall kosteten Geld.", 0.5m, 1.0m)),

                P("Immobilien und Vermietung", "immobil,vermiet,miet,makler,wohnung,wohnbau,hausverwaltung,grundstück,grundstueck", 3.0m, 20m, 40m, 0.6m,
                    E(Plus, "Alle Einheiten vermietet", "Der Leerstand ist auf null gefallen.", 0.5m, 1.0m),
                    E(Plus, "Maklerprovision", "Ein Gebäude wurde vermittelt, die Provision ist eingegangen.", 0.8m, 1.4m),
                    E(Plus, "Nachzahlung Nebenkosten", "Mieter haben offene Nebenkosten nachgezahlt.", 0.4m, 0.8m),
                    E(Plus, "Mieterhöhung durchgesetzt", "Neue Verträge bringen höhere Mieten.", 0.5m, 1.0m),
                    E(Minus, "Leerstand", "Mehrere Einheiten stehen leer und bringen keine Miete.", 0.6m, 1.1m),
                    E(Minus, "Mietausfall", "Ein Mieter zahlt nicht.", 0.5m, 1.0m),
                    E(Minus, "Sanierung", "Dach und Leitungen mussten saniert werden.", 0.7m, 1.3m),
                    E(Minus, "Schäden nach Horde", "Eine Horde hat Türen und Mauern beschädigt.", 0.6m, 1.2m)),

                P("Finanzen und Versicherung", "finanz,bank,versicher,kredit,börse,boerse,invest,pfand,leih,vermögen,vermoegen", 3.0m, 15m, 30m, 0.9m,
                    E(Plus, "Zinserträge", "Vergebene Kredite brachten mehr Zinsen als geplant.", 0.6m, 1.1m),
                    E(Plus, "Neue Kunden", "Mehrere Kunden haben Verträge abgeschlossen.", 0.5m, 1.0m),
                    E(Plus, "Erfolgreiche Geldanlage", "Eine Anlage wurde mit Gewinn verkauft.", 0.8m, 1.5m),
                    E(Plus, "Gebühreneinnahmen", "Viele Überweisungen und Umtauschgeschäfte brachten Gebühren.", 0.4m, 0.9m),
                    E(Minus, "Kreditausfall", "Ein Schuldner kann nicht zurückzahlen.", 0.8m, 1.5m),
                    E(Minus, "Schadensfall", "Ein großer Versicherungsschaden musste bezahlt werden.", 0.7m, 1.4m),
                    E(Minus, "Verlust bei Geldanlage", "Eine Anlage hat an Wert verloren.", 0.6m, 1.2m),
                    E(Minus, "Höhere Sicherheitskosten", "Tresor und Wachen kosteten mehr als geplant.", 0.4m, 0.8m)),

                P("Energie und Versorgung", "energie,strom,kraftwerk,wasserwerk,tankstelle,treibstoff,erdgas,solar,generator,raffinerie,versorgung", 2.6m, 8m, 18m, 0.7m,
                    E(Plus, "Hoher Verbrauch", "Die Stadt hat mehr Strom und Treibstoff abgenommen als üblich.", 0.5m, 1.0m),
                    E(Plus, "Neuer Großabnehmer", "Ein Betrieb wurde neu angeschlossen.", 0.7m, 1.3m),
                    E(Plus, "Günstiger Einkauf", "Rohöl und Brennstoff waren billig zu bekommen.", 0.4m, 0.9m),
                    E(Plus, "Notstromverkauf", "Nach einem Ausfall in der Nachbarstadt wurde teuer geliefert.", 0.7m, 1.3m),
                    E(Minus, "Anlage ausgefallen", "Ein Generator ist ausgefallen und musste repariert werden.", 0.7m, 1.3m),
                    E(Minus, "Leitungen beschädigt", "Leitungen wurden bei einem Angriff beschädigt.", 0.5m, 1.0m),
                    E(Minus, "Brennstoff teurer", "Der Einkauf von Brennstoff war deutlich teurer.", 0.5m, 1.0m),
                    E(Minus, "Wartungskosten", "Die regelmäßige Wartung fiel teurer aus als geplant.", 0.4m, 0.8m)),

                P("Handel und Allgemein", "handel,laden,shop,markt,kiosk,händler,haendler,verkauf,sonstiges", 2.0m, 3m, 8m, 0.9m,
                    E(Plus, "Erfolgreicher Verkaufsmonat", "Der Laden lief den ganzen Monat gut.", 0.6m, 1.1m),
                    E(Plus, "Größerer Kundenauftrag", "Ein Kunde hat einen großen Posten bestellt.", 0.8m, 1.4m),
                    E(Plus, "Offene Rechnungen beglichen", "Ein Kunde hat alte Rechnungen auf einmal bezahlt.", 0.5m, 1.0m),
                    E(Plus, "Restposten gut verkauft", "Lagerreste gingen zu einem guten Preis weg.", 0.4m, 0.8m),
                    E(Minus, "Schwache Verkaufszahlen", "Es wurde deutlich weniger verkauft als geplant.", 0.5m, 1.0m),
                    E(Minus, "Unerwartete Betriebskosten", "Strom, Miete und Sicherung waren teurer.", 0.4m, 0.8m),
                    E(Minus, "Reparatur und Instandhaltung", "Das Geschäft musste repariert werden.", 0.5m, 1.0m),
                    E(Minus, "Verzögerte Kundenzahlungen", "Mehrere Kunden zahlen verspätet.", 0.5m, 1.0m),
                    E(Minus, "Ware beschädigt", "Ein Teil der Ware ist verdorben oder beschädigt.", 0.4m, 0.9m))
            };
            return alle;
        }

        public static Branchenprofil Holen(string name)
        {
            return Alle().FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        static bool Passt(string branche, string wort)
        {
            wort = wort.Trim().ToLowerInvariant();
            if (wort.Length == 0) return false;
            if (wort.Length <= 2)   // kurze Wörter (z. B. "it") nur als ganzes Wort
            {
                foreach (var teil in branche.Split(' ', '-', '/', ',', '.', '&', '+', '(', ')'))
                    if (teil == wort) return true;
                return false;
            }
            return branche.Contains(wort);
        }

        /// <summary>Eng gefasste Profile werden zuerst geprüft, damit z. B. "Flughafen" nicht bei Transport landet.</summary>
        static bool Speziell(string n)
        {
            return n == "Kampfsport" || n == "Theater und Unterhaltung" || n == "Luftfahrt und Flughafen" || n == "Gesundheit und Medizin"
                || n == "Immobilien und Vermietung" || n == "Finanzen und Versicherung" || n == "Energie und Versorgung";
        }

        /// <summary>Profil aus der Branche erraten (Suchwörter, erstes passendes Profil gewinnt), sonst Handel und Allgemein.</summary>
        public static Branchenprofil Erkennen(string branche)
        {
            string b = (branche ?? "").ToLowerInvariant();
            // Landwirtschaft vor Bau prüfen ("Bauernhof")
            foreach (var p in Alle().OrderBy(x => x.Name == "Bauunternehmen" ? 2 : (Speziell(x.Name) ? 0 : 1)))
                foreach (var w in p.Suchwoerter)
                    if (Passt(b, w)) return p;
            return Holen("Handel und Allgemein");
        }

        public static Branchenprofil VonFirma(Unternehmen u)
        {
            var p = string.IsNullOrEmpty(u.SonderProfil) ? null : Holen(u.SonderProfil);
            return p ?? Erkennen(u.Branche);
        }
    }
}
