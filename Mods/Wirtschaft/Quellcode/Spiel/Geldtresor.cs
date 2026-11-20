// Wirtschaft-Mod – Geldtresor (Banking)
// Ein Behälter, der das Guthaben einer Firma als echte Gegenstände zeigt:
// Dukes (casinoCoin), Old Cash, Silberbarren, Goldbarren.
// - Das Guthaben bestimmt den Inhalt: kommt Einkommen dazu oder wird an der Hauptbank ausgezahlt,
//   wird der Tresor beim nächsten Abgleich (alle 0,5 s, nicht während er offen ist) neu befüllt.
// - Nimmt man Geld heraus oder legt welches hinein, wird das Firmenguthaben sofort angepasst.
// - Der Tresor kann einer Firma oder einer Privatperson gehören.
// - Fremde Gegenstände im Tresor werden nicht angefasst und zählen nicht als Geld.
// Geprüft gegen 7 Days to Die V 1.4 (b8) nach bestem Wissen: BlockLoot, TileEntityLootContainer
// (items, bTouched, IsUserAccessing, SetModified), World.GetChunkFromWorldPos, Chunk.GetTileEntity.
using System;
using System.Collections.Generic;
using System.Linq;
using Wirtschaft.Kern;
using Wirtschaft.Spiel;

namespace Wirtschaft.Spiel
{
    /// <summary>Wem ein Geldtresor gehört: einer Firma oder einer Privatperson. Beide haben ein Konto, eine Tresor-Position und einen Tresor-Bestand.</summary>
    public class Halter
    {
        public Unternehmen Firma;
        public Partei Person;

        public static Halter Von(Unternehmen u) { return new Halter { Firma = u }; }
        public static Halter Von(Partei p) { return new Halter { Person = p }; }

        public bool IstPerson { get { return Person != null; } }
        public string Name { get { return Person != null ? Person.Name : Firma.Name; } }
        /// <summary>Eindeutiger Schlüssel (eine Firma und eine Person dürfen gleich heißen).</summary>
        public string Schluessel { get { return (Person != null ? "P:" : "F:") + Name; } }
        public bool IstRegierung { get { return Person != null && WirtschaftMod.Bank != null && WirtschaftMod.Bank.IstRegierungspartei(Person); } }
        public string Art { get { return Person == null ? "Firma" : (IstRegierung ? "Regierung" : "Person"); } }

        public decimal Konto
        {
            get { return Person != null ? Person.Konto : Firma.Konto; }
            set { if (Person != null) Person.Konto = value; else Firma.Konto = value; }
        }
        public string Tresor
        {
            get { return Person != null ? Person.Geldtresor : Firma.Geldtresor; }
            set { if (Person != null) Person.Geldtresor = value; else Firma.Geldtresor = value; }
        }
        public decimal Bestand
        {
            get { return Person != null ? Person.GeldtresorBestand : Firma.GeldtresorBestand; }
            set { if (Person != null) Person.GeldtresorBestand = value; else Firma.GeldtresorBestand = value; }
        }

        public static bool Gleich(Halter a, Halter b)
        {
            if (a == null || b == null) return false;
            if (a.Person != null || b.Person != null) return ReferenceEquals(a.Person, b.Person);
            return ReferenceEquals(a.Firma, b.Firma);
        }
    }

    public static class Geldtresor
    {
        // ---------- Einstellungen für das automatische Umtauschen (Kaskade) ----------
        // Münzen -> Scheine -> Silber -> Gold. Jede Stufe hat ein Grundvolumen (in Dukes).
        // Was darüber liegt, geht zu 80 % in die nächsthöhere Stufe, 20 % bleiben liegen.
        /// <summary>Grundvolumen Münzen (Dukes).</summary>
        public const decimal VolumenMuenzen = 10000m;
        /// <summary>Grundvolumen Scheine, als Wert in Dukes (20.000 = 1.000 Scheine).</summary>
        public const decimal VolumenScheine = 20000m;
        /// <summary>Grundvolumen Silber, als Wert in Dukes (20.000 = 40 Barren).</summary>
        public const decimal VolumenSilber = 20000m;
        /// <summary>So viel Prozent vom Überschuss einer Stufe gehen in die nächsthöhere.</summary>
        public const decimal AnteilHoch = 80m;
        /// <summary>Fallen die Münzen darunter, werden höhere Stufen in Münzen zurückgetauscht.</summary>
        public const decimal MuenzenMinimum = 5000m;

