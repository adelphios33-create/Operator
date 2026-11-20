// Figuren-Mod – Fenster: Gespräch (E) und Einrichten (E gedrückt halten)
// Geprüft gegen 7 Days to Die V 1.4 (b8): XUiController, XUiC_SimpleButton (Text, IsVisible, OnPressed),
// GUIWindowManager.Open / CloseIfOpen. Controller werden über "FigGespraech, Figuren" -> XUiC_FigGespraech gefunden.
using System;
using System.Collections.Generic;
using System.Linq;
using Figuren.Kern;
using Figuren.Spiel;

static class FigHilfe
{
    public static XUiC_SimpleButton Knopf(XUiController fenster, string id, XUiEvent_OnPressEventHandler aktion)
    {
        var b = fenster.GetChildById(id) as XUiC_SimpleButton;
        if (b != null) b.OnPressed += aktion;
        else Log.Warning("[Figuren] Knopf '" + id + "' nicht gefunden.");
        return b;
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
            Log.Error("[Figuren] Fenster '" + gruppe + "' konnte nicht geöffnet werden: " + e.Message);
            return false;
        }
    }

    public static int Blaettern(int idx, int anzahl)
    {
        if (anzahl <= 0) return 0;
        return (idx % anzahl + anzahl) % anzahl;
    }
}

/// <summary>Gemeinsame Anzeige für beide KI-Fenster: Verlauf mit Blättern, Gedächtnis-Ansicht, Befehle /merke und /vergiss.</summary>
static class KiAnzeige
{
    const int MaxZeichen = 950;

    /// <summary>Eckige Klammern würden als Farbcodes gelesen, daher ersetzen.</summary>
    public static string Sicher(string t) { return (t ?? "").Replace('[', '(').Replace(']', ')'); }

    /// <summary>Nachrichten, die ins Fenster passen. "zurueck" = so viele neueste Nachrichten werden übersprungen (Blättern).</summary>
    public static string Verlauf(KiGespraech g, string name, int zurueck, out int angezeigt)
    {
        angezeigt = 0;
        int ende = Math.Max(0, g.Nachrichten.Count - Math.Max(0, zurueck));
        var teile = new List<string>();
        int zeichen = 0;
        for (int i = ende - 1; i >= 0; i--)
        {
            var n = g.Nachrichten[i];
            string zeile = (n.Wer == "assistant" ? name + ": " : "Du: ") + Sicher(n.Text);
            if (zeichen + zeile.Length > MaxZeichen && teile.Count > 0) break;
            if (zeile.Length > MaxZeichen) zeile = "..." + zeile.Substring(zeile.Length - MaxZeichen);
            teile.Insert(0, zeile);
            zeichen += zeile.Length;
            angezeigt++;
        }
        return string.Join("\n\n", teile.ToArray());
    }

    public static string Gedaechtnis(KiGespraech g, string name)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("GEDÄCHTNIS VON ").AppendLine(name.ToUpperInvariant());
        sb.AppendLine();
        sb.AppendLine("Feste Fakten (mit /merke eingetragen):");
        if (g.Fakten.Count == 0) sb.AppendLine("  keine");
        for (int i = 0; i < g.Fakten.Count; i++) sb.Append("  ").Append(i + 1).Append(". ").AppendLine(Sicher(g.Fakten[i]));
        sb.AppendLine();
        sb.AppendLine("Erinnerung (wird automatisch verdichtet):");
        sb.AppendLine(string.IsNullOrEmpty(g.Gedaechtnis) ? "  noch keine, entsteht ab etwa 30 Nachrichten" : Sicher(g.Gedaechtnis));
        sb.AppendLine();
        sb.Append("Wörtlich gespeichert: ").Append(g.Nachrichten.Count).AppendLine(" Nachrichten.");
        sb.Append("/merke Text  = fester Fakt     /vergiss Nummer  = Fakt löschen");
        string t = sb.ToString();
        return t.Length > 1600 ? t.Substring(0, 1600) + " ..." : t;
    }

    /// <summary>Befehle im Eingabefeld. true = es war ein Befehl (dann nicht an LM Studio schicken).</summary>
    public static bool Befehl(string rolle, string text, out string meldung)
    {
        meldung = "";
        string t = (text ?? "").Trim();
        if (!t.StartsWith("/")) return false;
        string kopf = t.Split(' ')[0].ToLowerInvariant();
        string rest = t.Length > kopf.Length ? t.Substring(kopf.Length).Trim() : "";
        if (kopf == "/merke" || kopf == "/merk") meldung = Ki.Merken(rolle, rest);
        else if (kopf == "/vergiss") meldung = Ki.FaktLoeschen(rolle, rest);
        else { meldung = "Unbekannter Befehl. Möglich: /merke Text, /vergiss Nummer"; return true; }
        FigurenMod.Speichern();
        return true;
    }

    /// <summary>Statuszeile beim Blättern bzw. in der Gedächtnis-Ansicht.</summary>
    public static string Hinweis(KiGespraech g, int zurueck, int angezeigt, bool gedView)
    {
        if (gedView) return "Gedächtnis-Ansicht. Nochmal auf Gedächtnis drücken, um zum Gespräch zurückzukehren.";
        if (zurueck <= 0) return "";
        int bis = Math.Max(0, g.Nachrichten.Count - zurueck);
        int von = Math.Max(1, bis - angezeigt + 1);
        return "Ältere Nachrichten " + von + " bis " + bis + " von " + g.Nachrichten.Count + ". Neuer = weiter nach unten.";
    }
}

/// <summary>Gespräch: Text des aktuellen Abschnitts und bis zu 4 Antworten als Knöpfe.</summary>
public class XUiC_FigGespraech : XUiController
{
    public static XUiC_FigGespraech Instanz;
    public const string Gruppe = "figGespraech";
    const int MaxWahl = 4;

    static int figurId;
    Rolle rolle;
    Abschnitt abschnitt;
    string ersatzText = "";
    XUiC_SimpleButton[] wahl = new XUiC_SimpleButton[MaxWahl];
    List<Antwort> angezeigt = new List<Antwort>();
    string textJetzt = "";

    public static bool Oeffnen(EntityPlayerLocal spieler, int id)
    {
        if (Instanz == null) return false;
        figurId = id;
        return FigHilfe.Oeffnen(spieler, Gruppe);
    }

    public override void Init()
    {
        base.Init();
        Instanz = this;
        for (int i = 0; i < MaxWahl; i++)
        {
            int n = i;
            wahl[i] = FigHilfe.Knopf(this, "btnWahl" + i, (s, m) => Gewaehlt(n));
        }
        FigHilfe.Knopf(this, "btnEinrichten", (s, m) => Einrichten());
    }

