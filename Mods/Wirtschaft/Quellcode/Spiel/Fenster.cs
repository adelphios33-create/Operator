// Wirtschaft-Mod – Fenster für Hauptbank und Firmentresor (XUi)
// Geprüft gegen 7 Days to Die V 1.4 (b8): XUiController (Init, OnOpen, OnClose, GetBindingValue, RefreshBindings,
// GetChildById), XUiC_SimpleButton.OnPressed, XUiC_TextInput (Text, OnChangeHandler), GUIWindowManager.Open/CloseIfOpen.
// XUiFromXml.parseController nutzt ReflectionHelpers.GetTypeWithPrefix wie die Blöcke:
// controller="WbHauptbank, Wirtschaft" findet XUiC_WbHauptbank in Wirtschaft.dll. Darum ohne Namespace.
using System;
using System.Collections.Generic;
using System.Linq;
using Wirtschaft.Kern;
using Wirtschaft.Spiel;

static class FensterHilfe
{
    public static void Knopf(XUiController fenster, string id, XUiEvent_OnPressEventHandler aktion)
    {
        var b = fenster.GetChildById(id) as XUiC_SimpleButton;
        if (b != null) b.OnPressed += aktion;
        else Log.Warning("[Wirtschaft] Knopf '" + id + "' nicht gefunden.");
    }

    public static XUiC_TextInput Feld(XUiController fenster, string id, XUiEvent_InputOnChangedEventHandler aktion)
    {
        var t = fenster.GetChildById(id) as XUiC_TextInput;
        if (t != null && aktion != null) t.OnChangeHandler += aktion;
        if (t == null) Log.Warning("[Wirtschaft] Eingabefeld '" + id + "' nicht gefunden.");
        return t;
    }

    public static string Lesen(XUiC_TextInput t) { return t == null ? "" : (t.Text ?? ""); }

    public static void Schreiben(XUiC_TextInput t, string text) { if (t != null) t.Text = text; }

    public static string B(decimal d) { return Befehle.Betrag(d); }

    /// <summary>+/- Knopf: Zahl im Feld um "schritt" ändern (für Controller, ohne Tippen).</summary>
    public static void Schritt(XUiC_TextInput t, decimal schritt, decimal max)
    {
        if (t == null) return;
        decimal d;
        if (!Befehle.VersucheZahl(Lesen(t), out d)) d = 0m;
        d += schritt;
        if (d < 0) d = 0;
        if (d > max) d = max;
        t.Text = B(d);
    }

    /// <summary>Liest Umsatz, Ausgaben und Steuer; fehlende Felder oder ungültige Eingaben behalten den gespeicherten Wert.</summary>
    public static void Werte(Unternehmen u, XUiC_TextInput um, XUiC_TextInput aus, XUiC_TextInput st,
                             out decimal umsatz, out decimal ausgaben, out decimal steuer)
    {
        decimal d;
        umsatz = um != null && Befehle.VersucheZahl(Lesen(um), out d) && d >= 0 ? d : u.UmsatzMonat;
        ausgaben = aus != null && Befehle.VersucheZahl(Lesen(aus), out d) && d >= 0 ? d : u.AusgabenMonat;
        steuer = st != null && Befehle.VersucheZahl(Lesen(st), out d) && d >= 0 && d <= 100 ? d : u.Steuersatz;
    }

    public static string Naechste(Bank bank, Unternehmen u)
    {
        if (Finanzen.InKonkurs(u)) return Finanzen.KonkursText(u, WirtschaftMod.Heute);
        int t = bank.TageBisBuchung(u, WirtschaftMod.Heute);
        if (t < 0) return "Pausiert: diese Firma läuft gerade nicht";
        if (t == 0) return "Buchung beim nächsten Tageswechsel";
        return "Nächste Buchung in " + t + (t == 1 ? " Spieltag" : " Spieltagen");
    }

    /// <summary>Besitzer für eine neue Firma: zunächst immer "Bürger". Den echten Inhaber stellst du später selbst ein.</summary>
    public static string NeuerBesitzer(Bank bank, string stadt)
    {
        return bank.BuergerHolen().Name;
    }

    /// <summary>Gibt dem Spieler Gegenstände; was nicht in den Rucksack passt, fällt vor ihm auf den Boden.</summary>
    public static void Geben(EntityPlayerLocal spieler, string item, int anzahl)
    {
        var wert = ItemClass.GetItem(item, true);
        if (wert == null || wert.IsEmpty()) throw new WirtschaftsFehler("Gegenstand '" + item + "' gibt es im Spiel nicht.");
        int rest = anzahl;
        while (rest > 0)
        {
            int n = Math.Min(rest, 250);
            rest -= n;
            var stapel = new ItemStack(ItemClass.GetItem(item, true), n);
            if (!spieler.bag.AddItem(stapel) && stapel.count > 0)
                GameManager.Instance.ItemDropServer(stapel, spieler.GetPosition(), UnityEngine.Vector3.zero, spieler.entityId, 120f, false);
        }
    }

    public static int Blaettern(int idx, int richtung, int anzahl)
    {
        if (anzahl <= 0) return 0;
        return ((idx + richtung) % anzahl + anzahl) % anzahl;
    }

    public static bool Oeffnen(EntityPlayerLocal spieler, string gruppe)
    {
        try
        {
            if (spieler == null || spieler.PlayerUI == null) return false;
            spieler.PlayerUI.windowManager.Open(gruppe, true, false, true);
            return true;
        }
        catch (Exception e)
        {
            Log.Error("[Wirtschaft] Fenster '" + gruppe + "' konnte nicht geöffnet werden: " + e.Message);
            return false;
        }
    }
}

/// <summary>Hauptbank: gehört zu einer Bank (Stadt). Zeigt deren Firmen, Regierung und alle Parteien.</summary>
public class XUiC_WbHauptbank : XUiController
{
    public static XUiC_WbHauptbank Instanz;
    public const string Gruppe = "wbHauptbank";
    static string position = "";

    XUiC_TextInput txtUmsatz, txtAusgaben, txtSteuer, txtTage, txtNeu, txtBankName, txtRegierung, txtBetrag;
    int firmaIdx, bankWahl, quelle, waehrungIdx;   // quelle: 0 = Firma, 1 = Regierung, ab 2 = Personen
    static EntityPlayerLocal spieler;
    bool laedt;
    string meldung = "";

    Bank B { get { return WirtschaftMod.Bank; } }
    Bankhaus Haus { get { return B == null ? null : B.BankAmStandort(position); } }

    List<Unternehmen> Firmen
    {
        get { var h = Haus; return (B == null || h == null) ? new List<Unternehmen>() : B.FirmenDerBank(h.Name); }
    }

    Unternehmen Firma
    {
        get
        {
            var liste = Firmen;
            if (liste.Count == 0) return null;
            if (firmaIdx < 0 || firmaIdx >= liste.Count) firmaIdx = 0;
            return liste[firmaIdx];
        }
    }

    public static bool Oeffnen(EntityPlayerLocal sp, string pos)
    {
        if (Instanz == null) return false;
        position = pos;
        spieler = sp;
        return FensterHilfe.Oeffnen(sp, Gruppe);
    }

    public override void Init()
    {
        base.Init();
        Instanz = this;
        txtUmsatz = FensterHilfe.Feld(this, "txtUmsatz", FeldGeaendert);
        txtAusgaben = FensterHilfe.Feld(this, "txtAusgaben", FeldGeaendert);
        txtSteuer = FensterHilfe.Feld(this, "txtSteuer", FeldGeaendert);
        txtTage = FensterHilfe.Feld(this, "txtTage", FeldGeaendert);
        txtNeu = FensterHilfe.Feld(this, "txtNeu", null);
        txtBetrag = FensterHilfe.Feld(this, "txtBetrag", null);
        txtBankName = FensterHilfe.Feld(this, "txtBankName", null);
        txtRegierung = FensterHilfe.Feld(this, "txtRegierung", null);
        FensterHilfe.Knopf(this, "btnFirmaZurueck", (s, m) => FirmaBlaettern(-1));
        FensterHilfe.Knopf(this, "btnFirmaVor", (s, m) => FirmaBlaettern(1));
        FensterHilfe.Knopf(this, "btnBesitzerZurueck", (s, m) => BesitzerBlaettern(-1));
        FensterHilfe.Knopf(this, "btnBesitzerVor", (s, m) => BesitzerBlaettern(1));
        FensterHilfe.Knopf(this, "btnRegZurueck", (s, m) => RegierungBlaettern(-1));
        FensterHilfe.Knopf(this, "btnRegVor", (s, m) => RegierungBlaettern(1));
        FensterHilfe.Knopf(this, "btnUmsatzMinus", (s, m) => Plus(txtUmsatz, -1000m, 1000000000m));
        FensterHilfe.Knopf(this, "btnUmsatzPlus", (s, m) => Plus(txtUmsatz, 1000m, 1000000000m));
        FensterHilfe.Knopf(this, "btnAusgabenMinus", (s, m) => Plus(txtAusgaben, -1000m, 1000000000m));
        FensterHilfe.Knopf(this, "btnAusgabenPlus", (s, m) => Plus(txtAusgaben, 1000m, 1000000000m));
        FensterHilfe.Knopf(this, "btnSteuerMinus", (s, m) => Plus(txtSteuer, -1m, 100m));
        FensterHilfe.Knopf(this, "btnSteuerPlus", (s, m) => Plus(txtSteuer, 1m, 100m));
        FensterHilfe.Knopf(this, "btnBetragMinus", (s, m) => Plus(txtBetrag, -100m, 1000000000m));
        FensterHilfe.Knopf(this, "btnBetragPlus", (s, m) => Plus(txtBetrag, 100m, 1000000000m));
        FensterHilfe.Knopf(this, "btnLaeuftAn", (s, m) => Laeuft(true));
        FensterHilfe.Knopf(this, "btnLaeuftAus", (s, m) => Laeuft(false));
        FensterHilfe.Knopf(this, "btnStadtAn", (s, m) => StadtLaeuft(true));
        FensterHilfe.Knopf(this, "btnStadtAus", (s, m) => StadtLaeuft(false));
        FensterHilfe.Knopf(this, "btnQuelleZurueck", (s, m) => QuelleBlaettern(-1));
        FensterHilfe.Knopf(this, "btnQuelleVor", (s, m) => QuelleBlaettern(1));
        FensterHilfe.Knopf(this, "btnWaehrungZurueck", (s, m) => { waehrungIdx--; RefreshBindings(true); });
        FensterHilfe.Knopf(this, "btnWaehrungVor", (s, m) => { waehrungIdx++; RefreshBindings(true); });
        FensterHilfe.Knopf(this, "btnAuszahlen", (s, m) => Auszahlen());
        FensterHilfe.Knopf(this, "btnKredit", (s, m) => KreditOeffnen());
        FensterHilfe.Knopf(this, "btnNeueFirma", (s, m) => NeueFirma());
        FensterHilfe.Knopf(this, "btnNeuePartei", (s, m) => NeuePartei());
        FensterHilfe.Knopf(this, "btnBankAnlegen", (s, m) => BankAnlegen());
        FensterHilfe.Knopf(this, "btnBankZurueck", (s, m) => { bankWahl--; RefreshBindings(true); });
        FensterHilfe.Knopf(this, "btnBankVor", (s, m) => { bankWahl++; RefreshBindings(true); });
        FensterHilfe.Knopf(this, "btnBankVerbinden", (s, m) => BankVerbinden());
        FensterHilfe.Knopf(this, "btnSpeichern", (s, m) => { Uebernehmen(); WirtschaftMod.Speichern(); Melden("Gespeichert."); });
        FensterHilfe.Knopf(this, "btnSchliessen", (s, m) => xui.playerUI.windowManager.CloseIfOpen(Gruppe));
    }