        public const string Gold = "resourceGoldNugget";
        public const string Silber = "resourceSilverNugget";
        public const string Schein = "oldCash";
        public const string Muenze = "casinoCoin";
        static readonly string[] Reihenfolge = { Gold, Silber, Schein, Muenze };

        static float naechsterTakt;
        /// <summary>Firmen, deren Inhalt nicht ganz in den Tresor passt: nicht jede halbe Sekunde neu schreiben.</summary>
        static readonly Dictionary<string, decimal> passtNicht = new Dictionary<string, decimal>();
        static readonly HashSet<string> vollGemeldet = new HashSet<string>();   // Warnung "voll" nur einmal pro Tresor und Sitzung
        static readonly HashSet<string> schonGemeldet = new HashSet<string>();

        /// <summary>Beim Laden eines Spielstands: Kurse einmalig auf die neuen Werte setzen.</summary>
        public static void Start(Bank bank)
        {
            passtNicht.Clear();
            vollGemeldet.Clear();
            schonGemeldet.Clear();
            naechsterTakt = 0f;
            if (bank == null || bank.Stand.KursStand >= 1) return;
            foreach (var w in bank.Stand.Waehrungen)
            {
                if (w.Item == Schein) w.Kurs = 20m;
                else if (w.Item == Silber) w.Kurs = 500m;
                else if (w.Item == Gold) w.Kurs = 5000m;
            }
            bank.Stand.KursStand = 1;
            Log.Out("[Wirtschaft] Kurse gesetzt: Old Cash 20, Silberbarren 500, Goldbarren 5000.");
        }

        /// <summary>Vom Spiel jeden Frame aufgerufen, arbeitet alle 0,5 Sekunden.</summary>
        public static void Takt()
        {
            var bank = WirtschaftMod.Bank;
            if (bank == null) return;
            float jetzt = UnityEngine.Time.time;
            if (jetzt < naechsterTakt) return;
            naechsterTakt = jetzt + 0.5f;

            bool geaendert = false;
            foreach (var h in AlleHalter(bank))
            {
                if (string.IsNullOrEmpty(h.Tresor)) continue;
                try
                {
                    if (Abgleichen(bank, h)) geaendert = true;
                }
                catch (Exception e)
                {
                    if (schonGemeldet.Add(h.Schluessel)) Log.Warning("[Wirtschaft] Geldtresor " + h.Name + ": " + e.Message);
                }
            }
            if (geaendert) WirtschaftMod.Speichern();
        }

        /// <summary>Alle, die einen Geldtresor haben können: Firmen, Privatpersonen und Regierungen.</summary>
        public static List<Halter> AlleHalter(Bank bank)
        {
            var l = new List<Halter>();
            foreach (var u in bank.Stand.Unternehmen) l.Add(Halter.Von(u));
            foreach (var p in bank.Personen()) l.Add(Halter.Von(p));
            foreach (var p in bank.Regierungen()) l.Add(Halter.Von(p));
            return l;
        }