    /// <summary>Wechselt vom Gespräch zum Einrichten (Rolle ändern, Figur entfernen).</summary>
    void Einrichten()
    {
        int id = figurId;
        var sp = xui.playerUI.entityPlayer;
        xui.playerUI.windowManager.CloseIfOpen(Gruppe);
        FigurenMod.Spaeter(() => XUiC_FigEinrichten.Oeffnen(sp, id));
    }


    public override void OnOpen()
    {
        base.OnOpen();
        // Block-Lebensanzeige einmal beim Öffnen schließen (nicht jeden Frame: das störte die Controller-Steuerung)
        try { xui.playerUI.windowManager.CloseIfOpen("focusedBlockHealth"); } catch { }
        var f = FigurenMod.FigurHolen(figurId);
        rolle = f == null ? null : FigurenMod.RolleVon(f);
        abschnitt = null;
        ersatzText = "";
        if (f == null) ersatzText = "Die Figur ist nicht mehr da.";
        else if (rolle == null) ersatzText = "Diese Figur hat noch keine Rolle.\nE gedrückt halten und \"Einrichten\" wählen.";
        else
        {
            abschnitt = rolle.Holen(rolle.Start);
            if (abschnitt == null) ersatzText = "In der Rollendatei fehlt der Startabschnitt.";
        }
        Neu();
    }

    void Gewaehlt(int i)
    {
        if (i < 0 || i >= angezeigt.Count) return;
        var a = angezeigt[i];
        string ziel = (a.Ziel ?? "").Trim();
        if (rolle == null || ziel.Length == 0 || ziel.Equals("ende", StringComparison.OrdinalIgnoreCase))
        {
            xui.playerUI.windowManager.CloseIfOpen(Gruppe);
            return;
        }
        var neu = rolle.Holen(ziel);
        if (neu == null) { ersatzText = "(Abschnitt [" + ziel + "] fehlt in " + rolle.Datei + ".txt)"; abschnitt = null; }
        else { abschnitt = neu; ersatzText = ""; }
        Neu();
    }

    void Neu()
    {
        angezeigt = abschnitt != null && abschnitt.Antworten.Count > 0
            ? abschnitt.Antworten.Take(MaxWahl).ToList()
            : new List<Antwort> { new Antwort { Text = "Tschüss", Ziel = "ende" } };
        textJetzt = abschnitt != null ? Rollen.Variante(abschnitt.Text, rolle) : ersatzText;
        if (abschnitt != null) Stimmen.Sprechen(FigurenMod.FigurHolen(figurId), textJetzt);
        for (int i = 0; i < MaxWahl; i++)
        {
            if (wahl[i] == null) continue;
            if (i < angezeigt.Count) { wahl[i].Text = Rollen.Variante(angezeigt[i].Text, rolle); wahl[i].IsVisible = true; }
            else wahl[i].IsVisible = false;
        }
        RefreshBindings(true);
    }

    public override bool GetBindingValue(ref string _value, string _bindingName)
    {
        switch (_bindingName)
        {
            case "figname": _value = rolle == null ? "FIGUR" : rolle.Name.ToUpperInvariant(); return true;
            case "figtitel": _value = rolle == null ? "" : rolle.Titel; return true;
            case "figtext": _value = textJetzt; return true;
        }
        return base.GetBindingValue(ref _value, _bindingName);
    }
}

/// <summary>Einrichten: Rolle aus der Liste wählen, Rollen neu laden, Figur entfernen.</summary>
public class XUiC_FigEinrichten : XUiController
{
    public static XUiC_FigEinrichten Instanz;
    public const string Gruppe = "figEinrichten";
    const int Zeilen = 6;

    static int figurId;
    static string stadt = "";
    static string branche = "";
    static int groesse;
    const int BrancheFelder = 16;
    XUiC_TextInput txtFirma, txtName;
    XUiC_SimpleButton[] brKnopf = new XUiC_SimpleButton[BrancheFelder];
    XUiC_SimpleButton[] grKnopf = new XUiC_SimpleButton[3];
    XUiC_SimpleButton knopfNeuF;
    int seite;
    int gewaehlt = -1;
    string meldung = "";
    XUiC_SimpleButton[] zeile = new XUiC_SimpleButton[Zeilen];
    XUiC_SimpleButton knopfNeu;

    public static bool Oeffnen(EntityPlayerLocal spieler, int id)
    {
        if (Instanz == null) return false;
        figurId = id;
        return FigHilfe.Oeffnen(spieler, Gruppe);
    }

    public override void Init()
    {
        base.Init();
        Instanz = this;
        for (int i = 0; i < Zeilen; i++)
        {
            int n = i;
            zeile[i] = FigHilfe.Knopf(this, "btnL" + i, (s, m) => Waehlen(n));
        }
        FigHilfe.Knopf(this, "btnZurueck", (s, m) => { seite--; Neu(); });
        FigHilfe.Knopf(this, "btnVor", (s, m) => { seite++; Neu(); });
        FigHilfe.Knopf(this, "btnStadtZurueck", (s, m) => StadtWechseln(-1));
        FigHilfe.Knopf(this, "btnStadtVor", (s, m) => StadtWechseln(1));
        FigHilfe.Knopf(this, "btnBrancheZurueck", (s, m) => BrancheWechseln(-1));
        FigHilfe.Knopf(this, "btnBrancheVor", (s, m) => BrancheWechseln(1));
        // Unternehmer: Firmenname (Textfeld), Branche (Auswahlfeld), Firmengröße (3 Stufen)
        txtFirma = GetChildById("txtFirma") as XUiC_TextInput;
        txtName = GetChildById("txtFigName") as XUiC_TextInput;
        if (txtName != null) txtName.OnSubmitHandler += (s, t) => NameAendern();
        FigHilfe.Knopf(this, "btnNameAendern", (s, m) => NameAendern());
        for (int i = 0; i < BrancheFelder; i++)
        {
            int n = i;
            brKnopf[i] = FigHilfe.Knopf(this, "btnBr" + i, (s, m) => BrancheWaehlen(n));
        }
        for (int i = 0; i < 3; i++)
        {
            int n = i;
            grKnopf[i] = FigHilfe.Knopf(this, "btnGr" + i, (s, m) => { groesse = n; Neu(); });
        }
        FigHilfe.Knopf(this, "btnAussZurueck", (s, m) => AussehenBlaettern(-1));
        FigHilfe.Knopf(this, "btnAussVor", (s, m) => AussehenBlaettern(1));
        FigHilfe.Knopf(this, "btnAussZufall", (s, m) => AussehenZufall());
        FigHilfe.Knopf(this, "btnWaffeZurueck", (s, m) => WaffeWechseln(-1));
        FigHilfe.Knopf(this, "btnWaffeVor", (s, m) => WaffeWechseln(1));
        FigHilfe.Knopf(this, "btnBewegungZurueck", (s, m) => BewegungWechseln(-1));
        FigHilfe.Knopf(this, "btnBewegungVor", (s, m) => BewegungWechseln(1));
        // Haltung nicht im Fenster wählbar: Söldner werden nur per Befehl feindlich (nf1 / nf2)
        FigHilfe.Knopf(this, "btnUebernehmen", (s, m) => Uebernehmen());
        knopfNeu = FigHilfe.Knopf(this, "btnNeuLaden", (s, m) => NeuLaden());
        FigHilfe.Knopf(this, "btnEntfernen", (s, m) => Entfernen());
        // zweite Reihe dieser drei Knöpfe, die im Firmen-Modus (Unternehmer) weiter unten sitzt
        FigHilfe.Knopf(this, "btnUebernehmenF", (s, m) => Uebernehmen());
        knopfNeuF = FigHilfe.Knopf(this, "btnNeuLadenF", (s, m) => NeuLaden());
        FigHilfe.Knopf(this, "btnEntfernenF", (s, m) => Entfernen());
        FigHilfe.Knopf(this, "btnSchliessen", (s, m) => xui.playerUI.windowManager.CloseIfOpen(Gruppe));
    }


