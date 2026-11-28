using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TrashCleaner3D
{
    /// <summary>
    /// v1.3 1인칭 컨트롤러:
    /// - [LMB 꾹(HOLD)] = 바닥 쓰레기 전용! (1단계 맨손 1.00초 진동 차징 → 강화할수록 0.25 → 0.07초)
    /// - [LMB 단타 클릭] = 큰 쓰레기 봉투/박스 타격 전용
    /// - [RMB] = 트럭 정산 구역에서 담긴 쓰레기 전부 쏟아 정산 (봉투 만들기/던지기 없음 — 요청 #4·#12)
    /// - [E] = 트럭 옆 '통합 강화소' 키오스크 열기/닫기 (엣 상시 [Tab] 트리는 삭제 — 요청 #5·#10)
    /// - [F] 현관문 / [K] 80% 조기 정산 / [Space] 점프 / [ESC] 설정
    /// - v1.3: 닦기 모션은 화면 중앙으로(요청 #13), 쓰레기는 도구/손 끝으로 빨려온다(요청 #9)
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonCleanerController : MonoBehaviour
    {
        public GamePrototypeManager manager;
        public Transform cameraTransform;
        public Transform rightToolPivot;
        public GameObject fistHandVisual;
        public GameObject tongsBroomVisual;
        public GameObject vacuumVisual;
        public GameObject tissueVisual;
        public GameObject mopVisual;
        public GameObject orbGunVisual;
        public GameObject blackHoleGunVisual;
        public Light flashlight;
        public float baseMoveSpeed = 3.85f;
        public float mouseSensitivity = 0.14f;
        public float jumpHeight = 0.85f;
        public float gravity = -18.5f;

        public bool AimIsPile { get; private set; }
        public bool AimIsSwarmNear { get; private set; }
        public bool AimIsStain { get; private set; }
        public InteractiveDoor AimDoor { get; private set; }
        public TreasureTarget AimTreasure { get; private set; }
        public SecurityCameraController AimCctv { get; private set; }
        public bool NeedMouseShakeHint { get; private set; }
        public float AimProgress01 { get; private set; } = -1f;
        public bool ShowHelp { get; private set; }
        public float AimPileHp01 { get; private set; } = 1f;

        private TreasureTarget _lastTreasure;

        /// <summary>트럭 정산 구역 안에 서 있는지 (HUD 프롬프트용)</summary>
        public bool SettlementZoneActive { get; private set; }
        public int SettlementReadyCount { get; private set; }

        // ===== v1.3 트럭 옆 '통합 강화소' 키오스크 상태 (HUD 표시용) =====
        public bool KioskActive { get; private set; }
        public string KioskTitle { get; private set; } = "";
        public string KioskDetail { get; private set; } = "";
        public Color KioskColor { get; private set; } = Color.white;

        private CharacterController _cc;
        private Camera _cam;
        private float _pitch;
        private float _verticalVel;
        private bool _isCrouching;
        private bool _camInit;
        private float _camStandLocalY = 1.50f;
        private float _ccStandHeight = 1.75f;
        private float _ccStandCenterY = 0.875f;
        private CleanableTarget _lastTarget;
        private InteractiveDoor _lastDoor;
        private float _punchCooldown;
        private float _binHintCooldown;

        private Vector3 _rightBasePos;
        private Quaternion _rightBaseRot;

        // ===== [v1.8] 양손 줍기 · 3·4단계 장비 =====
        private Transform _leftPivot;
        private GameObject _fistLeftCopy;
        private GameObject _orbGunBuilt;
        private GameObject _bhGunBuilt;
        private GarbageOrbProjectile _orbProjectile;
        private BlackHoleRig _blackHoleRig;
        private float _beatT = 9f;             // 4박자 모션 시작 후 경과
        private float _beatDur = 0.36f;
        private bool _beatRight = true;
        private static readonly Vector3[] BeatPos =
        {
            new Vector3(0f, 0.02f, 0.26f),    // 뻗기
            new Vector3(0f, -0.02f, 0.31f),   // 집기
            new Vector3(-0.10f, -0.10f, 0.07f), // 당기기
            new Vector3(0.05f, -0.16f, -0.13f)  // 넣기
        };
        private static readonly Vector3[] BeatEul =
        {
            new Vector3(8f, 0f, 0f), new Vector3(27f, -10f, 0f),
            new Vector3(39f, -21f, 6f), new Vector3(-6f, 22f, -8f)
        };

        public bool OrbCharging => _orbProjectile != null;
        public int OrbAbsorbedCount => _orbProjectile != null ? _orbProjectile.Absorbed : 0;
        public bool BlackHoleDeployed => _blackHoleRig != null;

        // v1.3 (요청 #9): 쓰레기가 빨려 들어올 목적지 = 실제 1인칭 손/노즐 끝.
        //   우측 도구 피벗의 자식으로 두어 수납·흡입 애니메이션을 그대로 따라간다.
        private Transform _suckTarget;

        // v1.2: 카메라 흔들림은 '로컬 포지션 오프셋'만 쓴다 (회전 멀미 없이 짧고 묵직하게)
        private Vector3 _camBaseLocalPos;
        private float _shakeIntensity;
        private float _shakeDuration;
        private float _shakeTime;
        private float _shakeSeed;

        // v1.2: 진공청소기 노즐 앞 흡입 소용돌이
        private VacuumSuctionVortex _vacuumVortex;
        private float _punchTimer;
        private float _grabSwingTimer;
        private float _scrubMotionWeight;
        private float _scrubPhase;
        private float _scrubSoundCooldown;
        private Vector2 _scrubSway;

        // v1.1.2: LMB 꾹 누르기만 해도 들어가는 기본 닦기 속도(마우스 흔들기는 가속 보너스)
        private const float BaseScrubPower = 0.55f;
        private bool _isHoldingTrashNow;
        private bool _isVacuumingNow;

        // [v1.8.1 수정 #1] 실제 타겟 쓰레기 월드 좌표까지 손이 뻗어가 집고 당겨오는 리치 모션 상태
        private Vector3 _reachTargetLocal;
        private bool _hasReachTarget;
        private float _pullBackTimer;
        private float _pullBackDur = 0.24f;
        private Vector3 _pullStartLocal;
        private bool _pullWithRight = true;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (_cc != null)
            {
                // 벽 통과(터널링) 및 벽 근접 시 시야 가려짐/뚫림 방지용 안전 물리 규격
                _cc.radius = Mathf.Max(_cc.radius, 0.34f);
                _cc.skinWidth = 0.04f;
                _cc.minMoveDistance = 0.0001f;
            }
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            if (cameraTransform != null)
            {
                _cam = cameraTransform.GetComponent<Camera>();
                if (_cam != null) _cam.nearClipPlane = 0.08f; // WebGL 깊이 버퍼 정밀도 확보 (바닥 Z-fighting 방지!)
            }
            if (rightToolPivot != null) { _rightBasePos = rightToolPivot.localPosition; _rightBaseRot = rightToolPivot.localRotation; }
            if (cameraTransform != null) _camBaseLocalPos = cameraTransform.localPosition;
            EnsureSuckTarget();
        }

        /// <summary>손/노즐 끝에 흡입 도착점을 만든다(없을 때만).</summary>
        private void EnsureSuckTarget()
        {
            if (_suckTarget != null) return;
            if (rightToolPivot == null) return;

            GameObject t = new GameObject("SuckTarget_HandTip");
            t.transform.SetParent(rightToolPivot, false);
            t.transform.localPosition = new Vector3(0f, -0.02f, 0.16f);
            t.transform.localRotation = Quaternion.identity;
            _suckTarget = t.transform;
        }

        private void Start()
        {
            if (manager == null) manager = FindFirstObjectByType<GamePrototypeManager>();
            if (manager != null) mouseSensitivity = manager.MouseSensitivity;
            EnsureVacuumVortex();
            EnsureGearVisuals();
#if !UNITY_WEBGL || UNITY_EDITOR
            if (!GamePrototypeManager.UiCursorFree &&
                (manager == null || manager.Phase == GamePhase.Phase1_TrashRemoval || manager.Phase == GamePhase.Phase2_FloorMopping))
                LockCursorStrict();
#endif
        }

        /// <summary>
        /// 진공 노즐 비주얼이 준비돼 있으면 그 입구에 흡입 소용돌이를 한 번만 붙인다.
        /// (씬을 다시 빌드하지 않아도 런타임에 항상 생기도록 컨트롤러가 직접 만든다)
        /// </summary>
        private void EnsureVacuumVortex()
        {
            if (_vacuumVortex != null || vacuumVisual == null) return;
            _vacuumVortex = VacuumSuctionVortex.Attach(
                vacuumVisual.transform,
                new Vector3(0f, -0.17f, 0.60f),
                Quaternion.Euler(14f, 0f, 0f));
        }

        /// <summary>
        /// [v1.8] 양손 줍기용 왼손 피벗·손 복제, 원기옥·블랙홀 건 비주얼을 런타임에 한 번 만든다.
        /// (씬을 다시 빌드하지 않아도 세이브를 그대로 열면 장비가 보인다)
        /// </summary>
        private void EnsureGearVisuals()
        {
            if (rightToolPivot == null) return;

            if (_leftPivot == null)
            {
                GameObject lp = new GameObject("LeftHandPivot");
                Transform parent = rightToolPivot.parent != null ? rightToolPivot.parent : cameraTransform;
                if (parent != null) lp.transform.SetParent(parent, false);
                lp.transform.localPosition = new Vector3(-_rightBasePos.x, _rightBasePos.y, _rightBasePos.z);
                lp.transform.localRotation = new Quaternion(_rightBaseRot.x, -_rightBaseRot.y, -_rightBaseRot.z, _rightBaseRot.w);
                _leftPivot = lp.transform;

                if (fistHandVisual != null)
                {
                    _fistLeftCopy = Instantiate(fistHandVisual, _leftPivot, false);
                    _fistLeftCopy.name = "Visual_FistHand_Left";
                    Vector3 s = _fistLeftCopy.transform.localScale;
                    _fistLeftCopy.transform.localScale = new Vector3(-Mathf.Abs(s.x), s.y, s.z);
                    _fistLeftCopy.SetActive(false);
                }
            }

            if (_orbGunBuilt == null)
            {
                _orbGunBuilt = orbGunVisual != null ? orbGunVisual
                    : BuildGunVisual("Visual_OrbBeamGun", new Color(0.20f, 0.94f, 0.86f), 0.0f);
            }
            if (_bhGunBuilt == null)
            {
                _bhGunBuilt = blackHoleGunVisual != null ? blackHoleGunVisual
                    : BuildGunVisual("Visual_BlackHoleGun", new Color(0.56f, 0.34f, 1.0f), 0.12f);
            }
        }

        /// <summary>프리미티브만으로 만드는 장비 건 실루엣(손잡이 + 몸통 + 총구 + 발광 코어).</summary>
        private GameObject BuildGunVisual(string name, Color glow, float tiltDeg)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material body = new Material(sh);
            if (body.HasProperty("_BaseColor")) body.SetColor("_BaseColor", new Color(0.16f, 0.17f, 0.20f));
            body.color = new Color(0.16f, 0.17f, 0.20f);
            if (body.HasProperty("_Smoothness")) body.SetFloat("_Smoothness", 0.7f);
            Material emit = new Material(sh);
            if (emit.HasProperty("_BaseColor")) emit.SetColor("_BaseColor", glow);
            emit.color = glow;
            if (emit.HasProperty("_EmissionColor"))
            {
                emit.SetColor("_EmissionColor", glow * 1.8f);
                emit.EnableKeyword("_EMISSION");
            }

            GameObject root = new GameObject(name);
            root.transform.SetParent(rightToolPivot, false);
            root.transform.localPosition = new Vector3(0f, 0f, 0.05f);
            root.transform.localRotation = Quaternion.Euler(tiltDeg, 0f, 0f);

            GameObject bodyObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(bodyObj.GetComponent<Collider>());
            bodyObj.name = "GunBody";
            bodyObj.transform.SetParent(root.transform, false);
            bodyObj.transform.localPosition = new Vector3(0f, -0.03f, 0.20f);
            bodyObj.transform.localScale = new Vector3(0.10f, 0.11f, 0.30f);
            bodyObj.GetComponent<Renderer>().sharedMaterial = body;

            GameObject grip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(grip.GetComponent<Collider>());
            grip.name = "GunGrip";
            grip.transform.SetParent(root.transform, false);
            grip.transform.localPosition = new Vector3(0f, -0.13f, 0.10f);
            grip.transform.localRotation = Quaternion.Euler(18f, 0f, 0f);
            grip.transform.localScale = new Vector3(0.075f, 0.19f, 0.09f);
            grip.GetComponent<Renderer>().sharedMaterial = body;

            GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(barrel.GetComponent<Collider>());
            barrel.name = "GunBarrel";
            barrel.transform.SetParent(root.transform, false);
            barrel.transform.localPosition = new Vector3(0f, -0.02f, 0.42f);
            barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            barrel.transform.localScale = new Vector3(0.055f, 0.15f, 0.055f);
            barrel.GetComponent<Renderer>().sharedMaterial = body;

            GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(core.GetComponent<Collider>());
            core.name = "GunGlow";
            core.transform.SetParent(root.transform, false);
            core.transform.localPosition = new Vector3(0f, -0.02f, 0.58f);
            core.transform.localScale = Vector3.one * 0.085f;
            core.GetComponent<Renderer>().sharedMaterial = emit;

            root.SetActive(false);
            return root;
        }

        /// <summary>
        /// 짧고 묵직한 카메라 흔들림. 여러 요청이 겹치면 더 센 쪽 강도와 더 긴 시간을 사용한다.
        /// (회전이 아니라 카메라 로컬 포지션만 흔들어 멀미를 만들지 않는다)
        /// </summary>
        public void AddCameraShake(float intensity, float duration)
        {
            if (intensity <= 0f || duration <= 0f) return;
            _shakeIntensity = Mathf.Max(_shakeIntensity, intensity);
            _shakeDuration = Mathf.Max(_shakeDuration, duration);
            _shakeTime = Mathf.Max(_shakeTime, duration);
            if (_shakeSeed <= 0f) _shakeSeed = UnityEngine.Random.Range(0.1f, 10f);
        }

        private void ApplyCameraShake()
        {
            if (cameraTransform == null) return;
            if (_shakeTime <= 0f) return;

            _shakeTime = Mathf.Max(0f, _shakeTime - Time.deltaTime);
            float k = _shakeDuration > 0.001f ? Mathf.Clamp01(_shakeTime / _shakeDuration) : 0f;
            float amp = _shakeIntensity * k * k;   // 끝으로 갈수록 빠르게 잦아든다
            float t = Time.time * 120f + _shakeSeed;

            Vector3 offset = new Vector3(
                Mathf.Sin(t * 1.13f) * amp,
                Mathf.Sin(t * 1.61f + 1.3f) * amp * 0.75f,
                Mathf.Sin(t * 0.87f + 2.1f) * amp * 0.55f);

            if (_shakeTime <= 0f)
            {
                _shakeIntensity = 0f;
                _shakeDuration = 0f;
                cameraTransform.localPosition = _camBaseLocalPos;
            }
            else
            {
                cameraTransform.localPosition = _camBaseLocalPos + offset;
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (hasFocus && !GamePrototypeManager.UiCursorFree && manager != null &&
                (manager.Phase == GamePhase.Phase1_TrashRemoval || manager.Phase == GamePhase.Phase2_FloorMopping))
            {
                LockCursorStrict();
            }
#endif
        }

        private static void LockCursorStrict()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void TeleportTo(Vector3 pos, Quaternion rot)
        {
            _cc.enabled = false;
            transform.position = pos;
            transform.rotation = rot;
            _pitch = 0f;
            _verticalVel = 0f;
            if (cameraTransform != null) cameraTransform.localRotation = Quaternion.identity;
            _cc.enabled = true;
        }

        private void Update()
        {
            if (manager == null) return;

            // v1.2: 오버레이(설정·스킬트리) 중에도 흔들림이 자연스럽게 잦아들도록 가장 먼저 갱신
            ApplyCameraShake();
            // 흡입 연출은 1단계 청소 중에만 — 보물 팝업·설정·정산 화면에서는 즉시 끈다
            if (_vacuumVortex != null && manager.Phase != GamePhase.Phase1_TrashRemoval) _vacuumVortex.SetIntensity(0f);

            if (WasTabPressed() &&
                (manager.Phase == GamePhase.Phase1_TrashRemoval || manager.Phase == GamePhase.Phase2_FloorMopping || manager.KioskOpen))
            {
                ClearHighlight();
                manager.ToggleTabUpgradeTree();
                return;
            }

            if (WasEscPressed() || (manager.KioskOpen && WasKeyEPressed()))
            {
                if (manager.KioskOpen) manager.CloseUpgradeKiosk();
                else if (manager.IsStageMindMapOpen) manager.CloseStageMindMap();
                else manager.ToggleSettingsMenu();
                ClearHighlight();
                return;
            }

            if (WasKeyMPressed() &&
                (manager.Phase == GamePhase.Phase1_TrashRemoval || manager.Phase == GamePhase.Phase2_FloorMopping || manager.IsStageMindMapOpen))
            {
                if (manager.IsStageMindMapOpen) manager.CloseStageMindMap();
                else manager.OpenStageMindMap();
                ClearHighlight();
                return;
            }

            if (manager.Phase == GamePhase.TreasureDecision)
            {
                ClearHighlight();
                HandleTreasureDecisionHotkeys();
                return;
            }

            if (WasAltPressed())
            {
                if (manager.Phase == GamePhase.Phase1_TrashRemoval || manager.Phase == GamePhase.Phase2_FloorMopping)
                {
                    ToggleUiCursor();
                    return;
                }
            }

            if (manager.Phase != GamePhase.Phase1_TrashRemoval && manager.Phase != GamePhase.Phase2_FloorMopping)
            {
                // [v1.8] 장비 연출 정리(원기옥 발사·블랙홀 회수)
                if (_orbProjectile != null) { _orbProjectile.Launch(); _orbProjectile = null; }
                if (_blackHoleRig != null) { Destroy(_blackHoleRig.gameObject); _blackHoleRig = null; }
                ClearHighlight();
                return;
            }

            if (manager.CanEarlyCheckout && WasKeyKPressed())
            {
                ClearHighlight();
                manager.TriggerEarlyCheckout();
                return;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            // 브라우저 보안 정책: WebGL 포인터 락은 사용자 클릭 제스처 안에서만 호출
            if (!GamePrototypeManager.UiCursorFree && Cursor.lockState != CursorLockMode.Locked)
            {
                bool clicked = false;
#if ENABLE_INPUT_SYSTEM
                clicked = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
                clicked = Input.GetMouseButtonDown(0);
#endif
                if (clicked) LockCursorStrict();
            }
#else
            if (Application.isFocused && !GamePrototypeManager.UiCursorFree && Cursor.lockState != CursorLockMode.Locked) LockCursorStrict();
#endif

            _isHoldingTrashNow = false;
            _isVacuumingNow = false;
            UpdateToolVisuals();
            UpdateCameraEffects();
            Vector2 mouseDelta = HandleMovementAndLook();
            HandleKiosk();
            HandleInteraction(mouseDelta);
            AnimateHands();
        }

        /// <summary>
        /// [v2.0] 4티어 산업용 사이클론 청소기 / 5티어 자율 청소 드론 편대 + 직결 호스.
        /// 수거통 용량(IsBinFull)을 철저히 준수해 용량 초과 버그를 원천 차단한다.
        /// </summary>
        private void HandleGearMode(bool lmbHeld, float floorPlaneY)
        {
            if (manager.trashSwarm == null || cameraTransform == null) return;
            ClearSwarmSelection();
            Vector3 camPos = cameraTransform.position;
            Vector3 fwd = cameraTransform.forward;

            // 수거통이 가득 찼고 직결 호스 자동정산이 아니면 흡입 중단 + 안내
            bool binFull = manager.IsBinFull && !(manager.Mode == GearMode.BlackHole && manager.HasWhiteHole);
            if (binFull)
            {
                if (_orbProjectile != null) { Destroy(_orbProjectile.gameObject); _orbProjectile = null; }
                if (_blackHoleRig != null) { Destroy(_blackHoleRig.gameObject); _blackHoleRig = null; }
                AimProgress01 = -1f;
                if (lmbHeld && _binHintCooldown <= 0f)
                {
                    _binHintCooldown = 1.4f;
                    manager.ShowToast(manager.FillBinTag);
                }
                return;
            }

            if (manager.Mode == GearMode.Orb)
            {
                // 4티어: 산업용 사이클론 청소기 (전방 회오리 원뿔 흡입)
                Vector3 center = camPos + fwd * 2.15f;
                center.y = Mathf.Max(floorPlaneY + 0.35f, center.y - 0.35f);
                if (lmbHeld)
                {
                    _isVacuumingNow = true;
                    if (_orbProjectile == null)
                    {
                        _orbProjectile = GarbageOrbProjectile.Spawn(manager, manager.trashSwarm, center);
                        manager.sfx.PlayPop(0.75f);
                    }
                    _orbProjectile.AbsorbTick(center, Time.deltaTime);
                    AimProgress01 = manager.BinFill01;
                }
                else if (_orbProjectile != null)
                {
                    Destroy(_orbProjectile.gameObject);
                    _orbProjectile = null;
                    AimProgress01 = -1f;
                }
                return;
            }

            // 5티어: 자율 청소 드론 편대 + 트럭 직결 호스
            if (lmbHeld)
            {
                _isVacuumingNow = true;
                if (_blackHoleRig == null)
                {
                    _blackHoleRig = BlackHoleRig.Spawn(manager, manager.trashSwarm, fwd, transform.position);
                    manager.sfx.PlayChime();
                }
                _blackHoleRig.FollowPlayer(transform.position, fwd, floorPlaneY, Time.deltaTime);
                AimProgress01 = manager.BinFill01;
            }
            else if (_blackHoleRig != null)
            {
                Destroy(_blackHoleRig.gameObject);
                _blackHoleRig = null;
                AimProgress01 = -1f;
            }
        }

        private void HandleTreasureDecisionHotkeys()
        {
            if (manager.TreasureDecisionLockTimer > 0f) return;
            bool qDown = false, eDown = false;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null) { qDown = kb.qKey.wasPressedThisFrame; eDown = kb.eKey.wasPressedThisFrame; }
#else
            qDown = Input.GetKeyDown(KeyCode.Q);
            eDown = Input.GetKeyDown(KeyCode.E);
#endif
            if (qDown) manager.ResolveActiveTreasure(false);
            else if (eDown) manager.ResolveActiveTreasure(true);
        }

        private static bool WasTabPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Tab);
