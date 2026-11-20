// Wirtschaft-Mod – Fenster für Geldtresor (Banking) und Finanzierung (Kredite)
// Gleiche Bauweise wie Fenster.cs: ohne Namespace, damit controller="WbGeldtresor, Wirtschaft" den Typ findet.
using System;
using System.Collections.Generic;
using System.Linq;
using Wirtschaft.Kern;
using Wirtschaft.Spiel;

/// <summary>Geldtresor: Firma oder Person wählen und verbinden, trennen, Inhalt öffnen.</summary>
public class XUiC_WbGeldtresor : XUiController
{
    public static XUiC_WbGeldtresor Instanz;
    public const string Gruppe = "wbGeldtresor";
    static Vector3i pos3;
    static string position = "";
    static EntityPlayerLocal spieler;

    int wahlIdx;
    int art;   // 0 = Firmen und Personen, 1 = nur Firmen, 2 = nur Personen
    bool wechseln, trennenFragen;
    string meldung = "";
    float uhr;
    XUiC_SimpleButton btnArt;

    Bank B { get { return WirtschaftMod.Bank; } }
    Halter Besitzer { get { return Geldtresor.HalterAmGeldtresor(B, position); } }

    List<Halter> Auswahl
    {
        get
        {
            var l = Geldtresor.Vorschlaege(B, pos3);
            if (art == 1) l = l.Where(h => !h.IstPerson).ToList();
            else if (art == 2) l = l.Where(h => h.IstPerson && !h.IstRegierung).ToList();
            else if (art == 3) l = l.Where(h => h.IstRegierung).ToList();
            return l;
        }
    }

    Halter Gewaehlt
    {
        get
        {
            var l = Auswahl;
            if (l.Count == 0) return null;
            return l[FensterHilfe.Blaettern(wahlIdx, 0, l.Count)];
        }
    }

    static string ArtName(int a) { return a == 1 ? "nur Firmen" : (a == 2 ? "nur Personen" : (a == 3 ? "nur Regierungen" : "Firmen, Personen, Regierungen")); }

    public static bool Oeffnen(EntityPlayerLocal sp, Vector3i p)
    {
        if (Instanz == null || WirtschaftMod.Bank == null) return false;
        pos3 = p;
        position = BlockHilfe.Position(p);
        spieler = sp;
        return FensterHilfe.Oeffnen(sp, Gruppe);
    }

    public override void Init()
    {
        base.Init();
        Instanz = this;
        FensterHilfe.Knopf(this, "btnOeffnen", (s, m) => InhaltOeffnen());
        FensterHilfe.Knopf(this, "btnWechseln", (s, m) => { wechseln = true; trennenFragen = false; wahlIdx = 0; Melden(""); });
        FensterHilfe.Knopf(this, "btnTrennen", (s, m) => Trennen());
        FensterHilfe.Knopf(this, "btnWahlZurueck", (s, m) => { wahlIdx--; trennenFragen = false; Melden(""); });
        FensterHilfe.Knopf(this, "btnWahlVor", (s, m) => { wahlIdx++; trennenFragen = false; Melden(""); });
        btnArt = GetChildById("btnWahlArt") as XUiC_SimpleButton;
        FensterHilfe.Knopf(this, "btnWahlArt", (s, m) => { art = (art + 1) % 4; wahlIdx = 0; trennenFragen = false; Melden(""); });
        FensterHilfe.Knopf(this, "btnVerbinden", (s, m) => Verbinden());
        FensterHilfe.Knopf(this, "btnAbbrechen", (s, m) => { wechseln = false; Melden(""); });
        FensterHilfe.Knopf(this, "btnSchliessen", (s, m) => xui.playerUI.windowManager.CloseIfOpen(Gruppe));
    }

    public override void OnOpen()
    {
        base.OnOpen();
        meldung = "";
        wechseln = false;
        trennenFragen = false;
        wahlIdx = 0;
        var h = Besitzer;
        if (h != null) Geldtresor.VorDemOeffnen(B, h);
        RefreshBindings(true);
    }

    public override void Update(float _dt)
    {
        base.Update(_dt);
        uhr += _dt;
        if (uhr >= 1f) { uhr = 0f; RefreshBindings(true); }   // Guthaben und Inhalt live
    }

    void Melden(string text)
    {
        meldung = text;
        if (btnArt != null) btnArt.Text = "Anzeigen: " + ArtName(art) + " (anklicken)";
        RefreshBindings(true);
    }