    public override void OnOpen()
    {
        base.OnOpen();
        // Block-Lebensanzeige einmal beim Öffnen schließen (nicht jeden Frame: das störte die Controller-Steuerung)
        try { xui.playerUI.windowManager.CloseIfOpen("focusedBlockHealth"); } catch { }
        meldung = "";
        gewaehlt = -1;
        // Vorauswahl: aktuelle Rolle der Figur und ihre Stadt
        var f = FigurenMod.FigurHolen(figurId);
        var p = f == null ? null : FigurenMod.PlatzVon(f);
        if (p != null)
        {
            var l = Rollen.Auswahl();
            string ziel = p.Vorlage.Length > 0 ? p.Vorlage : p.Rolle;
            gewaehlt = l.FindIndex(r => string.Equals(r.Name, ziel, StringComparison.OrdinalIgnoreCase));
            if (gewaehlt >= 0) seite = gewaehlt / Zeilen;
            if (p.Stadt.Length > 0) stadt = p.Stadt;
            if (p.Branche.Length > 0) branche = p.Branche;
            groesse = Rollen.GroesseIndex(p.Groesse);
        }
        if (txtFirma != null) txtFirma.Text = p != null ? p.Firma : "";
        Neu();
    }

    Rolle Gewaehlte
    {
        get
        {
            var l = Rollen.Auswahl();
            return gewaehlt >= 0 && gewaehlt < l.Count ? l[gewaehlt] : null;
        }
    }

    void Waehlen(int z)
    {
        int idx = seite * Zeilen + z;
        if (idx < 0 || idx >= Rollen.Auswahl().Count) return;
        gewaehlt = idx;
        var r = Gewaehlte;
        if (r != null && r.Vorlage)
        {
            var st = Rollen.StaedteFuer(r);
            if (st.Count > 0 && !st.Any(w => string.Equals(w, stadt, StringComparison.OrdinalIgnoreCase))) stadt = st[0];
            if (r.Branchen.Count > 0 && !r.Branchen.Any(w => string.Equals(w, branche, StringComparison.OrdinalIgnoreCase))) branche = r.Branchen[0];
            meldung = r.FirmaFelder ? "Firmenname eintragen, Branche und Größe wählen, dann Übernehmen drücken."
                : r.Branchen.Count > 0 ? "Stadt und Branche wählen, dann Übernehmen drücken." : "Stadt wählen und Übernehmen drücken.";
        }
        else meldung = "Übernehmen drücken.";
        Neu();
    }

    void StadtWechseln(int d)
    {
        var st = Rollen.StaedteFuer(Gewaehlte);
        if (st.Count == 0) return;
        int idx = st.FindIndex(w => string.Equals(w, stadt, StringComparison.OrdinalIgnoreCase));
        stadt = st[FigHilfe.Blaettern(idx < 0 ? 0 : idx + d, st.Count)];
        Neu();
    }

    void BrancheWechseln(int d)
    {
        var r = Gewaehlte;
        if (r == null || r.Branchen.Count == 0) return;
        int idx = r.Branchen.FindIndex(w => string.Equals(w, branche, StringComparison.OrdinalIgnoreCase));
        branche = r.Branchen[FigHilfe.Blaettern(idx < 0 ? 0 : idx + d, r.Branchen.Count)];
        Neu();
    }

    void BrancheWaehlen(int n)
    {
        var r = Gewaehlte;
        if (r == null || n < 0 || n >= r.Branchen.Count) return;
        branche = r.Branchen[n];
        Neu();
    }

    void Uebernehmen()
    {
        var r = Gewaehlte;
        if (r == null) { meldung = "Erst links eine Rolle wählen."; Neu(); return; }
        var f = FigurenMod.FigurHolen(figurId);
        if (f == null) { meldung = "Die Figur ist nicht mehr da."; Neu(); return; }
        try
        {
            string firma = r.FirmaFelder && txtFirma != null ? (txtFirma.Text ?? "").Trim() : "";
            figurId = FigurenMod.RolleSetzen(f, r, r.Vorlage ? stadt : "", r.Vorlage && r.Branchen.Count > 0 ? branche : "", firma, r.FirmaFelder ? Rollen.GroessenKey[groesse] : "");
            var pl = FigurenMod.PlatzVon(FigurenMod.FigurHolen(figurId));
            string name = pl != null && pl.Rolle.Length > 0 ? pl.Rolle : r.Name;
            meldung = r.Vorlage ? name + " ist jetzt " + r.Name + (r.FirmaFelder ? " von " + (firma.Length > 0 ? firma : "(Firma ohne Namen)") + " (" + Rollen.BrancheText(r, branche) + ", " + Rollen.GroessenAnzeige[groesse] + ")"
                : r.Branchen.Count > 0 ? " (" + branche + ")" : "") + " in " + stadt + "." : "Rolle " + name + " gesetzt.";
        }
        catch (Exception e) { meldung = "Fehler: " + e.Message; }
        Neu();
    }

    /// <summary>Eigenen Namen für die Figur setzen (Textfeld "Name").</summary>
    void NameAendern()
    {
        var f = FigurenMod.FigurHolen(figurId);
        var p = f == null ? null : FigurenMod.PlatzVon(f);
        if (f == null) { meldung = "Die Figur ist nicht mehr da."; Neu(); return; }
        try
        {
            meldung = FigurenMod.NameSetzen(p, txtName == null ? "" : txtName.Text);
            if (txtName != null) txtName.Text = "";
        }
        catch (Exception e) { meldung = e.Message; }
        Neu();
    }