        /// <summary>Gleicht Tresorinhalt und Guthaben ab. True, wenn sich etwas geändert hat.</summary>
        static bool Abgleichen(Bank bank, Halter h)
        {
            var te = Behaelter(h.Tresor);
            if (te == null || te.items == null) return false;   // Gebiet nicht geladen

            bool geaendert = false;
            string key = h.Schluessel;

            // 1. Hat jemand Geld herausgenommen oder hineingelegt? Dann das Guthaben anpassen.
            decimal inhalt = Wert(bank, te.items);
            if (inhalt != h.Bestand)
            {
                decimal unterschied = inhalt - h.Bestand;
                h.Konto += unterschied;
                h.Bestand = inhalt;
                Log.Out("[Wirtschaft] Geldtresor " + h.Name + ": " + (unterschied < 0 ? "entnommen " : "eingelegt ") + Befehle.Betrag(Math.Abs(unterschied)));
                geaendert = true;
            }

            // 2. Während jemand hineinschaut, nichts umräumen.
            if (te.IsUserAccessing()) return geaendert;

            // 3. Inhalt an das Guthaben anpassen (Einkommen, Auszahlung an der Hauptbank, Umtauschen).
            decimal ziel = Math.Max(0m, Math.Floor(h.Konto));
            var plan = Ziel(bank, te.items, ziel);
            if (Gleich(te.items, plan)) { passtNicht.Remove(key); return geaendert; }

            decimal versucht;
            if (passtNicht.TryGetValue(key, out versucht) && versucht == ziel) return geaendert;

            Befuellen(te, plan);
            h.Bestand = Wert(bank, te.items);
            if (Gleich(te.items, plan)) passtNicht.Remove(key);
            else
            {
                passtNicht[key] = ziel;
                if (vollGemeldet.Add(key))
                    Log.Warning("[Wirtschaft] Geldtresor " + h.Name + " ist voll, es wird nur ein Teil des Guthabens angezeigt (Meldung kommt nur einmal).");
            }
            return true;
        }

        /// <summary>
        /// Kaskade: bis 10.000 nur Münzen. Vom Überschuss gehen 80 % in Scheine, bis die Scheine 20.000 wert sind;
        /// von deren Überschuss 80 % in Silber, bis 20.000; von dessen Überschuss 80 % in Gold.
        /// Beispiel 30.000 = 14.000 Dukes + 800 Scheine.
        /// </summary>
        public static Dictionary<string, int> Aufteilen(Bank bank, decimal betrag)
        {
            string[] stufen = { Muenze, Schein, Silber, Gold };
            decimal[] volumen = { VolumenMuenzen, VolumenScheine, VolumenSilber, decimal.MaxValue };
            var plan = new Dictionary<string, int>();
            foreach (var n in stufen) plan[n] = 0;

            decimal x = Math.Max(0m, Math.Floor(betrag));   // Wert, der in dieser Stufe und darüber liegt
            for (int i = 0; i < stufen.Length; i++)
            {
                decimal k = Kurs(bank, stufen[i]);
                if (k <= 0) continue;
                decimal hoch = 0m;
                if (i + 1 < stufen.Length && x > volumen[i])
                {
                    // nächste Stufe mit gültigem Kurs suchen
                    int j = i + 1;
                    while (j < stufen.Length && Kurs(bank, stufen[j]) <= 0) j++;
                    if (j < stufen.Length)
                    {
                        decimal kn = Kurs(bank, stufen[j]);
                        hoch = Math.Floor((x - volumen[i]) * AnteilHoch / 100m / kn) * kn;
                    }
                }
                decimal bleibt = x - hoch;
                int stueck = (int)Math.Floor(bleibt / k);
                plan[stufen[i]] = stueck;
                x = hoch + (bleibt - stueck * k);   // Rest, der nicht in ganze Stücke passt, wandert mit
                if (x <= 0) break;
            }
            // Was am Ende übrig bleibt (kleiner als 1 Stück), kommt zu den Münzen.
            decimal kd = Kurs(bank, Muenze);
            if (x > 0 && kd > 0) plan[Muenze] += (int)Math.Floor(x / kd);
            return plan;
        }

