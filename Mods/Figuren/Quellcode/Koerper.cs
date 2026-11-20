// Figuren-Mod – Körpersteuerung direkt über die Knochen (ab 0.14.0)
// Die Animator-Controller der Modelle reagieren nicht zuverlässig auf Waffen-/Treffer-/Tod-Parameter. Deshalb stellt
// dieses Bauteil die Knochen NACH der Animation selbst (LateUpdate): Waffe anlegen und zielen (Arme über
// Zwei-Knochen-IK, Oberkörper dreht mit, Rückstoß), Blick zum Spieler, Zucken bei Treffern (je nach Körperteil),
// Umfallen beim Tod. Die Animation läuft darunter normal weiter; ohne Ziel/Treffer bleibt sie unverändert.
// Nur Unity-Grundfunktionen (Transform, Animator.GetBoneTransform, Light) - keine Spiel-Animationen nötig.
using System;
using System.Collections.Generic;
using Figuren.Spiel;
using UnityEngine;

[DefaultExecutionOrder(10000)]
public class FigurKoerper : MonoBehaviour
{
    public EntityFigur Figur;
    public bool Tot;

    Animator anim;
    Transform wurzel;
    Transform hueft, brust, hals, kopf, oaR, uaR, hR, oaL, uaL, hL, olR, ulR, fR, olL, ulL, fL;
    bool gesucht;
    float sucheWieder;
    float phase;

    // Zielen
    Vector3 zielPunkt;
    float zielZeit, zielGewicht, rueck;
    bool zweihand;
    Vector3 letzteRichtung = Vector3.forward;
    Transform waffe, muendung;
    Vector3 laufLokal = Vector3.forward, obenLokal = Vector3.up;
    float waffeSuche;
    Light blitz;
    float blitzZeit;

    // Gang (Typ 4): eigene Beinbewegung, falls das Modell beim Laufen keine Gehanimation zeigt
    Vector3 gangLetzte;
    bool gangStart;
    float tempo, gangPhase, gangGewicht;
    float fussSpanne, fussMin = 99f, fussMax = -99f, fussMessZeit;
    bool animGeht;   // Animation bewegt die Füße selbst -> nichts dazurechnen

    // Boden-Schalter des Animators (khz-Modelle): Das Spiel setzt "IsOnGround" nur bei Zombies (EntityHuman).
    // Ohne ihn kommt der Animator aus dem Sprung-Zustand nie wieder heraus (Arme hoch, Beine angewinkelt).
    int hIsOnGround, hCanFall, hJumpLandResponse, hIsClimbing, hJumpAir, hJumpStart, hDefault;
    bool hatIsOnGround, hatCanFall, hatJumpLandResponse, hatIsClimbing, hatDefault;
    float sprungZustandZeit;
    bool sprungGeloggt;
    public int SprungRaus;   // wie oft der Animator aus dem Sprung-Zustand geholt wurde

    // Pose-Schutz: Knochen, die die Animation in einem Frame nicht neu setzt, würden unsere Korrekturen aufsummieren
    Transform[] poseKnochen;
    Quaternion[] vorRot, nachRot;
    Vector3 hueftVor, hueftNach;
    bool poseGueltig, poseGeloggt;
    public int PoseFix;

    // Trefferzonen: eigene Kollider an den Knochen, falls das Modell keine hat
    public int ZonenDa, ZonenNeu;
    bool zonenFertig;

    // Blick
    float blick;
    Vector3 blickRichtung = Vector3.forward;

    // Treffer: Teil 0 Rumpf, 1 Kopf, 2 Arm links, 3 Arm rechts, 4 Bein links, 5 Bein rechts
    float trefferZeit = 99f, trefferStaerke;
    Vector3 trefferRichtung = Vector3.forward;
    int trefferTeil;

    // Tod
    float todZeit;
    Vector3 todRichtung, wurzelPos0, vorn0;
    Quaternion wurzelRot0;
    float todZufall;

    public static FigurKoerper Fuer(EntityFigur f)
    {
        if (f == null) return null;
        var k = f.gameObject.GetComponent<FigurKoerper>();
        if (k == null)
        {
            k = f.gameObject.AddComponent<FigurKoerper>();
            k.Figur = f;
            k.phase = (f.entityId % 97) * 0.37f;
            k.todZufall = (f.entityId % 2 == 0) ? 1f : -1f;
        }
        return k;
    }

    /// <summary>Auf diesen Punkt zielen (hält ca. 1,6 s an; jeden Frame neu aufrufen, solange ein Ziel da ist).</summary>
    public void Zielen(Vector3 punkt, bool beidhaendigLang)
    {
        zielPunkt = punkt;
        zweihand = beidhaendigLang;
        zielZeit = 1.6f;
    }

    /// <summary>Waffe ist oben und zeigt aufs Ziel - erst dann wird geschossen.</summary>
    public bool Bereit { get { return zielGewicht > 0.85f; } }

    /// <summary>Ort der Mündung (für Mündungsfeuer); Ersatz: vor der rechten Hand.</summary>
    public Vector3 MuendungsOrt()
    {
        if (muendung != null) return muendung.position;
        if (hR != null) return hR.position + letzteRichtung * 0.35f;
        return transform.position + Vector3.up * 1.4f + transform.forward * 0.5f;
    }

    public Vector3 SchussRichtung { get { return letzteRichtung; } }