    /// <summary>Grundrolle: nur ein neuer Zufallsname (Rolle, Stadt und Aussehen bleiben). Feste Figur: Rollendateien neu laden.</summary>
    void NeuLaden()
    {
        var f = FigurenMod.FigurHolen(figurId);
        var p = f == null ? null : FigurenMod.PlatzVon(f);
        if (p == null || p.Vorlage.Length == 0) { meldung = Rollen.Laden(); gewaehlt = -1; Neu(); return; }
        if (Gewaehlte == null || !string.Equals(Gewaehlte.Name, p.Vorlage, StringComparison.OrdinalIgnoreCase))
        { meldung = "Der Name wird nur für die gesetzte Rolle neu gewürfelt. Für eine andere Rolle erst Übernehmen drücken."; Neu(); return; }
        var v = Rollen.Liste.FirstOrDefault(w => w.Vorlage && string.Equals(w.Name, p.Vorlage, StringComparison.OrdinalIgnoreCase));
        if (v == null) { meldung = "Grundrolle " + p.Vorlage + " nicht gefunden."; Neu(); return; }
        string alt = p.Rolle;
        var r = Rollen.Instanz(v, p.Stadt, null, p.Aussehen, p.Branche, p.Firma, p.Groesse);
        if (alt.Length > 0 && !string.Equals(alt, r.Name, StringComparison.OrdinalIgnoreCase)) Rollen.Instanzen.Remove(alt);
        p.Rolle = r.Name;
        FigurenMod.Speichern();
        meldung = alt + " heißt jetzt " + r.Name + " (" + v.Name + " in " + p.Stadt + ").";
        Neu();
    }

    /// <summary>Aussehen der Figur sofort wechseln: durch alle Modelle blättern.</summary>
    void AussehenBlaettern(int d)
    {
        var f = FigurenMod.FigurHolen(figurId);
        if (f == null) { meldung = "Die Figur ist nicht mehr da."; Neu(); return; }
        var p = FigurenMod.PlatzVon(f);
        string akt = p != null ? p.Aussehen : "";
        var alle = Rollen.Aussehen;
        int idx = Array.FindIndex(alle, a => string.Equals(a, akt, StringComparison.OrdinalIgnoreCase));
        AussehenAnwenden(f, alle[FigHilfe.Blaettern(idx < 0 ? 0 : idx + d, alle.Length)]);
    }

    /// <summary>Zufälliges Aussehen aus der Liste der Rolle (Zeile "Aussehen:" in der Rollendatei).</summary>
    void AussehenZufall()
    {
        var f = FigurenMod.FigurHolen(figurId);
        if (f == null) { meldung = "Die Figur ist nicht mehr da."; Neu(); return; }
        var p = FigurenMod.PlatzVon(f);
        Rolle v = null;
        if (p != null && p.Vorlage.Length > 0) v = Rollen.Liste.FirstOrDefault(x => x.Vorlage && string.Equals(x.Name, p.Vorlage, StringComparison.OrdinalIgnoreCase));
        else if (Gewaehlte != null && Gewaehlte.Vorlage) v = Gewaehlte;
        AussehenAnwenden(f, Rollen.ZufallsAussehen(v, p != null ? p.Branche : "", p != null ? p.Aussehen : ""));
    }

    void AussehenAnwenden(EntityFigur f, string aussehen)
    {
        try
        {
            var p = FigurenMod.PlatzVon(f);
            string altName = p != null ? p.Rolle : "";
            figurId = FigurenMod.AussehenSetzen(f, aussehen);
            var pn = FigurenMod.PlatzVon(FigurenMod.FigurHolen(figurId));
            if (pn != null && !string.Equals(pn.Aussehen, aussehen, StringComparison.OrdinalIgnoreCase))
            { meldung = "Modell " + aussehen + " lässt sich nicht laden (Details im Log), Ersatz: " + pn.Aussehen + "."; Neu(); return; }
            meldung = "Aussehen: " + aussehen + (Rollen.IstFrau(aussehen) ? " (Frau)" : "")
                + (pn != null && altName.Length > 0 && !string.Equals(pn.Rolle, altName, StringComparison.OrdinalIgnoreCase) ? ". Neuer Name: " + pn.Rolle + "." : ".");
        }
        catch (Exception e) { meldung = "Fehler: " + e.Message; }
        Neu();
    }

    /// <summary>Typ 3: Waffe sofort wechseln (keine, Pistole, MP, AK, Schrotflinte).</summary>
    void WaffeWechseln(int d)
    {
        var f = FigurenMod.FigurHolen(figurId);
        var p = f == null ? null : FigurenMod.PlatzVon(f);
        if (p == null) { meldung = "Die Figur ist nicht mehr da."; Neu(); return; }
        var arten = Waffen.Arten;
        int idx = Array.FindIndex(arten, a => string.Equals(a.Name, p.Waffe, StringComparison.OrdinalIgnoreCase));
        meldung = Waffen.WaffeSetzen(f, arten[FigHilfe.Blaettern(idx < 0 ? 0 : idx + d, arten.Length)].Name);
        Neu();
    }

    /// <summary>Typ 4: steht fest / läuft im Umkreis umher.</summary>
    void BewegungWechseln(int d)
    {
        var f = FigurenMod.FigurHolen(figurId);
        var p = f == null ? null : FigurenMod.PlatzVon(f);
        if (p == null) { meldung = "Die Figur ist nicht mehr da."; Neu(); return; }
        var st = Umher.Stufen;
        int idx = Array.IndexOf(st, p.Umkreis);
        meldung = Umher.Setzen(f, p, st[FigHilfe.Blaettern(idx < 0 ? 0 : idx + d, st.Length)]);
        Neu();
    }

    /// <summary>Typ 3: Wache (auf Zombies, alle) / Feindlich (auch auf Spieler, nur Söldner).</summary>
    void HaltungWechseln(int d)
    {
        var f = FigurenMod.FigurHolen(figurId);
        var p = f == null ? null : FigurenMod.PlatzVon(f);
        if (p == null) { meldung = "Die Figur ist nicht mehr da."; Neu(); return; }
        var h = Waffen.Haltungen;
        int idx = Array.FindIndex(h, a => string.Equals(a, p.Haltung, StringComparison.OrdinalIgnoreCase));
        meldung = Waffen.HaltungSetzen(f, h[FigHilfe.Blaettern(idx < 0 ? 0 : idx + d, h.Length)]);
        Neu();
    }

    void Entfernen()
    {
        FigurenMod.Entfernen(figurId);
        xui.playerUI.windowManager.CloseIfOpen(Gruppe);
    }