        /// <summary>
        /// Was soll im Tresor liegen? Einnahmen werden sofort nach der Kaskade angelegt.
        /// Zahlungen (Rate, Auszahlung) gehen über die Münzen, bis sie unter 5.000 fallen; dann wird zurückgetauscht.
        /// </summary>
        static Dictionary<string, int> Ziel(Bank bank, ItemStack[] items, decimal ziel)
        {
            var plan = Aufteilen(bank, ziel);
            var z = Zaehlen(items);
            decimal kD = Kurs(bank, Muenze);
            if (kD <= 0) return plan;
            decimal andere = z[Gold] * Kurs(bank, Gold) + z[Silber] * Kurs(bank, Silber) + z[Schein] * Kurs(bank, Schein);
            decimal muenzen = Math.Floor((ziel - andere) / kD);
            bool genugMuenzen = muenzen >= Math.Min(MuenzenMinimum, plan[Muenze]);
            if (genugMuenzen && muenzen <= plan[Muenze] && andere + muenzen * kD == ziel)
            {
                z[Muenze] = (int)muenzen;
                return z;
            }
            return plan;
        }

        // ---------- Verbinden und Lösen (vom Fenster und der Börse benutzt) ----------

        /// <summary>Verbindet den Geldtresor an "pos" mit einer Firma oder Person. Geld, das noch darin lag, wird entfernt.</summary>
        public static void Verbinden(Bank bank, Unternehmen u, string pos) { Verbinden(bank, Halter.Von(u), pos); }

        public static void Verbinden(Bank bank, Halter h, string pos)
        {
            if (bank == null || h == null || string.IsNullOrEmpty(pos)) throw new WirtschaftsFehler("Niemand gewählt.");
            var alt = HalterAmGeldtresor(bank, pos);
            if (Halter.Gleich(alt, h)) return;
            if (alt != null) Loesen(bank, alt);                       // dieser Tresor gehörte jemand anderem
            if (!string.IsNullOrEmpty(h.Tresor)) Loesen(bank, h);     // er hatte schon einen anderen Tresor
            var te = Behaelter(pos);
            if (te != null && te.items != null) Leeren(te);
            h.Tresor = pos;
            h.Bestand = 0m;
            passtNicht.Remove(h.Schluessel);
            try { Abgleichen(bank, h); }
            catch (Exception e) { Log.Warning("[Wirtschaft] Geldtresor " + h.Name + ": " + e.Message); }
        }

        /// <summary>Löst den Geldtresor auf: die Gegenstände verschwinden, das Geld bleibt auf dem Konto.</summary>
        public static void Loesen(Bank bank, Unternehmen u) { if (u != null) Loesen(bank, Halter.Von(u)); }

        public static void Loesen(Bank bank, Halter h)
        {
            if (bank == null || h == null || string.IsNullOrEmpty(h.Tresor)) return;
            var te = Behaelter(h.Tresor);
            if (te != null && te.items != null)
            {
                // Was zuletzt noch herausgenommen oder hineingelegt wurde, vorher verbuchen.
                h.Konto += Wert(bank, te.items) - h.Bestand;
                Leeren(te);
            }
            h.Tresor = "";
            h.Bestand = 0m;
            passtNicht.Remove(h.Schluessel);
        }

        /// <summary>Zum Verbinden: zuerst Firmen (die mit dem nächsten Firmentresor zuerst), danach Privatpersonen nach Name.</summary>
        public static List<Halter> Vorschlaege(Bank bank, Vector3i pos)
        {
            if (bank == null) return new List<Halter>();
            var l = bank.Stand.Unternehmen.OrderBy(u => Entfernung(u, pos)).ThenBy(u => u.Name).Select(u => Halter.Von(u)).ToList();
            l.AddRange(bank.Personen().OrderBy(p => p.Name).Select(p => Halter.Von(p)));
            l.AddRange(bank.Regierungen().OrderBy(p => p.Name).Select(p => Halter.Von(p)));
            return l;
        }

