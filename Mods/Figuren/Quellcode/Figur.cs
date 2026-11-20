// Figuren-Mod – die Figur (NPC) und der Block zum Aufstellen
// Geprüft gegen 7 Days to Die V 1.4 (b8): EntityNPC, Entity.GetActivationCommands / OnEntityActivated,
// EntityActivationCommand(text, icon, enabled), Block.PlaceBlock(WorldBase, BlockPlacement.Result, EntityAlive).
// Die Klassen liegen ohne Namespace: Das Spiel sucht "EntityFigur, Figuren" bzw. "FigurAufstellen, Figuren" (-> BlockFigurAufstellen).
using System;
using Figuren.Spiel;
using UnityEngine;

/// <summary>Stehende Figur: bewegt sich nicht, nimmt keinen Schaden, E = reden, E halten = einrichten.</summary>
public class EntityFigur : EntityNPC
{
    static readonly EntityActivationCommand[] Befehle =
    {
        new EntityActivationCommand("talk", "talk", true),
        new EntityActivationCommand("Einrichten", "search", true)
    };

    public override void PostInit()
    {
        base.PostInit();
        try { SetIgnoredByAI(true); } catch (Exception) { }
    }

    /// <summary>Figuren springen nicht. Die Bewegungshilfe des Spiels (wie bei Zombies) springt sonst über Hindernisse
    /// und über andere Figuren (Sprunghöhe = Größe der anderen Figur + 0,8 m) - das sah aus wie Fliegen.
    /// Nur im Wasser bleibt der Schwimmzug erlaubt.</summary>
    public int SpruengeGesperrt;
    bool sprungGeloggt;

    public override bool Jumping
    {
        get { return base.Jumping; }
        set
        {
            if (value && !isSwimming)
            {
                SpruengeGesperrt++;
                if (!sprungGeloggt) { sprungGeloggt = true; Log.Out("[Figuren] Sprung gesperrt #" + entityId + " (Figuren springen nicht)"); }
                if (base.Jumping) base.Jumping = false;
                return;
            }
            base.Jumping = value;
        }
    }

    public override string EntityName
    {
        get
        {
            try
            {
                var r = FigurenMod.RolleVon(this);
                if (r != null) return r.Name;
            }
            catch (Exception) { }
            return base.EntityName;
        }
    }

    /// <summary>Wer kann wodurch sterben (sonst nur Zucken, je nach Körperteil):
    ///  - Haupt- und Nebenrollen: nie.
    ///  - alle anderen Figuren: durch Spieler und durch starke Explosionen (ab 100 Schaden, z. B. Granate, Rakete, Mine).
    ///  - Wachmänner und Söldner zusätzlich gegenseitig (feindliche Söldner gegen Wachmänner/freundliche Söldner).
    ///  - Zombies, Tiere und andere Figuren: nie, nur Zucken.
    /// Schaden aus der Umgebung (Ersticken, Feuer am Boden, Fallen ...) zählt nicht.</summary>
    float letzteMeldung = -99f;

    public override int DamageEntity(DamageSource _damageSource, int _strength, bool _criticalHit, float impulseScale)
    {
        try
        {
            if (IsDead() || Waffen.IstLeiche(entityId) || _damageSource == null || world == null) return 0;
            int quelle = _damageSource.getEntityId();
            if (quelle == entityId) return 0;
            var angreifer = quelle >= 0 ? world.GetEntity(quelle) as EntityAlive : null;
            bool explosion = false;
            try { explosion = _damageSource.GetDamageType() == EnumDamageTypes.Heat && _strength >= 100; } catch (Exception) { }
            if (angreifer == null && !explosion) return 0;

            var k = FigurKoerper.Fuer(this);
            int teil = TrefferTeil(_damageSource, k);
            bool kopf = teil == 1 || _criticalHit;
            Vector3 richtung = angreifer != null ? position - angreifer.position : Vector3.zero;
            if (richtung.sqrMagnitude < 0.01f) { try { richtung = _damageSource.getDirection(); } catch (Exception) { } }
            if (richtung.sqrMagnitude < 0.01f) richtung = -transform.forward;
            float staerke = Mathf.Clamp(_strength / 30f, 0.4f, 1.6f) * (kopf ? 1.2f : 1f);
            Vector3 ort = position + Vector3.up * 1.2f;
            try { var h = _damageSource.getHitTransformPosition(); if (h != Vector3.zero) ort = h; } catch (Exception) { }

            bool verwundbar = Waffen.Verwundbar(this);
            string grund = "unverwundbar: Haupt-/Nebenrolle";
            if (verwundbar && !explosion && !(angreifer is EntityPlayer))
            {
                var af = angreifer as EntityFigur;
                if (af != null)
                {
                    var ich = FigurenMod.PlatzVon(this);
                    var er = FigurenMod.PlatzVon(af);
                    if (!(Waffen.KampfGruppe(ich) && Waffen.KampfGruppe(er))) { verwundbar = false; grund = "nur Zucken: nur Wachmann/Söldner können sich gegenseitig töten"; }
                }
                else { verwundbar = false; grund = "nur Zucken: Zombies/Tiere töten keine Figuren"; }
            }
            if (Time.time - letzteMeldung > 2f)
            {
                letzteMeldung = Time.time;
                Log.Out("[Figuren] Schaden an #" + entityId + " von #" + quelle + ": " + _strength + (explosion ? " (Explosion)" : "") + (verwundbar ? " (zählt)" : " (" + grund + ")"));
            }
            if (!verwundbar)
            {
                k.Treffer(richtung, teil, Mathf.Min(staerke, 1f));
                return 0;
            }
            Waffen.Partikel("blood_impact", ort, Quaternion.identity);
            if (!Waffen.Treffer(this, _strength, kopf, explosion))
            {
                k.Treffer(richtung, teil, staerke);
                return 0;
            }
            Waffen.Gestorben(this, richtung);
            return 0;
        }
        catch (Exception e) { Log.Warning("[Figuren] Schaden: " + e.Message); return 0; }
    }