    void InhaltOeffnen()
    {
        trennenFragen = false;
        if (Besitzer == null) { Melden("Erst eine Firma oder Person verbinden."); return; }
        xui.playerUI.windowManager.CloseIfOpen(Gruppe);
        if (!BlockGeldtresor.InhaltOeffnen(pos3, spieler))
            Log.Warning("[Wirtschaft] Geldtresor bei " + position + " konnte nicht geöffnet werden.");
    }

    void Verbinden()
    {
        trennenFragen = false;
        var h = Gewaehlt;
        if (B == null || h == null) { Melden("Niemand zur Auswahl. Lege eine Firma oder Person an (Hauptbank oder Börse)."); return; }
        try
        {
            bool zog = !string.IsNullOrEmpty(h.Tresor) && h.Tresor != position;
            Geldtresor.Verbinden(B, h, position);
            WirtschaftMod.Speichern();
            wechseln = false;
            Melden("Verbunden mit " + h.Name + " (" + h.Art + ")." + (zog ? " Der alte Geldtresor ist jetzt frei." : ""));
        }
        catch (WirtschaftsFehler f) { Melden(f.Message); }
    }

    void Trennen()
    {
        var h = Besitzer;
        if (B == null || h == null) return;
        if (!trennenFragen)
        {
            trennenFragen = true;
            Melden("Nochmal 'Trennen' drücken. Das Geld bleibt auf dem Konto von " + h.Name + ".");
            return;
        }
        trennenFragen = false;
        Geldtresor.Loesen(B, h);
        WirtschaftMod.Speichern();
        Melden("Getrennt. " + FensterHilfe.B(h.Konto) + " bleiben auf dem Konto von " + h.Name + ".");
    }

    public override bool GetBindingValue(ref string _value, string _bindingName)
    {
        var bank = B;
        var h = Besitzer;
        switch (_bindingName)
        {
            case "wbverbunden": _value = (bank != null && h != null && !wechseln) ? "true" : "false"; return true;
            case "wbwahlansicht": _value = (bank != null && (h == null || wechseln)) ? "true" : "false"; return true;
            case "wbabbrechen": _value = (h != null && wechseln) ? "true" : "false"; return true;
            case "wbfirma": _value = h == null ? "nicht verbunden" : h.Name; return true;
            case "wbinfo":
                if (h == null) _value = "";
                else if (h.IstRegierung) _value = "Regierung   |   Steuern/Monat " + FensterHilfe.B(bank.RegierungSteuern(h.Person));
                else if (h.IstPerson) _value = "Privatperson";
                else _value = "Bank: " + h.Firma.Bank + "   |   Besitzer: " + h.Firma.Besitzer;
                return true;
            case "wbguthaben": _value = h == null ? "-" : FensterHilfe.B(h.Konto); return true;
            case "wbschulden":
                if (h == null) { _value = ""; return true; }
                if (h.IstRegierung) { _value = "Staatsausgaben/Monat " + FensterHilfe.B(bank.RegierungAusgaben(h.Person)) + (string.IsNullOrEmpty(h.Person.RegTresor) ? " (läuft automatisch)" : ""); return true; }
                if (h.IstPerson)
                {
                    decimal rest = bank.Stand.Kredite.Where(k => !k.Erledigt && string.Equals(k.Nehmer, h.Name, StringComparison.OrdinalIgnoreCase)).Sum(k => k.Rest);
                    _value = rest <= 0 ? "Keine Kredite" : "Kredite: " + FensterHilfe.B(rest);
                    return true;
                }
                var u = h.Firma;
                if (Finanzen.InKonkurs(u)) { _value = Finanzen.KonkursText(u, WirtschaftMod.Heute); return true; }
                _value = Finanzen.Schulden(bank, u) <= 0 ? "Keine Kredite"
                    : "Kredite: " + FensterHilfe.B(Finanzen.Schulden(bank, u)) + "   |   Schuldenquote " + Finanzen.QuoteText(bank, u, 0m);
                return true;
            case "wbinhalt": _value = h == null ? "" : Geldtresor.InhaltText(bank, h); return true;
            case "wbwahltitel": _value = wechseln ? "Andere Firma, Person oder Regierung wählen" : "Mit wem verbinden? (Firma, Person, Regierung)"; return true;
            case "wbwahl":
                {
                    var l = Auswahl;
                    if (l.Count == 0) { _value = "Niemand in dieser Auswahl"; return true; }
                    int i = FensterHilfe.Blaettern(wahlIdx, 0, l.Count);
                    _value = l[i].Name + "  [" + l[i].Art + "]   (" + (i + 1) + " / " + l.Count + ")";
                    return true;
                }
            case "wbwahlinfo":
                {
                    var g = Gewaehlt;
                    if (g == null) { _value = ""; return true; }
                    string tresor = g.Tresor == position ? "   |   ist schon verbunden"
                        : (!string.IsNullOrEmpty(g.Tresor) ? "   |   hat schon einen Geldtresor (zieht hierher um)" : "");
                    if (g.IstRegierung) _value = "Regierung   |   Steuern/Monat " + FensterHilfe.B(bank.RegierungSteuern(g.Person)) + "   |   Guthaben " + FensterHilfe.B(g.Konto) + tresor;
                    else if (g.IstPerson) _value = "Privatperson   |   Guthaben " + FensterHilfe.B(g.Konto) + tresor;
                    else
                    {
                        int d = Geldtresor.Entfernung(g, pos3);
                        string ort = d == int.MaxValue ? "kein Firmentresor" : "Firmentresor " + d + " Blöcke entfernt";
                        _value = "Bank: " + g.Firma.Bank + "   |   " + ort + "   |   Guthaben " + FensterHilfe.B(g.Konto) + tresor;
                    }
                    return true;
                }
            case "wbmeldung": _value = bank == null ? "Erst eine Welt laden." : meldung; return true;
        }
        return base.GetBindingValue(ref _value, _bindingName);
    }
}