    public override void OnOpen()
    {
        base.OnOpen();
        meldung = "";
        bankWahl = 0;
        FelderLaden();
        RefreshBindings(true);
    }

    public override void OnClose()
    {
        base.OnClose();
        Uebernehmen();
        WirtschaftMod.Speichern();
    }

    void FelderLaden()
    {
        laedt = true;
        var u = Firma;
        FensterHilfe.Schreiben(txtUmsatz, u == null ? "" : FensterHilfe.B(u.UmsatzMonat));
        FensterHilfe.Schreiben(txtAusgaben, u == null ? "" : FensterHilfe.B(u.AusgabenMonat));
        FensterHilfe.Schreiben(txtSteuer, u == null ? "" : FensterHilfe.B(u.Steuersatz));
        FensterHilfe.Schreiben(txtTage, B == null ? "" : B.Stand.TageProMonat.ToString());
        laedt = false;
    }

    void FeldGeaendert(XUiController sender, string text, bool vomCode)
    {
        if (laedt || vomCode) return;
        Uebernehmen();
        RefreshBindings(true);
    }

    void Uebernehmen()
    {
        if (B == null) return;
        var u = Firma;
        if (u != null)
        {
            decimal um, aus, st;
            FensterHilfe.Werte(u, txtUmsatz, txtAusgaben, txtSteuer, out um, out aus, out st);
            u.UmsatzMonat = um; u.AusgabenMonat = aus; u.Steuersatz = st;
        }
        decimal t;
        if (txtTage != null && Haus != null && Befehle.VersucheZahl(FensterHilfe.Lesen(txtTage), out t) && t >= 1 && t <= 365)
            B.Stand.TageProMonat = (int)t;
    }

    void Melden(string text) { meldung = text; RefreshBindings(true); }

    void FirmaBlaettern(int r)
    {
        var liste = Firmen;
        if (liste.Count == 0) return;
        Uebernehmen();
        firmaIdx = FensterHilfe.Blaettern(firmaIdx, r, liste.Count);
        meldung = "";
        FelderLaden();
        RefreshBindings(true);
    }

    void BesitzerBlaettern(int r)
    {
        var u = Firma;
        if (B == null || u == null) return;
        var liste = B.Stand.Parteien;
        if (liste.Count == 0) { Melden("Lege zuerst eine Partei an."); return; }
        int i = liste.FindIndex(w => string.Equals(w.Name, u.Besitzer, StringComparison.OrdinalIgnoreCase));
        i = i < 0 ? 0 : FensterHilfe.Blaettern(i, r, liste.Count);
        bool lief = u.Laeuft;
        B.BesitzerSetzen(u, liste[i].Name, WirtschaftMod.Heute);
        u.Laeuft = lief;
        B.Stand.AktivePartei = u.Besitzer;   // neue Firmen bekommen diesen Besitzer
        Melden("Besitzer: " + u.Besitzer);
    }

    void RegierungBlaettern(int r)
    {
        var h = Haus;
        if (B == null || h == null) return;
        var liste = B.Regierungen();
        if (liste.Count == 0) { Melden("Es gibt noch keine Regierung. Im Börsenterminal unter Personen 'Regierung anlegen'."); return; }
        int i = liste.FindIndex(w => string.Equals(w.Name, h.Regierung, StringComparison.OrdinalIgnoreCase));
        i = i < 0 ? 0 : FensterHilfe.Blaettern(i, r, liste.Count);
        B.RegierungSetzen(h.Name, liste[i].Name);
        Melden("Regierung von " + h.Name + ": " + h.Regierung);
    }

    void Laeuft(bool an)
    {
        var u = Firma;
        if (B == null || u == null) return;
        B.LaeuftSetzen(u, an, WirtschaftMod.Heute);
        WirtschaftMod.Speichern();
        Melden(u.Name + (an ? " läuft." : " pausiert."));
    }

    void Plus(XUiC_TextInput t, decimal schritt, decimal max)
    {
        if (t != txtBetrag && Firma == null) return;
        FensterHilfe.Schritt(t, schritt, max);
        Uebernehmen();
        RefreshBindings(true);
    }

    void StadtLaeuft(bool an)
    {
        var h = Haus;
        if (B == null || h == null) return;
        B.StadtLaeuftSetzen(h.Name, an, WirtschaftMod.Heute);
        WirtschaftMod.Speichern();
        Melden("Alle Firmen in " + h.Name + (an ? " laufen." : " pausiert."));
    }

    void QuelleBlaettern(int r)
    {
        if (B == null) return;
        quelle = FensterHilfe.Blaettern(quelle, r, 2 + B.Personen().Count);
        RefreshBindings(true);
    }

    /// <summary>Konto der gewählten Quelle (Firma, Regierung oder Person) und ihr Name.</summary>
    bool QuelleKonto(out string name, out decimal konto, out Unternehmen firma, out Partei partei)
    {
        name = "-"; konto = 0m; firma = null; partei = null;
        var h = Haus;
        if (B == null || h == null) return false;
        var personen = B.Personen();
        if (quelle >= 2 + personen.Count) quelle = 0;
        if (quelle == 0)
        {
            firma = Firma;
            if (firma == null) { name = "Firma"; return false; }
            name = firma.Name; konto = firma.Konto; return true;
        }
        partei = quelle == 1 ? B.RegierungVon(h.Name) : personen[quelle - 2];
        name = partei.Name; konto = partei.Konto; return true;
    }

    Zahlungsmittel Waehrung
    {
        get
        {
            if (B == null || B.Stand.Waehrungen.Count == 0) return null;
            return B.Stand.Waehrungen[FensterHilfe.Blaettern(waehrungIdx, 0, B.Stand.Waehrungen.Count)];
        }
    }

    void Auszahlen()
    {
        var h = Haus;
        if (B == null || h == null) return;
        try
        {
            if (spieler == null) throw new WirtschaftsFehler("Kein Spieler gefunden.");
            Uebernehmen();
            var w = Waehrung;
            decimal betrag;
            if (!Befehle.VersucheZahl(FensterHilfe.Lesen(txtBetrag), out betrag)) throw new WirtschaftsFehler("Betrag ist keine Zahl.");
            decimal kosten, konto;
            string name;
            Unternehmen firma;
            Partei partei;
            if (!QuelleKonto(out name, out konto, out firma, out partei)) throw new WirtschaftsFehler("Keine Quelle ausgewählt.");
            int stueck = Bank.Stueckzahl(konto, betrag, w, out kosten);
            FensterHilfe.Geben(spieler, w.Item, stueck);
            if (firma != null) firma.Konto -= kosten; else partei.Konto -= kosten;
            Melden(stueck + " " + w.Name + " ausgezahlt (" + name + ").");
            FensterHilfe.Schreiben(txtBetrag, "");
            WirtschaftMod.Speichern();
        }
        catch (WirtschaftsFehler f) { Melden(f.Message); }
    }

    /// <summary>Finanzierung der gewählten Firma (Kredit aufnehmen, tilgen) in eigenem Fenster.</summary>
    void KreditOeffnen()
    {
        var u = Firma;
        if (B == null || u == null) { Melden("Erst eine Firma wählen."); return; }
        Uebernehmen();
        WirtschaftMod.Speichern();
        if (!XUiC_WbKredit.Oeffnen(spieler, u, position)) Melden("Das Kreditfenster ist nicht geladen (Log ansehen).");
    }

    void NeueFirma()
    {
        var h = Haus;
        if (B == null || h == null) return;
        try
        {
            Uebernehmen();
            var u = B.FirmaAnlegen(FensterHilfe.Lesen(txtNeu), FensterHilfe.NeuerBesitzer(B, h.Name), null, WirtschaftMod.Heute, h.Name);
            firmaIdx = Firmen.IndexOf(u);
            FensterHilfe.Schreiben(txtNeu, "");
            FelderLaden();
            WirtschaftMod.Speichern();
            Melden("Firma " + u.Name + " angelegt.");
        }
        catch (WirtschaftsFehler f) { Melden(f.Message); }
    }

    void NeuePartei()
    {
        if (B == null) return;
        try
        {
            var p = B.ParteiAnlegen(FensterHilfe.Lesen(txtNeu), 0m);
            FensterHilfe.Schreiben(txtNeu, "");
            WirtschaftMod.Speichern();
            Melden("Partei " + p.Name + " angelegt.");
        }
        catch (WirtschaftsFehler f) { Melden(f.Message); }
    }

    void BankAnlegen()
    {
        if (B == null) return;
        try
        {
            var b = B.BankAnlegen(FensterHilfe.Lesen(txtBankName), FensterHilfe.Lesen(txtRegierung));
            B.StandortVerbinden(b.Name, position);
            FensterHilfe.Schreiben(txtBankName, "");
            FensterHilfe.Schreiben(txtRegierung, "");
            firmaIdx = 0;
            FelderLaden();
            WirtschaftMod.Speichern();
            Melden("Bank " + b.Name + " eröffnet. Regierung: " + b.Regierung);
        }
        catch (WirtschaftsFehler f) { Melden(f.Message); }
    }

    void BankVerbinden()
    {
        if (B == null) return;
        if (B.Stand.Banken.Count == 0) { Melden("Es gibt noch keine Bank. Lege oben eine an."); return; }
        var b = B.Stand.Banken[FensterHilfe.Blaettern(bankWahl, 0, B.Stand.Banken.Count)];
        B.StandortVerbinden(b.Name, position);
        firmaIdx = 0;
        FelderLaden();
        WirtschaftMod.Speichern();
        Melden("Diese Hauptbank gehört jetzt zu " + b.Name + ".");
    }