        /// <summary>Entfernung des Firmentresors in Blöcken, oder int.MaxValue ohne Firmentresor.</summary>
        public static int Entfernung(Unternehmen u, Vector3i pos)
        {
            Vector3i t;
            if (!Lesen(u.Tresor, out t)) return int.MaxValue;
            long dx = t.x - pos.x, dy = t.y - pos.y, dz = t.z - pos.z;
            return (int)Math.Round(Math.Sqrt(dx * dx + dy * dy + dz * dz));
        }

        public static int Entfernung(Halter h, Vector3i pos) { return h.IstPerson ? int.MaxValue : Entfernung(h.Firma, pos); }

        /// <summary>Was gerade im Tresor liegt (oder liegen wird), als Text mit einer Zeile pro Sorte.</summary>
        public static string InhaltText(Bank bank, Unternehmen u) { return InhaltText(bank, Halter.Von(u)); }

        public static string InhaltText(Bank bank, Halter h)
        {
            Dictionary<string, int> z = null;
            var te = Behaelter(h.Tresor);
            if (te != null && te.items != null) z = Zaehlen(te.items);
            if (z == null) z = Aufteilen(bank, Math.Max(0m, Math.Floor(h.Konto)));
            return "Goldbarren:  " + Befehle.Betrag(z[Gold]) + "\n"
                 + "Silberbarren:  " + Befehle.Betrag(z[Silber]) + "\n"
                 + "Old Cash (Scheine):  " + Befehle.Betrag(z[Schein]) + "\n"
                 + "Dukes (Münzen):  " + Befehle.Betrag(z[Muenze]);
        }

        // ---------- Block-Ereignisse ----------

        /// <summary>Vor dem Öffnen des Inhalts: Tresor und Guthaben abgleichen.</summary>
        public static void VorDemOeffnen(Bank bank, Unternehmen u) { if (u != null) VorDemOeffnen(bank, Halter.Von(u)); }

        public static void VorDemOeffnen(Bank bank, Halter h)
        {
            if (bank == null || h == null) return;
            try { Abgleichen(bank, h); }
            catch (Exception e) { Log.Warning("[Wirtschaft] Geldtresor " + h.Name + ": " + e.Message); }
            WirtschaftMod.Speichern();
        }

        /// <summary>Beim Abbauen: Geld verschwindet aus dem Tresor, bleibt aber auf dem Konto.</summary>
        public static void BeimEntfernen(Vector3i pos3)
        {
            var bank = WirtschaftMod.Bank;
            if (bank == null) return;
            string pos = BlockHilfe.Position(pos3);
            var h = HalterAmGeldtresor(bank, pos);
            if (h != null) { Loesen(bank, h); WirtschaftMod.Speichern(); return; }
            var te = Behaelter(pos);
            if (te != null && te.items != null) Leeren(te);
        }

        public static Unternehmen FirmaAmGeldtresor(Bank bank, string pos)
        {
            if (bank == null || string.IsNullOrEmpty(pos)) return null;
            return bank.Stand.Unternehmen.FirstOrDefault(w => w.Geldtresor == pos);
        }

        /// <summary>Wem gehört der Geldtresor an dieser Stelle: einer Firma oder einer Person. Null = frei.</summary>
        public static Halter HalterAmGeldtresor(Bank bank, string pos)
        {
            if (bank == null || string.IsNullOrEmpty(pos)) return null;
            var u = FirmaAmGeldtresor(bank, pos);
            if (u != null) return Halter.Von(u);
            var p = bank.Stand.Parteien.FirstOrDefault(w => w.Geldtresor == pos);
            return p == null ? null : Halter.Von(p);
        }

        // ---------- Behälter ----------

        static TileEntityLootContainer Behaelter(string pos)
        {
            Vector3i p;
            if (!Lesen(pos, out p)) return null;
            var welt = GameManager.Instance == null ? null : GameManager.Instance.World;
            if (welt == null) return null;
            var chunk = welt.GetChunkFromWorldPos(p) as Chunk;
            if (chunk == null) return null;
            return chunk.GetTileEntity(World.toBlock(p)) as TileEntityLootContainer;
        }