/// <summary>Finanzierung: Kredit für eine Firma aufnehmen, laufende Kredite ansehen und tilgen. Aus der Hauptbank geöffnet.
/// Die Regeln (Zins, höchste Laufzeit, Rate höchstens x % vom Nettoeinkommen) legt die Bank fest (wb kredit regeln).</summary>
public class XUiC_WbKredit : XUiController
{
    public static XUiC_WbKredit Instanz;
    public const string Gruppe = "wbKredit";
    static string firmaName = "";
    static string hauptbankPos = "";
    static EntityPlayerLocal spieler;

    XUiC_TextInput txtBetrag, txtLaufzeit, txtTilgung;
    int kreditIdx;
    string meldung = "";

    Bank B { get { return WirtschaftMod.Bank; } }
    Unternehmen Firma { get { return B == null ? null : B.FirmaOderNull(firmaName); } }
    List<Kredit> Offene { get { var u = Firma; return u == null ? new List<Kredit>() : Finanzen.Offene(B, u); } }

    Kredit Gewaehlt
    {
        get
        {
            var l = Offene;
            if (l.Count == 0) return null;
            return l[FensterHilfe.Blaettern(kreditIdx, 0, l.Count)];
        }
    }

    int MaxMonate { get { return B == null ? 36 : Math.Max(1, B.Stand.KreditMaxMonate); } }

    public static bool Oeffnen(EntityPlayerLocal sp, Unternehmen u, string pos)
    {
        if (Instanz == null || u == null) return false;
        firmaName = u.Name;
        hauptbankPos = pos ?? "";
        spieler = sp;
        return FensterHilfe.Oeffnen(sp, Gruppe);
    }

    public override void Init()
    {
        base.Init();
        Instanz = this;
        txtBetrag = FensterHilfe.Feld(this, "txtKBetrag", FeldGeaendert);
        txtLaufzeit = FensterHilfe.Feld(this, "txtKLaufzeit", FeldGeaendert);
        txtTilgung = FensterHilfe.Feld(this, "txtKTilgung", null);
        FensterHilfe.Knopf(this, "btnKBetragMinus", (s, m) => Plus(txtBetrag, -1000m, 1000000000m));
        FensterHilfe.Knopf(this, "btnKBetragPlus", (s, m) => Plus(txtBetrag, 1000m, 1000000000m));
        FensterHilfe.Knopf(this, "btnKLaufzeitMinus", (s, m) => Plus(txtLaufzeit, -1m, MaxMonate));
        FensterHilfe.Knopf(this, "btnKLaufzeitPlus", (s, m) => Plus(txtLaufzeit, 1m, MaxMonate));
        FensterHilfe.Knopf(this, "btnKTilgungMinus", (s, m) => Plus(txtTilgung, -1000m, 1000000000m));
        FensterHilfe.Knopf(this, "btnKTilgungPlus", (s, m) => Plus(txtTilgung, 1000m, 1000000000m));
        FensterHilfe.Knopf(this, "btnKAufnehmen", (s, m) => Aufnehmen());
        FensterHilfe.Knopf(this, "btnKKreditZurueck", (s, m) => { kreditIdx--; Melden(""); });
        FensterHilfe.Knopf(this, "btnKKreditVor", (s, m) => { kreditIdx++; Melden(""); });
        FensterHilfe.Knopf(this, "btnKTilgen", (s, m) => Tilgen(false));
        FensterHilfe.Knopf(this, "btnKAlles", (s, m) => Tilgen(true));
        FensterHilfe.Knopf(this, "btnKZurueck", (s, m) => ZurHauptbank());
        FensterHilfe.Knopf(this, "btnSchliessen", (s, m) => xui.playerUI.windowManager.CloseIfOpen(Gruppe));
    }