    public override bool GetBindingValue(ref string _value, string _bindingName)
    {
        var bank = B;
        var h = Haus;
        var u = Firma;
        Rechnung r = null;
        if (u != null)
        {
            decimal um, aus, st;
            FensterHilfe.Werte(u, txtUmsatz, txtAusgaben, txtSteuer, out um, out aus, out st);
            r = Bank.Rechne(u.Name, um, aus, st, 1);
        }
        switch (_bindingName)
        {
            case "wbeinrichten": _value = (bank != null && h == null) ? "true" : "false"; return true;
            case "wbbank": _value = (bank != null && h != null) ? "true" : "false"; return true;
            case "wbbankname": _value = h == null ? "Neue Hauptbank" : h.Name; return true;
            case "wbkopf":
                _value = bank == null ? "" : (bank.Stand.TestMinuten > 0 ? "TEST 1 Tag = " + bank.Stand.TestMinuten + " Min  |  " : "") + "Tag " + WirtschaftMod.Heute;
                return true;
            case "wbbankwahl":
                if (bank == null || bank.Stand.Banken.Count == 0) _value = "Noch keine Bank";
                else
                {
                    int i = FensterHilfe.Blaettern(bankWahl, 0, bank.Stand.Banken.Count);
                    _value = bank.Stand.Banken[i].Name + "   (" + (i + 1) + " / " + bank.Stand.Banken.Count + ")";
                }
                return true;
            case "wbfirma": _value = u == null ? "Noch keine Firma" : u.Name; return true;
            case "wbfirmazahl": _value = u == null ? "0 / 0" : (firmaIdx + 1) + " / " + Firmen.Count; return true;
            case "wbbesitzer": _value = u == null ? "-" : u.Besitzer; return true;
            case "wbumsatz": _value = r == null ? "-" : FensterHilfe.B(r.Umsatz); return true;
            case "wbausgaben": _value = r == null ? "-" : FensterHilfe.B(r.Ausgaben); return true;
            case "wbzwischen": _value = r == null ? "-" : FensterHilfe.B(r.Zwischensumme); return true;
            case "wbsteuer": _value = r == null ? "-" : FensterHilfe.B(r.Steuer); return true;
            case "wbeinkommen": _value = r == null ? "-" : FensterHilfe.B(r.Einkommen); return true;
            case "wbeinkommentext": _value = "= Einkommen der Firma"; return true;
            case "wbnaechste":
                _value = (u == null || bank == null) ? "Name unten bei 'Neu' eintippen und 'Neue Firma' drücken." : FensterHilfe.Naechste(bank, u);
                return true;
            case "wblaeuft": _value = u == null ? "-" : (u.Laeuft ? "Ja" : "Nein"); return true;
            case "wbguthaben":
                {
                    string qn; decimal qk; Unternehmen qf; Partei qp;
                    _value = QuelleKonto(out qn, out qk, out qf, out qp) ? qn + ": " + FensterHilfe.B(qk) : "";
                    return true;
                }
            case "wbquelle":
                {
                    string qn; decimal qk; Unternehmen qf; Partei qp;
                    QuelleKonto(out qn, out qk, out qf, out qp);
                    _value = qn;
                    return true;
                }
            case "wbwaehrung":
                var wz = Waehrung;
                _value = wz == null ? "-" : wz.Name + " (1 = " + FensterHilfe.B(wz.Kurs) + ")";
                return true;
            case "wbregname":
                _value = (bank == null || h == null) ? "-" : bank.RegierungVon(h.Name).Name;
                return true;
            case "wbregierung":
                if (bank == null || h == null) { _value = ""; return true; }
                _value = "Steuern/Monat " + FensterHilfe.B(bank.SteuernProMonat(h.Name)) + "  |  Konto " + FensterHilfe.B(bank.RegierungVon(h.Name).Konto);
                return true;
            case "wbmeldung": _value = bank == null ? "Erst eine Welt laden." : meldung; return true;
        }
        return base.GetBindingValue(ref _value, _bindingName);
    }
}

/// <summary>Firmentresor: bearbeitet die verbundene Firma, oder verbindet sich mit einer vorhandenen bzw. neuen Firma.</summary>
public class XUiC_WbTresor : XUiController
{
    public static XUiC_WbTresor Instanz;
    public const string Gruppe = "wbTresor";
    static string position = "";

    XUiC_TextInput txtUmsatz, txtAusgaben, txtSteuer, txtNeuFirma, txtMitarbeiter, txtImmo, txtFahrzeuge;
    XUiC_TextInput txtRegMit, txtRegAus;
    int wahlIdx, bankWahl, regWahl;
    bool laedt;
    string meldung = "";

    Bank B { get { return WirtschaftMod.Bank; } }
    Unternehmen Firma { get { return B == null ? null : B.FirmaAmTresor(position); } }
    Partei Reg { get { return B == null ? null : B.RegierungAmTresor(position); } }

    public static bool Oeffnen(EntityPlayerLocal spieler, string pos)
    {
        if (Instanz == null) return false;
        position = pos;
        return FensterHilfe.Oeffnen(spieler, Gruppe);
    }

    public override void Init()
    {
        base.Init();
        Instanz = this;
        txtUmsatz = FensterHilfe.Feld(this, "txtUmsatz", FeldGeaendert);
        txtAusgaben = FensterHilfe.Feld(this, "txtAusgaben", FeldGeaendert);
        txtSteuer = FensterHilfe.Feld(this, "txtSteuer", FeldGeaendert);
        txtNeuFirma = FensterHilfe.Feld(this, "txtNeuFirma", null);
        txtMitarbeiter = FensterHilfe.Feld(this, "txtMitarbeiter", FeldGeaendert);
        txtImmo = FensterHilfe.Feld(this, "txtImmo", FeldGeaendert);
        FensterHilfe.Knopf(this, "btnMitarbeiterMinus", (s, m) => Plus(txtMitarbeiter, -1m, 100000m));
        FensterHilfe.Knopf(this, "btnMitarbeiterPlus", (s, m) => Plus(txtMitarbeiter, 1m, 100000m));
        FensterHilfe.Knopf(this, "btnImmoMinus", (s, m) => Plus(txtImmo, -10000m, 10000000000m));
        FensterHilfe.Knopf(this, "btnImmoPlus", (s, m) => Plus(txtImmo, 10000m, 10000000000m));
        txtFahrzeuge = FensterHilfe.Feld(this, "txtFahrzeuge", FeldGeaendert);
        FensterHilfe.Knopf(this, "btnFahrzeugeMinus", (s, m) => Plus(txtFahrzeuge, -1000m, 10000000000m));
        FensterHilfe.Knopf(this, "btnFahrzeugePlus", (s, m) => Plus(txtFahrzeuge, 1000m, 10000000000m));
        FensterHilfe.Knopf(this, "btnWahlZurueck", (s, m) => { wahlIdx--; RefreshBindings(true); });
        FensterHilfe.Knopf(this, "btnWahlVor", (s, m) => { wahlIdx++; RefreshBindings(true); });
        FensterHilfe.Knopf(this, "btnVerbinden", (s, m) => Verbinden());
        FensterHilfe.Knopf(this, "btnBankZurueck", (s, m) => { bankWahl--; RefreshBindings(true); });
        FensterHilfe.Knopf(this, "btnBankVor", (s, m) => { bankWahl++; RefreshBindings(true); });
        FensterHilfe.Knopf(this, "btnAnlegen", (s, m) => AnlegenUndVerbinden());
        FensterHilfe.Knopf(this, "btnTrennen", (s, m) => Trennen());
        txtRegMit = FensterHilfe.Feld(this, "txtRegMit", FeldGeaendert);
        txtRegAus = FensterHilfe.Feld(this, "txtRegAus", FeldGeaendert);
        FensterHilfe.Knopf(this, "btnRegMitMinus", (s, m) => PlusReg(txtRegMit, -1m, 1000000m));
        FensterHilfe.Knopf(this, "btnRegMitPlus", (s, m) => PlusReg(txtRegMit, 1m, 1000000m));
        FensterHilfe.Knopf(this, "btnRegAusMinus", (s, m) => PlusReg(txtRegAus, -1000m, 1000000000m));
        FensterHilfe.Knopf(this, "btnRegAusPlus", (s, m) => PlusReg(txtRegAus, 1000m, 1000000000m));
        FensterHilfe.Knopf(this, "btnRegZurueck", (s, m) => { regWahl--; RefreshBindings(true); });
        FensterHilfe.Knopf(this, "btnRegVor", (s, m) => { regWahl++; RefreshBindings(true); });
        FensterHilfe.Knopf(this, "btnRegVerbinden", (s, m) => RegVerbinden());
        FensterHilfe.Knopf(this, "btnRegTrennen", (s, m) => RegTrennen());
        FensterHilfe.Knopf(this, "btnRegAn", (s, m) => RegLaeuft(true));
        FensterHilfe.Knopf(this, "btnRegAus", (s, m) => RegLaeuft(false));
        FensterHilfe.Knopf(this, "btnUmsatzMinus", (s, m) => Plus(txtUmsatz, -1000m, 1000000000m));
        FensterHilfe.Knopf(this, "btnUmsatzPlus", (s, m) => Plus(txtUmsatz, 1000m, 1000000000m));
        FensterHilfe.Knopf(this, "btnAusgabenMinus", (s, m) => Plus(txtAusgaben, -1000m, 1000000000m));
        FensterHilfe.Knopf(this, "btnAusgabenPlus", (s, m) => Plus(txtAusgaben, 1000m, 1000000000m));
        FensterHilfe.Knopf(this, "btnSteuerMinus", (s, m) => Plus(txtSteuer, -1m, 100m));
        FensterHilfe.Knopf(this, "btnSteuerPlus", (s, m) => Plus(txtSteuer, 1m, 100m));
        FensterHilfe.Knopf(this, "btnLaeuftAn", (s, m) => Laeuft(true));
        FensterHilfe.Knopf(this, "btnLaeuftAus", (s, m) => Laeuft(false));
        FensterHilfe.Knopf(this, "btnSpeichern", (s, m) => { Uebernehmen(); WirtschaftMod.Speichern(); Melden("Gespeichert."); });
        FensterHilfe.Knopf(this, "btnSchliessen", (s, m) => xui.playerUI.windowManager.CloseIfOpen(Gruppe));
    }

    public override void OnOpen()
    {
        base.OnOpen();
        meldung = "";
        wahlIdx = 0;
        bankWahl = 0;
        regWahl = 0;
        FelderLaden();
        RefreshBindings(true);
    }

    public override void OnClose()
    {
        base.OnClose();
        Uebernehmen();
        WirtschaftMod.Speichern();
    }

    void FelderLaden()
    {
        laedt = true;
        var u = Firma;
        FensterHilfe.Schreiben(txtUmsatz, u == null ? "" : FensterHilfe.B(u.UmsatzMonat));
        FensterHilfe.Schreiben(txtAusgaben, u == null ? "" : FensterHilfe.B(u.AusgabenMonat));
        FensterHilfe.Schreiben(txtSteuer, u == null ? "" : FensterHilfe.B(u.Steuersatz));
        FensterHilfe.Schreiben(txtMitarbeiter, u == null ? "" : u.Mitarbeiter.ToString());
        FensterHilfe.Schreiben(txtImmo, u == null ? "" : FensterHilfe.B(u.Immobilienwert));
        FensterHilfe.Schreiben(txtFahrzeuge, u == null ? "" : FensterHilfe.B(u.Fahrzeugwert));
        var g = Reg;
        FensterHilfe.Schreiben(txtRegMit, g == null ? "" : g.RegMitarbeiter.ToString());
        FensterHilfe.Schreiben(txtRegAus, g == null ? "" : FensterHilfe.B(g.RegAusgaben));
        laedt = false;
    }

