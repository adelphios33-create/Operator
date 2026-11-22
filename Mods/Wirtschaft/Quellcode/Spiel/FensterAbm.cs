// Wirtschaft-Mod – Fenster "Erweiterte Verwaltung" (Advanced Business Management), geöffnet aus dem Firmentresor.
// Reiter: Übersicht (an/aus, Module, Lohnabrechnung) und je ein Reiter pro Modul mit Liste links und Bearbeiten rechts.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Wirtschaft.Kern;
using Wirtschaft.Spiel;

public class XUiC_WbAbm : XUiController
{
    public static XUiC_WbAbm Instanz;
    public const string Gruppe = "wbAbm";
    const int Zeilen = 8;
    const int Reiter = 8;

    static string firma = "";
    int tab, seite;
    int gewaehlt = -1;          // Nr. des gewählten Datensatzes
    bool suche;                 // Lieferanten: links stehen Suchtreffer statt der Liste
    List<Abm.Treffer> treffer = new List<Abm.Treffer>();
    string meldung = "";
    string frage = "";
    float frageZeit;

    XUiC_SimpleButton[] reiter = new XUiC_SimpleButton[Reiter];
    XUiC_SimpleButton[] zeile = new XUiC_SimpleButton[Zeilen];
    XUiC_SimpleButton[] modKnopf = new XUiC_SimpleButton[7];
    XUiC_TextInput[] feld = new XUiC_TextInput[Abm.MaxFelder];
    XUiC_SimpleButton btnAn, btnLohnArt, btnNeu1, btnNeu2;
    XUiC_TextInput txtSuche, txtAktion;
    XUiC_SimpleButton btnAktion;

    Bank B { get { return WirtschaftMod.Bank; } }
    Unternehmen U { get { return B == null ? null : B.FirmaOderNull(firma); } }
    string Modul { get { return tab <= 0 ? "" : Abm.Module[tab - 1]; } }

    AbmEintrag Eintrag
    {
        get
        {
            var u = U;
            if (u == null || tab <= 0) return null;
            var e = u.Abm.FirstOrDefault(w => w.Nr == gewaehlt);
            if (e == null) return null;
            var d = Abm.ArtDef(e.Art);
            return d != null && d.Modul == Modul ? e : null;
        }
    }

    public static bool Oeffnen(EntityPlayerLocal spieler, string firmenname)
    {
        if (Instanz == null) return false;
        firma = firmenname ?? "";
        return FensterHilfe.Oeffnen(spieler, Gruppe);
    }