    void Neu()
    {
        var l = Rollen.Auswahl();
        int seiten = Math.Max(1, (l.Count + Zeilen - 1) / Zeilen);
        seite = FigHilfe.Blaettern(seite, seiten);
        for (int z = 0; z < Zeilen; z++)
        {
            var b = zeile[z];
            if (b == null) continue;
            int idx = seite * Zeilen + z;
            if (idx < l.Count)
            {
                var r = l[idx];
                b.Text = (idx == gewaehlt ? "> " : "") + r.Name + "  (" + r.Art + ")";
                b.IsVisible = true;
            }
            else b.IsVisible = false;
        }
        if (knopfNeu != null)
        {
            var f = FigurenMod.FigurHolen(figurId);
            var p = f == null ? null : FigurenMod.PlatzVon(f);
            bool vorlage = p != null && p.Vorlage.Length > 0;
            knopfNeu.Text = "Neuer Name";
            // Neuer Name nur bei Grundrolle/feste Figur und nur, wenn links genau diese Rolle gewählt ist
            knopfNeu.IsVisible = vorlage && Gewaehlte != null && string.Equals(Gewaehlte.Name, p.Vorlage, StringComparison.OrdinalIgnoreCase);
            if (knopfNeuF != null) { knopfNeuF.Text = knopfNeu.Text; knopfNeuF.IsVisible = knopfNeu.IsVisible; }
        }
        var gr = Gewaehlte;
        for (int i = 0; i < BrancheFelder; i++)
        {
            var b = brKnopf[i];
            if (b == null) continue;
            if (gr != null && gr.FirmaFelder && i < gr.Branchen.Count)
            {
                b.Text = (string.Equals(gr.Branchen[i], branche, StringComparison.OrdinalIgnoreCase) ? "> " : "") + gr.Branchen[i];
                b.IsVisible = true;
            }
            else b.IsVisible = false;
        }
        for (int i = 0; i < 3; i++)
            if (grKnopf[i] != null) grKnopf[i].Text = (i == groesse ? "> " : "") + Rollen.GroessenAnzeige[i];
        RefreshBindings(true);
    }

    public override bool GetBindingValue(ref string _value, string _bindingName)
    {
        var f = FigurenMod.FigurHolen(figurId);
        var p = f == null ? null : FigurenMod.PlatzVon(f);
        var r = f == null ? null : FigurenMod.RolleVon(f);
        var g = Gewaehlte;
        switch (_bindingName)
        {
            case "figaktuell":
                _value = f == null ? "Figur nicht gefunden"
                    : "Jetzt: " + (r != null ? r.Name + (r.Titel.Length > 0 ? " - " + r.Titel : "") : (p != null && p.Rolle.Length > 0 ? p.Rolle + " (Datei fehlt)" : "keine Rolle"));
                return true;
            case "fignamean":
                _value = p != null && p.Vorlage.Length > 0 ? "true" : "false";
                return true;
            case "figseite":
                _value = "Seite " + (seite + 1) + " / " + Math.Max(1, (Rollen.Auswahl().Count + Zeilen - 1) / Zeilen);
                return true;
            case "figstadt":
                _value = g == null ? "-" : (g.Vorlage ? stadt : "nicht wählbar");
                return true;
            case "figbranche":
                _value = g != null && g.Vorlage && g.Branchen.Count > 0 ? (g.FirmaFelder ? Rollen.BrancheText(g, branche) : branche) : "";
                return true;
            case "figbranchean":
                _value = g != null && g.Vorlage && g.Branchen.Count > 0 && !g.FirmaFelder ? "true" : "false";
                return true;
            case "figfirmaan":
                _value = g != null && g.Vorlage && g.FirmaFelder ? "true" : "false";
                return true;
            case "figfirmaaus":
                _value = g != null && g.Vorlage && g.FirmaFelder ? "false" : "true";
                return true;
            case "figgroessetext":
                _value = g != null && g.FirmaFelder ? Rollen.GroessenText[groesse] : "";
                return true;
            case "figstadtan":
                _value = g != null && g.Vorlage ? "true" : "false";
                return true;
            case "figinfo":
                if (g == null) { _value = "Links wählen. Grundrollen (KI, Zufallsname, Stadt), feste Figuren (Klick-Dialog, Zufallsname, Stadt), Hauptrollen und Nebenrollen (Einzelfiguren mit eigener Geschichte)."; return true; }
                _value = "Gewählt: " + g.Name + " (" + g.Art + ")" + (!g.Vorlage && g.Titel.Length > 0 ? " - " + g.Titel : "")
                         + "\nDatei: Rollen/" + g.Datei + ".txt"
                         + (g.Vorlage ? "\nZufallsname aus der Liste; \"Neuer Name\" würfelt neu." + (g.IstKi ? " Ist die Figur schon diese Grundrolle, behält sie Namen und Gedächtnis." : " Kurzer Klick-Dialog ohne KI.") : "\nAussehen: " + g.Aussehen)
                         + (g.Immobilien ? "\nKennt die Immobilien-Schilder seiner Stadt." : "");
                return true;
            case "figaussehen":
                {
                    string a = p != null && p.Aussehen.Length > 0 ? p.Aussehen : "-";
                    int i = Array.FindIndex(Rollen.Aussehen, x => string.Equals(x, a, StringComparison.OrdinalIgnoreCase));
                    _value = a + (Rollen.IstFrau(a) ? " (F)" : "") + (i >= 0 ? "  " + (i + 1) + "/" + Rollen.Aussehen.Length : "");
                    return true;
                }
            case "figwaffe":
                {
                    if (p == null) { _value = "-"; return true; }
                    _value = Waffen.ArtVon(p.Waffe).Name;
                    return true;
                }
            case "figbewegung": _value = p != null ? Umher.Text(p.Umkreis) : "-"; return true;
            case "fighaltung":
                {
                    string h = p != null ? Waffen.Wirksam(p) : "-";
                    _value = p == null ? "-" : !Waffen.IstSoeldner(p) ? "Freundlich (nur Zombies)" : h == "Feindlich" ? "Feindlich (Befehl nf2 = aus)" : "Freundlich (nf1 = feindlich)";
                    return true;
                }
            case "figmeldung": _value = meldung; return true;
        }
        return base.GetBindingValue(ref _value, _bindingName);
    }
}

public class XUiC_FigKi : XUiController
{
    public static XUiC_FigKi Instanz;
    public const string Gruppe = "figKi";

    static int figurId;
    Rolle rolle;
    string status = "";
    XUiC_TextInput eingabe;
    int zurueck, angezeigt;
    bool gedView;

    public static bool Oeffnen(EntityPlayerLocal spieler, int id)
    {
        if (Instanz == null) return false;
        figurId = id;
        return FigHilfe.Oeffnen(spieler, Gruppe);
    }