    void FeldGeaendert(XUiController sender, string text, bool vomCode)
    {
        if (laedt || vomCode) return;
        Uebernehmen();
        RefreshBindings(true);
    }

    void Uebernehmen()
    {
        var g = Reg;
        if (g != null)
        {
            decimal x;
            if (txtRegMit != null && Befehle.VersucheZahl(FensterHilfe.Lesen(txtRegMit), out x) && x >= 0 && x <= 1000000) g.RegMitarbeiter = (int)x;
            if (txtRegAus != null && Befehle.VersucheZahl(FensterHilfe.Lesen(txtRegAus), out x) && x >= 0) g.RegAusgaben = x;
        }
        var u = Firma;
        if (u == null) return;
        decimal um, aus, st, d;
        FensterHilfe.Werte(u, txtUmsatz, txtAusgaben, txtSteuer, out um, out aus, out st);
        u.UmsatzMonat = um; u.AusgabenMonat = aus; u.Steuersatz = st;
        if (txtMitarbeiter != null && Befehle.VersucheZahl(FensterHilfe.Lesen(txtMitarbeiter), out d) && d >= 0 && d <= 100000) u.Mitarbeiter = (int)d;
        if (txtImmo != null && Befehle.VersucheZahl(FensterHilfe.Lesen(txtImmo), out d) && d >= 0) u.Immobilienwert = d;
        if (txtFahrzeuge != null && Befehle.VersucheZahl(FensterHilfe.Lesen(txtFahrzeuge), out d) && d >= 0) u.Fahrzeugwert = d;
    }

    void Melden(string text) { meldung = text; RefreshBindings(true); }

    void Verbinden()
    {
        if (B == null || B.Stand.Unternehmen.Count == 0) { Melden("Es gibt noch keine Firma. Lege unten eine an."); return; }
        var u = B.Stand.Unternehmen[FensterHilfe.Blaettern(wahlIdx, 0, B.Stand.Unternehmen.Count)];
        B.TresorVerbinden(u.Name, position);
        WirtschaftMod.Speichern();
        FelderLaden();
        Melden("Verbunden mit " + u.Name + ".");
    }

    void AnlegenUndVerbinden()
    {
        if (B == null) return;
        try
        {
            if (B.Stand.Banken.Count == 0)
                throw new WirtschaftsFehler("Stell zuerst eine Hauptbank auf und eröffne dort eine Bank.");
            var bank = B.Stand.Banken[FensterHilfe.Blaettern(bankWahl, 0, B.Stand.Banken.Count)];
            var u = B.FirmaAnlegen(FensterHilfe.Lesen(txtNeuFirma), FensterHilfe.NeuerBesitzer(B, bank.Name), null, WirtschaftMod.Heute, bank.Name);
            B.TresorVerbinden(u.Name, position);
            FensterHilfe.Schreiben(txtNeuFirma, "");
            WirtschaftMod.Speichern();
            FelderLaden();
            Melden("Firma " + u.Name + " angelegt und verbunden.");
        }
        catch (WirtschaftsFehler f) { Melden(f.Message); }
    }

    void Laeuft(bool an)
    {
        var u = Firma;
        if (B == null || u == null) return;
        B.LaeuftSetzen(u, an, WirtschaftMod.Heute);
        WirtschaftMod.Speichern();
        Melden(u.Name + (an ? " läuft." : " pausiert."));
    }

    void Plus(XUiC_TextInput t, decimal schritt, decimal max)
    {
        if (Firma == null) return;
        FensterHilfe.Schritt(t, schritt, max);
        Uebernehmen();
        RefreshBindings(true);
    }

    void PlusReg(XUiC_TextInput t, decimal schritt, decimal max)
    {
        if (Reg == null) return;
        FensterHilfe.Schritt(t, schritt, max);
        Uebernehmen();
        RefreshBindings(true);
    }

    void RegVerbinden()
    {
        var l = B == null ? new List<Partei>() : B.Regierungen();
        if (l.Count == 0) { Melden("Es gibt noch keine Regierung."); return; }
        var p = l[FensterHilfe.Blaettern(regWahl, 0, l.Count)];
        try
        {
            B.RegierungTresorVerbinden(p.Name, position);
            WirtschaftMod.Speichern();
            FelderLaden();
            Melden(p.Name + " ist verbunden. Die Daten dieses Tresors gelten ab jetzt.");
        }
        catch (WirtschaftsFehler f) { Melden(f.Message); }
    }

    void RegTrennen()
    {
        var g = Reg;
        if (g == null) return;
        Uebernehmen();
        g.RegTresor = "";
        g.RegZuletzt = 0;
        WirtschaftMod.Speichern();
        FelderLaden();
        Melden(g.Name + " getrennt. Sie läuft wieder automatisch.");
    }

    void RegLaeuft(bool an)
    {
        var g = Reg;
        if (g == null) return;
        g.RegAus = !an;
        WirtschaftMod.Speichern();
        Melden(g.Name + (an ? ": Staatsausgaben laufen." : ": Staatsausgaben pausiert."));
    }

    void Trennen()
    {
        var u = Firma;
        if (u == null) return;
        Uebernehmen();
        u.Tresor = "";
        WirtschaftMod.Speichern();
        FelderLaden();
        Melden("Tresor von " + u.Name + " getrennt.");
    }

    public override bool GetBindingValue(ref string _value, string _bindingName)
    {
        var bank = B;
        var u = Firma;
        Rechnung r = null;
        if (u != null)
        {
            decimal um, aus, st;
            FensterHilfe.Werte(u, txtUmsatz, txtAusgaben, txtSteuer, out um, out aus, out st);
            r = Bank.Rechne(u.Name, um, aus, st, 1);
        }
        switch (_bindingName)
        {
            case "wbverbunden": _value = u != null ? "true" : "false"; return true;
            case "wbfrei": _value = (u == null && Reg == null) ? "true" : "false"; return true;
            case "wbregierung": _value = Reg != null ? "true" : "false"; return true;
            case "wbfirma": _value = u != null ? u.Name : (Reg != null ? Reg.Name : "Nicht verbunden"); return true;
            case "wbregwahl":
                {
                    var rl = bank == null ? new List<Partei>() : bank.Regierungen();
                    _value = rl.Count == 0 ? "Noch keine Regierung" : rl[FensterHilfe.Blaettern(regWahl, 0, rl.Count)].Name;
                    return true;
                }
            case "wbregbesitzer":
                _value = Reg == null ? "" : "Regierung   |   Daten dieses Tresors haben Vorrang";
                return true;
            case "wbregsteuer": _value = Reg == null ? "-" : FensterHilfe.B(bank.RegierungSteuern(Reg)); return true;
            case "wbreggehalt": _value = Reg == null ? "-" : FensterHilfe.B(Reg.RegMitarbeiter * Bank.RegGehalt); return true;
            case "wbregausgaben": _value = Reg == null ? "-" : FensterHilfe.B(bank.RegierungAusgaben(Reg)); return true;
            case "wbregsaldo": _value = Reg == null ? "-" : FensterHilfe.B(bank.RegierungSteuern(Reg) - (Reg.RegAus ? 0m : bank.RegierungAusgaben(Reg))); return true;
            case "wbreglaeuft": _value = Reg == null ? "-" : (Reg.RegAus ? "Nein" : "Ja"); return true;
            case "wbregkonto": _value = Reg == null ? "" : "Staatskasse: " + FensterHilfe.B(Reg.Konto) + "  (Konto der Regierung)"; return true;
            case "wbbesitzer":
                _value = u == null ? "" : "Besitzer: " + u.Name + "   |   Bank: " + u.Bank + "   |   Regierung: " + bank.RegierungVon(u.Bank).Name;
                return true;
            case "wbumsatz": _value = r == null ? "-" : FensterHilfe.B(r.Umsatz); return true;
            case "wbausgaben": _value = r == null ? "-" : FensterHilfe.B(r.Ausgaben); return true;
            case "wbzwischen": _value = r == null ? "-" : FensterHilfe.B(r.Zwischensumme); return true;
            case "wbsteuer": _value = r == null ? "-" : FensterHilfe.B(r.Steuer); return true;
            case "wbeinkommen": _value = r == null ? "-" : FensterHilfe.B(r.Einkommen); return true;
            case "wbeinkommentext": _value = "= Einkommen der Firma"; return true;
            case "wbnaechste": _value = (u == null || bank == null) ? "" : FensterHilfe.Naechste(bank, u); return true;
            case "wbguthaben": _value = u == null ? "" : "Guthaben: " + FensterHilfe.B(u.Konto) + "  (auszahlen an der Hauptbank)"; return true;
            case "wbwert":
                _value = u == null ? "" : "Firmenwert (Börse): " + FensterHilfe.B(bank.Firmenwert(u)) + "   |   1 % = " + FensterHilfe.B(bank.AnteilPreis(u, 1m));
                return true;
            case "wbwertteile":
                {
                    if (u == null) { _value = ""; return true; }
                    var bw = bank.Bewerten(u);
                    _value = "Umsatz ×" + FensterHilfe.B(bw.Faktor) + " " + FensterHilfe.B(bw.Umsatz) + "  +  Mitarbeiter " + FensterHilfe.B(bw.Mitarbeiter)
                        + "  +  Immobilien " + FensterHilfe.B(bw.Immobilien) + "  +  Fahrzeuge " + FensterHilfe.B(bw.Fahrzeuge)
                        + "  " + (bw.Ertrag < 0 ? "−  Verlust " : "+  Ertrag ") + FensterHilfe.B(Math.Abs(bw.Ertrag));
                    return true;
                }
            case "wblaeuft": _value = u == null ? "-" : (u.Laeuft ? "Ja" : "Nein"); return true;
            case "wbwahl":
                if (bank == null || bank.Stand.Unternehmen.Count == 0) _value = "Noch keine Firma";
                else
                {
                    int i = FensterHilfe.Blaettern(wahlIdx, 0, bank.Stand.Unternehmen.Count);
                    var f = bank.Stand.Unternehmen[i];
                    _value = f.Name + " (" + f.Bank + ")";
                }
                return true;
            case "wbbankwahl":
                if (bank == null || bank.Stand.Banken.Count == 0) _value = "Noch keine Bank";
                else _value = bank.Stand.Banken[FensterHilfe.Blaettern(bankWahl, 0, bank.Stand.Banken.Count)].Name;
                return true;
            case "wbmeldung": _value = bank == null ? "Erst eine Welt laden." : meldung; return true;
        }
        return base.GetBindingValue(ref _value, _bindingName);
    }
}