#endif
        }

        private static bool WasAltPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            return kb != null && (kb.leftAltKey.wasPressedThisFrame || kb.rightAltKey.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.LeftAlt) || Input.GetKeyDown(KeyCode.RightAlt);
#endif
        }

        /// <summary>
        /// [v1.5.1] 커서 모드 토글: 1인칭 포인터 락 상태에서는 마우스가 화면 중앙에 고정되어
        /// 하단 스테이지 퀵 전환 버튼을 눌러도 시선만 움직이고 클릭이 먹혔다.
        /// [Tab]/[Alt]로 커서를 풀어 버튼을 클릭할 수 있게 하고, 다시 누르면 1인칭으로 복귀한다.
        /// </summary>
        private void ToggleUiCursor()
        {
            if (manager == null) return;
            bool free = !GamePrototypeManager.UiCursorFree;
            GamePrototypeManager.UiCursorFree = free;

            if (free)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                manager.ShowToast("[Alt] 하단 테스트 버튼 클릭 ON");
            }
            else
            {
                LockCursorStrict();
                manager.ShowToast("[Alt] 1인칭 복귀");
            }
            ClearHighlight();
        }

        private static bool WasKeyRPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.R);
#endif
        }

        private static bool WasEscPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        private static bool WasKeyKPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.K);
