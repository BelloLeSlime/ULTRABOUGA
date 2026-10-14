using BepInEx;
using UnityEngine;
using UnityEngine.SceneManagement;
using HarmonyLib;
using System.Reflection;
using UnityEngine.Networking;
using System.Collections;
using System;

namespace ULTRABOUGA
{
    [BepInPlugin("fr.bello.ultrabouga", "ULTRABOUGA", "0.1.0")]
    public class Plugin : BaseUnityPlugin
    {

        private IEnumerator LoadCustomSound(string filePath, string desiredName, float volume=1f, bool changePitch=false)
        {
            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + filePath, AudioType.WAV))
            {
                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Logger.LogError($"Erreur de chargement audio {desiredName} : {www.error}");
                    yield break;
                }

                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                clip.name = desiredName;
                SFX sfx = new SFX();
                sfx._clip = clip;
                sfx.volume = volume;
                sfx.changePitch = changePitch;

                SoundManager soundManager = FindAnyObjectByType<SoundManager>();
                if (soundManager == null)
                {
                    Logger.LogError("SoundManager introuvable dans la scène !");
                    yield break;
                }

                InjectCustomSound(soundManager, sfx);
            }
        }

        private void InjectCustomSound(SoundManager soundManager, SFX sfx)
        {
            
        }

        void Awake()
        {
            Logger.LogInfo("Mod chargé !");
            new Harmony("fr.bello.ultrakillmod").PatchAll();
            
        }

        static readonly AccessTools.FieldRef<MeleeAttack, Transform> HitPointRef =
            AccessTools.FieldRefAccess<MeleeAttack, Transform>("hitPoint");

        static readonly AccessTools.FieldRef<MeleeAttack, float> RadiusRef =
            AccessTools.FieldRefAccess<MeleeAttack, float>("radius");

        static readonly MethodInfo DamageGetter =
            AccessTools.PropertyGetter(typeof(MeleeAttack), "Damage");

        static readonly MethodInfo KnockbackGetter =
            AccessTools.PropertyGetter(typeof(MeleeAttack), "Knockback");

        MeleeAttack swordMelee;
        Transform punchPoint;
        float nextPunch;
        float nextPunchKnuckleBlaster;
        float nextShockwave;

        static float? damageOverride;
        static float? knockbackOverride;

        [HarmonyPatch(typeof(MeleeAttack), "Damage", MethodType.Getter)]
        class DamageGetterPatch
        {
            static void Postfix(ref float __result)
            {
                if (damageOverride.HasValue) __result = damageOverride.Value;
            }
        }

        [HarmonyPatch(typeof(MeleeAttack), "Knockback", MethodType.Getter)]
        class KnockbackGetterPatch
        {
            static void Postfix(ref float __result)
            {
                if (knockbackOverride.HasValue) __result = knockbackOverride.Value;
            }
        }

        static string GetPath(Transform t)
        {
            string p = t.name;
            while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }

        void Update()
        {
            //if (Input.GetKeyDown(KeyCode.F6)) DumpGameScripts();
            //if (Input.GetKeyDown(KeyCode.F7)) DumpRootObjects();

            if (Input.GetKeyDown(KeyCode.F8) && Physics.Raycast(Camera.main.transform.position,
                Camera.main.transform.forward, out RaycastHit h, 50f))
            Logger.LogInfo($"Touché : {h.collider.name}, layer = {LayerMask.LayerToName(h.collider.gameObject.layer)}");

            if (Input.GetKeyDown(KeyCode.F) && Time.time > nextPunch)
            {
                nextPunch = Time.time + 0.3f;
                nextPunchKnuckleBlaster = Time.time + 0.3f;
                nextShockwave = 0f;
                Punch();
            }
            if (Input.GetKeyDown(KeyCode.G) && Time.time > nextPunchKnuckleBlaster)
            {
                nextPunch = Time.time + 0.3f;
                nextPunchKnuckleBlaster = Time.time + 0.5f;
                nextShockwave = Time.time + 0.9f;
                PunchKnuckleBlaster();
            }
            if (Time.time > nextShockwave && Input.GetKey(KeyCode.G) && nextShockwave > 0f)
            {
                nextPunch = Time.time + 0.3f;
                nextPunchKnuckleBlaster = Time.time + 1.5f;
                nextShockwave = 0f;
                Shockwave();
            }
        }

        void Punch()
        {
            if (swordMelee == null)
            {
                foreach (var b in FindObjectsByType<HandAnimationBridge>(FindObjectsInactive.Include))
                    if (GetPath(b.transform).Contains("Hand"))
                        swordMelee = b.GetComponentInChildren<MeleeAttack>(true);
                if (swordMelee == null) return;
            }

            var cam = Camera.main.transform;
            if (punchPoint == null) punchPoint = new GameObject("PunchPoint").transform;
            punchPoint.position = cam.position + cam.forward * 1.5f;

            var original = HitPointRef(swordMelee);
            HitPointRef(swordMelee) = punchPoint;
            try { swordMelee.DealDamage(); }
            finally { HitPointRef(swordMelee) = original;}
        }

        void PunchKnuckleBlaster()
        {
            if (swordMelee == null)
            {
                foreach (var b in FindObjectsByType<HandAnimationBridge>(FindObjectsInactive.Include))
                    if (GetPath(b.transform).Contains("Hand"))
                        swordMelee = b.GetComponentInChildren<MeleeAttack>(true);
                if (swordMelee == null) return;
            }

            var cam = Camera.main.transform;
            if (punchPoint == null) punchPoint = new GameObject("PunchPoint").transform;
            punchPoint.position = cam.position + cam.forward * 1.5f;

            var original = HitPointRef(swordMelee);
            HitPointRef(swordMelee) = punchPoint;
            var originalDamage = (float)DamageGetter.Invoke(swordMelee, null);
            damageOverride = originalDamage * 3f;
            var originalKnockback = (float)KnockbackGetter.Invoke(swordMelee, null);
            knockbackOverride = originalKnockback * 3f;
            try { swordMelee.DealDamage(); }
            finally { HitPointRef(swordMelee) = original; damageOverride = null; knockbackOverride = null; }
        }

        void Shockwave()
        {
            if (swordMelee == null)
            {
                foreach (var b in FindObjectsByType<HandAnimationBridge>(FindObjectsInactive.Include))
                    if (GetPath(b.transform).Contains("Hand"))
                        swordMelee = b.GetComponentInChildren<MeleeAttack>(true);
                if (swordMelee == null) return;
            }

            var cam = Camera.main.transform;
            if (punchPoint == null) punchPoint = new GameObject("PunchPoint").transform;
            punchPoint.position = cam.position;

            var original = HitPointRef(swordMelee);
            HitPointRef(swordMelee) = punchPoint;
            var originalKnockback = (float)KnockbackGetter.Invoke(swordMelee, null);
            knockbackOverride = originalKnockback * 5f;
            var originalRadius = RadiusRef(swordMelee);
            RadiusRef(swordMelee) = originalRadius * 5f;
            try { swordMelee.DealDamage(); }
            finally { HitPointRef(swordMelee) = original; knockbackOverride = null; RadiusRef(swordMelee) = originalRadius; }
        }
    

        void DumpGameScripts()
        {
            var scripts = FindObjectsByType<MonoBehaviour>();
            foreach (var s in scripts)
            {
                var t = s.GetType();
                if (t.Assembly.GetName().Name != "Assembly-CSharp") continue;
                Logger.LogInfo($"{t.FullName}  ->  objet '{s.gameObject.name}'");
            }
        }

        void DumpRootObjects()
        {
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
                Logger.LogInfo($"Racine : {go.name}");
        }
        
        void OnGUI()
        {
            GUI.Label(new Rect(10, 10, 300, 25), $"Position du joueur : {FindAnyObjectByType<PlayerMain>()?.transform.position}");
        }

        [HarmonyPatch(typeof(PlayerMouvement), "UpdateInput")]
        class UpdateInputPatch
        {
            static bool wasHeld;

            static void Prefix(ref MouvementInput __0)
            {
                bool held = __0.Run;

                __0.Dash = held && !wasHeld;
                wasHeld = held;

                __0.Run = false;
                __0.RunSunstain = false;
            }
        }
        /*
        [HarmonyPatch(typeof(PlayerMouvement), "dashSpeed", MethodType.Getter)]
        class DashSpeedGetterPatch
        {
            static void Postfix(ref float __result)
            {
                __result = 400f;
            }
        }*/

        [HarmonyPatch(typeof(PlayerMain), "Init")]
        class PlayerInitPatch
        {
            static void Postfix(PlayerMain __instance)
            {
                __instance.Inventory.UpdateMoney(500000000);
            }
        }

        [HarmonyPatch(typeof(EnemyAI), "UpdateKnockback")]
        class KnockbackWallPatch
        {
            static void Prefix(EnemyAI __instance, out Vector3 __state)
            {
                __state = __instance.transform.position;        // position avant le déplacement
            }

            static void Postfix(EnemyAI __instance, Vector3 __state,
                                ref Vector3 ___knockbackVel, bool ___knockedBack)
            {
                if (!___knockedBack) return;

                LayerMask WhatIsWall = LayerMask.GetMask("WhatIsWall", "WhatIsGround");
                LayerMask Default = LayerMask.GetMask("Default", "WhatIsGround");
                LayerMask Throne = LayerMask.GetMask("Throne", "WhatIsGround");

                Transform t = __instance.transform;
                Vector3 delta = t.position - __state;
                Vector3 horizontal = new Vector3(delta.x, 0f, delta.z);
                float dist = horizontal.magnitude;
                if (dist < 0.0001f) return;

                Vector3 dir = horizontal / dist;
                Vector3 origin = __state + Vector3.up * 0.9f;

                if (Physics.SphereCast(origin, 0.35f, dir, out RaycastHit hit, dist + 0.05f, WhatIsWall, QueryTriggerInteraction.Ignore)
                || Physics.SphereCast(origin, 0.35f, dir, out hit, dist + 0.05f, Default, QueryTriggerInteraction.Ignore)
                || Physics.SphereCast(origin, 0.35f, dir, out hit, dist + 0.05f, Throne, QueryTriggerInteraction.Ignore)
                )
                {
                    float allowed = Mathf.Max(0f, hit.distance - 0.05f);
                    Vector3 pos = t.position;
                    pos.x = __state.x + dir.x * allowed;
                    pos.z = __state.z + dir.z * allowed;
                    t.position = pos;

                    Vector3 n = new Vector3(hit.normal.x, 0f, hit.normal.z).normalized;
                    ___knockbackVel = Vector3.ProjectOnPlane(___knockbackVel, n);
                }
            }
        }
    }
}