    public override void Init()
    {
        base.Init();
        Instanz = this;
        for (int i = 0; i < Reiter; i++)
        {
            int n = i;
            reiter[i] = GetChildById("btnAT" + i) as XUiC_SimpleButton;
            FensterHilfe.Knopf(this, "btnAT" + i, (s, m) => { tab = n; seite = 0; gewaehlt = -1; suche = false; meldung = ""; Neu(); });
        }
        for (int i = 0; i < Zeilen; i++)
        {
            int n = i;
            zeile[i] = GetChildById("btnE" + i) as XUiC_SimpleButton;
            FensterHilfe.Knopf(this, "btnE" + i, (s, m) => ZeileGewaehlt(n));
        }
        for (int i = 0; i < 7; i++)
        {
            int n = i;
            modKnopf[i] = GetChildById("btnMod" + i) as XUiC_SimpleButton;
            FensterHilfe.Knopf(this, "btnMod" + i, (s, m) => ModulUmschalten(n));
        }
        for (int i = 0; i < Abm.MaxFelder; i++)
        {
            int n = i;
            feld[i] = FensterHilfe.Feld(this, "txtF" + i, null);
            FensterHilfe.Knopf(this, "btnW" + i, (s, m) => Durchschalten(n));
        }
        btnAn = GetChildById("btnAbmAn") as XUiC_SimpleButton;
        FensterHilfe.Knopf(this, "btnAbmAn", (s, m) => AnUmschalten());
        btnLohnArt = GetChildById("btnLohnArt") as XUiC_SimpleButton;
        FensterHilfe.Knopf(this, "btnLohnArt", (s, m) => LohnArtUmschalten());
        FensterHilfe.Knopf(this, "btnLoehneZahlen", (s, m) => LoehneZahlen());
        btnNeu1 = GetChildById("btnNeu1") as XUiC_SimpleButton;
        btnNeu2 = GetChildById("btnNeu2") as XUiC_SimpleButton;
        FensterHilfe.Knopf(this, "btnNeu1", (s, m) => NeuerEintrag(0));
        FensterHilfe.Knopf(this, "btnNeu2", (s, m) => NeuerEintrag(1));
        FensterHilfe.Knopf(this, "btnEZurueck", (s, m) => { seite--; Neu(); });
        FensterHilfe.Knopf(this, "btnEVor", (s, m) => { seite++; Neu(); });
        FensterHilfe.Knopf(this, "btnESpeichern", (s, m) => EintragSpeichern());
        FensterHilfe.Knopf(this, "btnELoeschen", (s, m) => EintragLoeschen());
        txtAktion = FensterHilfe.Feld(this, "txtAktion", null);
        btnAktion = GetChildById("btnAktion") as XUiC_SimpleButton;
        FensterHilfe.Knopf(this, "btnAktion", (s, m) => Aktion());
        txtSuche = FensterHilfe.Feld(this, "txtSuche", null);
        if (txtSuche != null) txtSuche.OnSubmitHandler += (s, t) => Suchen();
        FensterHilfe.Knopf(this, "btnSuchen", (s, m) => Suchen());
        FensterHilfe.Knopf(this, "btnSucheNeu", (s, m) => LieferantNeuAusSuche());
        FensterHilfe.Knopf(this, "btnAbmSchliessen", (s, m) => xui.playerUI.windowManager.CloseIfOpen(Gruppe));
    }

    public override void OnOpen()
    {
        base.OnOpen();
        if (B != null) B.Heute = WirtschaftMod.Heute;
        tab = 0; seite = 0; gewaehlt = -1; suche = false; meldung = ""; frage = "";
        Neu();
        try { xui.playerUI.windowManager.CloseIfOpen("focusedBlockHealth"); } catch (Exception) { }
    }

    public override void OnClose()
    {
        base.OnClose();
        WirtschaftMod.Speichern();
    }

    void Melden(string text) { meldung = text; Neu(); }

    bool Bestaetigt(string schluessel, string text)
    {
        float jetzt = UnityEngine.Time.time;
        if (frage == schluessel && jetzt - frageZeit < 15f) { frage = ""; return true; }
        frage = schluessel;
        frageZeit = jetzt;
        Melden(text);
        return false;
    }

    // ---------- Übersicht ----------

    void AnUmschalten()
    {
        var u = U;
        if (u == null) return;
        Melden(Abm.AnSetzen(u, !u.AbmAn, WirtschaftMod.Heute));
        WirtschaftMod.Speichern();
    }

    void ModulUmschalten(int i)
    {
        var u = U;
        if (u == null) return;
        string m = Abm.Module[i];
        string text = Abm.ModulSetzen(u, m, !Abm.ModulGewaehlt(u, m), WirtschaftMod.Heute);
        if (!u.AbmAn) text += " (Wirkt erst, wenn die erweiterte Verwaltung oben eingeschaltet ist.)";
        WirtschaftMod.Speichern();
        Melden(text);
    }

    void LohnArtUmschalten()
    {
        var u = U;
        if (u == null) return;
        if (u.LohnManuell && u.LohnOffen > 0) { Melden("Erst die " + u.LohnOffen + " offenen Monatslöhne zahlen, dann auf automatisch stellen."); return; }
        u.LohnManuell = !u.LohnManuell;
        WirtschaftMod.Speichern();
        Melden(u.LohnManuell
            ? "Löhne jetzt im Handbetrieb: jede Monatsbuchung merkt einen offenen Monat vor, bezahlt wird mit 'Offene Löhne jetzt zahlen'."
            : "Löhne laufen jetzt automatisch mit der Monatsbuchung (als Ausgaben vor der Steuer).");
    }