#endif
        }

        private static bool WasKeyEPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.E);
#endif
        }

        private static bool WasKeyMPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.M);
#endif
        }

        /// <summary>
        /// v1.3 (요청 #5·#10): 트럭 옆 통합 강화소(키오스크) 앞에 서 있을 때 안내를 띄우고,
        /// [E]로 강화 창을 연다. 발판 3개·상시 [Tab] 트리는 제거되어 이 한 곳이 유일한 강화 경로다.
        /// </summary>
        private void HandleKiosk()
        {
            KioskActive = false;
            if (manager == null || !manager.spawnFieldUpgradeStation || manager.fieldStation == null) return;

            FieldUpgradeStation station = manager.fieldStation;
            if (!station.IsPlayerInRange(transform.position)) return;

            KioskActive = true;
            int avail = manager.CountAffordableCashUpgrades();
            KioskTitle = avail > 0 ? $"[Tab] 강화 · {avail}" : "[Tab] 강화";
            KioskDetail = $"{manager.ToolBadge} · {manager.CollectingCount}/{manager.BinCapacity} · ${manager.Money:N0}";
            KioskColor = new Color(0.95f, 0.78f, 0.24f);

            if (WasKeyEPressed()) manager.OpenFieldUpgradeKiosk();
        }

        private void UpdateToolVisuals()
        {
            bool isP2 = manager.Phase == GamePhase.Phase2_FloorMopping;
            int tier = manager.ToolTier;
            GearMode mode = manager.Mode;
            bool gearGun = mode == GearMode.Orb || mode == GearMode.BlackHole;

            if (fistHandVisual != null) fistHandVisual.SetActive(!isP2 && !gearGun && tier == 0);
            if (tongsBroomVisual != null) tongsBroomVisual.SetActive(!isP2 && !gearGun && tier == 1);
            if (vacuumVisual != null) vacuumVisual.SetActive(!isP2 && tier >= 2 && !gearGun);
            if (tissueVisual != null) tissueVisual.SetActive(isP2 && !manager.HasMopUnlocked);
            if (mopVisual != null) mopVisual.SetActive(isP2 && manager.HasMopUnlocked);

            EnsureGearVisuals();
            if (_orbGunBuilt != null) _orbGunBuilt.SetActive(!isP2 && mode == GearMode.Orb);
            if (_bhGunBuilt != null) _bhGunBuilt.SetActive(!isP2 && mode == GearMode.BlackHole);

            // [v1.8] 양손 줍기 노드가 있으면 왼손을 꺼내 양손 교차 모션을 보여준다.
            bool showLeft = !isP2 && manager.HasTwoHand && (mode == GearMode.Hand || mode == GearMode.Broom);
            if (_fistLeftCopy != null) _fistLeftCopy.SetActive(showLeft);
            if (_leftPivot != null && !showLeft)
                _leftPivot.localPosition = new Vector3(-_rightBasePos.x, _rightBasePos.y - 0.25f, _rightBasePos.z - 0.05f);
        }

        private void UpdateCameraEffects()
        {
            if (_cam == null) return;
            int st = manager.StenchStage;
            // v1.2: 방(7.2×6.0m)과 마당 왕복 동선이 한눈에 들어오도록 기본 화각을 72°로 넓힘
            float baseFov = manager.IsAdrenalineZone ? 77f : (manager.HasFeverEngine && manager.IsFeverHot) ? 75f : 72f;
            float targetFov = st == 4 ? 62f : st == 3 ? 66f : st == 2 ? 69f : baseFov;
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, targetFov, 4.5f * Time.deltaTime);
        }

        private Vector2 HandleMovementAndLook()
        {
            float mx = 0f, mz = 0f;
            Vector2 lookDelta = Vector2.zero;
            bool toggleFlash = false, jumpDown = false, crouchHold = false, crouchToggle = false;
            bool lmbHeldNow = false;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed) mz += 1f;
                if (kb.sKey.isPressed) mz -= 1f;
                if (kb.dKey.isPressed) mx += 1f;
                if (kb.aKey.isPressed) mx -= 1f;
                if (kb.spaceKey.wasPressedThisFrame) jumpDown = true;
                if (kb.leftCtrlKey.isPressed) crouchHold = true;
                if (kb.cKey.wasPressedThisFrame) crouchToggle = true;
                if (kb.tKey.wasPressedThisFrame) toggleFlash = true;
                if (kb.hKey.wasPressedThisFrame) ShowHelp = !ShowHelp;
                if (!GamePrototypeManager.UiCursorFree)
                {
                    if (kb.digit1Key.wasPressedThisFrame) manager.SetToolSlot(false);
                    else if (kb.digit2Key.wasPressedThisFrame) manager.SetToolSlot(true);
                }
            }
            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                lookDelta = mouse.delta.ReadValue();
                lmbHeldNow = mouse.leftButton.isPressed;
            }