/// <summary>Börsenterminal: Reiter Firmen (Werte, Eigentümer), Inhaber (Gründer festlegen), Handel (Anteile kaufen/verkaufen) und Personen. Alle Namen stehen in einer Liste zum Anklicken.</summary>
public class XUiC_WbBoerse : XUiController
{
    public static XUiC_WbBoerse Instanz;
    public const string Gruppe = "wbBoerse";
    const int Zeilen = 6;

    XUiC_TextInput txtNeuPerson;
    XUiC_SimpleButton[] zeile = new XUiC_SimpleButton[Zeilen];
    XUiC_SimpleButton btnSlotVerk, btnSlotKaeuf, btnSlotG1, btnSlotG2;
    XUiC_SimpleButton[] reiter = new XUiC_SimpleButton[5];
    string[] reiterName = new string[] { "Firmen", "Inhaber", "Handel", "Personen", "Ereignisse" };
    XUiC_TextInput txtSMin, txtSZielVon, txtSZielBis, txtSMax, txtSBetrag;
    string sonderSchluessel = "";
    XUiC_SimpleButton btnSAn, btnSProfil, btnSIntens, btnSHaeufig, btnSPos, btnSNeg, btnSFavorit, btnSBenachricht;
    int tab, slot, seite, firmaIdx;
    int prozent = 1;      // Handel
    int gProzent = 100;   // Gründer 1
    string verk = "", kaeuf = "", g1 = "", g2 = "", person = "";
    string meldung = "";
    string bankSel = "";   // Personen-Reiter: gewählte Bank

    Bank B { get { return WirtschaftMod.Bank; } }

    static string startPos = "";
    string region = "";   // "" = alle Regionen, sonst Name einer Bank (Stadt)

    public static bool Oeffnen(EntityPlayerLocal spieler)
    {
        return Oeffnen(spieler, "");
    }

    /// <summary>Mit Position des Terminals: Die Börse startet dann in der Region der nächsten Hauptbank.</summary>
    public static bool Oeffnen(EntityPlayerLocal spieler, string pos)
    {
        if (Instanz == null) return false;
        startPos = pos ?? "";
        return FensterHilfe.Oeffnen(spieler, Gruppe);
    }

    public override void Init()
    {
        base.Init();
        Instanz = this;
        txtNeuPerson = FensterHilfe.Feld(this, "txtNeuPerson", null);
        for (int i = 0; i < Zeilen; i++)
        {
            int n = i;
            zeile[i] = GetChildById("btnL" + i) as XUiC_SimpleButton;
            FensterHilfe.Knopf(this, "btnL" + i, (s, m) => ZeileGewaehlt(n));
        }
        btnSlotVerk = GetChildById("btnSlotVerk") as XUiC_SimpleButton;
        btnSlotKaeuf = GetChildById("btnSlotKaeuf") as XUiC_SimpleButton;
        btnSlotG1 = GetChildById("btnSlotG1") as XUiC_SimpleButton;
        btnSlotG2 = GetChildById("btnSlotG2") as XUiC_SimpleButton;
        for (int i = 0; i < 5; i++)
        {
            int n = i;
            reiter[i] = GetChildById("btnTab" + i) as XUiC_SimpleButton;
            FensterHilfe.Knopf(this, "btnTab" + i, (s, m) => { tab = n; slot = 0; seite = 0; meldung = ""; Neu(); });
        }
        FensterHilfe.Knopf(this, "btnSlotVerk", (s, m) => { slot = 0; Neu(); });
        FensterHilfe.Knopf(this, "btnSlotKaeuf", (s, m) => { slot = 1; Neu(); });
        FensterHilfe.Knopf(this, "btnSlotG1", (s, m) => { slot = 0; Neu(); });
        FensterHilfe.Knopf(this, "btnSlotG2", (s, m) => { slot = 1; Neu(); });
        FensterHilfe.Knopf(this, "btnG2Keiner", (s, m) => { g2 = ""; slot = 0; Neu(); });
        FensterHilfe.Knopf(this, "btnRegionZurueck", (s, m) => RegionWechseln(-1));
        FensterHilfe.Knopf(this, "btnRegionVor", (s, m) => RegionWechseln(1));
        FensterHilfe.Knopf(this, "btnFirmaZurueck", (s, m) => { firmaIdx--; meldung = ""; Neu(); });
        FensterHilfe.Knopf(this, "btnFirmaVor", (s, m) => { firmaIdx++; meldung = ""; Neu(); });
        FensterHilfe.Knopf(this, "btnListeZurueck", (s, m) => { seite--; Neu(); });
        FensterHilfe.Knopf(this, "btnListeVor", (s, m) => { seite++; Neu(); });
        FensterHilfe.Knopf(this, "btnProzentMinus", (s, m) => { prozent = Math.Max(1, prozent - 1); Neu(); });
        FensterHilfe.Knopf(this, "btnProzentPlus", (s, m) => { prozent = Math.Min(100, prozent + 1); Neu(); });
        FensterHilfe.Knopf(this, "btnProzent10", (s, m) => { prozent = Math.Min(100, prozent + 10); Neu(); });
        FensterHilfe.Knopf(this, "btnGMinus", (s, m) => { gProzent = Math.Max(1, gProzent - 1); Neu(); });
        FensterHilfe.Knopf(this, "btnGPlus", (s, m) => { gProzent = Math.Min(100, gProzent + 1); Neu(); });
        FensterHilfe.Knopf(this, "btnG10", (s, m) => { gProzent = gProzent >= 100 ? 10 : Math.Min(100, gProzent + 10); Neu(); });
        FensterHilfe.Knopf(this, "btnVerkaufen", (s, m) => Handeln(true));
        FensterHilfe.Knopf(this, "btnEintragen", (s, m) => Handeln(false));
        FensterHilfe.Knopf(this, "btnGruenderSetzen", (s, m) => GruenderFestlegen());
        FensterHilfe.Knopf(this, "btnNeuePerson", (s, m) => NeuePerson());
        FensterHilfe.Knopf(this, "btnPersonUmbenennen", (s, m) => PersonUmbenennen());
        FensterHilfe.Knopf(this, "btnPersonLoeschen", (s, m) => PersonLoeschen());
        FensterHilfe.Knopf(this, "btnRegAnlegen", (s, m) => RegierungAnlegenKnopf());
        FensterHilfe.Knopf(this, "btnBankNeu", (s, m) => BankNeuKnopf());
        FensterHilfe.Knopf(this, "btnBankUmbenennen", (s, m) => BankUmbenennenKnopf());
        FensterHilfe.Knopf(this, "btnBankLoeschen", (s, m) => BankLoeschenKnopf());
        FensterHilfe.Knopf(this, "btnPBankZurueck", (s, m) => BankBlaettern(-1));
        FensterHilfe.Knopf(this, "btnPBankVor", (s, m) => BankBlaettern(1));
        FensterHilfe.Knopf(this, "btnRegZuweisen", (s, m) => RegZuweisenKnopf());
        txtSMin = FensterHilfe.Feld(this, "txtSMin", null);
        txtSZielVon = FensterHilfe.Feld(this, "txtSZielVon", null);
        txtSZielBis = FensterHilfe.Feld(this, "txtSZielBis", null);
        txtSMax = FensterHilfe.Feld(this, "txtSMax", null);
        txtSBetrag = FensterHilfe.Feld(this, "txtSBetrag", null);
        btnSAn = GetChildById("btnSAn") as XUiC_SimpleButton;
        btnSFavorit = GetChildById("btnSFavorit") as XUiC_SimpleButton;
        btnSBenachricht = GetChildById("btnSBenachricht") as XUiC_SimpleButton;
        FensterHilfe.Knopf(this, "btnSFavorit", (s, m) => SonderFirma(f => { f.Favorit = !f.Favorit; return "Favorit " + (f.Favorit ? "an: du bekommst seine Ereignis-Meldungen." : "aus."); }));
        FensterHilfe.Knopf(this, "btnSBenachricht", (s, m) => { if (B == null) return; B.Stand.SonderBenachrichtigung = !B.Stand.SonderBenachrichtigung; WirtschaftMod.Speichern(); Melden("Ereignis-Meldungen " + (B.Stand.SonderBenachrichtigung ? "an (nur Favoriten)." : "aus.")); });
        btnSProfil = GetChildById("btnSProfil") as XUiC_SimpleButton;
        btnSIntens = GetChildById("btnSIntens") as XUiC_SimpleButton;
        btnSHaeufig = GetChildById("btnSHaeufig") as XUiC_SimpleButton;
        btnSPos = GetChildById("btnSPos") as XUiC_SimpleButton;
        btnSNeg = GetChildById("btnSNeg") as XUiC_SimpleButton;
        FensterHilfe.Knopf(this, "btnSAn", (s, m) => SonderFirma(f => { Ereignisse.Schalten(f, !f.SonderAn); return "Sonderereignisse " + (f.SonderAn ? "eingeschaltet (ab der nächsten Monatsbuchung)." : "ausgeschaltet."); }));
        FensterHilfe.Knopf(this, "btnSProfil", (s, m) => SonderFirma(f => ProfilWechseln(f)));
        FensterHilfe.Knopf(this, "btnSIntens", (s, m) => SonderFirma(f => { f.SonderIntensitaet = (f.SonderIntensitaet + 1) % 3; return "Intensität: " + Ereignisse.IntensitaetName(f.SonderIntensitaet) + "."; }));
        FensterHilfe.Knopf(this, "btnSHaeufig", (s, m) => SonderFirma(f => { f.SonderHaeufigkeit = (f.SonderHaeufigkeit + 1) % 3; return "Häufigkeit: " + Ereignisse.HaeufigkeitName(f.SonderHaeufigkeit) + "."; }));
        FensterHilfe.Knopf(this, "btnSPos", (s, m) => SonderFirma(f => { f.SonderPositiv = !f.SonderPositiv; return "Positive Ereignisse " + (f.SonderPositiv ? "an." : "aus."); }));
        FensterHilfe.Knopf(this, "btnSNeg", (s, m) => SonderFirma(f => { f.SonderNegativ = !f.SonderNegativ; return "Negative Ereignisse " + (f.SonderNegativ ? "an." : "aus."); }));
        FensterHilfe.Knopf(this, "btnSAuto", (s, m) => SonderFirma(f => { f.SonderMin = 0; f.SonderZielVon = 0; f.SonderZielBis = 0; f.SonderMax = 0; sonderSchluessel = ""; return "Grenzen: automatischer Vorschlag aus den Firmendaten."; }));
        FensterHilfe.Knopf(this, "btnSSpeichern", (s, m) => GrenzenSpeichern());
        FensterHilfe.Knopf(this, "btnSPlus", (s, m) => SonderManuell(true));
        FensterHilfe.Knopf(this, "btnSMinus", (s, m) => SonderManuell(false));
        FensterHilfe.Knopf(this, "btnSAlleAn", (s, m) => SonderAuswahl(true));
        FensterHilfe.Knopf(this, "btnSAlleAus", (s, m) => SonderAuswahl(false));
        FensterHilfe.Knopf(this, "btnGeldtresorAufloesen", (s, m) => GeldtresorAufloesen());
        FensterHilfe.Knopf(this, "btnFirmaLoeschen", (s, m) => FirmaLoeschen());
        FensterHilfe.Knopf(this, "btnSchliessen", (s, m) => xui.playerUI.windowManager.CloseIfOpen(Gruppe));
    }