    void LoehneZahlen()
    {
        var u = U;
        if (u == null || B == null) return;
        try
        {
            decimal summe = Abm.LohnMonat(u, u.UmsatzMonat + Abm.VertragsUmsatzJetzt(u, WirtschaftMod.Heute, B.Stand.TageProMonat)) * u.LohnOffen;
            if (u.LohnOffen > 0 && !Bestaetigt("loehne:" + u.Name, "Löhne für " + u.LohnOffen + " Monat/e zahlen: " + FensterHilfe.B(summe) + " vom Firmenkonto. Nochmal drücken.")) return;
            string text = Abm.LoehneZahlen(B, u, WirtschaftMod.Heute);
            WirtschaftMod.Speichern();
            Melden(text);
        }
        catch (WirtschaftsFehler f) { Melden(f.Message); }
    }

    string Uebersicht()
    {
        var u = U;
        var bank = B;
        if (u == null || bank == null) return "Die Firma " + firma + " gibt es nicht mehr.";
        int t = bank.Stand.TageProMonat;
        int heute = WirtschaftMod.Heute;
        decimal vert = Abm.VertragsUmsatzJetzt(u, heute, t);
        decimal lohn = Abm.LohnMonat(u, u.UmsatzMonat + vert);
        var r = bank.Vorschau(u);
        var sb = new StringBuilder();
        sb.AppendLine("Erweiterte Verwaltung: " + (u.AbmAn ? "AN" : "AUS"));
        sb.AppendLine();
        sb.AppendLine("Sonstiger Umsatz / Monat:   " + FensterHilfe.B(u.UmsatzMonat));
        sb.AppendLine("+ Kundenverträge / Monat:   " + FensterHilfe.B(vert) + "  (" + u.Abm.Count(e => e.Art == "Vertrag" && e.Ist("Status", "Aktiv")) + " aktiv)");
        sb.AppendLine("Sonstige Ausgaben / Monat:  " + FensterHilfe.B(u.AusgabenMonat));
        sb.AppendLine("+ Laufende Kosten / Monat:  " + FensterHilfe.B(Abm.KostenMonat(u)));
        sb.AppendLine("+ Personalkosten / Monat:   " + FensterHilfe.B(lohn) + (Abm.ModulAn(u, Abm.Personal) ? (u.LohnManuell ? "  (Handbetrieb, " + u.LohnOffen + " Monat/e offen)" : "  (automatisch)") : "  (Personal aus)"));
        sb.AppendLine("= Einkommen nach Steuer:    " + FensterHilfe.B(r.Einkommen));
        sb.AppendLine("Firmenkonto:                " + FensterHilfe.B(u.Konto));
        sb.AppendLine("Monatsbuchung alle " + t + " Spieltag/e" + (bank.Stand.TestMinuten > 0 ? " (TESTMODUS: 1 Tag = " + bank.Stand.TestMinuten + " Ingame-Minuten!)" : ""));
        var aktiv = u.Abm.Where(e => e.Art == "Vertrag" && e.Ist("Status", "Aktiv")).ToList();
        if (aktiv.Count > 0) sb.AppendLine("Aktive Kunden: " + string.Join(", ", aktiv.Take(4).Select(e => e.Hol("Kunde") + " " + FensterHilfe.B(e.Zahl("Wert")) + "/M").ToArray()) + (aktiv.Count > 4 ? " ..." : ""));
        sb.AppendLine();
        int schon = u.Abm.Where(e => e.Art == "Gruppe" && e.Ist("InAusgaben", "Ja") && e.Ist("Status", "Aktiv")).Sum(e => (int)Math.Max(0m, e.Zahl("Anzahl")));
        sb.AppendLine("Mitarbeiter: " + Abm.Kopfzahl(u) + (schon > 0 ? "  (davon " + schon + " schon in den Ausgaben)" : ""));
        var empf = Abm.Lohnempfaenger(u, u.UmsatzMonat + vert);
        if (empf.Count > 0) sb.AppendLine("Lohn an Personen: " + string.Join(", ", empf.Take(6).Select(kv => kv.Key + " " + FensterHilfe.B(kv.Value)).ToArray()) + (empf.Count > 6 ? " ..." : ""));
        int unter = u.Abm.Count(e => e.Art == "Produkt" && e.Zahl("Bestand") < e.Zahl("Mindest"));
        sb.AppendLine("Lieferanten: " + u.Abm.Count(e => e.Art == "Lieferant") + "   Produkte: " + u.Abm.Count(e => e.Art == "Produkt") + (unter > 0 ? " (" + unter + " unter Mindestbestand)" : ""));
        sb.AppendLine("Lieferungen offen: " + u.Abm.Count(e => e.Art == "Lieferung" && !e.Ist("Status", "Geliefert") && !e.Ist("Status", "Storniert"))
                      + "   Projekte aktiv: " + u.Abm.Count(e => e.Art == "Projekt" && (e.Ist("Status", "Aktiv") || e.Ist("Status", "In Planung"))));
        return sb.ToString().TrimEnd();
    }

