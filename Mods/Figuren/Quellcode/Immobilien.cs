// Figuren-Mod – Immobilien-Schild: ein Wandschild (1 Block), an dem man ein Objekt zum Verkauf einträgt.
// Banker und Bürgermeister (Rollen mit "Immobilien: ja") bekommen die Angebote ihrer Stadt als echte Spieldaten.
// Geprüft gegen 7 Days to Die V 1.4 (b8): Block.PlaceBlock, GetActivationText, GetBlockActivationCommands,
// OnBlockActivated, OnBlockRemoved, XUiController, XUiC_SimpleButton, XUiC_TextInput (Text, OnSubmitHandler).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Figuren.Kern;
using Figuren.Spiel;

namespace Figuren.Spiel
{
    public class Immobilie
    {
        public int Nr;
        public string Stadt = "";
        public string Name = "";
        public int Typ;
        public int Preis;
        public bool Gesaeubert;
        public int Status;          // 0 zu verkaufen, 1 reserviert, 2 verkauft
        public string Notiz = "";
        public int X, Y, Z;
    }

    public static class Immo
    {
        public class Art
        {
            public string Name; public int Von; public int Bis; public int GebuehrVon; public int GebuehrBis; public int Saeuberung;
        }

        /// <summary>Richtwerte (Spielerangaben): Kaufpreis-Spanne, Gebühr Bürgermeister, Säuberung.</summary>
        public static readonly Art[] Arten =
        {
            new Art { Name = "Kleine Wohnung",       Von = 4000,  Bis = 8000,  GebuehrVon = 1000, GebuehrBis = 2000, Saeuberung = 1000 },
            new Art { Name = "Mittlere Wohnung",     Von = 8000,  Bis = 15000, GebuehrVon = 1500, GebuehrBis = 3000, Saeuberung = 1000 },
            new Art { Name = "Kleines Haus",         Von = 15000, Bis = 30000, GebuehrVon = 2000, GebuehrBis = 3000, Saeuberung = 2000 },
            new Art { Name = "Haus 2-3 Etagen",      Von = 30000, Bis = 60000, GebuehrVon = 3000, GebuehrBis = 6000, Saeuberung = 4000 },
            new Art { Name = "Gewerbeobjekt",        Von = 20000, Bis = 80000, GebuehrVon = 3000, GebuehrBis = 6000, Saeuberung = 3000 },
            new Art { Name = "Grundstück",           Von = 3000,  Bis = 15000, GebuehrVon = 1000, GebuehrBis = 2000, Saeuberung = 0 },
        };

        public static readonly string[] Stati = { "zu verkaufen", "reserviert", "verkauft" };

        public static Art ArtVon(int typ) { return Arten[Math.Max(0, Math.Min(Arten.Length - 1, typ))]; }

        public static string Dukes(int n) { return n.ToString("N0", CultureInfo.InvariantCulture).Replace(",", ".") + " Coins"; }

        static List<Immobilie> Liste { get { return FigurenMod.Stand == null ? null : FigurenMod.Stand.Immobilien; } }

        public static Immobilie Am(Vector3i p)
        {
            var l = Liste;
            return l == null ? null : l.FirstOrDefault(i => i.X == p.x && i.Y == p.y && i.Z == p.z);
        }

        public static Immobilie Neu(Vector3i p, string stadt)
        {
            var l = Liste;
            if (l == null) return null;
            var i = Am(p);
            if (i != null) return i;
            i = new Immobilie { Nr = l.Count == 0 ? 1 : l.Max(w => w.Nr) + 1, X = p.x, Y = p.y, Z = p.z, Stadt = stadt, Typ = 1 };
            i.Preis = Vorschlag(i);
            l.Add(i);
            return i;
        }

        public static void Loeschen(Vector3i p)
        {
            var l = Liste;
            if (l == null) return;
            if (l.RemoveAll(i => i.X == p.x && i.Y == p.y && i.Z == p.z) > 0) FigurenMod.Speichern();
        }

        /// <summary>Vorschlag: Mitte der Preisspanne + mittlere Gebühr + Säuberung, falls noch nicht gesäubert. Auf 500 gerundet.</summary>
        public static int Vorschlag(Immobilie i)
        {
            var a = ArtVon(i.Typ);
            int p = (a.Von + a.Bis) / 2 + (a.GebuehrVon + a.GebuehrBis) / 2 + (i.Gesaeubert ? 0 : a.Saeuberung);
            return (int)Math.Round(p / 500.0) * 500;
        }