    public override void OnOpen()
    {
        base.OnOpen();
        meldung = "";
        slot = 0;
        firmaIdx = 0;
        region = StandardRegion();
        Neu();
    }

    // ---------- Region (Filter nach Bank/Stadt) ----------

    List<string> Regionen()
    {
        var l = new List<string> { "" };
        if (B != null) l.AddRange(B.Stand.Banken.Select(x => x.Name));
        return l;
    }

    /// <summary>Region der Hauptbank, die dem Terminal am nächsten liegt (bis 300 Blöcke), sonst alle.</summary>
    string StandardRegion()
    {
        if (B == null || startPos.Length == 0) return "";
        try
        {
            var t = startPos.Split(',');
            float tx = float.Parse(t[0]), ty = float.Parse(t[1]), tz = float.Parse(t[2]);
            string beste = ""; float bestD = 300f * 300f;
            foreach (var bk in B.Stand.Banken)
                foreach (var sp in bk.Standorte)
                {
                    var q = sp.Split(',');
                    if (q.Length < 3) continue;
                    float dx = float.Parse(q[0]) - tx, dy = float.Parse(q[1]) - ty, dz = float.Parse(q[2]) - tz;
                    float d = dx * dx + dy * dy + dz * dz;
                    if (d < bestD) { bestD = d; beste = bk.Name; }
                }
            return beste;
        }
        catch (Exception) { return ""; }
    }

    void RegionWechseln(int richtung)
    {
        var l = Regionen();
        int i = Math.Max(0, l.FindIndex(x => string.Equals(x, region, StringComparison.OrdinalIgnoreCase)));
        region = l[FensterHilfe.Blaettern(i, richtung, l.Count)];
        firmaIdx = 0;
        meldung = "";
        Neu();
    }

    /// <summary>Firmen der gewählten Region (oder alle).</summary>
    List<Unternehmen> Firmen
    {
        get
        {
            if (B == null) return new List<Unternehmen>();
            if (region.Length == 0) return B.Stand.Unternehmen;
            return B.Stand.Unternehmen.Where(f => string.Equals(f.Bank, region, StringComparison.OrdinalIgnoreCase)).ToList();
        }
    }

    public override void OnClose()
    {
        base.OnClose();
        WirtschaftMod.Speichern();
    }

    Unternehmen Firma
    {
        get
        {
            var f = Firmen;
            if (f.Count == 0) return null;
            return f[FensterHilfe.Blaettern(firmaIdx, 0, f.Count)];
        }
    }

    /// <summary>Alle, die Anteile halten oder kaufen können: Personen und Regierungen.</summary>
    List<Partei> Liste { get { return B == null ? new List<Partei>() : B.Stand.Parteien; } }

    string Gueltig(string name)
    {
        return (B != null && !string.IsNullOrEmpty(name) && B.ParteiOderNull(name) != null) ? name : "";
    }

    void ZeileGewaehlt(int z)
    {
        var l = Liste;
        int idx = seite * Zeilen + z;
        if (idx < 0 || idx >= l.Count) return;
        string n = l[idx].Name;
        if (tab == 2) { if (slot == 0) verk = n; else kaeuf = n; }
        else if (tab == 1) { if (slot == 0) g1 = n; else g2 = n; }
        else person = n;
        meldung = "";
        Neu();
    }

    /// <summary>Knopftexte setzen und Anzeige neu zeichnen.</summary>
    void Neu()
    {
        var l = Liste;
        int seiten = Math.Max(1, (l.Count + Zeilen - 1) / Zeilen);
        seite = FensterHilfe.Blaettern(seite, 0, seiten);
        verk = Gueltig(verk); kaeuf = Gueltig(kaeuf); g1 = Gueltig(g1); g2 = Gueltig(g2); person = Gueltig(person);
        if (person == "" && l.Count > 0) person = l[0].Name;
        var u = Firma;
        for (int z = 0; z < Zeilen; z++)
        {
            var b = zeile[z];
            if (b == null) continue;
            int idx = seite * Zeilen + z;
            if (idx < l.Count)
            {
                var p = l[idx];
                string t = (idx + 1) + "  " + p.Name;
                if (B != null && B.IstRegierungspartei(p)) t += B.StaedteVon(p.Name).Count > 0 ? " (Regierung)" : " (Regierung, frei)";
                if (u != null && (tab == 1 || tab == 2))
                {
                    decimal a = B.AnteilVon(u, p.Name);
                    if (a > 0) t += "  - " + FensterHilfe.B(a) + " %";
                }
                b.Text = t;
                b.IsVisible = true;
            }
            else b.IsVisible = false;
        }
        for (int i = 0; i < 5; i++)
            if (reiter[i] != null) reiter[i].Text = (i == tab ? "[ " + reiterName[i] + " ]" : reiterName[i]);
        string pfeil = "> ";
        if (btnSlotVerk != null) btnSlotVerk.Text = (slot == 0 ? pfeil : "") + "Verkäufer: " + (verk == "" ? "wählen" : verk);
        if (btnSlotKaeuf != null) btnSlotKaeuf.Text = (slot == 1 ? pfeil : "") + "Käufer: " + (kaeuf == "" ? "wählen" : kaeuf);
        if (btnSlotG1 != null) btnSlotG1.Text = (slot == 0 ? pfeil : "") + "Gründer 1: " + (g1 == "" ? "wählen" : g1);
        if (btnSlotG2 != null) btnSlotG2.Text = (slot == 1 ? pfeil : "") + "Gründer 2: " + (g2 == "" ? "keiner" : g2);
        string sk = (u == null ? "" : u.Name) + "|" + tab;
        if (sk != sonderSchluessel) { sonderSchluessel = sk; SonderFelderFuellen(u); }
        SonderKnoepfe(u);
        RefreshBindings(true);
    }

    void Melden(string text) { meldung = text; Neu(); }

    void Handeln(bool mitZahlung)
    {
        var u = Firma;
        if (B == null || u == null || verk == "" || kaeuf == "") { Melden("Firma, Verkäufer und Käufer wählen."); return; }
        try
        {
            decimal preis = B.AnteileUebertragen(u, verk, kaeuf, prozent, mitZahlung, WirtschaftMod.Heute);
            WirtschaftMod.Speichern();
            Melden(prozent + " % von " + u.Name + ": " + verk + " -> " + kaeuf +
                   (mitZahlung ? " für " + FensterHilfe.B(preis) : " (ohne Zahlung)"));
        }
        catch (WirtschaftsFehler f) { Melden(f.Message); }
    }

    void GruenderFestlegen()
    {
        var u = Firma;
        if (B == null || u == null || g1 == "") { Melden("Firma und Gründer 1 wählen."); return; }
        try
        {
            B.GruenderSetzen(u, g1, g2 == "" ? 100 : gProzent, g2, WirtschaftMod.Heute);
            WirtschaftMod.Speichern();
            Melden("Gründer von " + u.Name + " festgelegt.");
        }
        catch (WirtschaftsFehler f) { Melden(f.Message); }
    }

    // ---------- Firma löschen und Geldtresor auflösen (mit Bestätigung: zweimal drücken) ----------

    string frage = "";
    float frageZeit;

    bool Bestaetigt(string schluessel, string text)
    {
        float jetzt = UnityEngine.Time.time;
        if (frage == schluessel && jetzt - frageZeit < 15f) { frage = ""; return true; }
        frage = schluessel;
        frageZeit = jetzt;
        Melden(text);
        return false;
    }

    void GeldtresorAufloesen()
    {
        var u = Firma;
        if (B == null || u == null) { Melden("Erst eine Firma wählen."); return; }
        if (string.IsNullOrEmpty(u.Geldtresor)) { Melden(u.Name + " hat keinen Geldtresor."); return; }
        if (!Bestaetigt("tresor:" + u.Name, "Nochmal drücken: Geldtresor von " + u.Name + " auflösen. Das Geld bleibt auf dem Firmenkonto.")) return;
        Geldtresor.Loesen(B, u);
        WirtschaftMod.Speichern();
        Melden("Geldtresor von " + u.Name + " aufgelöst. Guthaben " + FensterHilfe.B(u.Konto) + " bleibt auf dem Konto.");
    }

    void FirmaLoeschen()
    {
        var u = Firma;
        if (B == null || u == null) { Melden("Erst eine Firma wählen."); return; }
        decimal schulden = Finanzen.Schulden(B, u);
        string folgen = "Guthaben " + FensterHilfe.B(u.Konto) + (schulden > 0 ? ", Kredite " + FensterHilfe.B(schulden) : "") + " geht an die Eigentümer.";
        if (!Bestaetigt("loeschen:" + u.Name, "Nochmal drücken: " + u.Name + " wirklich LÖSCHEN? " + folgen)) return;
        try
        {
            Geldtresor.Loesen(B, u);
            string bericht = Finanzen.FirmaLoeschen(B, u, WirtschaftMod.Heute);
            firmaIdx = 0;
            WirtschaftMod.Speichern();
            Melden(bericht);
        }
        catch (WirtschaftsFehler f) { Melden(f.Message); }
    }

    // ---------- Dynamische Sonderereignisse (Reiter Ereignisse) ----------

    void SonderFirma(Func<Unternehmen, string> aktion)
    {
        var u = Firma;
        if (B == null || u == null) { Melden("Erst eine Firma wählen."); return; }
        string text = aktion(u);
        WirtschaftMod.Speichern();
        Melden(u.Name + ": " + text);
    }

    string ProfilWechseln(Unternehmen u)
    {
        var alle = Branchen.Alle();
        int i = alle.FindIndex(x => string.Equals(x.Name, u.SonderProfil, StringComparison.OrdinalIgnoreCase));
        // Reihenfolge: automatisch -> Profil 1 -> ... -> letztes Profil -> automatisch
        if (string.IsNullOrEmpty(u.SonderProfil) || i < 0) u.SonderProfil = alle[0].Name;
        else if (i + 1 < alle.Count) u.SonderProfil = alle[i + 1].Name;
        else u.SonderProfil = "";
        return "Branchenprofil: " + (u.SonderProfil.Length == 0 ? "automatisch (" + Branchen.VonFirma(u).Name + ")" : u.SonderProfil) + ".";
    }