    // ---------- Liste ----------

    List<AbmEintrag> Eintraege { get { var u = U; return u == null || tab <= 0 ? new List<AbmEintrag>() : Abm.Liste(u, Modul); } }

    int Anzahl { get { return suche ? treffer.Count : Eintraege.Count; } }

    static string Zeilentext(AbmEintrag e)
    {
        switch (e.Art)
        {
            case "Mitarbeiter": return e.Name + (e.Hol("Position").Length > 0 ? " - " + e.Hol("Position") : "") + (e.Ist("Status", "Aktiv") ? "" : " (" + e.Hol("Status") + ")");
            case "Gruppe": return Befehle.Betrag(e.Zahl("Anzahl")) + "x " + e.Name + (e.Ist("Status", "Aktiv") ? "" : " (aus)");
            case "Lieferant": return e.Name + " (" + e.Hol("Beziehung") + ")";
            case "Vertrag": return (e.Ist("Status", "Aktiv") ? "AKTIV " + Befehle.Betrag(e.Zahl("Wert")) + "/M " : "(" + e.Hol("Status") + ") ") + e.Hol("Kunde") + ": " + e.Name;
            case "Kosten": return (e.Ist("Status", "Aktiv") ? "-" + Befehle.Betrag(e.Zahl("Betrag")) + "/M " : "(" + e.Hol("Status") + ") ") + e.Name + (e.Ist("InAusgaben", "Ja") ? " (in Ausg.)" : "");
            case "Produkt": return e.Name + " [" + Befehle.Betrag(e.Zahl("Bestand")) + "]" + (e.Zahl("Bestand") < e.Zahl("Mindest") ? " !" : "");
            case "Material": return "Mat: " + e.Name + " [" + Befehle.Betrag(e.Zahl("Bestand")) + "]" + (e.Zahl("Bestand") < e.Zahl("Mindest") ? " !" : "");
            default: return e.Name + " (" + e.Hol("Status") + ")";
        }
    }

    void ZeileGewaehlt(int z)
    {
        int idx = seite * Zeilen + z;
        if (suche)
        {
            if (idx < 0 || idx >= treffer.Count) return;
            TrefferUebernehmen(treffer[idx]);
            return;
        }
        var l = Eintraege;
        if (idx < 0 || idx >= l.Count) return;
        gewaehlt = l[idx].Nr;
        meldung = "";
        FelderLaden();
        Neu();
    }

