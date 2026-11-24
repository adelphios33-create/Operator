// Bauplan-Mod – Fenster: Markierung speichern, Gebäude auswählen, drehen, platzieren, rückgängig.
// windows.xml: controller="Bauplan, Bauplan" -> XUiC_Bauplan (ohne Namespace).
// Geprüft gegen V 1.4: XUiController (Init, OnOpen, GetChildById, GetBindingValue(ref string, string), RefreshBindings),
// XUiC_SimpleButton (OnPressed, Text), XUiC_TextInput (Text, OnChangeHandler), LocalPlayerUI.windowManager.Open/CloseIfOpen.
using System;
using System.Collections.Generic;
using System.Linq;
using Bauplan;

public class XUiC_Bauplan : XUiController
{
    public static XUiC_Bauplan Instanz;
    public const string Gruppe = "bauplan";
    const int Zeilen = 8;

    XUiC_TextInput txtName, txtSuche;
    XUiC_SimpleButton btnFilter, btnDrehen, btnLuft, btnKeller, btnKopieren, btnZombies, btnMaterial;
    readonly XUiC_SimpleButton[] zeile = new XUiC_SimpleButton[Zeilen];
    int filter;          // 0 alle, 1 eigene, 2 Spiel
    int seite;
    int markiert = -1;   // Index in der gefilterten Liste
    string ueberschreibName = "";
    string loeschName = "";
    static EntityPlayerLocal spieler;

    public static bool Oeffnen(EntityPlayerLocal sp)
    {
        try
        {
            if (sp == null || sp.PlayerUI == null) return false;
            spieler = sp;
            sp.PlayerUI.windowManager.Open(Gruppe, true, false, true);
            return true;
        }
        catch (Exception e)
        {
            Log.Error("[Bauplan] Fenster konnte nicht geöffnet werden: " + e.Message);
            return false;
        }
    }

    void Knopf(string id, XUiEvent_OnPressEventHandler aktion)
    {
        var b = GetChildById(id) as XUiC_SimpleButton;
        if (b != null) b.OnPressed += aktion;
        else Log.Warning("[Bauplan] Knopf '" + id + "' nicht gefunden.");
    }

    public override void Init()
    {
        base.Init();
        Instanz = this;
        txtName = GetChildById("txtName") as XUiC_TextInput;
        txtSuche = GetChildById("txtSuche") as XUiC_TextInput;
        if (txtSuche != null) txtSuche.OnChangeHandler += (s, t, c) => { seite = 0; markiert = -1; RefreshBindings(true); };
        btnFilter = GetChildById("btnFilter") as XUiC_SimpleButton;
        btnDrehen = GetChildById("btnDrehen") as XUiC_SimpleButton;
        btnLuft = GetChildById("btnLuft") as XUiC_SimpleButton;
        btnKeller = GetChildById("btnKeller") as XUiC_SimpleButton;
        btnKopieren = GetChildById("btnKopieren") as XUiC_SimpleButton;

        Knopf("btnKopieren", (s, m) => Kopieren());
        Knopf("btnHoeherUnten", (s, m) => { Plan.Erweitern(1, 0); Melden(Plan.MarkierungText()); });
        Knopf("btnHoeherOben", (s, m) => { Plan.Erweitern(0, 1); Melden(Plan.MarkierungText()); });
        Knopf("btnLeeren", (s, m) => Melden(Plan.FlaecheLeeren(false)));
        Knopf("btnLeerenAlles", (s, m) => Melden(Plan.FlaecheLeeren(true)));
        Knopf("btnMarkLoeschen", (s, m) => { Plan.HatEcke1 = false; Plan.HatEcke2 = false; Vorschau.Weg(); Melden("Markierung gelöscht."); });
        Knopf("btnFilter", (s, m) => { filter = (filter + 1) % 3; seite = 0; markiert = -1; Neu(); });
        Knopf("btnZurueck", (s, m) => { seite = Math.Max(0, seite - 1); Neu(); });
        Knopf("btnVor", (s, m) => { seite++; Neu(); });
        for (int i = 0; i < Zeilen; i++)
        {
            int k = i;
            zeile[i] = GetChildById("btnL" + i) as XUiC_SimpleButton;
            Knopf("btnL" + i, (s, m) => Waehlen(k));
        }
        Knopf("btnDrehen", (s, m) => { Plan.Drehen(); Neu(); });
        Knopf("btnBoden", (s, m) => Melden(Plan.BodenReparieren()));
        Knopf("btnAuffuellen", (s, m) => Melden(Plan.BodenAuffuellen()));
        Knopf("btnAsphalt", (s, m) => Melden(Plan.BodenAuffuellen(true)));
        btnMaterial = GetChildById("btnMaterial") as XUiC_SimpleButton;
        Knopf("btnMaterial", (s, m) => { Plan.MaterialWeiter(); Neu(); });
        Knopf("btnFuellen", (s, m) => Melden(Plan.FlaecheFuellen()));
        Knopf("btnLuft", (s, m) => { Plan.MitLuft = !Plan.MitLuft; Neu(); });
        Knopf("btnKeller", (s, m) => { Plan.MitKeller = !Plan.MitKeller; Neu(); });
        btnZombies = GetChildById("btnZombies") as XUiC_SimpleButton;
        Knopf("btnZombies", (s, m) => { Plan.OhneZombies = !Plan.OhneZombies; Neu(); });
        Knopf("btnPlatzieren", (s, m) => Platzieren());
        Knopf("btnAblage", (s, m) => Melden(Plan.Platz == null ? "Erst ein Gebäude auswählen." :
            (Plan.InZwischenablage(Plan.Platz) ? Plan.GewaehltName + " liegt in der Zwischenablage. Werkzeug in die Hand, Rahmen setzen, Strg+V." : "Zwischenablage nicht verfügbar.")));
        Knopf("btnAbbrechen", (s, m) => { Plan.Abbrechen(); Melden("Platzieren beendet. Werkzeug markiert wieder Ecken."); });
        Knopf("btnRueckgaengig", (s, m) => Melden(Plan.Rueckgaengig()));
        Knopf("btnLoeschen", (s, m) => Loeschen());
        Knopf("btnNeuLaden", (s, m) => { Liste.Neu(); Melden("Liste neu eingelesen: " + Liste.Alle().Count + " Gebäude."); });
        Knopf("btnSchliessen", (s, m) => xui.playerUI.windowManager.CloseIfOpen(Gruppe));
    }