    static decimal ZahlLesen(XUiC_TextInput t)
    {
        string s = FensterHilfe.Lesen(t).Replace(" ", "").Replace(".", "").Replace(",", "").Trim();
        if (s.Length == 0) return 0m;
        decimal d;
        if (!decimal.TryParse(s, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out d)) return -1m;
        return Math.Floor(d);
    }

    static string Feldtext(decimal d) { return d > 0 ? Befehle.Betrag(d).Replace(".", "").Replace(",", "") : ""; }

    void SonderKnoepfe(Unternehmen u)
    {
        if (btnSAn != null) btnSAn.Text = u == null ? "An / Aus" : (u.SonderAn ? "Ist AN: ausschalten" : "Ist AUS: einschalten");
        if (btnSFavorit != null) btnSFavorit.Text = u == null ? "Favorit" : (u.Favorit ? "Favorit: AN" : "Favorit: aus");
        if (btnSBenachricht != null) btnSBenachricht.Text = B == null ? "Ereignis-Meldungen" : (B.Stand.SonderBenachrichtigung ? "Ereignis-Meldungen: AN" : "Ereignis-Meldungen: AUS");
        if (btnSProfil != null) btnSProfil.Text = u == null ? "Profil" : Branchen.VonFirma(u).Name + (string.IsNullOrEmpty(u.SonderProfil) ? " (auto)" : "");
        if (btnSIntens != null) btnSIntens.Text = u == null ? "Intensität" : "Stärke: " + Ereignisse.IntensitaetName(u.SonderIntensitaet);
        if (btnSHaeufig != null) btnSHaeufig.Text = u == null ? "Häufigkeit" : "Häufigkeit: " + Ereignisse.HaeufigkeitName(u.SonderHaeufigkeit);
        if (btnSPos != null) btnSPos.Text = u == null ? "Positive" : "Positive: " + (u.SonderPositiv ? "an" : "aus");
        if (btnSNeg != null) btnSNeg.Text = u == null ? "Negative" : "Negative: " + (u.SonderNegativ ? "an" : "aus");
    }

    void SonderFelderFuellen(Unternehmen u)
    {
        FensterHilfe.Schreiben(txtSMin, u == null ? "" : Feldtext(u.SonderMin));
        FensterHilfe.Schreiben(txtSZielVon, u == null ? "" : Feldtext(u.SonderZielVon));
        FensterHilfe.Schreiben(txtSZielBis, u == null ? "" : Feldtext(u.SonderZielBis));
        FensterHilfe.Schreiben(txtSMax, u == null ? "" : Feldtext(u.SonderMax));
    }

    void GrenzenSpeichern()
    {
        var u = Firma;
        if (B == null || u == null) { Melden("Erst eine Firma wählen."); return; }
        decimal min = ZahlLesen(txtSMin), zv = ZahlLesen(txtSZielVon), zb = ZahlLesen(txtSZielBis), max = ZahlLesen(txtSMax);
        if (min < 0 || zv < 0 || zb < 0 || max < 0) { Melden("Bitte nur ganze Zahlen eintragen (leer = Vorschlag)."); return; }
        var v = Ereignisse.Vorschlag(u);
        string fehler = Ereignisse.GrenzenPruefen(min > 0 ? min : v.Min, zv > 0 ? zv : v.ZielVon, zb > 0 ? zb : v.ZielBis, max > 0 ? max : v.Max);
        if (fehler != null) { Melden(fehler); return; }
        u.SonderMin = min; u.SonderZielVon = zv; u.SonderZielBis = zb; u.SonderMax = max;
        WirtschaftMod.Speichern();
        sonderSchluessel = "";
        Melden(u.Name + ": Liquiditätsgrenzen gespeichert.");
    }

    void SonderManuell(bool positiv)
    {
        var u = Firma;
        if (B == null || u == null) { Melden("Erst eine Firma wählen."); return; }
        decimal betrag = ZahlLesen(txtSBetrag);
        if (betrag < 0) { Melden("Betrag: ganze Zahl oder leer (leer = passend zur Firmengröße)."); return; }
        try
        {
            var e = Ereignisse.Manuell(B, u, positiv, betrag, WirtschaftMod.Heute);
            FensterHilfe.Schreiben(txtSBetrag, "");
            WirtschaftMod.Speichern();
            Melden(Ereignisse.Meldung(u, e));
        }
        catch (WirtschaftsFehler f) { Melden(f.Message); }
    }

    /// <summary>Alle Firmen der gewählten Region (oder alle Regionen) ein- oder ausschalten.</summary>
    void SonderAuswahl(bool an)
    {
        if (B == null) return;
        var liste = Firmen;
        if (liste.Count == 0) { Melden("Keine Firmen in dieser Auswahl."); return; }
        string schluessel = "sonder:" + (an ? "an:" : "aus:") + region;
        string wo = region.Length == 0 ? "ALLEN " + liste.Count + " Firmen (alle Regionen)" : "allen " + liste.Count + " Firmen in " + region;
        if (!Bestaetigt(schluessel, "Nochmal drücken: Sonderereignisse bei " + wo + (an ? " einschalten." : " ausschalten."))) return;
        foreach (var f in liste) Ereignisse.Schalten(f, an);
        WirtschaftMod.Speichern();
        Melden("Sonderereignisse bei " + wo + (an ? " eingeschaltet." : " ausgeschaltet."));
    }

    void PersonUmbenennen()
    {
        if (B == null) return;
        if (person == "") { Melden("Erst links eine Person anklicken."); return; }
        string neu = FensterHilfe.Lesen(txtNeuPerson).Trim();
        if (neu.Length == 0) { Melden("Neuen Namen bei 'Name' eintippen, dann Umbenennen drücken."); return; }
        try
        {
            string alt = person;
            var p = B.ParteiUmbenennen(alt, neu);
            FensterHilfe.Schreiben(txtNeuPerson, "");
            person = p.Name;
            WirtschaftMod.Speichern();
            Melden(alt + " heißt jetzt " + p.Name + ".");
        }
        catch (WirtschaftsFehler f) { Melden(f.Message); }
    }

    void PersonLoeschen()
    {
        if (B == null) return;
        if (person == "") { Melden("Erst links eine Person anklicken."); return; }
        try
        {
            string folgen = B.LoeschFolgen(person);   // wirft bei "Bürger"
            if (!Bestaetigt("person:" + person, "Bist du sicher? " + folgen + " Nochmal 'Löschen' drücken.")) return;
            string weg = person;
            var p = B.ParteiOderNull(weg);
            if (p != null && !string.IsNullOrEmpty(p.Geldtresor)) Geldtresor.Loesen(B, Halter.Von(p));
            B.ParteiLoeschen(weg);
            person = "";
            WirtschaftMod.Speichern();
            Melden(weg + " wurde gelöscht. Alles, was dranhing, gehört jetzt " + Bank.BuergerName + ".");
        }
        catch (WirtschaftsFehler f) { Melden(f.Message); }
    }

    // ---------- Banken und Regierungen (Personen-Reiter) ----------

    Bankhaus BankGewaehlt()
    {
        if (B == null || B.Stand.Banken.Count == 0) return null;
        var l = B.Stand.Banken;
        int i = l.FindIndex(x => string.Equals(x.Name, bankSel, StringComparison.OrdinalIgnoreCase));
        return l[i < 0 ? 0 : i];
    }

    void BankBlaettern(int r)
    {
        if (B == null || B.Stand.Banken.Count == 0) return;
        var l = B.Stand.Banken;
        int i = Math.Max(0, l.FindIndex(x => string.Equals(x.Name, bankSel, StringComparison.OrdinalIgnoreCase)));
        bankSel = l[FensterHilfe.Blaettern(i, r, l.Count)].Name;
        meldung = "";
        Neu();
    }

    void RegierungAnlegenKnopf()
    {
        if (B == null) return;
        try
        {
            var p = B.RegierungAnlegen(FensterHilfe.Lesen(txtNeuPerson));
            FensterHilfe.Schreiben(txtNeuPerson, "");
            person = p.Name;
            seite = Math.Max(0, (B.Stand.Parteien.IndexOf(p)) / Zeilen);
            WirtschaftMod.Speichern();
            Melden("Regierung " + p.Name + " angelegt. Mit 'Regierung für diese Bank' einer Bank zuweisen.");
        }
        catch (WirtschaftsFehler f) { Melden(f.Message); }
    }

    void BankNeuKnopf()
    {
        if (B == null) return;
        try
        {
            var b = B.BankAnlegen(FensterHilfe.Lesen(txtNeuPerson), null);
            FensterHilfe.Schreiben(txtNeuPerson, "");
            bankSel = b.Name;
            WirtschaftMod.Speichern();
            Melden("Bank " + b.Name + " angelegt, Regierung: " + b.Regierung + ". Hauptbank-Block dafür im Spiel mit ihr verbinden.");
        }
        catch (WirtschaftsFehler f) { Melden(f.Message); }
    }

    void BankUmbenennenKnopf()
    {
        if (B == null) return;
        var h = BankGewaehlt();
        if (h == null) { Melden("Es gibt noch keine Bank."); return; }
        string neu = FensterHilfe.Lesen(txtNeuPerson).Trim();
        if (neu.Length == 0) { Melden("Neuen Namen bei 'Name' eintippen, dann 'Bank umbenennen' drücken."); return; }
        try
        {
            string alt = h.Name;
            B.BankUmbenennen(alt, neu);
            if (string.Equals(region, alt, StringComparison.OrdinalIgnoreCase)) region = neu;
            bankSel = neu;
            FensterHilfe.Schreiben(txtNeuPerson, "");
            WirtschaftMod.Speichern();
            Melden("Bank " + alt + " heißt jetzt " + neu + ".");
        }
        catch (WirtschaftsFehler f) { Melden(f.Message); }
    }

    void BankLoeschenKnopf()
    {
        if (B == null) return;
        var h = BankGewaehlt();
        if (h == null) { Melden("Es gibt keine Bank zum Löschen."); return; }
        try
        {
            string folgen = B.BankLoeschFolgen(h.Name);   // wirft, wenn es nicht geht
            if (!Bestaetigt("bank:" + h.Name, "Bist du sicher? " + folgen + " Nochmal 'Bank löschen' drücken.")) return;
            string weg = h.Name;
            B.BankLoeschen(weg);
            if (string.Equals(region, weg, StringComparison.OrdinalIgnoreCase)) region = "";
            bankSel = "";
            firmaIdx = 0;
            WirtschaftMod.Speichern();
            Melden("Bank " + weg + " wurde gelöscht.");
        }
        catch (WirtschaftsFehler f) { Melden(f.Message); }
    }

    void RegZuweisenKnopf()
    {
        if (B == null) return;
        var h = BankGewaehlt();
        if (h == null) { Melden("Es gibt noch keine Bank."); return; }
        if (person == "") { Melden("Erst links eine Regierung anklicken."); return; }
        try
        {
            B.RegierungSetzen(h.Name, person);
            WirtschaftMod.Speichern();
            Melden(person + " ist jetzt Regierung von " + h.Name + " (bekommt dort die Steuern).");
        }
        catch (WirtschaftsFehler f) { Melden(f.Message); }
    }