    void FelderLaden()
    {
        var e = Eintrag;
        var d = e == null ? null : Abm.ArtDef(e.Art);
        for (int i = 0; i < Abm.MaxFelder; i++)
            FensterHilfe.Schreiben(feld[i], d != null && i < d.Felder.Length ? e.Hol(d.Felder[i].Key) : "");
        FensterHilfe.Schreiben(txtAktion, e == null || U == null ? "" : Abm.AktionVorschlag(U, e));
    }

    /// <summary>Kaufen, Herstellen, Projekt bezahlen, Lieferung annehmen. Ungespeicherte Änderungen werden vorher gespeichert.</summary>
    void Aktion()
    {
        var u = U;
        var e = Eintrag;
        if (u == null || e == null || B == null) return;
        string text = Abm.AktionText(e);
        if (text.Length == 0) return;
        // erst die Felder übernehmen (z. B. neuer Preis), damit mit den angezeigten Werten gerechnet wird
        var d = Abm.ArtDef(e.Art);
        var alt = e.Felder.Select(f => new AbmFeld { K = f.K, V = f.V }).ToList();
        for (int i = 0; i < d.Felder.Length; i++)
            if (!d.Felder[i].NurLesen) e.Setz(d.Felder[i].Key, FensterHilfe.Lesen(feld[i]));
        string fehler = Abm.Pruefen(u, e);
        if (fehler != null) { e.Felder = alt; FelderLaden(); Melden("Erst korrigieren: " + fehler); return; }
        decimal menge = 0m;
        Befehle.VersucheZahl(FensterHilfe.Lesen(txtAktion), out menge);
        try
        {
            string erg;
            int heute = WirtschaftMod.Heute;
            if (e.Art == "Material")
            {
                decimal kosten = Bank.Runden(e.Zahl("Preis") * menge);
                if (!Bestaetigt("kauf:" + e.Nr + ":" + menge, Befehle.Betrag(menge) + "x " + e.Name + " für " + FensterHilfe.B(kosten) + " kaufen? Nochmal 'Jetzt kaufen' drücken.")) return;
                erg = Abm.MaterialKaufen(B, u, e, menge, heute);
            }
            else if (e.Art == "Produkt") erg = Abm.Herstellen(u, e, (int)Math.Floor(menge));
            else if (e.Art == "Projekt")
            {
                if (!Bestaetigt("proj:" + e.Nr, "Projekt " + e.Name + " für " + FensterHilfe.B(e.Zahl("Kosten")) + " bezahlen? Nochmal drücken.")) return;
                erg = Abm.ProjektBezahlen(B, u, e, heute);
            }
            else erg = Abm.LieferungAnnehmen(B, u, e, heute);
            FelderLaden();
            WirtschaftMod.Speichern();
            Melden(erg);
        }
        catch (WirtschaftsFehler f) { Melden(f.Message); }
    }

    void NeuerEintrag(int welcher)
    {
        var u = U;
        if (u == null) return;
        var arten = Abm.ArtenVon(Modul);
        if (welcher >= arten.Count) return;
        var e = Abm.Neu(u, arten[welcher].Art, WirtschaftMod.Heute);
        e.Setz("Name", "Neu: " + arten[welcher].Titel + " " + e.Nr);
        Abm.Abgleichen(u);
        gewaehlt = e.Nr;
        suche = false;
        var l = Eintraege;
        seite = Math.Max(0, l.FindIndex(w => w.Nr == e.Nr) / Zeilen);
        FelderLaden();
        WirtschaftMod.Speichern();
        Melden(arten[welcher].Titel + " angelegt. Rechts ausfüllen und Speichern drücken.");
    }

    void Durchschalten(int i)
    {
        var e = Eintrag;
        var d = e == null ? null : Abm.ArtDef(e.Art);
        if (d == null || i >= d.Felder.Length || d.Felder[i].Werte == null || feld[i] == null) return;
        var w = d.Felder[i].Werte;
        string jetzt = FensterHilfe.Lesen(feld[i]).Trim();
        int k = Array.FindIndex(w, x => string.Equals(x, jetzt, StringComparison.OrdinalIgnoreCase));
        feld[i].Text = w[FensterHilfe.Blaettern(k, 1, w.Length)];
    }