    public override void OnOpen()
    {
        base.OnOpen();
        ueberschreibName = "";
        loeschName = "";
        Neu();
    }

    public void Neu()
    {
        if (btnFilter != null) btnFilter.Text = "Zeigen: " + (filter == 1 ? "Eigene" : (filter == 2 ? "Spiel-Gebäude" : "Alle")) + " (anklicken)";
        if (btnDrehen != null) btnDrehen.Text = "Drehen: " + Plan.Drehung * 90 + "°";
        if (btnLuft != null) btnLuft.Text = "Fläche freiräumen: " + (Plan.MitLuft ? "AN" : "AUS");
        if (btnKeller != null) btnKeller.Text = "Keller einsenken: " + (Plan.MitKeller ? "AN" : "AUS");
        if (btnMaterial != null) btnMaterial.Text = "Material: " + Plan.MaterialNamen[Plan.Material];
        if (btnZombies != null) btnZombies.Text = Plan.OhneZombies ? "Zombies: AUS (keine)" : "Zombies: AN (wie im Spiel)";
        var l = Gefiltert();
        int seiten = Math.Max(1, (l.Count + Zeilen - 1) / Zeilen);
        if (seite >= seiten) seite = seiten - 1;
        for (int i = 0; i < Zeilen; i++)
        {
            if (zeile[i] == null) continue;
            int idx = seite * Zeilen + i;
            zeile[i].Text = idx < l.Count ? (idx == markiert ? "> " : "") + l[idx].Name + (l[idx].Eigen ? "  [eigen]" : "") : "-";
        }
        RefreshBindings(true);
    }

    void Melden(string text) { Plan.Meldung = text; Neu(); }

    List<Eintrag> Gefiltert() { return Liste.Gefiltert(filter, txtSuche == null ? "" : txtSuche.Text); }

    Eintrag Markiert()
    {
        var l = Gefiltert();
        return markiert >= 0 && markiert < l.Count ? l[markiert] : null;
    }

    void Waehlen(int zeileNr)
    {
        var l = Gefiltert();
        int idx = seite * Zeilen + zeileNr;
        if (idx >= l.Count) return;
        markiert = idx;
        loeschName = "";
        Melden(Plan.Waehlen(l[idx]));
    }