        static bool Lesen(string pos, out Vector3i p)
        {
            p = Vector3i.zero;
            if (string.IsNullOrEmpty(pos)) return false;
            var teile = pos.Split(',');
            int x, y, z;
            if (teile.Length != 3 || !int.TryParse(teile[0], out x) || !int.TryParse(teile[1], out y) || !int.TryParse(teile[2], out z)) return false;
            p = new Vector3i(x, y, z);
            return true;
        }

        static string ItemName(ItemStack s)
        {
            if (s == null || s.IsEmpty() || s.itemValue == null) return null;
            var ic = s.itemValue.ItemClass;
            return ic == null ? null : ic.Name;
        }

        static bool IstGeld(string item) { return item != null && Array.IndexOf(Reihenfolge, item) >= 0; }

        static decimal Kurs(Bank bank, string item)
        {
            var w = bank.Stand.Waehrungen.FirstOrDefault(x => x.Item == item);
            return w == null ? 0m : w.Kurs;
        }

        /// <summary>Wert aller Geld-Gegenstände im Tresor, in Dukes.</summary>
        static decimal Wert(Bank bank, ItemStack[] items)
        {
            decimal summe = 0m;
            foreach (var s in items)
            {
                string n = ItemName(s);
                if (IstGeld(n)) summe += s.count * Kurs(bank, n);
            }
            return summe;
        }

        static Dictionary<string, int> Zaehlen(ItemStack[] items)
        {
            var z = new Dictionary<string, int>();
            foreach (var n in Reihenfolge) z[n] = 0;
            foreach (var s in items)
            {
                string n = ItemName(s);
                if (IstGeld(n)) z[n] += s.count;
            }
            return z;
        }

        static bool Gleich(ItemStack[] items, Dictionary<string, int> plan)
        {
            var z = Zaehlen(items);
            foreach (var n in Reihenfolge) if (z[n] != plan[n]) return false;
            return true;
        }

        static int Stapelgroesse(string item)
        {
            try
            {
                var ic = ItemClass.GetItemClass(item, true);
                if (ic != null && ic.Stacknumber != null) return Math.Max(1, ic.Stacknumber.Value);
            }
            catch (Exception) { }
            return 250;
        }

        static void Leeren(TileEntityLootContainer te)
        {
            var items = te.items;
            for (int i = 0; i < items.Length; i++)
                if (IstGeld(ItemName(items[i]))) items[i] = ItemStack.Empty.Clone();
            te.SetModified();
        }

        /// <summary>Ersetzt alle Geld-Gegenstände durch den Plan: Gold zuerst, dann Silber, Scheine, Dukes.</summary>
        static void Befuellen(TileEntityLootContainer te, Dictionary<string, int> plan)
        {
            var items = te.items;
            for (int i = 0; i < items.Length; i++)
                if (IstGeld(ItemName(items[i]))) items[i] = ItemStack.Empty.Clone();

            foreach (var n in Reihenfolge)
            {
                int rest = plan[n];
                if (rest <= 0) continue;
                var wert = ItemClass.GetItem(n, true);
                if (wert == null || wert.IsEmpty()) continue;
                int max = Stapelgroesse(n);
                for (int i = 0; i < items.Length && rest > 0; i++)
                {
                    if (items[i] != null && !items[i].IsEmpty()) continue;
                    int k = Math.Min(rest, max);
                    items[i] = new ItemStack(ItemClass.GetItem(n, true), k);
                    rest -= k;
                }
            }
            te.bTouched = true;
            te.SetModified();
        }
    }
}