    void NeuePerson()
    {
        if (B == null) return;
        try
        {
            var p = B.ParteiAnlegen(FensterHilfe.Lesen(txtNeuPerson), 0m);
            FensterHilfe.Schreiben(txtNeuPerson, "");
            person = p.Name;
            seite = Math.Max(0, (B.Stand.Parteien.IndexOf(p)) / Zeilen);
            WirtschaftMod.Speichern();
            Melden("Person " + p.Name + " angelegt.");
        }
        catch (WirtschaftsFehler f) { Melden(f.Message); }
    }

    public override bool GetBindingValue(ref string _value, string _bindingName)
    {
        var bank = B;
        var u = Firma;
        switch (_bindingName)
        {
            case "wbtab0": _value = (bank != null && tab == 0) ? "true" : "false"; return true;
            case "wbtab1": _value = (bank != null && tab == 1) ? "true" : "false"; return true;
            case "wbtab2": _value = (bank != null && tab == 2) ? "true" : "false"; return true;
            case "wbtab3": _value = (bank != null && tab == 3) ? "true" : "false"; return true;
            case "wbtab4": _value = (bank != null && tab == 4) ? "true" : "false"; return true;
            case "wbfirmenwahl": _value = (bank != null && tab != 3) ? "true" : "false"; return true;
            case "wbliste": _value = (bank != null && (tab == 1 || tab == 2 || tab == 3)) ? "true" : "false"; return true;
            case "wbsstatus":
                {
                    if (u == null) { _value = ""; return true; }
                    var g = Ereignisse.Grenzen(u);
                    _value = "Sonderereignisse: " + (u.SonderAn ? "AN" : "AUS") + "   |   Liquidität " + FensterHilfe.B(u.Konto) + " (" + Ereignisse.LageName(Ereignisse.Lage(u, g)) + ")";
                    return true;
                }
            case "wbsgrenzen":
                {
                    if (u == null) { _value = ""; return true; }
                    var g = Ereignisse.Grenzen(u);
                    var v = Ereignisse.Vorschlag(u);
                    _value = "Gilt: Min " + FensterHilfe.B(g.Min) + "  |  Ziel " + FensterHilfe.B(g.ZielVon) + " - " + FensterHilfe.B(g.ZielBis) + "  |  Max " + FensterHilfe.B(g.Max)
                        + (g.Auto ? "  (Vorschlag)" : "  (Vorschlag wäre " + FensterHilfe.B(v.Min) + " / " + FensterHilfe.B(v.ZielVon) + "-" + FensterHilfe.B(v.ZielBis) + " / " + FensterHilfe.B(v.Max) + ")");
                    return true;
                }
            case "wbsauswahl":
                {
                    if (bank == null) { _value = ""; return true; }
                    var l = Firmen;
                    _value = "Auswahl " + (region.Length == 0 ? "alle Regionen" : region) + ": " + l.Count + " Firmen, davon " + l.Count(f => f.SonderAn) + " mit Sonderereignissen"
                        + "   |   Summe hier: +" + (u == null ? "0" : FensterHilfe.B(Ereignisse.Summe(u, true))) + " / -" + (u == null ? "0" : FensterHilfe.B(Ereignisse.Summe(u, false)));
                    return true;
                }
            case "wbsprotokoll": _value = u == null ? "" : Ereignisse.Protokoll(u, 3); return true;
            case "wbseite":
                _value = bank == null ? "" : "Seite " + (seite + 1) + " / " + Math.Max(1, (Liste.Count + Zeilen - 1) / Zeilen);
                return true;
            case "wbkopf": _value = bank == null ? "" : "Tag " + WirtschaftMod.Heute; return true;
            case "wbfirma": _value = u == null ? (region.Length == 0 ? "Noch keine Firma" : "Keine Firma in dieser Region") : u.Name; return true;
            case "wbregion":
                {
                    if (bank == null) { _value = ""; return true; }
                    if (region.Length == 0) { _value = "Alle Regionen"; return true; }
                    var bh = bank.Stand.Banken.FirstOrDefault(x => string.Equals(x.Name, region, StringComparison.OrdinalIgnoreCase));
                    _value = region + (bh != null && bh.Regierung.Length > 0 ? "  (Regierung: " + bh.Regierung + ")" : "");
                    return true;
                }
            case "wbregionzahl": _value = bank == null ? "" : Firmen.Count + " Firmen"; return true;
            case "wbfirmazahl":
                _value = u == null ? "0 / 0" : (FensterHilfe.Blaettern(firmaIdx, 0, Firmen.Count) + 1) + " / " + Firmen.Count;
                return true;
            case "wbfirmainfo":
                _value = u == null ? "" : "Bank: " + u.Bank + "   |   Besitzer: " + u.Besitzer + "   |   Guthaben: " + FensterHilfe.B(u.Konto)
                    + (Finanzen.Schulden(bank, u) > 0 ? "   |   Kredite: " + FensterHilfe.B(Finanzen.Schulden(bank, u)) : "");
                return true;
            case "wbkonkurs": _value = u == null ? "" : Finanzen.KonkursText(u, WirtschaftMod.Heute); return true;
            case "wbschuldenquote":
                _value = u == null ? "-" : Finanzen.QuoteText(bank, u, 0m);
                return true;
            case "wbgeldtresor":
                _value = u == null ? "" : (string.IsNullOrEmpty(u.Geldtresor) ? "Geldtresor: keiner" : "Geldtresor: verbunden (" + u.Geldtresor + ")");
                return true;
            case "wbumsatztext":
                _value = u == null ? "Monatsumsatz" : "Monatsumsatz " + FensterHilfe.B(u.UmsatzMonat) + " × " + FensterHilfe.B(bank.Bewerten(u).Faktor);
                return true;
            case "wbumsatz": _value = u == null ? "-" : FensterHilfe.B(bank.Bewerten(u).Umsatz); return true;
            case "wbmitarbeitertext":
                _value = u == null ? "+ Mitarbeiter" : "+ Mitarbeiter: " + u.Mitarbeiter + "  (Staffel 1.000 / 2.000 / 3.000)";
                return true;
            case "wbmitarbeiterwert": _value = u == null ? "-" : FensterHilfe.B(Bank.MitarbeiterWert(u.Mitarbeiter)); return true;
            case "wbimmo": _value = u == null ? "-" : FensterHilfe.B(u.Immobilienwert); return true;
            case "wbfahrzeuge": _value = u == null ? "-" : FensterHilfe.B(u.Fahrzeugwert); return true;
            case "wbertragtext":
                _value = (u != null && bank.Bewerten(u).Ertrag < 0) ? "− Verlust (" + Bank.Ertragsmonate + " Monatsverluste)" : "+ Ertrag (" + Bank.Ertragsmonate + " Monatsgewinne nach Steuer)";
                return true;
            case "wbertrag": _value = u == null ? "-" : FensterHilfe.B(bank.Bewerten(u).Ertrag); return true;
            case "wbkennzahl":
                {
                    if (u == null) { _value = ""; return true; }
                    var pr = Branchen.VonFirma(u);
                    _value = "Branche: " + pr.Name + "   |   Umsatzrendite " + FensterHilfe.B(Befehle.Rendite(u)) + " %  (üblich "
                        + FensterHilfe.B(pr.MargeVon) + "–" + FensterHilfe.B(pr.MargeBis) + " %)";
                    return true;
                }
            case "wbwert":
                _value = u == null ? "-" : FensterHilfe.B(bank.Firmenwert(u)) + (u.Wert > 0 ? " (fest)" : "");
                return true;
            case "wbpreise":
                _value = u == null ? "" : "1 % = " + FensterHilfe.B(bank.AnteilPreis(u, 1m)) + "      10 % = " + FensterHilfe.B(bank.AnteilPreis(u, 10m));
                return true;
            case "wbeigentuemer":
                {
                    if (u == null || u.Anteile.Count == 0) { _value = "-"; return true; }
                    var sb = new System.Text.StringBuilder();
                    for (int k = 0; k < u.Anteile.Count; k++)
                    {
                        if (k > 0) sb.Append(k % 2 == 0 ? "\n" : "   |   ");
                        sb.Append(u.Anteile[k].Partei + " " + FensterHilfe.B(u.Anteile[k].Prozent) + " %");
                    }
                    _value = sb.ToString();
                    return true;
                }
            case "wbprozent": _value = prozent + " %"; return true;
            case "wbpreis": _value = u == null ? "-" : "Preis: " + FensterHilfe.B(bank.AnteilPreis(u, prozent)); return true;
            case "wbgprozent": _value = g2 == "" ? "100 %" : gProzent + " %"; return true;
            case "wbgrest":
                _value = g2 == "" ? "Kein Gründer 2: Gründer 1 hält alles." : (g2 + " erhält " + (gProzent >= 100 ? 0 : 100 - gProzent) + " %");
                return true;
            case "wbperson": _value = person == "" ? "Noch keine Person" : person; return true;
            case "wbbank":
                {
                    var bh = BankGewaehlt();
                    _value = bh == null ? "Noch keine Bank" : "Bank: " + bh.Name + "  (" + (bank.Stand.Banken.IndexOf(bh) + 1) + "/" + bank.Stand.Banken.Count + ")";
                    return true;
                }
            case "wbbankinfo":
                {
                    var bh = BankGewaehlt();
                    _value = bh == null ? "" : "Regierung: " + (string.IsNullOrEmpty(bh.Regierung) ? "keine (wird bei Bedarf neu angelegt)" : bh.Regierung)
                        + "  |  " + bank.FirmenDerBank(bh.Name).Count + " Firmen";
                    return true;
                }
            case "wbpersonkonto":
                {
                    var p = person == "" ? null : bank.ParteiOderNull(person);
                    _value = p == null ? "" : "Konto: " + FensterHilfe.B(p.Konto) + "\nVermögen (Konto + Anteile): " + FensterHilfe.B(bank.Vermoegen(p));
                    return true;
                }
            case "wbpersonanteile":
                {
                    if (person == "") { _value = ""; return true; }
                    var teile = bank.Stand.Unternehmen.Where(w => bank.AnteilVon(w, person) > 0)
                        .Select(w => w.Name + " " + FensterHilfe.B(bank.AnteilVon(w, person)) + " % = " + FensterHilfe.B(bank.AnteilPreis(w, bank.AnteilVon(w, person))))
                        .ToArray();
                    _value = teile.Length == 0 ? "Noch keine Anteile" : string.Join("\n", teile);
                    return true;
                }
            case "wbmeldung": _value = bank == null ? "Erst eine Welt laden." : meldung; return true;
        }
        return base.GetBindingValue(ref _value, _bindingName);
    }
}