    void Kopieren()
    {
        string n = txtName == null ? "" : txtName.Text;
        string sauber = Plan.SauberName(n);
        bool erneut = sauber.Length > 0 && sauber == ueberschreibName;
        string antwort = Plan.Kopieren(n, erneut);
        ueberschreibName = antwort.StartsWith("Den Namen") ? sauber : "";
        if (antwort.StartsWith("Gespeichert")) { filter = 1; seite = 0; markiert = -1; }
        Melden(antwort);
    }

    void Platzieren()
    {
        string antwort = Plan.Platzieren(spieler);
        Melden(antwort);
        if (Plan.Modus == Modus.Platzieren) xui.playerUI.windowManager.CloseIfOpen(Gruppe);
    }

    void Loeschen()
    {
        var e = Markiert();
        if (e == null) { Melden("Erst ein eigenes Gebäude in der Liste anklicken."); return; }
        if (!e.Eigen) { Melden("Spiel-Gebäude lassen sich nicht löschen."); return; }
        if (loeschName != e.Name) { loeschName = e.Name; Melden("Wirklich '" + e.Name + "' löschen? Zum Bestätigen nochmal 'Löschen' drücken."); return; }
        loeschName = "";
        markiert = -1;
        Melden(Plan.Loeschen(e));
    }

    // ---------- Vorschaubild ----------
    // Spiel-Gebäude haben neben der .tts ein .jpg (z. B. Data/Prefabs/POIs/hospital_01.jpg). Das wird geladen und angezeigt.
    string bildFuer = null;
    bool bildGeladen;
    UnityEngine.Texture2D bildTex;

    void BildAktualisieren()
    {
        string pfad = Plan.Platz == null ? "" : Plan.GewaehltBildPfad();
        if (pfad == bildFuer) return;
        bildFuer = pfad;
        bildGeladen = false;
        try
        {
            var v = GetChildById("bpBild");
            var tv = v == null ? null : v.ViewComponent as XUiV_Texture;
            if (tv == null) return;
            if (bildTex != null) { UnityEngine.Object.Destroy(bildTex); bildTex = null; }
            if (pfad.Length == 0 || !System.IO.File.Exists(pfad)) { tv.Texture = null; return; }
            var tex = new UnityEngine.Texture2D(2, 2);
            if (!UnityEngine.ImageConversion.LoadImage(tex, System.IO.File.ReadAllBytes(pfad))) { UnityEngine.Object.Destroy(tex); tv.Texture = null; return; }
            bildTex = tex;
            tv.Texture = tex;
            bildGeladen = true;
        }
        catch (Exception e) { Log.Warning("[Bauplan] Vorschaubild " + pfad + ": " + e.Message); }
    }

    public override bool GetBindingValue(ref string _value, string _bindingName)
    {
        switch (_bindingName)
        {
            case "bpmodus": _value = Plan.Modus == Modus.Platzieren ? "Modus: Platzieren" : "Modus: Markieren"; return true;
            case "bpmarkierung": _value = Plan.MarkierungText(); return true;
            case "bpanzahl": _value = Gefiltert().Count + " Gebäude"; return true;
            case "bpseite":
                {
                    int n = Gefiltert().Count;
                    _value = "Seite " + (seite + 1) + " / " + Math.Max(1, (n + Zeilen - 1) / Zeilen);
                    return true;
                }
            case "bpauswahl":
                if (Plan.Platz == null) { _value = "Noch kein Gebäude ausgewählt.\nLinks in der Liste anklicken."; return true; }
                _value = Plan.GewaehltName + (Plan.GewaehltEigen ? "  (eigene Kopie)" : "  (Spiel-Gebäude)")
                         + "\nGröße: " + Plan.Platz.size.x + " × " + Plan.Platz.size.z + ", " + Plan.Platz.size.y + " hoch"
                         + (Plan.GewaehltEigen || Plan.Platz.yOffset == 0 ? "" : "\nKeller: " + (-Plan.Platz.yOffset) + " Blöcke unter dem Boden")
                         + "\nDrehung: " + Plan.Drehung * 90 + "°";
                return true;
            case "bpbildan": BildAktualisieren(); _value = bildGeladen ? "true" : "false"; return true;
            case "bpbildtext":
                BildAktualisieren();
                _value = bildGeladen ? "" : (Plan.Platz == null ? "Gebäude links auswählen" : "Kein Vorschaubild\nfür dieses Gebäude");
                return true;
            case "bprueck": _value = Plan.KannRueckgaengig ? "true" : "false"; return true;
            case "bpmeldung": _value = Plan.Meldung; return true;
        }
        return base.GetBindingValue(ref _value, _bindingName);
    }
}