    void EintragSpeichern()
    {
        var u = U;
        var e = Eintrag;
        if (u == null || e == null || B == null) return;
        var d = Abm.ArtDef(e.Art);
        var alt = e.Felder.Select(f => new AbmFeld { K = f.K, V = f.V }).ToList();
        for (int i = 0; i < d.Felder.Length; i++)
            if (!d.Felder[i].NurLesen) e.Setz(d.Felder[i].Key, FensterHilfe.Lesen(feld[i]));
        string fehler = Abm.Pruefen(u, e);
        string hinweis = "";
        if (fehler == null && e.Art == "Mitarbeiter") fehler = LohnkontoPruefen(u, e, out hinweis);
        if (fehler == null && e.Art == "Lieferant") hinweis = LieferantVerknuepfen(u, e);
        if (fehler != null)
        {
            e.Felder = alt;
            FelderLaden();
            Melden("Nicht gespeichert: " + fehler);
            return;
        }
        if (e.Art == "Lieferung") hinweis += Abm.LieferungEinbuchen(u, e);
        Abm.Abgleichen(u);
        FelderLaden();
        WirtschaftMod.Speichern();
        Melden(Abm.ArtDef(e.Art).Titel + " " + e.Name + " gespeichert." + (hinweis.Length > 0 ? " " + hinweis : ""));
    }

    /// <summary>Mitarbeiter mit Lohnkonto: Person muss es im Wirtschaftssystem geben. Neue Namen werden als Person mit 0 angelegt.</summary>
    string LohnkontoPruefen(Unternehmen u, AbmEintrag e, out string hinweis)
    {
        hinweis = "";
        string k = e.Hol("Konto");
        if (k.Length == 0) return null;
        var p = B.ParteiOderNull(k);
        if (p != null)
        {
            if (B.IstRegierungspartei(p)) return k + " ist eine Regierung und kann kein Lohnkonto sein.";
            e.Setz("Konto", p.Name);
            return null;
        }
        if (B.FirmaOderNull(k) != null) return k + " ist eine Firma, keine Person.";
        try
        {
            var neu = B.ParteiAnlegen(k, 0m);
            e.Setz("Konto", neu.Name);
            hinweis = "Person " + neu.Name + " neu angelegt (Konto 0). Der Lohn geht ab jetzt jeden Monat auf ihr Konto.";
            return null;
        }
        catch (WirtschaftsFehler f) { return f.Message; }
    }

    /// <summary>Heißt der Lieferant wie eine Firma im Spiel, wird er mit ihr verknüpft (nie doppelt).</summary>
    string LieferantVerknuepfen(Unternehmen u, AbmEintrag e)
    {
        var f = B.FirmaOderNull(e.Name);
        if (f == null)
        {
            if (e.Hol("Firma").Length > 0 && B.FirmaOderNull(e.Hol("Firma")) == null) e.Setz("Firma", "");
            return "";
        }
        if (f == u) return "";
        bool neu = !string.Equals(e.Hol("Firma"), f.Name, StringComparison.OrdinalIgnoreCase);
        e.Setz("Firma", f.Name);
        e.Setz("Name", f.Name);
        if (e.Hol("Branche").Length == 0) e.Setz("Branche", f.Branche);
        if (e.Hol("Standort").Length == 0) e.Setz("Standort", f.Bank);
        return neu ? "Mit der Firma " + f.Name + " im Spiel verknüpft." : "";
    }

    void EintragLoeschen()
    {
        var u = U;
        var e = Eintrag;
        if (u == null || e == null) return;
        string titel = Abm.ArtDef(e.Art).Titel;
        if (!Bestaetigt("abmweg:" + e.Nr, titel + " " + e.Name + " wirklich löschen? Nochmal 'Löschen' drücken.")) return;
        u.Abm.Remove(e);
        gewaehlt = -1;
        Abm.Abgleichen(u);
        FelderLaden();
        WirtschaftMod.Speichern();
        Melden(titel + " " + e.Name + " gelöscht.");
    }