        public static string Richtwert(Immobilie i)
        {
            var a = ArtVon(i.Typ);
            return a.Name + ": Kaufpreis " + Dukes(a.Von) + " bis " + Dukes(a.Bis) + ", Gebühr Bürgermeister " + Dukes(a.GebuehrVon) + " bis " + Dukes(a.GebuehrBis)
                   + (a.Saeuberung > 0 ? ", Säuberung ca. " + Dukes(a.Saeuberung) : "");
        }

        /// <summary>Angebote einer Stadt als Text für das Modell (echte Spieldaten).</summary>
        public static string FuerKi(string stadt)
        {
            var l = Liste;
            if (l == null) return "";
            var sb = new StringBuilder();
            var auswahl = l.Where(i => stadt.Length == 0 || string.Equals(i.Stadt, stadt, StringComparison.OrdinalIgnoreCase)).OrderBy(i => i.Status).ThenBy(i => i.Nr).ToList();
            string ort = stadt.Length > 0 ? stadt : "allen Städten";
            if (auswahl.Count == 0)
                return "IMMOBILIEN-ANGEBOTE in " + ort + " (echte Spieldaten): Derzeit ist kein Objekt eingetragen. Erfinde keine Objekte; sag ehrlich, dass gerade nichts im Angebot ist, oder biete an, eine Einschätzung zu geben.";
            sb.AppendLine("IMMOBILIEN-ANGEBOTE in " + ort + " (dein aktueller Bestand laut Verkaufsschildern. Nur diese Objekte gibt es. Preise genau so nennen; sie enthalten bereits Gebühren und, wo nötig, die Säuberung. Verhandlungsspielraum höchstens 10 Prozent. Nenne die Liste nicht ungefragt komplett, sondern nur, was gerade gefragt ist):");
            foreach (var i in auswahl)
            {
                sb.Append("- Objekt ").Append(i.Nr).Append(": ").Append(i.Name.Length > 0 ? i.Name : ArtVon(i.Typ).Name)
                  .Append(" | Art: ").Append(ArtVon(i.Typ).Name)
                  .Append(" | Preis: ").Append(Dukes(i.Preis))
                  .Append(" | Zustand: ").Append(i.Gesaeubert ? "gesäubert, bezugsfertig" : "noch nicht gesäubert (Zombies), Säuberung ist eingerechnet")
                  .Append(" | Status: ").Append(Stati[Math.Max(0, Math.Min(2, i.Status))])
                  .Append(" | Lage: Koordinaten ").Append(i.X).Append(" / ").Append(i.Z);
                if (i.Notiz.Length > 0) sb.Append(" | Beschreibung: ").Append(i.Notiz);
                if (i.Status == 0)
                {
                    string raten = WirtschaftBruecke.RatenText(i.Preis);
                    if (raten.Length > 0 && !raten.StartsWith("NEIN")) sb.Append(" | ").Append(raten);
                }
                sb.AppendLine();
            }
            return sb.ToString().Trim();
        }
    }
}

/// <summary>Immobilien-Schild. Ohne Namespace: "Class=Immobilienschild, Figuren" wird als BlockImmobilienschild gesucht.</summary>
public class BlockImmobilienschild : Block
{
    static readonly BlockActivationCommand[] Befehl = { new BlockActivationCommand("immo_open", "search", true, false) };

    public override string GetActivationText(WorldBase _world, BlockValue _blockValue, int _clrIdx, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        var i = Immo.Am(_blockPos);
        if (i == null) return "[E] Immobilien-Schild (noch leer)";
        return "[E] " + (i.Name.Length > 0 ? i.Name : Immo.ArtVon(i.Typ).Name) + " - " + Immo.Dukes(i.Preis) + " (" + Immo.Stati[Math.Max(0, Math.Min(2, i.Status))] + ")";
    }

    public override bool HasBlockActivationCommands(WorldBase _world, BlockValue _blockValue, int _clrIdx, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        return true;
    }

    public override BlockActivationCommand[] GetBlockActivationCommands(WorldBase _world, BlockValue _blockValue, int _clrIdx, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        return Befehl;
    }