    public override void Init()
    {
        base.Init();
        Instanz = this;
        eingabe = GetChildById("txtEingabe") as XUiC_TextInput;
        if (eingabe != null) eingabe.OnSubmitHandler += (s, text) => Senden();
        FigHilfe.Knopf(this, "btnSenden", (s, m) => Senden());
        FigHilfe.Knopf(this, "btnNeu", (s, m) => Neubeginn());
        FigHilfe.Knopf(this, "btnEinrichten", (s, m) => Einrichten());
        FigHilfe.Knopf(this, "btnSchliessen", (s, m) => xui.playerUI.windowManager.CloseIfOpen(Gruppe));
        FigHilfe.Knopf(this, "btnAelter", (s, m) => Blaettern(true));
        FigHilfe.Knopf(this, "btnNeuer", (s, m) => Blaettern(false));
        FigHilfe.Knopf(this, "btnGedaechtnis", (s, m) => { gedView = !gedView; status = ""; RefreshBindings(true); });
        FigHilfe.Knopf(this, "btnKredit", (s, m) => KreditAbschliessen());
        FigHilfe.Knopf(this, "btnKreditNein", (s, m) => { if (rolle != null) KreditAngebote.Verwerfen(rolle.Name); status = "Kreditangebot verworfen."; RefreshBindings(true); });
    }

    void KreditAbschliessen()
    {
        if (rolle == null) return;
        string erg = KreditAngebote.Abschliessen(rolle.Name);
        status = erg.StartsWith("OK") ? erg.Substring(3).Trim() : "Nicht möglich: " + erg.Replace("NEIN:", "").Trim();
        RefreshBindings(true);
    }

    KreditAngebote.Angebot Angebot { get { return rolle == null ? null : KreditAngebote.Offen(rolle.Name); } }

    void Blaettern(bool aelter)
    {
        if (rolle == null) return;
        gedView = false;
        int anzahl = Ki.Gespraech(rolle.Name).Nachrichten.Count;
        if (aelter) { if (zurueck + angezeigt < anzahl) zurueck += Math.Max(1, angezeigt); }
        else zurueck = Math.Max(0, zurueck - Math.Max(1, angezeigt));
        status = "";
        RefreshBindings(true);
    }


    public override void OnOpen()
    {
        base.OnOpen();
        // Block-Lebensanzeige einmal beim Öffnen schließen (nicht jeden Frame: das störte die Controller-Steuerung)
        try { xui.playerUI.windowManager.CloseIfOpen("focusedBlockHealth"); } catch { }
        zurueck = 0;
        gedView = false;
        var f = FigurenMod.FigurHolen(figurId);
        rolle = f == null ? null : FigurenMod.RolleVon(f);
        status = rolle == null ? "Diese Figur hat keine KI-Rolle." : (Ki.Wartet(rolle.Name) ? rolle.Name + " denkt nach ..." : "");
        RefreshBindings(true);
    }

    void Senden()
    {
        if (rolle == null || eingabe == null) return;
        string text = (eingabe.Text ?? "").Trim();
        if (text.Length == 0) return;
        string meldung;
        if (KiAnzeige.Befehl(rolle.Name, text, out meldung)) { eingabe.Text = ""; status = meldung; gedView = true; RefreshBindings(true); return; }
        if (Ki.Wartet(rolle.Name)) { status = rolle.Name + " antwortet noch ..."; RefreshBindings(true); return; }
        eingabe.Text = "";
        zurueck = 0;
        gedView = false;
        var sp = xui.playerUI.entityPlayer;
        string spieler = sp != null ? sp.EntityName : "";
        string name = rolle.Name;
        status = name + " denkt nach ...";
        Ki.Fragen(rolle, spieler, text, fehler =>
        {
            if (Instanz == null) return;
            Instanz.status = fehler ?? "";
            Instanz.zurueck = 0;
            Instanz.RefreshBindings(true);
        });
        RefreshBindings(true);
    }

    void Neubeginn()
    {
        if (rolle == null) return;
        if (Ki.Wartet(rolle.Name)) { status = "Bitte auf die Antwort warten."; RefreshBindings(true); return; }
        Ki.Vergessen(rolle.Name);
        KreditAngebote.Verwerfen(rolle.Name);
        FigurenMod.Speichern();
        zurueck = 0;
        status = rolle.Name + " hat das Gespräch und die Erinnerung vergessen. Feste Fakten bleiben.";
        RefreshBindings(true);
    }

    void Einrichten()
    {
        int id = figurId;
        var sp = xui.playerUI.entityPlayer;
        xui.playerUI.windowManager.CloseIfOpen(Gruppe);
        FigurenMod.Spaeter(() => XUiC_FigEinrichten.Oeffnen(sp, id));
    }

    /// <summary>Die letzten Nachrichten, so viele wie ins Fenster passen (neueste unten), oder das Gedächtnis.</summary>
    string VerlaufText()
    {
        if (rolle == null) return "";
        var g = Ki.Gespraech(rolle.Name);
        if (gedView) return KiAnzeige.Gedaechtnis(g, rolle.Name);
        if (g.Nachrichten.Count == 0) return "(Noch kein Gespräch. Schreib unten etwas und drück Enter oder Senden.)\n\nTipp: Mit /merke Text speichert sich " + rolle.Name + " einen festen Fakt, zum Beispiel: /merke Wir haben zusammen die Tankstelle überfallen.";
        return KiAnzeige.Verlauf(g, rolle.Name, zurueck, out angezeigt);
    }

    public override bool GetBindingValue(ref string _value, string _bindingName)
    {
        switch (_bindingName)
        {
            case "kiname": _value = rolle == null ? "KI-FIGUR" : rolle.Name.ToUpperInvariant(); return true;
            case "kititel": _value = rolle == null ? "" : rolle.Titel + "   (KI-Gespräch über LM Studio)"; return true;
            case "kiverlauf": _value = VerlaufText(); return true;
            case "kistatus":
                {
                    var an = Angebot;
                    if (an != null && status.Length == 0) { _value = KiAnzeige.Sicher(KreditAngebote.Text(an)); return true; }
                    _value = status.Length > 0 || rolle == null ? status : KiAnzeige.Hinweis(Ki.Gespraech(rolle.Name), zurueck, angezeigt, gedView);
                    return true;
                }
            case "kikreditan":
                { var an = Angebot; _value = an != null && an.Ok ? "true" : "false"; return true; }
            case "kikreditneinan":
                { _value = Angebot != null ? "true" : "false"; return true; }
        }
        return base.GetBindingValue(ref _value, _bindingName);
    }
}

/// <summary>Leitungsfenster (Taste, Standard F7): Operator/Spielleiter und Gespräche mit KI-Figuren aus der Ferne.</summary>
public class XUiC_FigLeitung : XUiController
{
    public static XUiC_FigLeitung Instanz;
    public const string Gruppe = "figLeitung";

    static int wahl;
    string status = "";
    XUiC_TextInput eingabe;
    int zurueck, angezeigt;
    bool gedView;
    // Game Operator: Ansicht "Ereignisse" (Filter, gewähltes Ereignis) und Infotext (Hilfe, /info)
    bool evView;
    static int evFilter, evWahl;
    int evId, evAnzahl;
    string infoText = "";
    int fertigFrageId;
    float fertigFrageZeit;

    public static bool Oeffnen(EntityPlayerLocal spieler)
    {
        if (Instanz == null) return false;
        return FigHilfe.Oeffnen(spieler, Gruppe);
    }