    /// <summary>Knochen gefunden (sonst schießt die Figur ohne Arm-Steuerung, wie bisher).</summary>
    public bool HatKnochen { get { if (!gesucht) Suchen(); return anim != null && brust != null && oaR != null && uaR != null && hR != null; } }

    /// <summary>Körperteil am nächsten zum Trefferpunkt: 0 Rumpf, 1 Kopf, 2 Arm links, 3 Arm rechts, 4 Bein links, 5 Bein rechts.</summary>
    public int TeilBei(Vector3 punkt)
    {
        if (!gesucht) Suchen();
        if (anim == null) return 0;
        int best = 0;
        float bd = float.MaxValue;
        Transform[] t = { brust, hueft, kopf, uaL, hL, oaL, uaR, hR, oaR, ulL, olL, ulR, olR };
        int[] teil = { 0, 0, 1, 2, 2, 2, 3, 3, 3, 4, 4, 5, 5 };
        for (int i = 0; i < t.Length; i++)
        {
            if (t[i] == null) continue;
            float d = (t[i].position - punkt).sqrMagnitude;
            if (d < bd) { bd = d; best = teil[i]; }
        }
        return best;
    }

    public void Schuss()
    {
        rueck = 1f;
        try
        {
            if (blitz == null)
            {
                var go = new GameObject("FigurBlitz");
                go.transform.SetParent(transform, false);
                blitz = go.AddComponent<Light>();
                blitz.type = LightType.Point;
                blitz.color = new Color(1f, 0.75f, 0.4f);
                blitz.range = 5f;
                blitz.intensity = 2.5f;
                blitz.shadows = LightShadows.None;
            }
            blitz.transform.position = MuendungsOrt();
            blitz.enabled = true;
            blitzZeit = 0.06f;
        }
        catch (Exception) { }
    }

    public void Treffer(Vector3 richtung, int teil, float staerke)
    {
        if (Tot) return;
        richtung.y = 0f;
        if (richtung.sqrMagnitude < 0.0001f) richtung = -transform.forward;
        trefferRichtung = richtung.normalized;
        trefferTeil = teil;
        trefferStaerke = Mathf.Clamp(staerke, 0.4f, 1.6f);
        trefferZeit = 0f;
    }

    public void Sterben(Vector3 richtung)
    {
        if (Tot) return;
        if (!gesucht) Suchen();
        richtung.y = 0f;
        if (richtung.sqrMagnitude < 0.0001f) richtung = -transform.forward;
        todRichtung = richtung.normalized;
        vorn0 = transform.forward;
        vorn0.y = 0f;
        if (vorn0.sqrMagnitude < 0.01f) vorn0 = Vector3.forward;
        vorn0.Normalize();
        if (wurzel != null && wurzel != transform)
        {
            wurzelPos0 = transform.InverseTransformPoint(wurzel.position);
            wurzelRot0 = Quaternion.Inverse(transform.rotation) * wurzel.rotation;
        }
        todZeit = 0f;
        Tot = true;
        zielZeit = 0f;
        if (blitz != null) blitz.enabled = false;
        // Stand-Kollision klein machen, damit man über den Liegenden gehen kann (Bauteil bleibt an: das Spiel benutzt es weiter)
        try
        {
            foreach (var cc in GetComponentsInChildren<CharacterController>(true))
            {
                if (cc == null) continue;
                cc.height = 0.3f;
                cc.radius = 0.15f;
                cc.center = new Vector3(0f, 0.15f, 0f);
            }
        }
        catch (Exception) { }
    }