    public override bool OnBlockActivated(string _commandName, WorldBase _world, int _cIdx, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        return XUiC_FigImmo.Oeffnen(_player, _blockPos);
    }

    public override bool OnBlockActivated(WorldBase _world, int _clrIdx, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        return XUiC_FigImmo.Oeffnen(_player, _blockPos);
    }

    public override void PlaceBlock(WorldBase _world, BlockPlacement.Result _result, EntityAlive _ea)
    {
        base.PlaceBlock(_world, _result, _ea);
        try
        {
            var spieler = _ea as EntityPlayerLocal;
            if (spieler == null) return;
            var pos = _result.blockPos;
            FigurenMod.Spaeter(() => XUiC_FigImmo.Oeffnen(spieler, pos));
        }
        catch (Exception e) { Log.Warning("[Figuren] Immobilien-Schild nach dem Aufstellen: " + e.Message); }
    }

    public override void OnBlockRemoved(WorldBase _world, Chunk _chunk, Vector3i _blockPos, BlockValue _blockValue)
    {
        base.OnBlockRemoved(_world, _chunk, _blockPos, _blockValue);
        try { Immo.Loeschen(_blockPos); }
        catch (Exception e) { Log.Warning("[Figuren] Immobilie beim Abbau: " + e.Message); }
    }
}

/// <summary>Fenster am Immobilien-Schild: Bezeichnung, Stadt, Art, Zustand, Status, Preis, Beschreibung.</summary>
public class XUiC_FigImmo : XUiController
{
    public static XUiC_FigImmo Instanz;
    public const string Gruppe = "figImmo";
    static Vector3i pos;
    static string letzteStadt = "";

    XUiC_TextInput txtName, txtPreis, txtNotiz;
    XUiC_SimpleButton btnZustand, btnStatus;
    string meldung = "";

    public static bool Oeffnen(EntityPlayerLocal spieler, Vector3i p)
    {
        if (Instanz == null || FigurenMod.Stand == null) return false;
        pos = p;
        return FigHilfe.Oeffnen(spieler, Gruppe);
    }

    Immobilie Objekt
    {
        get
        {
            var i = Immo.Am(pos);
            if (i == null)
            {
                var st = Rollen.Staedte;
                string stadt = letzteStadt.Length > 0 ? letzteStadt : (st.Count > 0 ? st[0] : "");
                i = Immo.Neu(pos, stadt);
            }
            return i;
        }
    }

    public override void Init()
    {
        base.Init();
        Instanz = this;
        txtName = GetChildById("txtImmoName") as XUiC_TextInput;
        txtPreis = GetChildById("txtImmoPreis") as XUiC_TextInput;
        txtNotiz = GetChildById("txtImmoNotiz") as XUiC_TextInput;
        if (txtPreis != null) txtPreis.OnSubmitHandler += (s, t) => Speichern();
        if (txtName != null) txtName.OnSubmitHandler += (s, t) => Speichern();
        if (txtNotiz != null) txtNotiz.OnSubmitHandler += (s, t) => Speichern();
        FigHilfe.Knopf(this, "btnImmoStadtZurueck", (s, m) => Stadt(-1));
        FigHilfe.Knopf(this, "btnImmoStadtVor", (s, m) => Stadt(1));
        FigHilfe.Knopf(this, "btnImmoTypZurueck", (s, m) => Typ(-1));
        FigHilfe.Knopf(this, "btnImmoTypVor", (s, m) => Typ(1));
        btnZustand = GetChildById("btnImmoZustand") as XUiC_SimpleButton;
        btnStatus = GetChildById("btnImmoStatus") as XUiC_SimpleButton;
        FigHilfe.Knopf(this, "btnImmoZustand", (s, m) => { Uebernehmen(); var i = Objekt; if (i != null) { i.Gesaeubert = !i.Gesaeubert; } Neu(); });
        FigHilfe.Knopf(this, "btnImmoStatus", (s, m) => { Uebernehmen(); var i = Objekt; if (i != null) { i.Status = (i.Status + 1) % 3; } Neu(); });
        FigHilfe.Knopf(this, "btnImmoVorschlag", (s, m) => { Uebernehmen(); var i = Objekt; if (i != null) { i.Preis = Immo.Vorschlag(i); meldung = "Richtwert eingesetzt: " + Immo.Dukes(i.Preis); } Felder(); Neu(); });
        FigHilfe.Knopf(this, "btnImmoSpeichern", (s, m) => Speichern());
        FigHilfe.Knopf(this, "btnImmoSchliessen", (s, m) => { Speichern(); xui.playerUI.windowManager.CloseIfOpen(Gruppe); });
    }