    /// <summary>Operator zuerst, dann die KI-Rollen mit "Leitung: ja".</summary>
    static List<Rolle> Eintraege()
    {
        var l = new List<Rolle>();
        l.Add(Ki.Erzaehler);
        l.AddRange(Rollen.Liste.Where(r => r.IstKi && r.InLeitung));
        return l;
    }

    Rolle Aktuell()
    {
        var l = Eintraege();
        wahl = FigHilfe.Blaettern(wahl, l.Count);
        return l[wahl];
    }

    public override void Init()
    {
        base.Init();
        Instanz = this;
        eingabe = GetChildById("txtEingabe") as XUiC_TextInput;
        if (eingabe != null) eingabe.OnSubmitHandler += (s, text) => Senden();
        FigHilfe.Knopf(this, "btnSenden", (s, m) => Senden());
        FigHilfe.Knopf(this, "btnNeu", (s, m) => Neubeginn());
        FigHilfe.Knopf(this, "btnZurueck", (s, m) => Wechseln(-1));
        FigHilfe.Knopf(this, "btnVor", (s, m) => Wechseln(1));
        FigHilfe.Knopf(this, "btnSchliessen", (s, m) => xui.playerUI.windowManager.CloseIfOpen(Gruppe));
        FigHilfe.Knopf(this, "btnAelter", (s, m) => Blaettern(true));
        FigHilfe.Knopf(this, "btnNeuer", (s, m) => Blaettern(false));
        FigHilfe.Knopf(this, "btnGedaechtnis", (s, m) => { gedView = !gedView; evView = false; infoText = ""; status = ""; RefreshBindings(true); });
        // Game Operator (nur sichtbar, wenn der Operator gewählt ist)
        FigHilfe.Knopf(this, "btnOpSzenarien", (s, m) => OperatorStarten("szenarien", EingabeAlsThema(), 0));
        FigHilfe.Knopf(this, "btnOpWelt", (s, m) => OperatorStarten("welt", EingabeAlsThema(), 0));
        FigHilfe.Knopf(this, "btnOpEreignisse", (s, m) => { evView = true; gedView = false; infoText = ""; status = ""; RefreshBindings(true); });
        FigHilfe.Knopf(this, "btnOpHilfe", (s, m) => { infoText = infoText.Length > 0 ? "" : GameOperator.Hilfe; gedView = false; evView = false; status = ""; RefreshBindings(true); });
        FigHilfe.Knopf(this, "btnEvZurueck", (s, m) => { evWahl = Math.Max(0, evWahl - 1); status = ""; RefreshBindings(true); });
        FigHilfe.Knopf(this, "btnEvVor", (s, m) => { evWahl = Math.Min(Math.Max(0, evAnzahl - 1), evWahl + 1); status = ""; RefreshBindings(true); });
        FigHilfe.Knopf(this, "btnEvFilter", (s, m) => { evFilter = (evFilter + 1) % GameOperator.Filter.Length; evWahl = 0; status = ""; RefreshBindings(true); });
        FigHilfe.Knopf(this, "btnEvPlanen", (s, m) => EvPlanen());
        FigHilfe.Knopf(this, "btnEvFreigeben", (s, m) => EvStatus(GameOperator.Freigegeben));
        FigHilfe.Knopf(this, "btnEvAktiv", (s, m) => EvStatus(GameOperator.Aktiv));
        FigHilfe.Knopf(this, "btnEvFertig", (s, m) => EvStatus(GameOperator.Abgeschlossen));
        FigHilfe.Knopf(this, "btnEvSpaeter", (s, m) => EvStatus(GameOperator.Zurueckgestellt));
        FigHilfe.Knopf(this, "btnEvAbbruch", (s, m) => EvStatus(GameOperator.Abgebrochen));
        FigHilfe.Knopf(this, "btnEvFolgen", (s, m) => { if (evId <= 0) { status = "Erst ein Ereignis auswählen."; RefreshBindings(true); return; } OperatorStarten("folgen", EingabeAlsThema(), evId); });
        FigHilfe.Knopf(this, "btnEvGespraech", (s, m) => { evView = false; status = ""; RefreshBindings(true); });
    }

    /// <summary>Text im Eingabefeld als Thema für eine Simulation (Befehle zählen nicht). Leert das Feld.</summary>
    string EingabeAlsThema()
    {
        if (eingabe == null) return "";
        string t = (eingabe.Text ?? "").Trim();
        if (t.StartsWith("/")) return "";
        eingabe.Text = "";
        return t;
    }

    void OperatorStarten(string art, string thema, int bezug)
    {
        var sp = xui.playerUI.entityPlayer;
        string spieler = sp != null ? sp.EntityName : "";
        string m = GameOperator.Starten(art, thema, bezug, spieler, fehler =>
        {
            if (Instanz == null) return;
            Instanz.status = fehler ?? "";
            Instanz.zurueck = 0;
            Instanz.RefreshBindings(true);
        });
        status = m;
        if (Ki.Wartet(Ki.ErzaehlerName)) { evView = false; gedView = false; infoText = ""; zurueck = 0; }
        FigurenMod.Speichern();
        RefreshBindings(true);
    }

    void EvPlanen()
    {
        if (evId <= 0) { status = "Erst ein Ereignis auswählen."; RefreshBindings(true); return; }
        string m = GameOperator.StatusSetzen(evId, GameOperator.InPlanung);
        var e = GameOperator.Holen(evId);
        FigurenMod.Speichern();
        if (e != null && e.Status == GameOperator.InPlanung)
        {
            int id = evId;
            OperatorStarten("plan", EingabeAlsThema(), id);
            if (!Ki.Wartet(Ki.ErzaehlerName)) status = m + " " + status;
            return;
        }
        status = m;
        RefreshBindings(true);
    }

    void EvStatus(string neu)
    {
        if (evId <= 0) { status = "Erst ein Ereignis auswählen."; RefreshBindings(true); return; }
        if (neu == GameOperator.Abgeschlossen)
        {
            float jetzt = UnityEngine.Time.time;
            if (fertigFrageId != evId || jetzt - fertigFrageZeit > 15f)
            {
                fertigFrageId = evId;
                fertigFrageZeit = jetzt;
                var e = GameOperator.Holen(evId);
                status = e != null && e.Status == GameOperator.Abgeschlossen
                    ? "#" + evId + " ist schon abgeschlossen."
                    : "Ist #" + evId + " im Spiel wirklich eingetreten? Dann nochmal Fertig drücken (das Ergebnis vorher ins Eingabefeld schreiben, wenn du willst).";
                RefreshBindings(true);
                return;
            }
            fertigFrageId = 0;
            status = GameOperator.StatusSetzen(evId, neu, EingabeAlsThema());
        }
        else status = GameOperator.StatusSetzen(evId, neu);
        FigurenMod.Speichern();
        RefreshBindings(true);
    }

