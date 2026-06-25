using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AestikModLoader.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[assembly: ModInfo("adh-aestik-expansion-v1", "Aestik Expansion V1", "1.9.0", Author = "ADH", Description = "Aestik expansion pack for ADH with lightweight enemy recoil and native enemy lifecycle support.")]

namespace ADH.EnemyHpBar
{
    public sealed class AestikEnemyHpBarMod : IAestikMod
    {
        internal static ModContext Context;
        internal static Action<string> Log;
        internal static GameObject RootObject;
        internal static HpBarManager Manager;
        private static bool bootstrapSubscribed;

        public void Initialize(ModContext context)
        {
            Context = context;
            Log = context.Log;
            EnsureBootstrapSubscription();
            TryCreateRootForLoadedScene(SceneManager.GetActiveScene());
            WriteLog("Aestik Expansion V1 armed.");
        }

        public void Shutdown()
        {
            if (bootstrapSubscribed)
            {
                SceneManager.sceneLoaded -= OnBootstrapSceneLoaded;
                bootstrapSubscribed = false;
            }

            if (RootObject != null)
            {
                UnityEngine.Object.Destroy(RootObject);
            }

            RootObject = null;
            Manager = null;
            WriteLog("Aestik Expansion V1 shutdown.");
        }

        private static void EnsureBootstrapSubscription()
        {
            if (bootstrapSubscribed)
            {
                return;
            }

            SceneManager.sceneLoaded += OnBootstrapSceneLoaded;
            bootstrapSubscribed = true;
        }

        private static void OnBootstrapSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryCreateRootForLoadedScene(scene);
        }

        private static void TryCreateRootForLoadedScene(Scene scene)
        {
            if (RootObject != null)
            {
                return;
            }

            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            RootObject = new GameObject("ADH Aestik Health Bars");
            UnityEngine.Object.DontDestroyOnLoad(RootObject);
            Manager = RootObject.AddComponent<HpBarManager>();
            WriteLog("Aestik Expansion V1 initialized in scene '" + scene.name + "'.");
        }