    // ---------- Lieferantensuche ----------

    void Suchen()
    {
        var u = U;
        if (u == null || B == null) return;
        treffer = Abm.Suchen(B.Stand, u, FensterHilfe.Lesen(txtSuche));
        suche = true;
        seite = 0;
        Melden(treffer.Count == 0 ? "Nichts gefunden. Mit 'Als neu anlegen' wird der Name als neuer Geschäftspartner angelegt."
                                  : treffer.Count + " Treffer. Einen anklicken, um ihn als Lieferanten zu übernehmen.");
    }

    void TrefferUebernehmen(Abm.Treffer t)
    {
        LieferantAnlegen(t.Name);
    }

    void LieferantNeuAusSuche()
    {
        LieferantAnlegen(FensterHilfe.Lesen(txtSuche));
    }

    void LieferantAnlegen(string name)
    {
        var u = U;
        if (u == null || B == null) return;
        try
        {
            string hinweis;
            var e = Abm.LieferantAnlegen(B.Stand, u, name, WirtschaftMod.Heute, out hinweis);
            suche = false;
            gewaehlt = e.Nr;
            var l = Eintraege;
            seite = Math.Max(0, l.FindIndex(w => w.Nr == e.Nr) / Zeilen);
            FelderLaden();
            WirtschaftMod.Speichern();
            Melden(hinweis);
        }
        catch (WirtschaftsFehler f) { Melden(f.Message); }
    }

    // ---------- Anzeige ----------

    void Neu()
    {
        var u = U;
        for (int i = 0; i < Reiter; i++)
            if (reiter[i] != null) reiter[i].Text = (i == tab ? "> " : "") + (i == 0 ? "Übersicht" : Abm.Module[i - 1]);
        if (btnAn != null) btnAn.Text = u != null && u.AbmAn ? "Erweiterte Verwaltung: AN (ausschalten)" : "Erweiterte Verwaltung: AUS (einschalten)";
        for (int i = 0; i < 7; i++)
            if (modKnopf[i] != null) modKnopf[i].Text = (u != null && Abm.ModulGewaehlt(u, Abm.Module[i]) ? "[x] " : "[  ] ") + Abm.ModulText[i];
        if (btnLohnArt != null) btnLohnArt.Text = "Lohnabrechnung: " + (u != null && u.LohnManuell ? "manuell (umschalten)" : "automatisch (umschalten)");
        var arten = tab > 0 ? Abm.ArtenVon(Modul) : new List<AbmArt>();
        if (btnNeu1 != null) btnNeu1.Text = arten.Count > 0 ? "+ Neu: " + arten[0].Titel : "-";
        if (btnNeu2 != null) btnNeu2.Text = arten.Count > 1 ? "+ Neu: " + arten[1].Titel : "-";
        var akt = Eintrag;
        if (btnAktion != null) btnAktion.Text = akt == null ? "-" : Abm.AktionText(akt);

        int anzahl = Anzahl;
        int seiten = Math.Max(1, (anzahl + Zeilen - 1) / Zeilen);
        seite = FensterHilfe.Blaettern(seite, 0, seiten);
        var l = suche ? null : Eintraege;
        for (int z = 0; z < Zeilen; z++)
        {
            var b = zeile[z];
            if (b == null) continue;
            int idx = seite * Zeilen + z;
            if (idx < anzahl)
            {
                if (suche)
                {
                    var t = treffer[idx];
                    b.Text = "+ " + t.Name + " (" + (t.Branche.Length > 0 ? t.Branche + ", " : "") + t.Herkunft + ")";
                }
                else b.Text = (l[idx].Nr == gewaehlt ? "> " : "") + Zeilentext(l[idx]);
                b.IsVisible = true;
            }
            else b.IsVisible = false;
        }
        RefreshBindings(true);
    }