/// <summary>Geldtresor (Banking): ein Behälter, der das Firmenguthaben als Münzen, Scheine und Barren zeigt.
/// E öffnet zuerst das Geldtresor-Fenster (Firma wählen, trennen, Inhalt öffnen).</summary>
public class BlockGeldtresor : BlockLoot
{
    public override string GetActivationText(WorldBase _world, BlockValue _blockValue, int _clrIdx, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        var bank = WirtschaftMod.Bank;
        var h = Geldtresor.HalterAmGeldtresor(bank, BlockHilfe.Position(_blockPos));
        if (h == null) return "[E] Geldtresor (nicht verbunden)";
        return "[E] Geldtresor " + h.Name + ": " + Befehle.Betrag(h.Konto) + " Dukes";
    }

    public override bool HasBlockActivationCommands(WorldBase _world, BlockValue _blockValue, int _clrIdx, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        return true;
    }

    public override BlockActivationCommand[] GetBlockActivationCommands(WorldBase _world, BlockValue _blockValue, int _clrIdx, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        return BlockHilfe.Befehl;
    }

    /// <summary>True, während der Behälter selbst geöffnet wird: dann alles an das Spiel durchreichen.</summary>
    static bool durchreichen;

    public override bool OnBlockActivated(string _commandName, WorldBase _world, int _cIdx, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        if (durchreichen) return base.OnBlockActivated(_commandName, _world, _cIdx, _blockPos, _blockValue, _player);
        return Fenster(_world, _cIdx, _blockPos, _blockValue, _player);
    }

    public override bool OnBlockActivated(WorldBase _world, int _clrIdx, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        if (durchreichen) return base.OnBlockActivated(_world, _clrIdx, _blockPos, _blockValue, _player);
        return Fenster(_world, _clrIdx, _blockPos, _blockValue, _player);
    }

    bool Fenster(WorldBase _world, int _clrIdx, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        if (XUiC_WbGeldtresor.Oeffnen(_player, _blockPos)) return true;
        // Notlösung, falls das Fenster nicht geladen ist: verbunden = Inhalt direkt öffnen
        return InhaltOeffnen(_world, _clrIdx, _blockPos, _blockValue, _player);
    }

    /// <summary>Öffnet den eigentlichen Behälter (die Gegenstände). Nur wenn eine Firma verbunden ist.</summary>
    public bool InhaltOeffnen(WorldBase _world, int _clrIdx, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        var bank = WirtschaftMod.Bank;
        var h = Geldtresor.HalterAmGeldtresor(bank, BlockHilfe.Position(_blockPos));
        if (h == null)
        {
            try { GameManager.ShowTooltip(_player, "Erst eine Firma oder Person verbinden.", false); } catch (Exception) { }
            return false;
        }
        Geldtresor.VorDemOeffnen(bank, h);
        durchreichen = true;
        try
        {
            if (base.OnBlockActivated(_world, _clrIdx, _blockPos, _blockValue, _player)) return true;
            return base.OnBlockActivated("Search", _world, _clrIdx, _blockPos, _blockValue, _player);
        }
        finally { durchreichen = false; }
    }

    /// <summary>Vom Fenster aus: Behälter an dieser Stelle öffnen.</summary>
    public static bool InhaltOeffnen(Vector3i pos, EntityPlayerLocal spieler)
    {
        var welt = GameManager.Instance == null ? null : GameManager.Instance.World;
        if (welt == null) return false;
        var bv = welt.GetBlock(pos);
        var block = bv.Block as BlockGeldtresor;
        if (block == null) return false;
        return block.InhaltOeffnen(welt, 0, pos, bv, spieler);
    }

    public override void OnBlockRemoved(WorldBase _world, Chunk _chunk, Vector3i _blockPos, BlockValue _blockValue)
    {
        try { Geldtresor.BeimEntfernen(_blockPos); }
        catch (Exception e) { Log.Warning("[Wirtschaft] Geldtresor abbauen: " + e.Message); }
        base.OnBlockRemoved(_world, _chunk, _blockPos, _blockValue);
    }
}