    public override void OnOpen()
    {
        base.OnOpen();
        // Block-Lebensanzeige einmal beim Öffnen schließen (nicht jeden Frame: das störte die Controller-Steuerung)
        try { xui.playerUI.windowManager.CloseIfOpen("focusedBlockHealth"); } catch { }
        meldung = "";
        Felder();
        Neu();
    }

    void Felder()
    {
        var i = Objekt;
        if (i == null) return;
        if (txtName != null) txtName.Text = i.Name;
        if (txtPreis != null) txtPreis.Text = i.Preis.ToString(CultureInfo.InvariantCulture);
        if (txtNotiz != null) txtNotiz.Text = i.Notiz;
    }

    /// <summary>Text aus den Eingabefeldern ins Objekt übernehmen.</summary>
    bool Uebernehmen()
    {
        var i = Objekt;
        if (i == null) return false;
        if (txtName != null) i.Name = (txtName.Text ?? "").Trim();
        if (txtNotiz != null) i.Notiz = (txtNotiz.Text ?? "").Trim();
        if (txtPreis != null)
        {
            string t = (txtPreis.Text ?? "").Replace(".", "").Replace(",", "").Replace(" ", "").Replace("Dukes", "").Replace("Coins", "").Trim();
            int p;
            if (int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out p) && p >= 0) i.Preis = p;
            else if (t.Length > 0) { meldung = "Preis bitte als Zahl eingeben, z. B. 12000."; return false; }
        }
        return true;
    }

    void Speichern()
    {
        if (Uebernehmen())
        {
            FigurenMod.Speichern();
            meldung = "Gespeichert. Banker und Bürgermeister in " + Objekt.Stadt + " kennen das Objekt jetzt.";
        }
        Felder();
        Neu();
    }

    void Stadt(int d)
    {
        Uebernehmen();
        var i = Objekt;
        var st = Rollen.Staedte;
        if (i == null || st.Count == 0) return;
        int idx = st.FindIndex(w => string.Equals(w, i.Stadt, StringComparison.OrdinalIgnoreCase));
        idx = FigHilfe.Blaettern(idx < 0 ? 0 : idx + d, st.Count);
        i.Stadt = st[idx];
        letzteStadt = i.Stadt;
        Neu();
    }

    void Typ(int d)
    {
        Uebernehmen();
        var i = Objekt;
        if (i == null) return;
        i.Typ = FigHilfe.Blaettern(i.Typ + d, Immo.Arten.Length);
        Neu();
    }

    void Neu()
    {
        var i = Immo.Am(pos);
        if (i != null)
        {
            if (btnZustand != null) btnZustand.Text = i.Gesaeubert ? "Gesäubert" : "Noch nicht gesäubert";
            if (btnStatus != null) btnStatus.Text = "Status: " + Immo.Stati[Math.Max(0, Math.Min(2, i.Status))];
        }
        RefreshBindings(true);
    }

    public override bool GetBindingValue(ref string _value, string _bindingName)
    {
        var i = Immo.Am(pos);
        switch (_bindingName)
        {
            case "immonr": _value = i == null ? "" : "Objekt " + i.Nr + "   (Position " + i.X + " " + i.Y + " " + i.Z + ")"; return true;
            case "immostadt": _value = i == null ? "" : i.Stadt; return true;
            case "immotyp": _value = i == null ? "" : Immo.ArtVon(i.Typ).Name; return true;
            case "immozustand": _value = i == null ? "" : (i.Gesaeubert ? "Gesäubert" : "Noch nicht gesäubert"); return true;
            case "immostatus": _value = i == null ? "" : "Status: " + Immo.Stati[Math.Max(0, Math.Min(2, i.Status))]; return true;
            case "immorichtwert": _value = i == null ? "" : "Richtwert " + Immo.Richtwert(i) + "\nVorschlag inkl. Nebenkosten: " + Immo.Dukes(Immo.Vorschlag(i)); return true;
            case "immomeldung": _value = meldung; return true;
        }
        return base.GetBindingValue(ref _value, _bindingName);
    }
}