    /// <summary>Getroffenes Körperteil: über die Treffer-Zonen des Spiels, sonst über den nächsten Knochen.</summary>
    int TrefferTeil(DamageSource ds, FigurKoerper k)
    {
        EnumBodyPartHit bp = EnumBodyPartHit.None;
        try { bp = ds.GetEntityDamageBodyPart(this); } catch (Exception) { }
        if ((bp & EnumBodyPartHit.Head) != 0) return 1;
        if ((bp & (EnumBodyPartHit.LeftUpperArm | EnumBodyPartHit.LeftLowerArm)) != 0) return 2;
        if ((bp & (EnumBodyPartHit.RightUpperArm | EnumBodyPartHit.RightLowerArm)) != 0) return 3;
        if ((bp & (EnumBodyPartHit.LeftUpperLeg | EnumBodyPartHit.LeftLowerLeg)) != 0) return 4;
        if ((bp & (EnumBodyPartHit.RightUpperLeg | EnumBodyPartHit.RightLowerLeg)) != 0) return 5;
        if ((bp & EnumBodyPartHit.Torso) != 0) return 0;
        try
        {
            var h = ds.getHitTransformPosition();
            if (h != Vector3.zero) return k.TeilBei(h);
        }
        catch (Exception) { }
        return 0;
    }

    public override bool canDespawn() { return false; }

    /// <summary>Leichen werden nicht gespeichert (sie verschwinden ohnehin nach 3 Minuten).</summary>
    public override bool IsSavedToFile() { return !Waffen.IstLeiche(entityId); }

    static readonly EntityActivationCommand[] Keine = new EntityActivationCommand[0];

    public override EntityActivationCommand[] GetActivationCommands(Vector3i _tePos, EntityAlive _entityFocusing)
    {
        if (Waffen.IstLeiche(entityId)) return Keine;
        return Befehle;
    }

    public override bool OnEntityActivated(int _indexInBlockActivationCommands, Vector3i _tePos, EntityAlive _entityFocusing)
    {
        var spieler = _entityFocusing as EntityPlayerLocal;
        if (spieler == null || Waffen.IstLeiche(entityId)) return false;
        Umher.Pause(this, 40f);
        try { SetRotationAndStopTurning(new Vector3(0f, FigurenMod.DrehungZu(position, spieler.position), 0f)); } catch (Exception) { }
        if (_indexInBlockActivationCommands == 1) return XUiC_FigEinrichten.Oeffnen(spieler, entityId);
        return FigurenMod.Reden(spieler, entityId);
    }
}

/// <summary>Block "Figur aufstellen": setzt keinen Block, sondern eine Figur, die zum Spieler schaut.</summary>
public class BlockFigurAufstellen : Block
{
    public override void PlaceBlock(WorldBase _world, BlockPlacement.Result _result, EntityAlive _ea)
    {
        try
        {
            var bp = _result.blockPos;
            var pos = new Vector3(bp.x + 0.5f, bp.y, bp.z + 0.5f);
            float dreh = _ea != null ? FigurenMod.DrehungZu(pos, _ea.position) : 0f;
            var spieler = _ea as EntityPlayerLocal;
            // Plausibilität: Figur braucht 2 Blöcke Höhe, sonst steckt der Kopf in der Decke
            try
            {
                var oben = _world.GetBlock(new Vector3i(bp.x, bp.y + 1, bp.z));
                if (!oben.isair && oben.Block != null && oben.Block.IsCollideMovement)
                {
                    if (spieler != null) GameManager.ShowTooltip(spieler, "Zu wenig Platz: Die Figur braucht 2 Blöcke Höhe.", false);
                    return;
                }
            }
            catch (Exception) { }
            var f = FigurenMod.Aufstellen("", pos, dreh);
            if (spieler != null)
            {
                int id = f.entityId;
                FigurenMod.Spaeter(() => XUiC_FigEinrichten.Oeffnen(spieler, id));
            }
        }
        catch (Exception e)
        {
            Log.Error("[Figuren] Aufstellen fehlgeschlagen: " + e);
        }
    }
}