#else
            mx = Input.GetAxis("Horizontal");
            mz = Input.GetAxis("Vertical");
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                lookDelta = new Vector2(Input.GetAxis("Mouse X") * 12f, Input.GetAxis("Mouse Y") * 12f);
                lmbHeldNow = Input.GetMouseButton(0);
            }
            if (Input.GetKeyDown(KeyCode.Space)) jumpDown = true;
            if (Input.GetKey(KeyCode.LeftControl)) crouchHold = true;
            if (Input.GetKeyDown(KeyCode.C)) crouchToggle = true;
            if (Input.GetKeyDown(KeyCode.T)) toggleFlash = true;
            if (Input.GetKeyDown(KeyCode.H)) ShowHelp = !ShowHelp;
            if (!GamePrototypeManager.UiCursorFree)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) manager.SetToolSlot(false);
                else if (Input.GetKeyDown(KeyCode.Alpha2)) manager.SetToolSlot(true);
            }
#endif

            // [v2.0] [C] 토글 / [LeftCtrl] 홀드 앉기 — 침대·의자·책상 밑 청소!
            if (crouchToggle) _isCrouching = !_isCrouching;
            bool wantCrouch = _isCrouching || crouchHold;
            if (!_camInit && cameraTransform != null)
            {
                _camInit = true;
                _camStandLocalY = cameraTransform.localPosition.y;
                if (_cc != null) { _ccStandHeight = _cc.height; _ccStandCenterY = _cc.center.y; }
            }
            if (cameraTransform != null)
            {
                float targetCamY = wantCrouch ? Mathf.Max(0.62f, _camStandLocalY * 0.48f) : _camStandLocalY;
                Vector3 lp = cameraTransform.localPosition;
                lp.y = Mathf.Lerp(lp.y, targetCamY, 12f * Time.deltaTime);
                cameraTransform.localPosition = lp;
            }
            if (_cc != null)
            {
                float targetH = wantCrouch ? 1.05f : _ccStandHeight;
                float targetCy = wantCrouch ? 0.525f : _ccStandCenterY;
                _cc.height = Mathf.Lerp(_cc.height, targetH, 12f * Time.deltaTime);
                Vector3 ccC = _cc.center;
                ccC.y = Mathf.Lerp(ccC.y, targetCy, 12f * Time.deltaTime);
                _cc.center = ccC;
            }

            if (toggleFlash && flashlight != null) flashlight.enabled = !flashlight.enabled;

            // v1.1.2: Phase 2 걸레질(얼룩을 실제로 닦는 중)에 LMB를 쥐고 있으면 시점은 고정(!) — 멀미 방지.
            // 마우스 이동은 카메라가 아니라 걸레 닦기 파워/모션에만 쓰인다 (LMB를 떼면 즉시 정상 회전).
            // 닦을 얼룩이 없을 때는 LMB를 쥐고 걸어다녀도 시점이 돌아간다(Phase 1 습관 보호).
            bool mopCameraLock = lmbHeldNow && manager.Phase == GamePhase.Phase2_FloorMopping &&
                                 (AimIsStain || _scrubMotionWeight > 0.01f);
            Vector2 cameraLook = mopCameraLock ? Vector2.zero : lookDelta;

            float activeSens = manager.MouseSensitivity;
            float sens = (_scrubMotionWeight > 0.2f) ? activeSens * 0.55f : activeSens;
            transform.Rotate(Vector3.up * (cameraLook.x * sens));
            if (cameraTransform != null)
            {
                _pitch = Mathf.Clamp(_pitch - cameraLook.y * sens, -82f, 82f);
                float coughOffset = manager.CoughKick01 * 2.6f;
                cameraTransform.localRotation = Quaternion.Euler(Mathf.Clamp(_pitch + coughOffset, -84f, 84f), 0f, 0f);
            }

            float dt = Mathf.Min(Time.deltaTime, 0.05f); // 프레임 드랍 시 벽 뚫고 순간이동(터널링) 방지
            if (_cc.isGrounded)
            {
                if (_verticalVel < 0f) _verticalVel = -2.0f;
                if (jumpDown) _verticalVel = Mathf.Sqrt(2f * Mathf.Abs(gravity) * jumpHeight);
            }
            else
            {
                _verticalVel += gravity * dt;
            }

            float speed = baseMoveSpeed * manager.MoveSpeedTotalMult;
            Vector3 horiz = (transform.right * mx + transform.forward * mz).normalized * speed;
            Vector3 move = new Vector3(horiz.x, _verticalVel, horiz.z);
            _cc.Move(move * dt);

            if (transform.position.y < -1.2f && manager.playerSpawnPoint != null)
            {
                TeleportTo(manager.playerSpawnPoint.position + Vector3.up * 0.2f, manager.playerSpawnPoint.rotation);
            }

            return lookDelta;
        }

        private void HandleInteraction(Vector2 mouseDelta)
        {
            if (_punchCooldown > 0f) _punchCooldown -= Time.deltaTime;
            if (_scrubSoundCooldown > 0f) _scrubSoundCooldown -= Time.deltaTime;
            if (_binHintCooldown > 0f) _binHintCooldown -= Time.deltaTime;

            ClearHighlight(false);
            AimIsPile = false;
            AimIsSwarmNear = false;
            AimIsStain = false;
            AimDoor = null;
            AimTreasure = null;
            AimCctv = null;
            NeedMouseShakeHint = false;
            AimProgress01 = -1f;
            _scrubMotionWeight = Mathf.MoveTowards(_scrubMotionWeight, 0f, 6f * Time.deltaTime);
            _scrubSway = Vector2.Lerp(_scrubSway, Vector2.zero, 8f * Time.deltaTime);

            if (cameraTransform == null || Cursor.lockState != CursorLockMode.Locked)
            {
                ClearSwarmSelection();
                if (manager.trashSwarm != null) manager.trashSwarm.SetCursorRing(false, Vector3.zero, 0f);
                return;
            }

            bool lmbHeld = false, lmbDown = false, rmbDown = false, fDown = false, rDown = false;
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            if (mouse != null)
            {
                lmbHeld = mouse.leftButton.isPressed;
                lmbDown = mouse.leftButton.wasPressedThisFrame;
                rmbDown = mouse.rightButton.wasPressedThisFrame;
            }
            if (kb != null)
            {
                fDown = kb.fKey.wasPressedThisFrame;
                rDown = kb.rKey.wasPressedThisFrame;
            }
#else
            lmbHeld = Input.GetMouseButton(0);
            lmbDown = Input.GetMouseButtonDown(0);
            rmbDown = Input.GetMouseButtonDown(1);
            fDown = Input.GetKeyDown(KeyCode.F);
            rDown = Input.GetKeyDown(KeyCode.R);
#endif

            Ray aimRay = new Ray(cameraTransform.position, cameraTransform.forward);
            float floorPlaneY = Mathf.Max(0f, Mathf.Round(transform.position.y * 10f) / 10f);
            if (floorPlaneY < 1.2f) floorPlaneY = 0f;
            else if (floorPlaneY < 4.5f) floorPlaneY = 3.11f;
            else floorPlaneY = 6.11f;
            float planeT = aimRay.direction.y < -0.02f ? (floorPlaneY - aimRay.origin.y) / aimRay.direction.y : -1f;

            CleanableTarget hitPile = null;
            CleanableTarget hitStain = null;
            InteractiveDoor hitDoor = null;
            TreasureTarget hitTreasure = null;
            SecurityCameraController hitCctv = null;
            bool blockedByDoor = false;
            float blockerDist = float.MaxValue;

            // [v1.9] 수평(XZ) 거리 기준 사거리 + 3D 레이 길이 여유!
            float maxReach = manager.PickupReach;
            float rayMax = Mathf.Max(4.8f, maxReach + 2.2f);
            RaycastHit[] hits = Physics.RaycastAll(aimRay, rayMax);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var h in hits)
            {
                if (h.collider.GetComponentInParent<FirstPersonCleanerController>() != null) continue;

                var door = h.collider.GetComponentInParent<InteractiveDoor>();
                if (door != null)
                {
                    if (hitDoor == null && h.distance <= 3.1f) hitDoor = door;
                    if (!door.IsOpen)
                    {
                        blockedByDoor = true;
                        if (h.distance < blockerDist) blockerDist = h.distance;
                        break;
                    }
                    continue;
                }

                var tt = h.collider.GetComponentInParent<TreasureTarget>();
                if (tt != null && tt.State == TreasureState.HiddenInRoom && h.distance <= 3.4f)
                {
                    if (hitTreasure == null) hitTreasure = tt;
                    continue;
                }

                var cctv = h.collider.GetComponentInParent<SecurityCameraController>();
                if (cctv != null && !cctv.IsBlinded && h.distance <= 4.8f)
                {
                    if (hitCctv == null) hitCctv = cctv;
                    continue;
                }

                var c = h.collider.GetComponentInParent<CleanableTarget>();
                if (c != null && !c.IsCleaned)
                {
                    if (c.kind == CleanableKind.TrashPile && hitPile == null) hitPile = c;
                    else if (c.kind == CleanableKind.FloorStain && hitStain == null) hitStain = c;
                    continue;
                }

                // [v2.0] 벽/천장/문틀 같은 대형 구조물만 시선 차단! 의자·침대·책상 같은 가구는 밑 쓰레기 흡입 허용!
                string hName = h.collider.name;
                bool isWallOrStructure =
                    h.collider.bounds.size.y >= 1.85f ||
                    hName.IndexOf("Wall", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    hName.IndexOf("Partition", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    hName.IndexOf("Ceiling", StringComparison.OrdinalIgnoreCase) >= 0;
                if (isWallOrStructure && h.distance < blockerDist) blockerDist = h.distance;
            }

            Vector3 aimPoint = Vector3.zero;
            bool hasAimPoint = false;
            // [v1.9] 1순위: 시선 레이가 3D 쓰레기 더미 표면을 스치면 그 쓰레기 높이에 바로 락온!
            if (manager.trashSwarm != null && !blockedByDoor &&
                manager.trashSwarm.TryGetRayTrashHit(aimRay, maxReach, Mathf.Max(0.24f, manager.SweepRadius * 0.45f), out Vector3 trashHitPt))
            {
                float td = Vector3.Distance(cameraTransform.position, trashHitPt);
                if (td < blockerDist - 0.03f)
                {
                    aimPoint = trashHitPt;
                    hasAimPoint = true;
                }
            }
            // 2순위: 바닥 평면 교차점 (수평 거리 maxReach 이내!)
            if (!hasAimPoint && planeT > 0.05f)
            {
                float useT = Mathf.Min(planeT, blockerDist - 0.05f);
                if (useT > 0.18f)
                {
                    Vector3 pt = aimRay.GetPoint(useT);
                    pt.y = floorPlaneY;
                    Vector2 hDelta = new Vector2(pt.x - transform.position.x, pt.z - transform.position.z);
                    if (hDelta.magnitude <= maxReach)
                    {
                        aimPoint = pt;
                        hasAimPoint = true;
                    }
                }
            }

            if (manager.trashSwarm != null)
            {
                bool showRing = hasAimPoint && manager.Phase == GamePhase.Phase1_TrashRemoval;
                manager.trashSwarm.SetCursorRing(showRing, aimPoint, manager.SweepRadius);
            }

            // ===== 트럭 1:1 정산 구역 =====
            SettlementZoneActive = manager.IsPlayerInTruckSettleZone(transform.position);
            SettlementReadyCount = manager.CollectingCount;

            if (rmbDown)
            {
                if (SettlementZoneActive && manager.CollectingCount > 0)
                {
                    Vector3 origin = manager.GetLowRightPourOrigin();
                    if (manager.SettleBinAtTruck(origin, false))
                    {
                        _grabSwingTimer = 0.22f;
                    }
                    return;
                }
                if (manager.CollectingCount > 0 && !SettlementZoneActive && _binHintCooldown <= 0f)
                {
                    _binHintCooldown = 1.6f;
                    manager.ShowToast(manager.VehicleHasRemoteSettle
                        ? $"자동 정산 대기 ({manager.RemoteSettleFill01 * 100f:0}%)"
                        : manager.VehicleHasPorchSettle ? "현관 앞 자동 정산"
                            : manager.VehicleHasYardSuction ? "마당 진입 시 자동 정산"
                                : $"수거통 {manager.CollectingCount}개 · 차량에서 [RMB] 정산");
                }
            }

            // 1) [F] 현관문 열기/닫기
            if (hitDoor != null)
            {
                _lastDoor = hitDoor;
                hitDoor.SetHighlighted(true);
                AimDoor = hitDoor;
                if (fDown) { hitDoor.ToggleDoor(manager.sfx); return; }
            }

            if (blockedByDoor) { ClearSwarmSelection(); return; }

            // 1.5) [v2.1] 보물 / 의뢰인 분실물 발견 → [F]/[R] 또는 클릭 시 화려한 대형 득템 팝업 오픈!
            if (hitTreasure != null)
            {
                _lastTreasure = hitTreasure;
                hitTreasure.SetHighlighted(true);
                AimTreasure = hitTreasure;
                if (fDown || rDown || lmbDown)
                {
                    ClearSwarmSelection();
                    manager.OpenTreasureDecision(hitTreasure);
                    return;
                }
            }

            bool binBlocked = manager.IsBinFull;

            float workMult = Mathf.Max(0.35f, manager.StenchWorkSpeedMult);

            // ===== PHASE 1 =====
            if (manager.Phase == GamePhase.Phase1_TrashRemoval)
            {
                if (hitPile != null && Vector3.Distance(cameraTransform.position, hitPile.transform.position) <= 4.0f)
                {
                    _lastTarget = hitPile;
                    hitPile.SetHighlighted(true);
                    AimIsPile = true;
                    AimProgress01 = hitPile.CleanProgress01;
                    AimPileHp01 = hitPile.HpRatio01;

                    if (lmbDown && _punchCooldown <= 0f)
                    {
                        _punchCooldown = 0.12f;
                        _punchTimer = 0.15f;
                        float dmg = manager.RollFistDamage(out bool fistCrit);
                        if (hitPile.ApplyCleanDamage(dmg, null)) manager.OnBigPileBurst(hitPile);
                        else manager.sfx.PlayScrub();
                        if (fistCrit) manager.SpawnFloatingText(hitPile.transform.position + Vector3.up * 0.9f,
                            $"CRIT ×{manager.FistCritMult:0.0}!", new Color(1f, 0.62f, 0.24f), 0.8f);
                        ClearSwarmSelection();
                        return;
                    }
                }

                if (manager.Mode == GearMode.Orb || manager.Mode == GearMode.BlackHole)
                {
                    HandleGearMode(lmbHeld, floorPlaneY);
                    return;
                }

                _hasReachTarget = false;
                if (manager.trashSwarm != null && hasAimPoint && !binBlocked)
                {
                    float r = manager.SweepRadius;
                    int grabLimit = manager.AutoGrabPerTick;
                    manager.trashSwarm.UpdateHoverSelection(true, lmbHeld, aimPoint, r, grabLimit, cameraTransform.position, cameraTransform.forward);
                    AimIsSwarmNear = manager.trashSwarm.HoveredCount > 0;

                    if (AimIsSwarmNear && manager.trashSwarm.TryGetPrimaryTargetPos(out Vector3 targetWorld))
                    {
                        Vector3 localT = cameraTransform.InverseTransformPoint(targetWorld);
                        float dist = localT.magnitude;
                        if (dist > 1.25f) localT = localT.normalized * 1.25f;
                        localT.x = Mathf.Clamp(localT.x, -0.58f, 0.58f);
                        localT.y = Mathf.Clamp(localT.y + 0.05f, -0.72f, 0.16f);
                        localT.z = Mathf.Clamp(localT.z - 0.06f, 0.34f, 1.22f);
                        _reachTargetLocal = localT;
                        _hasReachTarget = true;
                    }

                    EnsureSuckTarget();
                    Transform dest = _suckTarget != null ? _suckTarget : rightToolPivot;
                    int picked = manager.trashSwarm.TickHoldChargeAndPull(
                        lmbHeld && AimIsSwarmNear,
                        Time.deltaTime,
                        manager.EffectiveHoldDuration,
                        dest,
                        manager.CritChance,
                        manager.ChainChance,
                        manager.ChainExtraCount,
                        out bool didCrit,
                        out bool didChain,
                        out Vector3 fxPos);

                    if (lmbHeld && AimIsSwarmNear)
                    {
                        _isHoldingTrashNow = true;
                        if (manager.ToolTier >= 2) _isVacuumingNow = true;

                        float charge01 = manager.trashSwarm.MaxSelectedCharge01;
                        if (charge01 > 0.01f) AimProgress01 = charge01;
                    }

                    if (didCrit) manager.SpawnFloatingText(fxPos + Vector3.up * 0.25f, "CRIT ×2!", new Color(1f, 0.88f, 0.22f), 0.9f);
                    if (didChain) manager.SpawnFloatingText(fxPos + Vector3.up * 0.45f, "CHAIN!", new Color(1f, 0.56f, 0.22f), 0.9f);

                    if (picked > 0 && manager.ToolTier < 2 && _punchTimer <= 0f)
                    {
                        // [v1.8.1 수정 #1] 쓰레기를 집는 순간 타겟 위치에서 내 쪽 수거통으로 쭉 당겨오는 회수 모션 발동!
                        _pullBackDur = Mathf.Clamp(manager.EffectiveHoldDuration * 0.32f, 0.14f, 0.26f);
                        _pullBackTimer = _pullBackDur;
                        _pullWithRight = manager.HasTwoHand ? !_pullWithRight : true;
                        _pullStartLocal = _hasReachTarget ? _reachTargetLocal
                            : (_pullWithRight ? rightToolPivot.localPosition : (_leftPivot != null ? _leftPivot.localPosition : rightToolPivot.localPosition));
                        _grabSwingTimer = 0f;
                    }
                }
                else
                {
                    ClearSwarmSelection();
                }
            }
            // ===== PHASE 2: 밀대/걸레 바닥 닦기 & CCTV 거품 무력화 =====
            else if (manager.Phase == GamePhase.Phase2_FloorMopping)
            {
                ClearSwarmSelection();
                if (hitCctv != null)
                {
                    AimCctv = hitCctv;
                    if (lmbHeld)
                    {
                        _scrubMotionWeight = 1f;
                        _scrubPhase += 10f * Time.deltaTime;
                        if (_scrubSoundCooldown <= 0f) { _scrubSoundCooldown = 0.16f; manager.sfx.PlayScrub(); }
                        if (hitCctv.ApplyFoamSpray(Time.deltaTime, 25f))
                        {
                            manager.ShowToast("★ CCTV 거품 무력화! (25초간 시야 차단)");
                            manager.sfx.PlayChime();
                        }
                    }
                    return;
                }
                if (hitStain != null)
                {
                    _lastTarget = hitStain;
                    hitStain.SetHighlighted(true);
                    AimIsStain = true;
                    AimProgress01 = hitStain.CleanProgress01;

                    if (lmbHeld)
                    {
                        float shake = mouseDelta.magnitude;
                        if (shake < 1.8f) NeedMouseShakeHint = true;

                        // v1.1.2: 꾹 누르고만 있어도 기본 속도로 닦이고, 마우스를 흔들면 가속(보너스).
                        float power = (BaseScrubPower + Mathf.Min(shake, 50f) * 0.145f) * manager.ScrubSpeedMult * workMult;
                        _scrubMotionWeight = 1f;
                        _scrubPhase += (6f + Mathf.Min(shake, 32f) * 0.9f) * Time.deltaTime;
                        _scrubSway = Vector2.Lerp(_scrubSway, Vector2.ClampMagnitude(mouseDelta, 40f), 12f * Time.deltaTime);
                        if (_scrubSoundCooldown <= 0f) { _scrubSoundCooldown = 0.18f; manager.sfx.PlayScrub(); }
                        if (hitStain.ApplyCleanDamage(power * Time.deltaTime, null)) manager.OnStainCleaned(hitStain);
                    }
                }
            }
        }

        private void AnimateHands()
        {
            if (rightToolPivot == null) return;
            Vector3 targetPos = _rightBasePos;
            Quaternion targetRot = _rightBaseRot;
            bool leftShown = _leftPivot != null && _fistLeftCopy != null && _fistLeftCopy.activeSelf;
            Vector3 leftPos = leftShown ? new Vector3(-_rightBasePos.x, _rightBasePos.y - 0.05f, _rightBasePos.z) : Vector3.zero;
            Quaternion leftRot = _rightBaseRot;

            // v1.2: 진공 단계에서만 노즐 앞 소용돌이가 보이도록 (차징이 오를수록 조금 더 세짐)
            if (_vacuumVortex != null)
            {
                bool sucking = _isVacuumingNow && manager.Mode == GearMode.Vacuum;
                float charge = manager.trashSwarm != null ? Mathf.Clamp01(manager.trashSwarm.MaxSelectedCharge01) : 0f;
                _vacuumVortex.SetIntensity(sucking ? 0.75f + charge * 0.25f : 0f);
            }

            if (_scrubMotionWeight > 0.05f && manager.Phase == GamePhase.Phase2_FloorMopping)
            {
                // v1.3 (요청 #13): 행주질은 화면 구석이 아니라 '화면 중앙 앞쪽'에서 좌우/앞뒤로 문지른다.
                //   피벗 기본 위치(0.32, -0.26, 0.52)를 중앙(0, -0.16, 0.60)으로 끌어와 걸레가 시야 가운데를 쓴다.
                float swayX = Mathf.Clamp(_scrubSway.x * 0.012f, -0.11f, 0.11f);
                float swayZ = Mathf.Clamp(-_scrubSway.y * 0.007f, -0.07f, 0.07f);
                float centerX = -_rightBasePos.x;          // 피벗 기본 X를 화면 중앙(0)으로 되돌린다
                float centerY = -_rightBasePos.y + 0.08f;  // 조금 위로 올려 손이 화면 가운데를 쓴다
                if (manager.HasMopUnlocked)
                {
                    float s = Mathf.Sin(_scrubPhase * 1.6f);
                    float c = Mathf.Cos(_scrubPhase * 1.6f);
                    targetPos = new Vector3(centerX + s * 0.17f + swayX, centerY - 0.05f, 0.62f + c * 0.09f + swayZ);
                    targetRot = Quaternion.Euler(54f, s * 10f + swayX * 90f, -s * 8f + swayZ * 60f);
                }
                else
                {
                    float s = Mathf.Cos(_scrubPhase * 2.6f) * 0.13f;
                    float c = Mathf.Sin(_scrubPhase * 2.6f) * 0.10f;
                    targetPos = new Vector3(centerX + s + swayX, centerY - 0.08f, 0.56f + c + swayZ);
                    targetRot = Quaternion.Euler(64f, s * 60f + swayX * 110f, -c * 70f + swayZ * 60f);
                }
            }
            else if (_isVacuumingNow)
            {
                targetPos += new Vector3(0f, 0.01f, 0.08f) + UnityEngine.Random.insideUnitSphere * 0.012f;
                targetRot = Quaternion.Euler(-5f, 0f, 0f);
            }
            else if (_pullBackTimer > 0f && !_isVacuumingNow && (manager.Mode == GearMode.Hand || manager.Mode == GearMode.Broom))
            {
                // [v1.8.1 수정 #1] 집은 쓰레기를 타겟 위치에서 내 몸(수거통) 쪽으로 쭉 당겨 넣기!
                _pullBackTimer = Mathf.Max(0f, _pullBackTimer - Time.deltaTime);
                float u = 1f - (_pullBackTimer / Mathf.Max(0.05f, _pullBackDur));
                float ease = u * u * (3f - 2f * u);
                Vector3 tuckPos = new Vector3(_pullWithRight ? 0.10f : -0.10f, -0.44f, 0.28f);
                Vector3 curPos = u < 0.72f
                    ? Vector3.Lerp(_pullStartLocal, tuckPos, ease / 0.72f)
                    : Vector3.Lerp(tuckPos, _pullWithRight ? _rightBasePos : new Vector3(-_rightBasePos.x, _rightBasePos.y - 0.05f, _rightBasePos.z), (u - 0.72f) / 0.28f);
                Quaternion pinchRot = Quaternion.Slerp(Quaternion.Euler(46f, _pullWithRight ? -24f : 24f, _pullWithRight ? 18f : -18f), Quaternion.Euler(-12f, _pullWithRight ? 16f : -16f, 0f), ease);

                if (_pullWithRight || !leftShown)
                {
                    targetPos = curPos;
                    targetRot = pinchRot;
                }
                else
                {
                    leftPos = curPos;
                    leftRot = pinchRot;
                }
            }
            else if (_punchTimer > 0f)
            {
                _punchTimer -= Time.deltaTime;
                float t = Mathf.Sin(Mathf.Clamp01(_punchTimer / 0.15f) * Mathf.PI);
                targetPos += new Vector3(-0.14f * t, 0.04f * t, 0.28f * t);
                targetRot = Quaternion.Euler(-20f * t, -16f * t, 14f * t);
            }
            else if (_grabSwingTimer > 0f)
            {
                _grabSwingTimer -= Time.deltaTime;
                float dur = manager.ToolTier == 1 ? 0.14f : 0.18f;
                float t = Mathf.Sin(Mathf.Clamp01(_grabSwingTimer / dur) * Mathf.PI);
                targetPos += new Vector3(-0.20f * t, -0.11f * t, 0.20f * t);
                targetRot = Quaternion.Euler(26f * t, -30f * t, 14f * t);
            }
            else if (_isHoldingTrashNow && (manager.Mode == GearMode.Hand || manager.Mode == GearMode.Broom))
            {
                // [v1.8.1 수정 #1] 손이 실제 타겟팅한 쓰레기 좌표까지 쭉 뻗어가(0→0.55) 손으로 꼭 집는 모션(0.55→1.0)! (진동 없음)
                float ch = manager.trashSwarm != null ? Mathf.Clamp01(manager.trashSwarm.MaxSelectedCharge01) : 0f;
                Vector3 goal = _hasReachTarget ? _reachTargetLocal : (_rightBasePos + new Vector3(-0.18f, -0.12f, 0.42f));
                bool useRight = !leftShown || _pullWithRight;
                Vector3 startP = useRight ? _rightBasePos : new Vector3(-_rightBasePos.x, _rightBasePos.y - 0.05f, _rightBasePos.z);

                float reachT = Mathf.Clamp01(ch / 0.52f);
                reachT = reachT * reachT * (3f - 2f * reachT);
                float pinchT = Mathf.Clamp01((ch - 0.52f) / 0.48f);

                // 타겟까지 아치형으로 뻗어가서(reachT) 살짝 아래로 눌러 꼭 쥐는(pinchT) 리듬!
                Vector3 p = Vector3.Lerp(startP, goal, reachT);
                p.y += Mathf.Sin(reachT * Mathf.PI) * 0.06f - pinchT * 0.035f;
                Quaternion rot = Quaternion.Euler(
                    Mathf.Lerp(10f, 38f, reachT) + pinchT * 14f,
                    (useRight ? -1f : 1f) * (Mathf.Lerp(6f, 22f, reachT) + pinchT * 8f),
                    (useRight ? 1f : -1f) * (pinchT * 16f));

                if (useRight)
                {
                    targetPos = p;
                    targetRot = rot;
                }
                else
                {
                    leftPos = p;
                    leftRot = rot;
                }
            }
            else
            {
                // v1.2: 티어마다 손에 든 도구의 기본 자세를 다르게 → 실루엣이 한눈에 구분된다
                if (manager.Phase == GamePhase.Phase1_TrashRemoval)
                {
                    int tier = manager.ToolTier;
                    if (tier >= 2)
                    {
                        // 진공: 무게감 있게 낮게, 노즐을 바닥 쪽으로
                        targetPos += new Vector3(0.03f, 0.02f, 0.03f);
                        targetRot = Quaternion.Euler(8f, 2f, -2f);
                    }
                    else if (tier == 1)
                    {
                        // 빗자루: 몸 바깥으로 비스듬히 쓸어내는 자세
                        targetPos += new Vector3(-0.03f, -0.01f, 0.02f);
                        targetRot = Quaternion.Euler(-6f, -8f, 7f);
                    }
                }
                targetPos += new Vector3(0f, Mathf.Sin(Time.time * 2.4f) * 0.005f, 0f);
                if (leftShown)
                    leftPos += new Vector3(0f, Mathf.Sin(Time.time * 2.1f + 1.3f) * 0.006f, 0f);
            }

            rightToolPivot.localPosition = Vector3.Lerp(rightToolPivot.localPosition, targetPos, 22f * Time.deltaTime);
            rightToolPivot.localRotation = Quaternion.Slerp(rightToolPivot.localRotation, targetRot, 22f * Time.deltaTime);

            if (leftShown && _leftPivot != null)
            {
                _leftPivot.localPosition = Vector3.Lerp(_leftPivot.localPosition, leftPos, 26f * Time.deltaTime);
                _leftPivot.localRotation = Quaternion.Slerp(_leftPivot.localRotation, leftRot, 26f * Time.deltaTime);
            }
        }

        private void ClearHighlight(bool clearSwarmLock = true)
        {
            if (_lastTarget != null) { _lastTarget.SetHighlighted(false); _lastTarget = null; }
            if (_lastDoor != null) { _lastDoor.SetHighlighted(false); _lastDoor = null; }
            if (_lastTreasure != null) { _lastTreasure.SetHighlighted(false); _lastTreasure = null; }
            if (clearSwarmLock) ClearSwarmSelection();
        }

        private void ClearSwarmSelection()
        {
            if (manager != null && manager.trashSwarm != null)
                manager.trashSwarm.UpdateHoverSelection(false, false, Vector3.zero, 0f, 0);
        }
    }
}