    public override void OnOpen()
    {
        base.OnOpen();
        meldung = "";
        kreditIdx = 0;
        FensterHilfe.Schreiben(txtBetrag, "10.000");
        FensterHilfe.Schreiben(txtLaufzeit, Math.Min(12, MaxMonate).ToString());
        FensterHilfe.Schreiben(txtTilgung, "");
        RefreshBindings(true);
    }

    public override void OnClose()
    {
        base.OnClose();
        WirtschaftMod.Speichern();
    }

    void FeldGeaendert(XUiController sender, string text, bool vomCode)
    {
        if (vomCode) return;
        RefreshBindings(true);
    }

    void Plus(XUiC_TextInput t, decimal schritt, decimal max)
    {
        FensterHilfe.Schritt(t, schritt, max);
        RefreshBindings(true);
    }

    void Melden(string text) { meldung = text; RefreshBindings(true); }

    /// <summary>Eingaben lesen. False, wenn etwas fehlt oder ungültig ist (Text in "fehler").</summary>
    bool Eingaben(out decimal betrag, out int monate, out string fehler)
    {
        decimal d;
        betrag = 0m; monate = 0; fehler = "";
        if (!Befehle.VersucheZahl(FensterHilfe.Lesen(txtBetrag), out betrag) || Math.Floor(betrag) < 1) { fehler = "Kreditbetrag eingeben (ganze Coins)."; return false; }
        betrag = Math.Floor(betrag);
        if (!Befehle.VersucheZahl(FensterHilfe.Lesen(txtLaufzeit), out d) || d < 1 || d != Math.Floor(d))
        { fehler = "Laufzeit in ganzen Monaten eingeben (1 bis " + MaxMonate + ")."; return false; }
        monate = (int)d;
        return true;
    }

    void Aufnehmen()
    {
        var u = Firma;
        if (B == null || u == null) { Melden("Die Firma gibt es nicht mehr."); return; }
        try
        {
            decimal betrag; int monate; string fehler;
            if (Finanzen.InKonkurs(u)) throw new WirtschaftsFehler(u.Name + " ist im Konkursverfahren. Keine neuen Kredite.");
            if (!Eingaben(out betrag, out monate, out fehler)) throw new WirtschaftsFehler(fehler);
            var k = B.KreditVergeben(u.Name, betrag, monate, "Finanzierung", WirtschaftMod.Heute);
            kreditIdx = Finanzen.Offene(B, u).IndexOf(k);
            WirtschaftMod.Speichern();
            Melden(FensterHilfe.B(k.Summe) + " gutgeschrieben. Rate " + FensterHilfe.B(k.Rate) + " pro Monat.");
        }
        catch (WirtschaftsFehler f) { Melden(f.Message); }
    }

    void Tilgen(bool alles)
    {
        var u = Firma;
        var k = Gewaehlt;
        if (B == null || u == null || k == null) { Melden("Kein laufender Kredit."); return; }
        try
        {
            decimal betrag = 0m;
            if (!alles)
            {
                if (!Befehle.VersucheZahl(FensterHilfe.Lesen(txtTilgung), out betrag) || betrag < 1)
                    throw new WirtschaftsFehler("Betrag für die Sondertilgung eingeben (oder 'Ganz zurückzahlen').");
            }
            decimal bezahlt = Finanzen.Tilgen(B, u, k, alles ? 0m : betrag, WirtschaftMod.Heute);
            FensterHilfe.Schreiben(txtTilgung, "");
            WirtschaftMod.Speichern();
            Melden(k.Erledigt
                ? "Kredit ganz zurückgezahlt (" + FensterHilfe.B(bezahlt) + ")."
                : FensterHilfe.B(bezahlt) + " getilgt. Rest " + FensterHilfe.B(k.Rest) + ", noch " + Finanzen.RestMonate(k) + " Monate.");
        }
        catch (WirtschaftsFehler f) { Melden(f.Message); }
    }

