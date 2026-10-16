using BepInEx;
using UnityEngine;
using UnityEngine.SceneManagement;
using HarmonyLib;
using System.Reflection;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.IO;


namespace ULTRABOUGA
{
    [BepInPlugin("fr.bello.ultrabouga", "ULTRABOUGA", "0.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        AudioClip punchClip;
        AudioClip punchHitClip;
        AudioClip knucklePunchClip;
        AudioClip knucklePunchHitClip;
        AudioClip knuckleShockwaveClip;
        AudioSource sfxSource;

        void Awake()
        {
            Logger.LogInfo("Mod chargé !");
            new Harmony("fr.bello.ultrakillmod").PatchAll();

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.spatialBlend = 0f;
            StartCoroutine(LoadClip("assets/sounds/PunchSwoosh.ogg", AudioType.OGGVORBIS, c => punchClip = c));
            StartCoroutine(LoadClip("assets/sounds/Punch_heavy.ogg", AudioType.OGGVORBIS, c => punchHitClip = c));
            StartCoroutine(LoadClip("assets/sounds/KnuckleblasterPunch.ogg", AudioType.OGGVORBIS, c => knucklePunchClip = c));
            StartCoroutine(LoadClip("assets/sounds/Punch_heavy_1.wav", AudioType.WAV, c => knucklePunchHitClip = c));
            StartCoroutine(LoadClip("assets/sounds/Explosion_Wave.ogg", AudioType.OGGVORBIS, c => knuckleShockwaveClip = c));
        }

        IEnumerator LoadClip(string fileName, AudioType type, System.Action<AudioClip> done)
        {
            string path = Path.Combine(Path.GetDirectoryName(Info.Location), fileName);
            string url = new System.Uri(path).AbsoluteUri;

            using (var req = UnityWebRequestMultimedia.GetAudioClip(url, type))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Logger.LogError($"Impossible de charger {fileName} : {req.error}");
                    yield break;
                }
                var clip = DownloadHandlerAudioClip.GetContent(req);
                clip.name = Path.GetFileNameWithoutExtension(fileName);
                done(clip);
            }
        }

        static readonly AccessTools.FieldRef<MeleeAttack, Transform> HitPointRef =
            AccessTools.FieldRefAccess<MeleeAttack, Transform>("hitPoint");
        
        static readonly AccessTools.FieldRef<MeleeAttack, LayerMask> layerMaskEnnemies =
            AccessTools.FieldRefAccess<MeleeAttack, LayerMask>("targetMask");

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

        static string GetPath(Transform t)
        {
            string p = t.name;
            while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }

        public void PlaySound(AudioClip clip, float volume=1f)
        {
            if (clip != null) sfxSource.PlayOneShot(clip, volume);
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

        int DealMeleeDamage(float Damage, Transform hitPoint, float radius, float Knockback)
        {
            if (hitPoint == null || swordMelee == null)
            {
                return (0);
            }
            Collider[] array = Physics.OverlapSphere(hitPoint.position, radius, layerMaskEnnemies(swordMelee));
            HashSet<IDamageable> hashSet = new HashSet<IDamageable>();
            int targets = 0;
            foreach (Collider collider in array)
            {
                targets += 1;
                IDamageable componentInChildren = collider.GetComponentInChildren<IDamageable>();
                if (componentInChildren != null && !hashSet.Contains(componentInChildren))
                {
                    hashSet.Add(componentInChildren);
                    Vector3 normalized = (collider.transform.position - hitPoint.position).normalized;
                    componentInChildren.TakeDamage(Damage, normalized, Knockback);
                }
            }
            return (targets);
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
            float damage = (float)DamageGetter.Invoke(swordMelee, null);
            float radius = RadiusRef(swordMelee);
            float knockback = (float)KnockbackGetter.Invoke(swordMelee, null);
            int targets = this.DealMeleeDamage(damage , punchPoint, radius, knockback);
            if (targets == 0) this.PlaySound(punchClip, 1f);
            else this.PlaySound(punchHitClip, 1f);
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
            float damage = (float)DamageGetter.Invoke(swordMelee, null) * 3f;
            float radius = RadiusRef(swordMelee);
            float knockback = (float)KnockbackGetter.Invoke(swordMelee, null) * 3f;
            int targets = this.DealMeleeDamage(damage , punchPoint, radius, knockback);
            if (targets == 0) this.PlaySound(knucklePunchClip, 1f);
            else this.PlaySound(knucklePunchHitClip, 1f);
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
            float damage = (float)DamageGetter.Invoke(swordMelee, null);
            float radius = RadiusRef(swordMelee) * 5f;
            float knockback = (float)KnockbackGetter.Invoke(swordMelee, null) * 5f;
            this.DealMeleeDamage(damage , punchPoint, radius, knockback);    
            this.PlaySound(knuckleShockwaveClip, 1.5f);
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