    void Blaettern(bool aelter)
    {
        var r = Aktuell();
        if (evView)
        {
            evWahl = aelter ? Math.Max(0, evWahl - 6) : Math.Min(Math.Max(0, evAnzahl - 1), evWahl + 6);
            status = "";
            RefreshBindings(true);
            return;
        }
        gedView = false;
        infoText = "";
        int anzahl = Ki.Gespraech(r.Name).Nachrichten.Count;
        if (aelter) { if (zurueck + angezeigt < anzahl) zurueck += Math.Max(1, angezeigt); }
        else zurueck = Math.Max(0, zurueck - Math.Max(1, angezeigt));
        status = "";
        RefreshBindings(true);
    }


    public override void OnOpen()
    {
        base.OnOpen();
        // Block-Lebensanzeige einmal beim Öffnen schließen (nicht jeden Frame: das störte die Controller-Steuerung)
        try { xui.playerUI.windowManager.CloseIfOpen("focusedBlockHealth"); } catch { }
        zurueck = 0;
        gedView = false;
        evView = false;
        infoText = "";
        var r = Aktuell();
        status = Ki.Wartet(r.Name) ? r.Name + " denkt nach ..." : "";
        RefreshBindings(true);
    }

    void Wechseln(int d)
    {
        wahl += d;
        zurueck = 0;
        gedView = false;
        evView = false;
        infoText = "";
        var r = Aktuell();
        status = Ki.Wartet(r.Name) ? r.Name + " denkt nach ..." : "";
        RefreshBindings(true);
    }

    void Senden()
    {
        var r = Aktuell();
        if (eingabe == null) return;
        string text = (eingabe.Text ?? "").Trim();
        if (text.Length == 0) return;
        string meldung;
        if (r.Spielleiter)
        {
            string starten, thema;
            int bezug;
            if (GameOperator.Befehl(text, out meldung, out starten, out thema, out bezug))
            {
                eingabe.Text = "";
                FigurenMod.Speichern();
                if (starten.Length > 0)
                {
                    OperatorStarten(starten, thema, bezug);
                    if (meldung.Length > 0) status = meldung + " " + status;
                    RefreshBindings(true);
                    return;
                }
                if (meldung.Length > 120 || meldung.IndexOf('\n') >= 0) { infoText = meldung; status = ""; evView = false; gedView = false; }
                else status = meldung;
                RefreshBindings(true);
                return;
            }
        }
        if (KiAnzeige.Befehl(r.Name, text, out meldung)) { eingabe.Text = ""; status = meldung; gedView = true; evView = false; infoText = ""; RefreshBindings(true); return; }
        if (Ki.Wartet(r.Name)) { status = r.Name + " antwortet noch ..."; RefreshBindings(true); return; }
        if (r.Spielleiter && evView && evId > 0)
        {
            var ev = GameOperator.Holen(evId);
            if (ev != null) text = "Zu Ereignis #" + ev.Id + " (" + ev.Titel + ", " + ev.Status + "): " + text;
        }
        evView = false;
        infoText = "";
        eingabe.Text = "";
        zurueck = 0;
        gedView = false;
        var sp = xui.playerUI.entityPlayer;
        string spieler = sp != null ? sp.EntityName : "";
        status = r.Name + " denkt nach ...";
        Ki.Fragen(r, spieler, text, fehler =>
        {
            if (Instanz == null) return;
            Instanz.status = fehler ?? "";
            Instanz.zurueck = 0;
            Instanz.RefreshBindings(true);
        }, !r.Spielleiter);
        RefreshBindings(true);
    }

    void Neubeginn()
    {
        var r = Aktuell();
        if (Ki.Wartet(r.Name)) { status = "Bitte auf die Antwort warten."; RefreshBindings(true); return; }
        Ki.Vergessen(r.Name);
        FigurenMod.Speichern();
        zurueck = 0;
        status = r.Name + " hat das Gespräch und die Erinnerung vergessen. Feste Fakten bleiben.";
        RefreshBindings(true);
    }

    string VerlaufText(Rolle r)
    {
        var g = Ki.Gespraech(r.Name);
        if (r.Spielleiter && evView)
        {
            int anzahl, id;
            string ansicht = GameOperator.Ansicht(evFilter, evWahl, out anzahl, out id);
            evAnzahl = anzahl;
            evId = id;
            if (evWahl > Math.Max(0, anzahl - 1)) evWahl = Math.Max(0, anzahl - 1);
            return ansicht;
        }
        if (r.Spielleiter && infoText.Length > 0) return KiAnzeige.Sicher(infoText);
        if (gedView) return KiAnzeige.Gedaechtnis(g, r.Name);
        if (g.Nachrichten.Count == 0)
            return r.Spielleiter
                ? "(Beschreibe unten deine Lage und frag den Operator, zum Beispiel: Ich habe in Springfield eine Wohnung gekauft und gehe jetzt rein. Was könnte passieren?)\n\nSzenarien: der Operator schlägt mindestens drei mögliche Ereignisse vor (Text im Eingabefeld = Thema).\nWelt entwickeln: was sich ohne dich in der Welt tut.\nEreignisse: alle Vorschläge, Planungen, aktiven und abgeschlossenen Ereignisse, mit Knöpfen zum Planen, Freigeben, Aktivieren, Abschließen.\nBefehle: /hilfe"
                : "(Noch kein Gespräch. Du erreichst " + r.Name + " aus der Ferne. Schreib unten etwas und drück Enter.)\n\nTipp: /merke Text speichert einen festen Fakt.";
        return KiAnzeige.Verlauf(g, r.Name, zurueck, out angezeigt);
    }

    public override bool GetBindingValue(ref string _value, string _bindingName)
    {
        var r = Aktuell();
        switch (_bindingName)
        {
            case "lname": _value = r.Name.ToUpperInvariant() + (r.Titel.Length > 0 ? "  -  " + r.Titel : ""); return true;
            case "lzaehler": _value = (wahl + 1) + " / " + Eintraege().Count; return true;
            case "lverlauf": _value = VerlaufText(r); return true;
            case "lstatus":
                _value = status.Length > 0 ? status : KiAnzeige.Hinweis(Ki.Gespraech(r.Name), zurueck, angezeigt, gedView);
                if (_value.Length == 0 && r.Spielleiter)
                    _value = evView ? "Ansicht: " + GameOperator.Filter[evFilter % GameOperator.Filter.Length] + ". Eingetippter Text bespricht das gewählte Ereignis." : GameOperator.Erinnerung();
                _value = KiAnzeige.Sicher(_value);
                return true;
            case "lopchat": _value = r.Spielleiter && !evView ? "true" : "false"; return true;
            case "lopev": _value = r.Spielleiter && evView ? "true" : "false"; return true;
        }
        return base.GetBindingValue(ref _value, _bindingName);
    }
}