    void ZurHauptbank()
    {
        WirtschaftMod.Speichern();
        if (hauptbankPos.Length == 0 || !XUiC_WbHauptbank.Oeffnen(spieler, hauptbankPos))
            xui.playerUI.windowManager.CloseIfOpen(Gruppe);
    }

    public override bool GetBindingValue(ref string _value, string _bindingName)
    {
        var bank = B;
        var u = Firma;
        switch (_bindingName)
        {
            case "wbfirma": _value = u == null ? "-" : u.Name; return true;
            case "wbinfo":
                _value = (bank == null || u == null) ? "" : "Bank: " + u.Bank + "   |   Regeln: höchstens " + bank.Stand.KreditMaxMonate + " Monate, Rate höchstens "
                    + Befehle.Betrag(bank.Stand.KreditMaxQuote) + " % vom Nettoeinkommen";
                return true;
            case "wbguthaben": _value = u == null ? "-" : FensterHilfe.B(u.Konto); return true;
            case "wbwert": _value = u == null ? "-" : FensterHilfe.B(bank.Firmenwert(u)); return true;
            case "wbschulden":
                _value = u == null ? "-" : FensterHilfe.B(Finanzen.Schulden(bank, u))
                    + (Finanzen.Schulden(bank, u) > 0 ? "   (Raten " + FensterHilfe.B(Finanzen.RatenProMonat(bank, u)) + " / Monat)" : "");
                return true;
            case "wbquote": _value = u == null ? "-" : (Finanzen.InKonkurs(u) ? "KONKURS" : Finanzen.QuoteText(bank, u, 0m)); return true;
            case "wbkonkurs": _value = u == null ? "" : Finanzen.KonkursText(u, WirtschaftMod.Heute); return true;
            case "wbzins": _value = bank == null ? "" : Befehle.Betrag(bank.Stand.KreditZins) + " % (fest)"; return true;
            case "wbvorschau":
                {
                    decimal b = 0m; int n = 0; string f = "";
                    if (u == null || !Eingaben(out b, out n, out f)) { _value = u == null ? "" : f; return true; }
                    decimal rate = Bank.Annuitaet(b, bank.Stand.KreditZins, n);
                    decimal gesamt = rate * n;
                    _value = "Monatsrate " + FensterHilfe.B(rate) + "   |   Zinsen gesamt ca. " + FensterHilfe.B(Math.Max(0m, gesamt - b))
                           + "   |   zurück gesamt ca. " + FensterHilfe.B(gesamt);
                    return true;
                }
            case "wbvorschau2":
                {
                    decimal b = 0m; int n = 0; string f = "";
                    if (u == null || !Eingaben(out b, out n, out f)) { _value = ""; return true; }
                    if (n > bank.Stand.KreditMaxMonate) { _value = "Nicht möglich: höchstens " + bank.Stand.KreditMaxMonate + " Monate Laufzeit."; return true; }
                    decimal rate = Bank.Annuitaet(b, bank.Stand.KreditZins, n);
                    decimal frei = 0m;
                    try { frei = bank.FreieRate(u.Name); } catch (WirtschaftsFehler) { }
                    if (rate > frei)
                        _value = "Nicht möglich: freie Rate nur " + FensterHilfe.B(frei) + ". Höchstens " + FensterHilfe.B(Bank.MaxSumme(frei, bank.Stand.KreditZins, n)) + " über " + n + " Monate.";
                    else
                        _value = "Möglich. Schuldenquote danach: " + Finanzen.QuoteText(bank, u, b);
                    return true;
                }
            case "wbkreditzahl": _value = u == null ? "" : Offene.Count + " offen"; return true;
            case "wbkredit":
                {
                    var k = Gewaehlt;
                    if (k == null) { _value = "Keine laufenden Kredite"; return true; }
                    var l = Offene;
                    int i = FensterHilfe.Blaettern(kreditIdx, 0, l.Count);
                    _value = (i + 1) + " / " + l.Count + ":   Nr. " + k.Nr + "   " + FensterHilfe.B(k.Summe) + " zu " + Befehle.Betrag(k.Zins)
                           + " %   ->   Rest " + FensterHilfe.B(k.Rest);
                    return true;
                }
            case "wbkredit2":
                {
                    var k = Gewaehlt;
                    _value = k == null ? "" : "Rate " + FensterHilfe.B(k.Rate) + " / Monat   |   noch " + Finanzen.RestMonate(k)
                        + " Monate   |   aufgenommen Tag " + k.Beginn;
                    return true;
                }
            case "wbmeldung": _value = bank == null ? "Erst eine Welt laden." : meldung; return true;
        }
        return base.GetBindingValue(ref _value, _bindingName);
    }
}