    // ---------- Knochen finden ----------
    void Suchen()
    {
        gesucht = true;
        anim = null;
        foreach (var a in GetComponentsInChildren<Animator>(true))
        {
            if (a != null && a.avatar != null) { anim = a; break; }
        }
        if (anim == null) anim = GetComponentInChildren<Animator>(true);
        if (anim == null) return;
        // immer animieren: sonst würden die Knochen-Korrekturen außerhalb des Bildes aufsummiert
        try { anim.cullingMode = AnimatorCullingMode.AlwaysAnimate; } catch (Exception) { }
        wurzel = anim.transform;
        var n = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in wurzel.GetComponentsInChildren<Transform>(true))
        {
            string k = t.name;
            int dp = k.LastIndexOf(':');
            if (dp >= 0) k = k.Substring(dp + 1);
            if (!n.ContainsKey(k)) n[k] = t;
        }
        bool mensch = false;
        try { mensch = anim.isHuman; } catch (Exception) { }
        hueft = B(mensch, HumanBodyBones.Hips, n, "Hips", "Pelvis");
        brust = B(mensch, HumanBodyBones.Chest, n, "Spine2", "Spine1", "Chest", "Spine");
        if (brust == null) brust = B(mensch, HumanBodyBones.Spine, n, "Spine");
        hals = B(mensch, HumanBodyBones.Neck, n, "Neck");
        kopf = B(mensch, HumanBodyBones.Head, n, "Head");
        oaR = B(mensch, HumanBodyBones.RightUpperArm, n, "RightArm", "RightUpperArm");
        uaR = B(mensch, HumanBodyBones.RightLowerArm, n, "RightForeArm", "RightLowerArm");
        hR = B(mensch, HumanBodyBones.RightHand, n, "RightHand");
        oaL = B(mensch, HumanBodyBones.LeftUpperArm, n, "LeftArm", "LeftUpperArm");
        uaL = B(mensch, HumanBodyBones.LeftLowerArm, n, "LeftForeArm", "LeftLowerArm");
        hL = B(mensch, HumanBodyBones.LeftHand, n, "LeftHand");
        olR = B(mensch, HumanBodyBones.RightUpperLeg, n, "RightUpLeg", "RightUpperLeg");
        ulR = B(mensch, HumanBodyBones.RightLowerLeg, n, "RightLeg", "RightLowerLeg");
        fR = B(mensch, HumanBodyBones.RightFoot, n, "RightFoot");
        olL = B(mensch, HumanBodyBones.LeftUpperLeg, n, "LeftUpLeg", "LeftUpperLeg");
        ulL = B(mensch, HumanBodyBones.LeftLowerLeg, n, "LeftLeg", "LeftLowerLeg");
        fL = B(mensch, HumanBodyBones.LeftFoot, n, "LeftFoot");
        ParameterSuchen();
        poseKnochen = null;
        Trefferzonen();
    }

    // ---------- Trefferzonen ----------
    /// <summary>Damit Kugeln die Figur treffen, braucht jedes Körperteil einen Kollider mit Spiel-Kennung "E_BP_...".
    /// Hat das Modell keine (z. B. khz-Modelle), werden eigene angelegt (Ebene 0, wie bei den Zombies des Spiels).
    /// Alle Kollider der Figur werden gegen ihre eigene Stand-Kollision ausgenommen - sonst hebt sie sich selbst an.</summary>
    void Trefferzonen()
    {
        if (zonenFertig || wurzel == null) return;
        zonenFertig = true;
        try
        {
            ZonenDa = 0;
            foreach (var c in wurzel.GetComponentsInChildren<Collider>(true))
            {
                if (c == null || c is CharacterController) continue;
                string t = "";
                try { t = c.gameObject.tag; } catch (Exception) { }
                if (t.StartsWith("E_BP_") && c.enabled && !c.isTrigger) ZonenDa++;
            }
            ZonenNeu = 0;
            if (ZonenDa < 4)
            {
                Zone(hueft, brust, "E_BP_Body", 0.16f);
                Zone(brust, hals != null ? hals : kopf, "E_BP_Body", 0.17f);
                Zone(kopf, null, "E_BP_Head", 0.13f);
                Zone(oaL, uaL, "E_BP_LArm", 0.06f);
                Zone(uaL, hL, "E_BP_LLowerArm", 0.05f);
                Zone(oaR, uaR, "E_BP_RArm", 0.06f);
                Zone(uaR, hR, "E_BP_RLowerArm", 0.05f);
                Zone(olL, ulL, "E_BP_LLeg", 0.08f);
                Zone(ulL, fL, "E_BP_LLowerLeg", 0.06f);
                Zone(olR, ulR, "E_BP_RLeg", 0.08f);
                Zone(ulR, fR, "E_BP_RLowerLeg", 0.06f);
            }
            // eigene Stand-Kollision nie mit den Körper-Kollidern zusammenstoßen lassen
            var ccs = GetComponentsInChildren<CharacterController>(true);
            foreach (var c in wurzel.GetComponentsInChildren<Collider>(true))
            {
                if (c == null || c is CharacterController) continue;
                foreach (var cc in ccs) if (cc != null) Physics.IgnoreCollision(cc, c, true);
            }
            Log.Out("[Figuren] Trefferzonen #" + (Figur != null ? Figur.entityId : -1) + ": vom Modell " + ZonenDa + ", neu angelegt " + ZonenNeu);
        }
        catch (Exception e) { Log.Warning("[Figuren] Trefferzonen: " + e.Message); }
    }

    void Zone(Transform a, Transform b, string tag, float r)
    {
        if (a == null) return;
        try
        {
            var go = new GameObject("FigurZone");
            go.transform.SetParent(a, false);
            go.layer = 0;
            go.tag = tag;
            // Knochen können skaliert sein (z. B. 0,01): Größen in Meter auf die Knochen-Einheit umrechnen
            Vector3 ls = a.lossyScale;
            float massstab = (Mathf.Abs(ls.x) + Mathf.Abs(ls.y) + Mathf.Abs(ls.z)) / 3f;
            if (massstab < 0.0001f) massstab = 1f;
            r /= massstab;
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            if (b == null)
            {
                var k = go.AddComponent<SphereCollider>();
                k.radius = r;
                // Kopfmitte liegt etwas über dem Kopfknochen
                k.center = a.InverseTransformPoint(a.position + Vector3.up * 0.08f);
            }
            else
            {
                Vector3 ende = a.InverseTransformPoint(b.position);
                var k = go.AddComponent<CapsuleCollider>();
                float ax = Mathf.Abs(ende.x), ay = Mathf.Abs(ende.y), az = Mathf.Abs(ende.z);
                k.direction = ax >= ay && ax >= az ? 0 : (ay >= az ? 1 : 2);
                float laenge = ende.magnitude;
                k.center = ende * 0.5f;
                k.height = laenge + r;
                k.radius = r;
            }
            ZonenNeu++;
        }
        catch (Exception e) { Log.Warning("[Figuren] Zone " + tag + ": " + e.Message); }
    }

    // ---------- Boden-Schalter / Sprung-Zustand ----------
    void ParameterSuchen()
    {
        hatIsOnGround = hatCanFall = hatJumpLandResponse = hatIsClimbing = hatDefault = false;
        if (anim == null) return;
        try
        {
            foreach (var p in anim.parameters)
            {
                if (p.name == "IsOnGround" && p.type == AnimatorControllerParameterType.Bool) { hIsOnGround = p.nameHash; hatIsOnGround = true; }
                else if (p.name == "CanFall" && p.type == AnimatorControllerParameterType.Bool) { hCanFall = p.nameHash; hatCanFall = true; }
                else if (p.name == "JumpLandResponse" && p.type == AnimatorControllerParameterType.Int) { hJumpLandResponse = p.nameHash; hatJumpLandResponse = true; }
                else if (p.name == "IsClimbing" && p.type == AnimatorControllerParameterType.Bool) { hIsClimbing = p.nameHash; hatIsClimbing = true; }
            }
            hJumpAir = Animator.StringToHash("Base Layer.JumpAir");
            hJumpStart = Animator.StringToHash("Base Layer.JumpStart");
            hDefault = Animator.StringToHash("Base Layer.Default");
            hatDefault = anim.HasState(0, hDefault);
        }
        catch (Exception) { }
    }

    /// <summary>Jeden Frame: Figur steht am Boden, kann nicht fallen, landet normal, klettert nicht.
    /// Hängt der Animator trotzdem länger als 1,5 s im Sprung, wird er in den Grundzustand überblendet.</summary>
    void BodenSchalter(float dt)
    {
        if (anim == null) return;
        if (hatIsOnGround) anim.SetBool(hIsOnGround, true);
        if (hatCanFall) anim.SetBool(hCanFall, false);
        if (hatJumpLandResponse) anim.SetInteger(hJumpLandResponse, 0);
        if (hatIsClimbing) anim.SetBool(hIsClimbing, false);
        var st = anim.GetCurrentAnimatorStateInfo(0);
        bool imSprung = st.fullPathHash == hJumpAir || st.fullPathHash == hJumpStart;
        sprungZustandZeit = imSprung ? sprungZustandZeit + dt : 0f;
        if (sprungZustandZeit > 1.5f && hatDefault)
        {
            sprungZustandZeit = 0f;
            SprungRaus++;
            anim.CrossFade(hDefault, 0.25f, 0);
            if (!sprungGeloggt) { sprungGeloggt = true; Log.Out("[Figuren] Sprung-Zustand #" + (Figur != null ? Figur.entityId : -1) + ": Animator in den Grundzustand geholt"); }
        }
    }

    // ---------- Pose-Schutz ----------
    static bool Gleich(Quaternion a, Quaternion b) { return a.x == b.x && a.y == b.y && a.z == b.z && a.w == b.w; }

    /// <summary>Vor unseren Korrekturen: Hat die Animation einen Knochen seit dem letzten Frame nicht neu gesetzt
    /// (er steht noch genau so, wie wir ihn verlassen haben), wird er auf den Stand vor unserer Korrektur zurückgestellt.</summary>
    void PoseVorher()
    {
        if (poseKnochen == null)
        {
            poseKnochen = new[] { hueft, brust, hals, kopf, oaR, uaR, hR, oaL, uaL, hL, olR, ulR, fR, olL, ulL, fL };
            vorRot = new Quaternion[poseKnochen.Length];
            nachRot = new Quaternion[poseKnochen.Length];
            poseGueltig = false;
        }
        int fix = 0;
        for (int i = 0; i < poseKnochen.Length; i++)
        {
            var t = poseKnochen[i];
            if (t == null) continue;
            Quaternion q = t.localRotation;
            if (poseGueltig && Gleich(q, nachRot[i]) && !Gleich(q, vorRot[i])) { t.localRotation = vorRot[i]; fix++; }
            vorRot[i] = t.localRotation;
        }
        if (hueft != null)
        {
            Vector3 p = hueft.localPosition;
            if (poseGueltig && p.x == hueftNach.x && p.y == hueftNach.y && p.z == hueftNach.z && (p.x != hueftVor.x || p.y != hueftVor.y || p.z != hueftVor.z)) { hueft.localPosition = hueftVor; fix++; }
            hueftVor = hueft.localPosition;
        }
        if (fix > 0)
        {
            PoseFix += fix;
            if (!poseGeloggt) { poseGeloggt = true; Log.Out("[Figuren] Pose-Schutz #" + (Figur != null ? Figur.entityId : -1) + ": " + fix + " Knochen ohne Animation zurückgestellt"); }
        }
    }

    void PoseNachher()
    {
        if (poseKnochen == null) return;
        for (int i = 0; i < poseKnochen.Length; i++)
            if (poseKnochen[i] != null) nachRot[i] = poseKnochen[i].localRotation;
        if (hueft != null) hueftNach = hueft.localPosition;
        poseGueltig = true;
    }

    Transform B(bool mensch, HumanBodyBones b, Dictionary<string, Transform> n, params string[] namen)
    {
        Transform t = null;
        if (mensch) { try { t = anim.GetBoneTransform(b); } catch (Exception) { t = null; } }
        if (t != null) return t;
        foreach (var s in namen) if (n.TryGetValue(s, out t) && t != null) return t;
        return null;
    }

    /// <summary>Für "npc körper": welche Knochen gefunden wurden.</summary>
    public string Bericht()
    {
        if (!gesucht) Suchen();
        if (anim == null) return "Kein Animator gefunden.";
        bool mensch = false;
        try { mensch = anim.isHuman; } catch (Exception) { }
        WaffeSuchen(true);
        return "Animator " + anim.name + (mensch ? " (Humanoid)" : " (Generic, Suche über Namen)")
            + "\nRumpf: Hüfte " + J(hueft) + ", Brust " + J(brust) + ", Hals " + J(hals) + ", Kopf " + J(kopf)
            + "\nArm rechts: " + J(oaR) + " / " + J(uaR) + " / " + J(hR) + "   Arm links: " + J(oaL) + " / " + J(uaL) + " / " + J(hL)
            + "\nBein rechts: " + J(olR) + " / " + J(ulR) + " / " + J(fR) + "   Bein links: " + J(olL) + " / " + J(ulL) + " / " + J(fL)
            + "\nTrefferzonen: vom Modell " + ZonenDa + ", von der Mod " + ZonenNeu
            + "\nBoden-Schalter: IsOnGround " + (hatIsOnGround ? "ja" : "nein") + ", aus Sprung geholt " + SprungRaus + "x, Pose-Schutz " + PoseFix + "x, Sprünge gesperrt " + (Figur != null ? Figur.SpruengeGesperrt : 0) + "x"
            + "\nWaffe: " + (waffe != null ? waffe.name + ", Lauf-Achse " + laufLokal + (muendung != null ? ", Mündung " + muendung.name : ", keine Mündung gefunden") : "keine am Haltepunkt Gunjoint");
    }

    static string J(Transform t) { return t != null ? t.name : "FEHLT"; }

    void WaffeSuchen(bool sofort)
    {
        waffeSuche -= Time.deltaTime;
        if (!sofort && waffeSuche > 0f && waffe != null) return;
        if (!sofort && waffeSuche > 0f) return;
        waffeSuche = 1f;
        Transform neu = null;
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (string.Equals(t.name, "Gunjoint", StringComparison.OrdinalIgnoreCase) && t.childCount > 0) { neu = t.GetChild(0); break; }
        }
        if (neu == waffe) return;
        waffe = neu;
        muendung = null;
        laufLokal = Vector3.forward;
        obenLokal = Vector3.up;
        if (waffe == null) return;
        foreach (var t in waffe.GetComponentsInChildren<Transform>(true))
        {
            if (t != waffe && t.name.ToLowerInvariant().Contains("muzzle")) { muendung = t; break; }
        }
        if (muendung != null)
        {
            // Lauf-Achse = die Achse der Waffe, die am ehesten zur Mündung zeigt
            Vector3 v = Quaternion.Inverse(waffe.rotation) * (muendung.position - waffe.position);
            float ax = Mathf.Abs(v.x), ay = Mathf.Abs(v.y), az = Mathf.Abs(v.z);
            if (ax + ay + az > 0.01f)
            {
                if (ax >= ay && ax >= az) laufLokal = new Vector3(Mathf.Sign(v.x), 0f, 0f);
                else if (ay >= ax && ay >= az) laufLokal = new Vector3(0f, Mathf.Sign(v.y), 0f);
                else laufLokal = new Vector3(0f, 0f, Mathf.Sign(v.z));
            }
        }
        if (Mathf.Abs(laufLokal.y) > 0.5f) obenLokal = Vector3.forward;
    }

    // ---------- Hilfen ----------
    static void Dreh(Transform t, Vector3 achse, float grad)
    {
        if (t == null || achse.sqrMagnitude < 1e-8f || Mathf.Abs(grad) < 0.001f) return;
        t.rotation = Quaternion.AngleAxis(grad, achse) * t.rotation;
    }

    static Vector3 Flach(Vector3 v) { v.y = 0f; return v; }

    static float Glatt(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

    /// <summary>Zwei-Knochen-IK: a = Oberarm/Oberschenkel, b = Unterarm/Unterschenkel, c = Hand/Fuß. pol = wohin Ellbogen/Knie zeigt.</summary>
    static void IK(Transform a, Transform b, Transform c, Vector3 ziel, Vector3 pol, float w)
    {
        if (a == null || b == null || c == null || w <= 0.001f) return;
        Quaternion la = a.localRotation, lb = b.localRotation;
        Vector3 pa = a.position, pb = b.position, pc = c.position;
        float lab = (pb - pa).magnitude, lcb = (pc - pb).magnitude;
        if (lab < 0.001f || lcb < 0.001f) return;
        float lat = Mathf.Clamp((ziel - pa).magnitude, 0.02f, (lab + lcb) * 0.995f);
        float w0 = Vector3.Angle(pc - pa, pb - pa);
        float w1 = Vector3.Angle(pa - pb, pc - pb);
        float s0 = Mathf.Acos(Mathf.Clamp((lcb * lcb - lab * lab - lat * lat) / (-2f * lab * lat), -1f, 1f)) * Mathf.Rad2Deg;
        float s1 = Mathf.Acos(Mathf.Clamp((lat * lat - lab * lab - lcb * lcb) / (-2f * lab * lcb), -1f, 1f)) * Mathf.Rad2Deg;
        Vector3 achse = Vector3.Cross(pc - pa, pb - pa);
        if (achse.sqrMagnitude < 1e-8f) achse = Vector3.Cross(pc - pa, pol - pa);
        if (achse.sqrMagnitude < 1e-8f) achse = Vector3.up;
        achse.Normalize();
        a.rotation = Quaternion.AngleAxis(s0 - w0, achse) * a.rotation;
        b.rotation = Quaternion.AngleAxis(s1 - w1, achse) * b.rotation;
        a.rotation = Quaternion.FromToRotation(c.position - pa, ziel - pa) * a.rotation;
        Vector3 n = (ziel - pa).normalized;
        Vector3 e = Vector3.ProjectOnPlane(b.position - pa, n), p = Vector3.ProjectOnPlane(pol - pa, n);
        if (e.sqrMagnitude > 1e-6f && p.sqrMagnitude > 1e-6f)
            a.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(e, p, n), n) * a.rotation;
        if (w < 0.999f)
        {
            Quaternion na = a.localRotation, nb = b.localRotation;
            a.localRotation = Quaternion.Slerp(la, na, w);
            b.localRotation = Quaternion.Slerp(lb, nb, w);
        }
    }

    /// <summary>Hüfte absenken, Füße bleiben stehen (Knie beugen sich).</summary>
    void Einknicken(float tiefe, Vector3 seite, Vector3 vorn)
    {
        if (hueft == null || olL == null || ulL == null || fL == null || olR == null || ulR == null || fR == null || tiefe < 0.002f) return;
        Vector3 pl = fL.position, pr = fR.position;
        Quaternion ql = fL.rotation, qr = fR.rotation;
        hueft.position += Vector3.down * tiefe + seite;
        IK(olL, ulL, fL, pl, ulL.position + vorn * 0.6f, 1f);
        IK(olR, ulR, fR, pr, ulR.position + vorn * 0.6f, 1f);
        fL.rotation = ql;
        fR.rotation = qr;
    }

    // ---------- jeden Frame nach der Animation ----------
    void LateUpdate()
    {
        try
        {
            if (Figur == null) return;
            float dt = Time.deltaTime;
            if (blitzZeit > 0f)
            {
                blitzZeit -= dt;
                if (blitzZeit <= 0f && blitz != null) blitz.enabled = false;
            }
            if (!gesucht) Suchen();
            else if (anim == null)
            {
                sucheWieder -= dt;
                if (sucheWieder <= 0f) { sucheWieder = 2f; Suchen(); }
            }
            if (anim == null || brust == null) return;
            PoseVorher();
            if (Tot) { TodSchritt(dt); PoseNachher(); return; }
            try { BodenSchalter(dt); } catch (Exception) { }

            Vector3 oben = Vector3.up;
            Vector3 vorn = Flach(transform.forward);
            if (vorn.sqrMagnitude < 0.01f) vorn = Vector3.forward;
            vorn.Normalize();
            Vector3 rechts = Vector3.Cross(oben, vorn);

            bool zielt = zielZeit > 0f;
            zielZeit -= dt;
            zielGewicht = Mathf.MoveTowards(zielGewicht, zielt ? 1f : 0f, dt / (zielt ? 0.32f : 0.55f));

            // leichtes Atmen/Gewicht verlagern, damit niemand wie eingefroren steht
            Dreh(brust, vorn, Mathf.Sin(Time.time * 0.55f + phase) * 0.9f);
            Dreh(brust, rechts, Mathf.Sin(Time.time * 1.3f + phase * 2f) * 0.5f);

            GangSchritt(dt, oben, vorn, rechts);
            Blick(dt, zielt, oben, vorn, rechts);
            TrefferSchritt(dt, oben, vorn, rechts);
            if (zielGewicht > 0.01f) ZielSchritt(oben, vorn);
            rueck = Mathf.MoveTowards(rueck, 0f, dt * 7f);
            PoseNachher();
        }
        catch (Exception) { }
    }

    /// <summary>Laufen: Tempo aus der Bewegung der Figur messen. Bewegt die Animation die Füße schon (Gehanimation da),
    /// bleibt alles wie es ist. Sonst Beine, Knie und Arme im Schrittrhythmus schwingen, Oberkörper leicht gegendrehen.</summary>
    void GangSchritt(float dt, Vector3 oben, Vector3 vorn, Vector3 rechts)
    {
        Vector3 p = Figur.position;
        if (!gangStart) { gangStart = true; gangLetzte = p; }
        float v = Flach(p - gangLetzte).magnitude / Mathf.Max(dt, 0.001f);
        gangLetzte = p;
        if (v > 8f) v = 0f;   // Sprung durch Teleport/Spawn
        tempo = Mathf.Lerp(tempo, v, Mathf.Clamp01(dt * 6f));

        // Füße der Animation beobachten (vor unseren Änderungen): schwingen sie beim Laufen, gibt es eine Gehanimation
        if (fL != null && wurzel != null && tempo > 0.5f)
        {
            float z = Vector3.Dot(fL.position - wurzel.position, vorn);
            fussMin = Mathf.Min(fussMin, z); fussMax = Mathf.Max(fussMax, z);
            fussMessZeit += dt;
            if (fussMessZeit > 1.2f)
            {
                fussSpanne = fussMax - fussMin;
                animGeht = fussSpanne > 0.18f;
                fussMessZeit = 0f; fussMin = 99f; fussMax = -99f;
            }
        }

        gangGewicht = Mathf.MoveTowards(gangGewicht, tempo > 0.3f && !animGeht ? 1f : 0f, dt * 4f);
        if (gangGewicht < 0.01f) return;
        gangPhase += dt * tempo * (2f * Mathf.PI / 1.4f);   // ein Doppelschritt pro 1,4 m
        float a = Mathf.Clamp(tempo / 1.6f, 0.35f, 1.2f) * 30f * gangGewicht;
        float s = Mathf.Sin(gangPhase);
        // Oberschenkel (vorn = negativer Winkel um "rechts"), Knie beugen beim Vorschwingen
        Dreh(olL, rechts, -s * a);
        Dreh(olR, rechts, s * a);
        Dreh(ulL, rechts, Mathf.Max(0f, Mathf.Sin(gangPhase + 1.3f)) * a * 1.3f);
        Dreh(ulR, rechts, Mathf.Max(0f, -Mathf.Sin(gangPhase + 1.3f)) * a * 1.3f);
        // Füße abrollen: vorn Zehen hoch, hinten Ferse hoch
        Dreh(fL, rechts, s * a * 0.45f);
        Dreh(fR, rechts, -s * a * 0.45f);
        // Becken: dreht mit dem Schritt, kippt zur Standbein-Seite, federt auf und ab (zwei Mal pro Doppelschritt)
        Dreh(hueft, oben, s * a * 0.14f);
        Dreh(hueft, vorn, Mathf.Cos(gangPhase) * a * 0.10f);
        float g = gangGewicht * Mathf.Clamp01(tempo / 1.2f);
        if (hueft != null) hueft.position += oben * (-Mathf.Abs(Mathf.Cos(gangPhase)) * 0.035f * g) + rechts * (Mathf.Cos(gangPhase) * 0.018f * g);
        // Oberkörper gegendrehen, leicht nach vorn lehnen, Kopf bleibt ruhig
        Dreh(brust, oben, -s * a * 0.22f);
        Dreh(brust, rechts, -3f * g - Mathf.Clamp01((tempo - 2f) / 2f) * 6f);
        if (kopf != null) Dreh(kopf, oben, s * a * 0.10f);
        // Arme gegengleich mit leicht gebeugtem Ellbogen, nicht beim Zielen
        float arm = a * 0.6f * (1f - zielGewicht);
        if (arm > 0.5f)
        {
            Dreh(oaL, rechts, s * arm);
            Dreh(oaR, rechts, -s * arm);
            Dreh(uaL, rechts, -(8f + Mathf.Max(0f, -s) * 14f) * (arm / 18f));
            Dreh(uaR, rechts, -(8f + Mathf.Max(0f, s) * 14f) * (arm / 18f));
        }
    }

    public string GangBericht()
    {
        return "Gang: Tempo " + tempo.ToString("0.0") + " m/s, Fuß-Schwung der Animation " + fussSpanne.ToString("0.00") + " m -> "
            + (animGeht ? "Modell hat eigene Gehanimation" : "eigene Beinbewegung der Mod") ;
    }

    void Blick(float dt, bool zielt, Vector3 oben, Vector3 vorn, Vector3 rechts)
    {
        if (kopf == null) return;
        float soll = 0f;
        if (!zielt)
        {
            EntityPlayerLocal sp = null;
            try { sp = GameManager.Instance.World.GetPrimaryPlayer(); } catch (Exception) { }
            if (sp != null)
            {
                Vector3 d = sp.position + oben * 1.6f - kopf.position;
                Vector3 df = Flach(d);
                if (d.sqrMagnitude < 49f && df.sqrMagnitude > 0.04f && Vector3.Dot(df.normalized, vorn) > 0.15f)
                {
                    soll = 1f;
                    blickRichtung = Vector3.Slerp(blickRichtung, d.normalized, Mathf.Clamp01(dt * 5f));
                }
            }
        }
        blick = Mathf.MoveTowards(blick, soll, dt * 1.8f);
        if (blick < 0.01f) return;
        float g = Glatt(blick);
        Vector3 bf = Flach(blickRichtung);
        if (bf.sqrMagnitude < 0.0001f) return;
        float gier = Mathf.Clamp(Vector3.SignedAngle(vorn, bf, oben), -65f, 65f) * g;
        float neig = Mathf.Clamp(Mathf.Atan2(blickRichtung.y, bf.magnitude) * Mathf.Rad2Deg, -25f, 25f) * g;
        Dreh(hals, oben, gier * 0.35f);
        Dreh(kopf, oben, gier * 0.55f);
        Dreh(kopf, Quaternion.AngleAxis(gier, oben) * rechts, -neig * 0.8f);
    }

    void TrefferSchritt(float dt, Vector3 oben, Vector3 vorn, Vector3 rechts)
    {
        trefferZeit += dt;
        if (trefferZeit > 1.3f) return;
        float h = trefferZeit < 0.06f ? trefferZeit / 0.06f : Mathf.Exp(-(trefferZeit - 0.06f) * 5.5f);
        h *= trefferStaerke;
        Vector3 achse = Vector3.Cross(oben, trefferRichtung);   // dreht "oben" in Flugrichtung der Kugel
        if (trefferTeil == 1)
        {
            Dreh(kopf, achse, 26f * h);
            Dreh(hals, achse, 10f * h);
            Dreh(brust, achse, 9f * h);
        }
        else if (trefferTeil == 2 || trefferTeil == 3)
        {
            bool links = trefferTeil == 2;
            Dreh(brust, oben, (links ? -1f : 1f) * 15f * h);
            Dreh(brust, achse, 6f * h);
            Dreh(links ? oaL : oaR, achse, 38f * h);
            Dreh(links ? uaL : uaR, achse, 20f * h);
        }
        else if (trefferTeil == 4 || trefferTeil == 5)
        {
            Einknicken(0.17f * Mathf.Min(h, 1.2f), rechts * ((trefferTeil == 4 ? -1f : 1f) * 0.05f * Mathf.Min(h, 1.2f)), vorn);
            Dreh(brust, rechts, 12f * h);
        }
        else
        {
            Dreh(brust, achse, 15f * h);
            Dreh(kopf, achse, -5f * h);
            Dreh(oaL, achse, 8f * h);
            Dreh(oaR, achse, 8f * h);
        }
        // starker Treffer: kurz das Gleichgewicht verlieren
        if (trefferStaerke >= 1.25f && trefferTeil != 4 && trefferTeil != 5)
            Einknicken(0.09f * Mathf.Min(h, 1.3f), trefferRichtung * (0.05f * Mathf.Min(h, 1.3f)), vorn);
    }

    void ZielSchritt(Vector3 oben, Vector3 vorn)
    {
        if (oaR == null || uaR == null || hR == null) return;
        float w = Glatt(zielGewicht);
        Vector3 mitte = oaL != null ? (oaR.position + oaL.position) * 0.5f : brust.position;
        Vector3 r = zielPunkt - mitte;
        Vector3 rf = Flach(r);
        if (rf.sqrMagnitude < 0.01f) rf = vorn;
        float gier = Mathf.Clamp(Vector3.SignedAngle(vorn, rf, oben), -50f, 50f);
        float neig = Mathf.Clamp(Mathf.Atan2(r.y, rf.magnitude) * Mathf.Rad2Deg, -40f, 40f);
        Vector3 zf = Quaternion.AngleAxis(gier, oben) * vorn;
        Vector3 zr = Vector3.Cross(oben, zf);
        Vector3 richt = Quaternion.AngleAxis(-neig, zr) * zf;
        Vector3 hoch = Vector3.Cross(richt, zr);
        letzteRichtung = richt;

        // Oberkörper dreht und neigt sich mit, Rückstoß drückt ihn kurz zurück
        Dreh(brust, oben, gier * 0.5f * w);
        Dreh(brust, zr, (-neig * 0.35f - rueck * 3f) * w);

        Vector3 schR = oaR.position;
        Vector3 sm = oaL != null ? (oaR.position + oaL.position) * 0.5f : schR - zr * 0.18f;
        Vector3 zielR = zweihand
            ? schR + richt * 0.27f - hoch * 0.10f - zr * 0.05f
            : sm + richt * 0.50f + hoch * 0.03f;
        zielR += -richt * (rueck * 0.07f) + hoch * (rueck * 0.025f);
        IK(oaR, uaR, hR, zielR, schR - oben * 0.5f + zr * 0.3f, w);

        // Waffe (hängt an der Hand) auf das Ziel ausrichten
        WaffeSuchen(false);
        if (waffe != null && waffe.parent != null)
        {
            Quaternion grund;
            if (!Waffen.GrundDrehung(Figur.entityId, out grund)) grund = waffe.localRotation;
            Quaternion wr = waffe.parent.rotation * grund;
            Vector3 lauf = wr * laufLokal, wo = wr * obenLokal;
            Quaternion q1 = Quaternion.FromToRotation(lauf, richt);
            Vector3 wo1 = Vector3.ProjectOnPlane(q1 * wo, richt);
            float roll = wo1.sqrMagnitude > 1e-6f ? Vector3.SignedAngle(wo1, hoch, richt) : 0f;
            Quaternion vorher = hR.rotation;
            Quaternion soll = Quaternion.AngleAxis(roll, richt) * q1 * vorher;
            hR.rotation = Quaternion.Slerp(vorher, soll, w);
        }

        // linke Hand: bei langen Waffen vorn am Schaft, bei der Pistole an der rechten Hand
        if (oaL != null && uaL != null && hL != null)
        {
            Vector3 zielL = zweihand
                ? hR.position + richt * 0.30f + hoch * 0.04f
                : hR.position - zr * 0.05f - hoch * 0.02f;
            IK(oaL, uaL, hL, zielL, oaL.position - oben * 0.5f - zr * 0.3f, w);
        }
    }

    void TodSchritt(float dt)
    {
        try { if (anim != null) anim.speed = 0f; } catch (Exception) { }   // Pose einfrieren; die Knochen stellen wir selbst
        todZeit += dt;
        float t = Mathf.Clamp01(todZeit / 0.85f);
        float fall = t * t;   // beschleunigt wie ein fallender Körper
        float winkel = 84f * fall;
        if (todZeit > 0.85f) winkel -= Mathf.Sin(Mathf.Clamp01((todZeit - 0.85f) / 0.3f) * Mathf.PI) * 3f;   // kurzes Nachfedern
        Vector3 oben = Vector3.up;
        Vector3 achse = Vector3.Cross(oben, todRichtung);
        Quaternion R = Quaternion.AngleAxis(winkel, achse);
        if (wurzel != null && wurzel != transform)
        {
            Vector3 fuss = transform.position;
            Vector3 p0 = transform.TransformPoint(wurzelPos0);
            wurzel.position = fuss + R * (p0 - fuss) + oben * (0.12f * fall);
            wurzel.rotation = R * (transform.rotation * wurzelRot0);
        }
        // erst knicken die Knie ein, dann werden Arme, Bein und Kopf schlaff
        float s = Glatt(todZeit / 0.7f);
        Vector3 v = R * vorn0, re = R * Vector3.Cross(oben, vorn0), ob = R * oben;
        Dreh(brust, re, -7f * s);
        Dreh(oaR, v, (30f + 12f * todZufall) * s);
        Dreh(oaL, v, -(34f - 10f * todZufall) * s);
        Dreh(uaR, re, -18f * s);
        Dreh(todZufall > 0f ? ulR : ulL, re, 36f * s);
        Dreh(todZufall > 0f ? olL : olR, v, todZufall * 10f * s);
        Dreh(kopf, ob, 26f * todZufall * s);
    }
}