        internal static void WriteLog(string message)
        {
            if (Log != null)
            {
                Log("[ADH Health] " + message);
            }
        }
    }

    internal sealed class HpBarManager : MonoBehaviour
    {
        private const float ScanInterval = 2.5f;
        private const float RecoilScanInterval = 0.2f;
        private const float RecoilForce = 0.6f;
        private const float RecoilVerticalLift = 0.04f;
        private const float RecoilFallbackOffset = 0.08f;
        private const int BossThreshold = 200;
        private static readonly string[] KnownBossTokens = new string[]
        {
            "boss",
            "bronker",
            "glyb",
            "ovay",
            "ooph",
            "don_",
            "gonboss",
            "generalofneychah",
            "neychah",
            "ouvo",
            "floatingboss",
            "grottaria"
        };
        private static readonly string[] KnownEnemyTokens = new string[]
        {
            "bluggler",
            "bluggworm",
            "apexbluggworm",
            "bubbler",
            "glubbler",
            "mysploder",
            "corglyb",
            "corooph",
            "brimstalker",
            "squishplant",
            "bronker",
            "residentneychah",
            "guardneychah",
            "mystsorcneychah",
            "generalofneychah",
            "daughterofneychah",
            "roguegnoblat",
            "scoundrelghoo",
            "grandoddzard",
            "jumpkin",
            "corgnoblat",
            "heartleech",
            "ancientparasite",
            "anglerfish",
            "armedfish",
            "devilfish",
            "spitfish",
            "satanfish",
            "spiritfish",
            "celestialfish",
            "trubbler",
            "grottaria",
            "ovay",
            "cyberworm",
            "evoworm",
            "spiritheartdevotee",
            "ouvo",
            "neychah",
            "hermitbluggler",
            "clobbernaut",
            "celestialtraveler",
            "celestialmoon",
            "madcelestial",
            "fadingmemory",
            "oddmundsgrief"
        };

        private float scanTimer;
        private int warmupScansRemaining;
        private int lastSeenTargetCount;
        private bool loggedNoTargets;
        private float recoilTimer;
        private readonly List<string> activeBossKeys = new List<string>();
        private readonly Dictionary<string, int> activeBossLookup = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<int, EnemyRecoilTracker> recoilTrackers = new Dictionary<int, EnemyRecoilTracker>();
        private Sprite bgSprite;
        private Sprite fgSprite;
        private Sprite mgSprite;
        private Sprite olSprite;
        private Sprite bossBgSprite;
        private Sprite bossFgSprite;
        private Sprite bossOlSprite;
        private Canvas overlayCanvas;
        private RectTransform overlayRoot;

        internal Canvas OverlayCanvas
        {
            get { return overlayCanvas; }
        }

        internal RectTransform OverlayRoot
        {
            get { return overlayRoot; }
        }

        internal Sprite BgSprite
        {
            get { return bgSprite; }
        }

        internal Sprite FgSprite
        {
            get { return fgSprite; }
        }

        internal Sprite MgSprite
        {
            get { return mgSprite; }
        }

        internal Sprite OlSprite
        {
            get { return olSprite; }
        }

        internal Sprite BossBgSprite
        {
            get { return bossBgSprite; }
        }

        internal Sprite BossFgSprite
        {
            get { return bossFgSprite; }
        }

        internal Sprite BossOlSprite
        {
            get { return bossOlSprite; }
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            EnsureCanvas();
            LoadSprites();
            warmupScansRemaining = 4;
            recoilTimer = 0f;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnemyLifecycleBridge.EnemyHealthStarted += OnEnemyHealthStarted;
            EnemyLifecycleBridge.EnemyHealthTriggered += OnEnemyHealthTriggered;
            ScanForEnemies();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            EnemyLifecycleBridge.EnemyHealthStarted -= OnEnemyHealthStarted;
            EnemyLifecycleBridge.EnemyHealthTriggered -= OnEnemyHealthTriggered;
        }

        private void Update()
        {
            scanTimer -= Time.unscaledDeltaTime;
            if (scanTimer <= 0f)
            {
                if (warmupScansRemaining > 0)
                {
                    warmupScansRemaining--;
                    scanTimer = 0.25f;
                }
                else
                {
                    scanTimer = ScanInterval;
                }

                ScanForEnemies();
            }

            recoilTimer -= Time.unscaledDeltaTime;
            if (recoilTimer <= 0f)
            {
                recoilTimer = RecoilScanInterval;
                UpdateEnemyRecoil();
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            activeBossKeys.Clear();
            activeBossLookup.Clear();
            recoilTrackers.Clear();
            lastSeenTargetCount = 0;
            loggedNoTargets = false;
            warmupScansRemaining = 4;
            scanTimer = 0.1f;
            recoilTimer = 0.1f;
        }

        private void EnsureCanvas()
        {
            if (overlayCanvas != null)
            {
                return;
            }

            GameObject canvasObject = new GameObject("ADH Enemy HP Canvas");
            canvasObject.transform.SetParent(transform, false);
            overlayCanvas = canvasObject.AddComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = 35000;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();
            overlayRoot = canvasObject.GetComponent<RectTransform>();
            overlayRoot.anchorMin = Vector2.zero;
            overlayRoot.anchorMax = Vector2.one;
            overlayRoot.offsetMin = Vector2.zero;
            overlayRoot.offsetMax = Vector2.zero;
        }

        private void LoadSprites()
        {
            bgSprite = LoadSprite("bg.png");
            fgSprite = LoadSprite("fg.png");
            mgSprite = LoadSprite("mg.png");
            olSprite = LoadSprite("ol.png");
            bossBgSprite = LoadSprite("bossbg.png");
            bossFgSprite = LoadSprite("bossfg.png");
            bossOlSprite = LoadSprite("bossol.png");
        }

        private static Sprite LoadSprite(string fileName)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string resourceName = assembly.GetManifestResourceNames().FirstOrDefault(
                delegate(string name) { return name.EndsWith(fileName, StringComparison.OrdinalIgnoreCase); });

            if (string.IsNullOrEmpty(resourceName))
            {
                throw new FileNotFoundException("Embedded resource was not found.", fileName);
            }

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    throw new FileNotFoundException("Embedded resource stream was not found.", resourceName);
                }

                byte[] data = new byte[stream.Length];
                stream.Read(data, 0, data.Length);
                Texture2D texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                texture.LoadImage(data);
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), Vector2.zero);
            }
        }

        private void ScanForEnemies()
        {
            List<EnemyTargetHandle> enemies = EnemyTargetHandle.FindAll();
            if (enemies.Count == 0)
            {
                if (!loggedNoTargets)
                {
                    loggedNoTargets = true;
                }

                return;
            }

            if (lastSeenTargetCount != enemies.Count)
            {
                AestikEnemyHpBarMod.WriteLog("Detected " + enemies.Count + " enemy health target(s).");
                lastSeenTargetCount = enemies.Count;
            }

            loggedNoTargets = false;

            for (int i = 0; i < enemies.Count; i++)
            {
                AttachEnemyTarget(enemies[i], false);
            }
        }

        private static bool ShouldTrack(EnemyTargetHandle enemy)
        {
            if (enemy == null || enemy.Target == null)
            {
                return false;
            }

            GameObject gameObject = enemy.Target.gameObject;
            if (gameObject == null || !gameObject.activeInHierarchy)
            {
                return false;
            }

            if (enemy.Target.hideFlags != HideFlags.None)
            {
                return false;
            }

            if (enemy.IsDead)
            {
                return false;
            }

            if (enemy.NativeTarget == null && !LooksLikeEnemy(gameObject.name) && !LooksLikeBoss(gameObject.name))
            {
                return false;
            }

            return enemy.MaxHealth > 0f;
        }

        internal int RegisterBoss(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return 0;
            }

            if (!activeBossLookup.ContainsKey(key))
            {
                activeBossKeys.Add(key);
                activeBossLookup[key] = activeBossKeys.Count - 1;
            }

            return activeBossLookup[key];
        }

        internal void UnregisterBoss(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            int index;
            if (!activeBossLookup.TryGetValue(key, out index))
            {
                return;
            }

            activeBossLookup.Remove(key);
            activeBossKeys.Remove(key);

            for (int i = 0; i < activeBossKeys.Count; i++)
            {
                activeBossLookup[activeBossKeys[i]] = i;
            }
        }

        internal int GetBossIndex(string key)
        {
            int index;
            if (!activeBossLookup.TryGetValue(key, out index))
            {
                return RegisterBoss(key);
            }

            return index;
        }

        private static void EnsureRelay(GameObject anchor, EnemyTargetHandle enemy)
        {
            if (anchor == null || enemy == null || enemy.Target == null)
            {
                return;
            }

            HealthTargetRelay relay = anchor.GetComponent<HealthTargetRelay>();
            if (relay == null)
            {
                relay = anchor.AddComponent<HealthTargetRelay>();
            }

            relay.Target = enemy.Target;
            relay.NativeTarget = enemy.NativeTarget;
            relay.AnchorTransform = ChooseAnchorTransform(anchor, enemy.Target.gameObject);
            relay.DisplayName = ResolveDisplayName(anchor, enemy.Target.gameObject);
            relay.IsBoss = enemy.IsBoss;
        }

        private void OnEnemyHealthStarted(Component component)
        {
            EnemyHealth native = component as EnemyHealth;
            if (native == null)
            {
                return;
            }

            EnemyTargetHandle handle;
            if (!EnemyTargetHandle.TryCreateDirect(native, out handle))
            {
                return;
            }

            AttachEnemyTarget(handle, true);
        }

        private void OnEnemyHealthTriggered(Component component, Collider2D other)
        {
            EnemyHealth native = component as EnemyHealth;
            if (native == null)
            {
                return;
            }

            EnemyRecoilTracker tracker = FindRecoilTracker(native);
            if (tracker == null)
            {
                EnemyTargetHandle handle;
                if (!EnemyTargetHandle.TryCreateDirect(native, out handle))
                {
                    return;
                }

                AttachEnemyTarget(handle, true);
                tracker = FindRecoilTracker(native);
            }

            if (tracker != null)
            {
                tracker.TriggerImmediateRecoil(other);
            }
        }

        private void AttachEnemyTarget(EnemyTargetHandle enemy, bool fromLifecycleHook)
        {
            if (!ShouldTrack(enemy))
            {
                return;
            }

            GameObject anchor = ResolveAnchorObject(enemy);
            if (anchor == null)
            {
                return;
            }

            EnsureRelay(anchor, enemy);

            if (enemy.IsBoss)
            {
                if (anchor.GetComponent<BossBarPresenter>() == null)
                {
                    anchor.AddComponent<BossBarPresenter>();
                }
            }
            else
            {
                if (anchor.GetComponent<EnemyBarPresenter>() == null)
                {
                    anchor.AddComponent<EnemyBarPresenter>();
                }
            }

            EnsureRecoilTracker(anchor, enemy);
        }

        private static GameObject ResolveAnchorObject(EnemyTargetHandle enemy)
        {
            if (enemy == null || enemy.Target == null || enemy.Target.gameObject == null)
            {
                return null;
            }

            Transform current = enemy.Target.transform;
            GameObject best = enemy.Target.gameObject;

            while (current != null)
            {
                if (LooksLikeEnemy(current.name) || LooksLikeBoss(current.name))
                {
                    best = current.gameObject;
                }

                current = current.parent;
            }

            return best;
        }

        private static Transform ChooseAnchorTransform(GameObject anchor, GameObject source)
        {
            if (source == null)
            {
                return anchor != null ? anchor.transform : null;
            }

            Renderer childRenderer = source.GetComponentInChildren<Renderer>();
            if (childRenderer != null)
            {
                return childRenderer.transform;
            }

            Collider2D childCollider = source.GetComponentInChildren<Collider2D>();
            if (childCollider != null)
            {
                return childCollider.transform;
            }

            return source.transform;
        }

        private static string ResolveDisplayName(GameObject anchor, GameObject source)
        {
            if (anchor != null && !string.IsNullOrEmpty(anchor.name))
            {
                return anchor.name;
            }

            return source != null ? source.name : "Enemy";
        }

        private void EnsureRecoilTracker(GameObject anchor, EnemyTargetHandle enemy)
        {
            if (anchor == null || enemy == null || enemy.Target == null)
            {
                return;
            }

            int id = enemy.Target.GetInstanceID();
            EnemyRecoilTracker tracker;
            if (!recoilTrackers.TryGetValue(id, out tracker) || tracker == null)
            {
                tracker = anchor.GetComponent<EnemyRecoilTracker>();
                if (tracker == null)
                {
                    tracker = anchor.AddComponent<EnemyRecoilTracker>();
                }
                recoilTrackers[id] = tracker;
            }

            tracker.Bind(enemy.Target, enemy.NativeTarget, enemy.IsBoss);
        }

        private EnemyRecoilTracker FindRecoilTracker(Component target)
        {
            if (target == null)
            {
                return null;
            }

            EnemyRecoilTracker tracker;
            if (recoilTrackers.TryGetValue(target.GetInstanceID(), out tracker) && tracker != null)
            {
                return tracker;
            }

            return target.GetComponentInParent<EnemyRecoilTracker>();
        }

        private void UpdateEnemyRecoil()
        {
            if (recoilTrackers.Count == 0)
            {
                return;
            }

            List<int> stale = null;
            foreach (KeyValuePair<int, EnemyRecoilTracker> pair in recoilTrackers)
            {
                EnemyRecoilTracker tracker = pair.Value;
                if (tracker == null || !tracker.isActiveAndEnabled)
                {
                    if (stale == null)
                    {
                        stale = new List<int>();
                    }
                    stale.Add(pair.Key);
                    continue;
                }

                tracker.TickRecoil();
            }

            if (stale == null)
            {
                return;
            }

            for (int i = 0; i < stale.Count; i++)
            {
                recoilTrackers.Remove(stale[i]);
            }
        }

        private static bool LooksLikeEnemy(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            string needle = text.Replace(" ", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
            for (int i = 0; i < KnownEnemyTokens.Length; i++)
            {
                if (needle.IndexOf(KnownEnemyTokens[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool LooksLikeBoss(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            string needle = text.Replace(" ", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
            for (int i = 0; i < KnownBossTokens.Length; i++)
            {
                if (needle.IndexOf(KnownBossTokens[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class EnemyTargetHandle
        {
            public Component Target { get; private set; }
            public EnemyHealth NativeTarget { get; private set; }
            public bool IsBoss { get; private set; }
            public float CurrentHealth { get; private set; }
            public float MaxHealth { get; private set; }
            public bool IsDead { get; private set; }

            public static List<EnemyTargetHandle> FindAll()
            {
                List<EnemyTargetHandle> results = new List<EnemyTargetHandle>();
                HashSet<int> seenInstanceIds = new HashSet<int>();

                EnemyHealth[] directEnemies = FindDirectEnemies();
                for (int i = 0; i < directEnemies.Length; i++)
                {
                    EnemyTargetHandle directHandle;
                    if (!TryCreateDirect(directEnemies[i], out directHandle))
                    {
                        continue;
                    }

                    if (seenInstanceIds.Add(directHandle.Target.GetInstanceID()))
                    {
                        results.Add(directHandle);
                    }
                }

                return results;
            }

            private static EnemyHealth[] FindDirectEnemies()
            {
                Dictionary<int, EnemyHealth> results = new Dictionary<int, EnemyHealth>();
                EnemyHealth[] activeEnemies = FindObjectsOfType<EnemyHealth>();
                for (int i = 0; i < activeEnemies.Length; i++)
                {
                    AddNative(results, activeEnemies[i]);
                }

                EnemyHealth[] loadedEnemies = Resources.FindObjectsOfTypeAll<EnemyHealth>();
                for (int i = 0; i < loadedEnemies.Length; i++)
                {
                    AddNative(results, loadedEnemies[i]);
                }

                enemyFightRoom[] rooms = FindObjectsOfType<enemyFightRoom>();
                for (int roomIndex = 0; roomIndex < rooms.Length; roomIndex++)
                {
                    EnemyHealth[] roomHealths = GetRoomHealths(rooms[roomIndex]);
                    if (roomHealths == null)
                    {
                        continue;
                    }

                    for (int healthIndex = 0; healthIndex < roomHealths.Length; healthIndex++)
                    {
                        AddNative(results, roomHealths[healthIndex]);
                    }
                }

                return results.Values.ToArray();
            }

            private static EnemyHealth[] GetRoomHealths(enemyFightRoom room)
            {
                if (room == null)
                {
                    return null;
                }

                BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                FieldInfo field = room.GetType().GetField("health", flags);
                if (field == null)
                {
                    return null;
                }

                return field.GetValue(room) as EnemyHealth[];
            }

            private static void AddNative(Dictionary<int, EnemyHealth> results, EnemyHealth candidate)
            {
                if (candidate == null || candidate.gameObject == null)
                {
                    return;
                }

                if (!candidate.gameObject.scene.IsValid() || !candidate.gameObject.scene.isLoaded)
                {
                    return;
                }

                if (candidate.hideFlags != HideFlags.None)
                {
                    return;
                }

                results[candidate.GetInstanceID()] = candidate;
            }

            internal static bool TryCreateDirect(EnemyHealth component, out EnemyTargetHandle handle)
            {
                handle = null;
                if (component == null || component.gameObject == null)
                {
                    return false;
                }

                string combinedName = ((component.gameObject.name ?? string.Empty) + "|EnemyHealth").ToLowerInvariant();
                bool isBoss = component.fullHealth >= BossThreshold;
                if (!isBoss)
                {
                    for (int i = 0; i < KnownBossTokens.Length; i++)
                    {
                        if (combinedName.IndexOf(KnownBossTokens[i], StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            isBoss = true;
                            break;
                        }
                    }
                }

                handle = new EnemyTargetHandle();
                handle.Target = component;
                handle.NativeTarget = component;
                handle.IsBoss = isBoss;
                handle.CurrentHealth = component.Health;
                handle.MaxHealth = Math.Max(1f, component.fullHealth);
                handle.IsDead = component.dead;
                return true;
            }

        }
    }

    internal sealed class EnemyRecoilTracker : MonoBehaviour
    {
        private const float LocalRecoilVerticalLift = 0.01f;
        private const float LocalRecoilFallbackOffset = 0.03f;
        private Component target;
        private EnemyHealth nativeTarget;
        private bool isBoss;
        private float lastHealth = -1f;
        private float lastRecoilTime;
        private Rigidbody2D body;
        private Transform cachedTransform;
        private bool initialized;
        private Collider2D pendingHitCollider;

        public void Bind(Component enemyTarget, EnemyHealth native, bool boss)
        {
            target = enemyTarget;
            nativeTarget = native;
            isBoss = boss;

            if (!initialized)
            {
                cachedTransform = ResolveBodyTransform();
                body = ResolveBody();
                initialized = true;
            }

            if (lastHealth < 0f)
            {
                lastHealth = ReadHealth();
            }
        }

        public void TickRecoil()
        {
            if (target == null)
            {
                return;
            }

            if (pendingHitCollider != null)
            {
                ApplyRecoilFromCollider(pendingHitCollider);
                pendingHitCollider = null;
                lastHealth = ReadHealth();
                return;
            }

            float current = ReadHealth();
            if (current < 0f)
            {
                return;
            }

            if (lastHealth < 0f)
            {
                lastHealth = current;
                return;
            }

            if (current < lastHealth - 0.01f)
            {
                ApplyRecoil();
            }

            lastHealth = current;
        }

        public void TriggerImmediateRecoil(Collider2D other)
        {
            if (target == null)
            {
                return;
            }

            pendingHitCollider = other;
            ApplyRecoilFromCollider(other);
            lastHealth = ReadHealth();
        }

        private float ReadHealth()
        {
            if (nativeTarget != null)
            {
                if (nativeTarget.dead)
                {
                    return 0f;
                }

                return nativeTarget.Health;
            }

            HealthTargetRelay relay = GetComponent<HealthTargetRelay>();
            float currentHealth;
            float maxHealth;
            bool dead;
            if (relay != null && relay.TryRead(out currentHealth, out maxHealth, out dead))
            {
                return dead ? 0f : currentHealth;
            }

            return -1f;
        }

        private void ApplyRecoil()
        {
            if (Time.unscaledTime - lastRecoilTime < 0.08f)
            {
                return;
            }

            Vector2 pushDirection = ResolvePushDirection();
            float strength = isBoss ? 0.08f : 0.14f;

            if (body != null && body.bodyType == RigidbodyType2D.Dynamic)
            {
                Vector2 force = pushDirection * strength;
                if (Mathf.Abs(force.y) < LocalRecoilVerticalLift)
                {
                    force.y = LocalRecoilVerticalLift;
                }

                body.velocity = new Vector2(body.velocity.x * 0.9f, body.velocity.y);
                body.AddForce(force, ForceMode2D.Impulse);
                Rigidbody2D[] parentBodies = GetComponentsInParent<Rigidbody2D>();
                for (int i = 0; i < parentBodies.Length; i++)
                {
                    Rigidbody2D parentBody = parentBodies[i];
                    if (parentBody != null && parentBody != body && parentBody.bodyType == RigidbodyType2D.Dynamic)
                    {
                        parentBody.velocity = new Vector2(parentBody.velocity.x * 0.95f, parentBody.velocity.y);
                    }
                }
            }
            else if (cachedTransform != null)
            {
                cachedTransform.position += (Vector3)(pushDirection * LocalRecoilFallbackOffset);
                Transform root = cachedTransform.root;
                if (root != null && root != cachedTransform)
                {
                    root.position += (Vector3)(pushDirection * (LocalRecoilFallbackOffset * 0.5f));
                }
            }

            lastRecoilTime = Time.unscaledTime;
        }

        private void ApplyRecoilFromCollider(Collider2D other)
        {
            if (Time.unscaledTime - lastRecoilTime < 0.08f)
            {
                return;
            }

            if (other != null && !LooksLikePlayerHit(other))
            {
                return;
            }

            Vector2 pushDirection = ResolvePushDirectionFromCollider(other);
            ApplyRecoilWithDirection(pushDirection);
        }

        private void ApplyRecoilWithDirection(Vector2 pushDirection)
        {
            float strength = isBoss ? 0.08f : 0.14f;

            if (body != null && body.bodyType == RigidbodyType2D.Dynamic)
            {
                Vector2 force = pushDirection * strength;
                if (Mathf.Abs(force.y) < LocalRecoilVerticalLift)
                {
                    force.y = LocalRecoilVerticalLift;
                }

                body.velocity = new Vector2(body.velocity.x * 0.9f, body.velocity.y);
                body.AddForce(force, ForceMode2D.Impulse);
            }
            else if (cachedTransform != null)
            {
                cachedTransform.position += (Vector3)(pushDirection * LocalRecoilFallbackOffset);
                Transform root = cachedTransform.root;
                if (root != null && root != cachedTransform)
                {
                    root.position += (Vector3)(pushDirection * (LocalRecoilFallbackOffset * 0.5f));
                }
            }

            lastRecoilTime = Time.unscaledTime;
        }

        private Vector2 ResolvePushDirection()
        {
            Transform player = FindPlayerTransform();
            if (player != null && cachedTransform != null)
            {
                Vector2 away = (Vector2)(cachedTransform.position - player.position);
                if (away.sqrMagnitude > 0.001f)
                {
                    away.Normalize();
                    if (Mathf.Abs(away.y) < 0.1f)
                    {
                        away.y = Mathf.Max(away.y, LocalRecoilVerticalLift);
                    }
                    return away;
                }
            }

            if (cachedTransform != null)
            {
                Vector2 fallback = cachedTransform.right.x >= 0f ? Vector2.right : Vector2.left;
                fallback.y = LocalRecoilVerticalLift;
                return fallback.normalized;
            }

            return Vector2.right;
        }

        private Vector2 ResolvePushDirectionFromCollider(Collider2D other)
        {
            if (other != null && cachedTransform != null)
            {
                Vector2 away = (Vector2)(cachedTransform.position - other.transform.position);
                if (away.sqrMagnitude > 0.001f)
                {
                    away.Normalize();
                    if (Mathf.Abs(away.y) < 0.1f)
                    {
                        away.y = Mathf.Max(away.y, LocalRecoilVerticalLift);
                    }
                    return away.normalized;
                }
            }

            return ResolvePushDirection();
        }

        private static bool LooksLikePlayerHit(Collider2D other)
        {
            if (other == null)
            {
                return true;
            }

            GameObject source = other.gameObject;
            if (source == null)
            {
                return true;
            }

            if (source.CompareTag("Player"))
            {
                return true;
            }

            string text = ((source.name ?? string.Empty) + "|" + (source.tag ?? string.Empty)).ToLowerInvariant();
            return text.Contains("player") ||
                text.Contains("attack") ||
                text.Contains("slash") ||
                text.Contains("sword") ||
                text.Contains("shoot") ||
                text.Contains("projectile") ||
                text.Contains("bullet") ||
                text.Contains("spell") ||
                text.Contains("magic");
        }

        private Transform ResolveBodyTransform()
        {
            if (target == null)
            {
                return transform;
            }

            Rigidbody2D localBody = target.GetComponent<Rigidbody2D>();
            if (localBody != null)
            {
                return localBody.transform;
            }

            Rigidbody2D parentBody = target.GetComponentInParent<Rigidbody2D>();
            if (parentBody != null)
            {
                return parentBody.transform;
            }

            return target.transform.root != null ? target.transform.root : target.transform;
        }

        private Rigidbody2D ResolveBody()
        {
            if (target == null)
            {
                return null;
            }

            Rigidbody2D localBody = target.GetComponent<Rigidbody2D>();
            if (localBody != null)
            {
                return localBody;
            }

            Rigidbody2D parentBody = target.GetComponentInParent<Rigidbody2D>();
            if (parentBody != null)
            {
                return parentBody;
            }

            return null;
        }

        private static Transform FindPlayerTransform()
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                return player.transform;
            }

            string[] names = new string[] { "Player", "player", "Hero", "hero", "Ayzee", "ayzee" };
            for (int i = 0; i < names.Length; i++)
            {
                GameObject found = GameObject.Find(names[i]);
                if (found != null)
                {
                    return found.transform;
                }
            }

            return null;
        }
    }

    internal sealed class HealthTargetRelay : MonoBehaviour
    {
        public Component Target;
        public EnemyHealth NativeTarget;
        public Transform AnchorTransform;
        public string DisplayName;
        public bool IsBoss;
        private FieldInfo healthField;
        private FieldInfo maxHealthField;
        private FieldInfo deadField;
        private Type cachedType;

        public bool TryRead(out float currentHealth, out float maxHealth, out bool dead)
        {
            currentHealth = 0f;
            maxHealth = 0f;
            dead = false;

            if (Target == null)
            {
                return false;
            }

            if (NativeTarget != null)
            {
                currentHealth = NativeTarget.Health;
                maxHealth = NativeTarget.fullHealth > 0 ? NativeTarget.fullHealth : NativeTarget.Health;
                dead = NativeTarget.dead;
                return true;
            }

            EnsureBinding();
            if (healthField == null)
            {
                return false;
            }

            currentHealth = ReadFloat(healthField, Target);
            maxHealth = maxHealthField != null ? ReadFloat(maxHealthField, Target) : currentHealth;
            if (maxHealth <= 0f)
            {
                maxHealth = currentHealth;
            }

            dead = deadField != null && ReadBool(deadField, Target);
            return true;
        }

        private void EnsureBinding()
        {
            Type type = Target != null ? Target.GetType() : null;
            if (type == null || type == cachedType)
            {
                return;
            }

            cachedType = type;
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            healthField = FindField(type, flags, new string[] { "Health", "hp", "currentHealth", "currHP" });
            maxHealthField = FindField(type, flags, new string[] { "fullHealth", "maxHP", "maxHealth" });
            deadField = FindField(type, flags, new string[] { "dead", "isDead" });
        }

        private static FieldInfo FindField(Type type, BindingFlags flags, string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                FieldInfo field = type.GetField(names[i], flags);
                if (field != null)
                {
                    return field;
                }
            }

            return null;
        }

        private static float ReadFloat(FieldInfo field, object instance)
        {
            object value = field.GetValue(instance);
            if (value == null)
            {
                return 0f;
            }

            if (value is int)
            {
                return (int)value;
            }

            if (value is float)
            {
                return (float)value;
            }

            if (value is double)
            {
                return (float)(double)value;
            }

            float parsed;
            return float.TryParse(Convert.ToString(value), out parsed) ? parsed : 0f;
        }

        private static bool ReadBool(FieldInfo field, object instance)
        {
            object value = field.GetValue(instance);
            if (value is bool)
            {
                return (bool)value;
            }

            bool parsed;
            return bool.TryParse(Convert.ToString(value), out parsed) && parsed;
        }
    }

    internal abstract class BaseBarPresenter : MonoBehaviour
    {
        protected HealthTargetRelay relay;
        protected Transform anchorTransform;
        protected float delayedHealth;
        protected float maxHealth;
        protected float lastHealth;
        protected RectTransform root;
        protected CanvasGroup group;
        protected Image backgroundImage;
        protected Image middleImage;
        protected Image foregroundImage;
        protected Image outlineImage;
        protected float lastSeenChangeTime;

        protected HpBarManager Manager
        {
            get { return AestikEnemyHpBarMod.Manager; }
        }

        protected virtual void Awake()
        {
            relay = GetComponent<HealthTargetRelay>();
            if (relay == null || Manager == null || Manager.OverlayRoot == null)
            {
                Destroy(this);
                return;
            }

            float currentHealth;
            bool dead;
            if (!relay.TryRead(out currentHealth, out maxHealth, out dead))
            {
                Destroy(this);
                return;
            }

            anchorTransform = relay.AnchorTransform != null ? relay.AnchorTransform : transform;
            maxHealth = Math.Max(1f, maxHealth);
            delayedHealth = Mathf.Max(0f, currentHealth);
            lastHealth = delayedHealth;
            lastSeenChangeTime = Time.unscaledTime;

            root = CreateRect(gameObject.name + " HP Root", Manager.OverlayRoot, Vector2.zero, Vector2.zero);
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            CreateImages();
        }

        protected virtual void OnDestroy()
        {
            if (root != null)
            {
                Destroy(root.gameObject);
            }
        }

        protected virtual void OnDisable()
        {
            if (group != null)
            {
                group.alpha = 0f;
            }
        }

        protected virtual void Update()
        {
            if (relay == null || root == null || Manager == null)
            {
                return;
            }

            float currentHealth;
            bool dead;
            if (!relay.TryRead(out currentHealth, out maxHealth, out dead))
            {
                group.alpha = 0f;
                return;
            }

            maxHealth = Mathf.Max(1f, maxHealth);
            currentHealth = Mathf.Max(0f, currentHealth);

            if (dead || maxHealth <= 0f)
            {
                group.alpha = 0f;
                return;
            }

            if (Math.Abs(currentHealth - lastHealth) > 0.01f)
            {
                lastSeenChangeTime = Time.unscaledTime;
            }

            if (delayedHealth > currentHealth)
            {
                delayedHealth = Mathf.MoveTowards(delayedHealth, currentHealth, Time.unscaledDeltaTime * GetLagSpeed());
            }
            else
            {
                delayedHealth = currentHealth;
            }

            if (foregroundImage != null)
            {
                foregroundImage.fillAmount = Mathf.Clamp01(currentHealth / maxHealth);
            }

            if (middleImage != null)
            {
                middleImage.fillAmount = Mathf.Clamp01(delayedHealth / maxHealth);
            }

            group.alpha = ShouldShow(currentHealth) ? GetVisibleAlpha(currentHealth) : 0f;
            lastHealth = currentHealth;
            UpdatePlacement();
        }

        protected abstract void CreateImages();
        protected abstract void UpdatePlacement();
        protected abstract float GetLagSpeed();

        protected virtual bool ShouldShow(float currentHealth)
        {
            return currentHealth > 0f && (currentHealth < maxHealth || Time.unscaledTime - lastSeenChangeTime <= 1.75f);
        }

        protected virtual float GetVisibleAlpha(float currentHealth)
        {
            if (currentHealth >= maxHealth)
            {
                float elapsed = Time.unscaledTime - lastSeenChangeTime;
                return elapsed <= 0.6f ? Mathf.Lerp(0.75f, 0f, elapsed / 0.6f) : 0f;
            }

            return 1f;
        }

        protected static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
            return rect;
        }

        protected static Image CreateImage(string name, RectTransform parent, Sprite sprite, Vector2 size)
        {
            RectTransform rect = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            rect.sizeDelta = size;
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = false;
            return image;
        }

        protected Renderer FindTargetRenderer()
        {
            if (anchorTransform == null)
            {
                return null;
            }

            Renderer renderer = anchorTransform.GetComponent<Renderer>();
            if (renderer != null)
            {
                return renderer;
            }

            return anchorTransform.GetComponentInChildren<Renderer>();
        }

        protected Bounds? GetTargetBounds()
        {
            Renderer renderer = FindTargetRenderer();
            if (renderer != null)
            {
                return renderer.bounds;
            }

            Collider2D collider = anchorTransform != null ? anchorTransform.GetComponent<Collider2D>() : null;
            if (collider != null)
            {
                return collider.bounds;
            }

            collider = anchorTransform != null ? anchorTransform.GetComponentInChildren<Collider2D>() : null;
            if (collider != null)
            {
                return collider.bounds;
            }

            return null;
        }
    }

    internal sealed class EnemyBarPresenter : BaseBarPresenter
    {
        protected override void CreateImages()
        {
            root.anchorMin = new Vector2(0f, 0f);
            root.anchorMax = new Vector2(0f, 0f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(175f, 19f);

            backgroundImage = CreateImage("BG", root, Manager.BgSprite, new Vector2(175f, 19f));
            middleImage = CreateImage("MG", root, Manager.MgSprite, new Vector2(117f, 10f));
            foregroundImage = CreateImage("FG", root, Manager.FgSprite, new Vector2(117f, 10f));
            outlineImage = CreateImage("OL", root, Manager.OlSprite, new Vector2(175f, 19f));

            middleImage.type = Image.Type.Filled;
            middleImage.fillMethod = Image.FillMethod.Horizontal;
            foregroundImage.type = Image.Type.Filled;
            foregroundImage.fillMethod = Image.FillMethod.Horizontal;
        }

        protected override void UpdatePlacement()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                camera = FindObjectOfType<Camera>();
            }

            if (camera == null)
            {
                group.alpha = 0f;
                return;
            }

            Transform targetTransform = anchorTransform != null ? anchorTransform : transform;
            Vector3 worldPosition = targetTransform.position + (Vector3.up * 1.5f);
            Bounds? bounds = GetTargetBounds();
            if (bounds.HasValue)
            {
                Bounds value = bounds.Value;
                worldPosition = value.center + Vector3.up * (value.extents.y + 0.55f);
            }

            Vector3 screenPoint = camera.WorldToScreenPoint(worldPosition);
            if (screenPoint.z <= 0f)
            {
                group.alpha = 0f;
                return;
            }

            root.position = new Vector3(screenPoint.x, screenPoint.y + 28f, 0f);
        }

        protected override float GetLagSpeed()
        {
            return 22f;
        }
    }

    internal sealed class BossBarPresenter : BaseBarPresenter
    {
        private string bossKey;

        protected override void Awake()
        {
            bossKey = gameObject.scene.name + "|" + gameObject.name + "|" + GetInstanceID().ToString();
            if (Manager != null)
            {
                Manager.RegisterBoss(bossKey);
            }

            base.Awake();
        }

        private void OnEnable()
        {
            if (Manager != null && !string.IsNullOrEmpty(bossKey))
            {
                Manager.RegisterBoss(bossKey);
            }
        }

        protected override void OnDestroy()
        {
            if (Manager != null)
            {
                Manager.UnregisterBoss(bossKey);
            }

            base.OnDestroy();
        }

        protected override void OnDisable()
        {
            if (Manager != null)
            {
                Manager.UnregisterBoss(bossKey);
            }

            base.OnDisable();
        }

        protected override void CreateImages()
        {
            root.anchorMin = new Vector2(0.5f, 0f);
            root.anchorMax = new Vector2(0.5f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.sizeDelta = new Vector2(966f, 27f);

            backgroundImage = CreateImage("BossBG", root, Manager.BossBgSprite, new Vector2(960f, 27f));
            foregroundImage = CreateImage("BossFG", root, Manager.BossFgSprite, new Vector2(960f, 27f));
            outlineImage = CreateImage("BossOL", root, Manager.BossOlSprite, new Vector2(966f, 27f));

            foregroundImage.type = Image.Type.Filled;
            foregroundImage.fillMethod = Image.FillMethod.Horizontal;
        }

        protected override void UpdatePlacement()
        {
            int index = Manager != null ? Manager.GetBossIndex(bossKey) : 0;
            root.anchoredPosition = new Vector2(0f, 28f + (index * 32f));
        }

        protected override float GetLagSpeed()
        {
            return 12f;
        }

        protected override float GetVisibleAlpha(float currentHealth)
        {
            if (currentHealth >= maxHealth)
            {
                float elapsed = Time.unscaledTime - lastSeenChangeTime;
                return elapsed <= 0.6f ? Mathf.Lerp(0.75f, 0f, elapsed / 0.6f) : 0f;
            }

            return Time.timeScale == 0f ? 0.8f : 1f;
        }
    }
}