    public override bool GetBindingValue(ref string _value, string _bindingName)
    {
        var u = U;
        bool an = u != null && tab > 0 && Abm.ModulAn(u, Modul);
        var e = Eintrag;
        var d = e == null ? null : Abm.ArtDef(e.Art);
        if (_bindingName.StartsWith("abmzeile") || _bindingName.StartsWith("abmlabel") || _bindingName.StartsWith("abmwahl"))
        {
            string nr = _bindingName.Substring(_bindingName.StartsWith("abmzeile") ? 8 : (_bindingName.StartsWith("abmlabel") ? 8 : 7));
            int i;
            if (int.TryParse(nr, out i))
            {
                var f = d != null && i < d.Felder.Length ? d.Felder[i] : null;
                if (_bindingName.StartsWith("abmzeile")) _value = f != null ? "true" : "false";
                else if (_bindingName.StartsWith("abmlabel")) _value = f == null ? "" : f.Label + (f.NurLesen ? " (auto)" : "");
                else _value = f != null && f.Werte != null ? "true" : "false";
                return true;
            }
        }
        switch (_bindingName)
        {
            case "abmfirma": _value = u == null ? "-" : u.Name; return true;
            case "abmtab0": _value = tab == 0 ? "true" : "false"; return true;
            case "abmliste": _value = an ? "true" : "false"; return true;
            case "abmaus": _value = tab > 0 && !an ? "true" : "false"; return true;
            case "abmaustext":
                _value = u == null ? "" : (!u.AbmAn ? "Die erweiterte Verwaltung ist für " + u.Name + " aus. Im Reiter Übersicht einschalten."
                                                  : "Das Modul " + Abm.ModulText[Math.Max(0, tab - 1)] + " ist aus. Im Reiter Übersicht einschalten. Vorhandene Daten bleiben erhalten.");
                return true;
            case "abmeditor": _value = an && e != null && !suche ? "true" : "false"; return true;
            case "abmneu2an": _value = tab > 0 && Abm.ArtenVon(Modul).Count > 1 ? "true" : "false"; return true;
            case "abmsuchean": _value = Modul == Abm.Lieferanten ? "true" : "false"; return true;
            case "abmseite": _value = "Seite " + (seite + 1) + " / " + Math.Max(1, (Anzahl + Zeilen - 1) / Zeilen) + (suche ? " (Suche)" : ""); return true;
            case "abmtitel":
                if (suche) _value = "Suchtreffer links anklicken = als Lieferant übernehmen";
                else if (e == null) _value = Anzahl == 0 ? "Noch nichts angelegt: links auf '+ Neu' drücken" : "Links einen Eintrag wählen";
                else _value = d.Titel + " Nr. " + e.Nr;
                return true;
            case "abmwirkung":
                _value = e == null || u == null || B == null || suche ? "" : Abm.Wirkung(B, u, e);
                return true;
            case "abmuebersicht": _value = Uebersicht(); return true;
            case "abmaktionan": _value = an && e != null && !suche && Abm.AktionText(e).Length > 0 ? "true" : "false"; return true;
            case "abmaktionmenge": _value = e != null && Abm.AktionLabel(e).Length > 0 ? "true" : "false"; return true;
            case "abmaktionlabel":
                if (e == null) _value = "";
                else if (e.Art == "Produkt") { int h = Abm.Herstellbar(u, e); _value = h < 0 ? "Stück herstellen (Rezept fehlt)" : "Stück herstellen (max " + h + ")"; }
                else if (e.Art == "Material") _value = "Menge kaufen (" + FensterHilfe.B(e.Zahl("Preis")) + " je Stück)";
                else if (e.Art == "Projekt") _value = "Kosten vom Firmenkonto";
                else _value = "Ins Lager buchen";
                return true;
            case "abmmeldung": _value = B == null ? "Erst eine Welt laden." : meldung; return true;
        }
        return base.GetBindingValue(ref _value, _bindingName);
    }
}
