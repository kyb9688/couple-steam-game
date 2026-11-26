using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TrashCleaner3D
{
    /// <summary>
    /// v1.0 1인칭 컨트롤러:
    /// - [LMB 꾹(HOLD)] = 바닥 쓰레기 전용! (맨손 0.22초 진동 차징 후 쏙 / 집게 0.15초 / 진공 0.07초 — 큰 쓰레기 봉지에는 데미지 X!)
    /// - [E] = 트럭 옆 '현장 강화' 발판 위에서 범위·흡입·용량 강화 구매
    /// - [LMB 단타 클릭] = 큰 쓰레기 봉투/박스 타격 전용!
    /// - 봉투 100% 정직 원복 (벽 충돌 과적 폭발 제거), 완성 시 [RMB] 투하 / 바닥 봉지 [RMB] 회수 통일!
    /// - [F] 현관문 / [K] 80% 조기 정산 / [Space] 점프 / [Tab] 스킬트리 / [ESC] 설정
    /// - v1.2: 진공 단계 흡입 소용돌이 연출 + 트럭 정산·봉투 폭발·구역 클리어의 짧은 카메라 흔들림
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonCleanerController : MonoBehaviour
    {
        public GamePrototypeManager manager;
        public Transform cameraTransform;
        public Transform rightToolPivot;
        public Transform leftBagPivot;
        public GameObject fistHandVisual;
        public GameObject tongsBroomVisual;
        public GameObject vacuumVisual;
        public GameObject tissueVisual;
        public GameObject mopVisual;
        public Light flashlight;
        public float baseMoveSpeed = 3.85f;
        public float mouseSensitivity = 0.14f;
        public float jumpHeight = 0.85f;
        public float gravity = -18.5f;

        public bool AimIsPile { get; private set; }
        public bool AimIsSwarmNear { get; private set; }
        public bool AimIsStain { get; private set; }
        public InteractiveDoor AimDoor { get; private set; }
        public ThrownTrashBag AimDroppedBag { get; private set; }
        public bool NeedMouseShakeHint { get; private set; }
        public float AimProgress01 { get; private set; } = -1f;
        public bool ShowHelp { get; private set; }

        // ===== v1.0 트럭 옆 '현장 강화' 발판 상태 (HUD 표시용) =====
        public bool FieldPadActive { get; private set; }
        public bool FieldPadCanBuy { get; private set; }
        public bool FieldPadMaxed { get; private set; }
        public string FieldPadTitle { get; private set; } = "";
        public string FieldPadDetail { get; private set; } = "";
        public Color FieldPadColor { get; private set; } = Color.white;

        private CharacterController _cc;
        private Camera _cam;
        private float _pitch;
        private float _verticalVel;
        private CleanableTarget _lastTarget;
        private InteractiveDoor _lastDoor;
        private ThrownTrashBag _lastDroppedBag;
        private float _punchCooldown;
        private float _bagHintCooldown;

        private Vector3 _rightBasePos;
        private Quaternion _rightBaseRot;
        private Vector3 _leftBasePos;

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

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            if (cameraTransform != null) _cam = cameraTransform.GetComponent<Camera>();
            if (rightToolPivot != null) { _rightBasePos = rightToolPivot.localPosition; _rightBaseRot = rightToolPivot.localRotation; }
            if (leftBagPivot != null) _leftBasePos = leftBagPivot.localPosition;
            if (cameraTransform != null) _camBaseLocalPos = cameraTransform.localPosition;
        }

        private void Start()
        {
            if (manager == null) manager = FindFirstObjectByType<GamePrototypeManager>();
            if (manager != null) mouseSensitivity = manager.MouseSensitivity;
            EnsureVacuumVortex();
#if !UNITY_WEBGL || UNITY_EDITOR
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
            if (hasFocus && manager != null &&
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

            if (WasEscPressed()) { manager.ToggleSettingsMenu(); ClearHighlight(); return; }

            if (manager.Phase == GamePhase.TreasureDecision)
            {
                ClearHighlight();
                HandleTreasureDecisionHotkeys();
                return;
            }

            if (WasTabPressed()) { manager.ToggleMindMap(); ClearHighlight(); return; }

            if (manager.Phase != GamePhase.Phase1_TrashRemoval && manager.Phase != GamePhase.Phase2_FloorMopping)
            {
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
            if (Cursor.lockState != CursorLockMode.Locked)
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
            if (Application.isFocused && Cursor.lockState != CursorLockMode.Locked) LockCursorStrict();
#endif

            _isHoldingTrashNow = false;
            _isVacuumingNow = false;
            UpdateToolVisuals();
            UpdateCameraEffects();
            Vector2 mouseDelta = HandleMovementAndLook();
            HandleFieldStation();
            HandleInteraction(mouseDelta);
            AnimateHandsAndBag();
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

        /// <summary>
        /// 트럭 옆 '현장 강화' 발판 위에 서 있으면 HUD 안내를 켜고, [E]로 그 자리에서 바로 강화를 산다.
        /// </summary>
        private void HandleFieldStation()
        {
            FieldPadActive = false;
            if (manager == null || manager.fieldStation == null) return;

            FieldUpgradeStation station = manager.fieldStation;
            FieldUpgradeStation.PadInfo info;
            if (!station.TryGetStandingPad(transform.position, out info)) return;

            FieldPadActive = true;
            FieldPadCanBuy = info.affordable;
            FieldPadMaxed = info.maxed;
            FieldPadTitle = info.title;
            FieldPadDetail = info.maxed
                ? info.stat
                : $"{info.stat}   ·   보유 ${manager.Money}";
            FieldPadColor = info.color;

            if (WasKeyEPressed()) station.TryBuy(info);
        }

        private void UpdateToolVisuals()
        {
            bool isP2 = manager.Phase == GamePhase.Phase2_FloorMopping;
            int tier = manager.ToolTier;

            if (fistHandVisual != null) fistHandVisual.SetActive(!isP2 && tier == 0);
            if (tongsBroomVisual != null) tongsBroomVisual.SetActive(!isP2 && tier == 1);
            if (vacuumVisual != null) vacuumVisual.SetActive(!isP2 && tier >= 2);
            if (tissueVisual != null) tissueVisual.SetActive(isP2 && !manager.HasMopUnlocked);
            if (mopVisual != null) mopVisual.SetActive(isP2 && manager.HasMopUnlocked);

            if (leftBagPivot != null) leftBagPivot.gameObject.SetActive(manager.HasCarriedBag);
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
            bool toggleFlash = false, jumpDown = false;
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
                if (kb.tKey.wasPressedThisFrame) toggleFlash = true;
                if (kb.hKey.wasPressedThisFrame) ShowHelp = !ShowHelp;
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
            if (Input.GetKeyDown(KeyCode.T)) toggleFlash = true;
            if (Input.GetKeyDown(KeyCode.H)) ShowHelp = !ShowHelp;
#endif

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

            if (_cc.isGrounded)
            {
                if (_verticalVel < 0f) _verticalVel = -2.0f;
                if (jumpDown) _verticalVel = Mathf.Sqrt(2f * Mathf.Abs(gravity) * jumpHeight);
            }
            else
            {
                _verticalVel += gravity * Time.deltaTime;
            }

            float speed = baseMoveSpeed * manager.MoveSpeedTotalMult;
            Vector3 horiz = (transform.right * mx + transform.forward * mz).normalized * speed;
            Vector3 move = new Vector3(horiz.x, _verticalVel, horiz.z);
            _cc.Move(move * Time.deltaTime);

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
            if (_bagHintCooldown > 0f) _bagHintCooldown -= Time.deltaTime;

            // 주의: 매 프레임 스웜 락온을 지우지 않음! (clearSwarmLock: false)
            ClearHighlight(false);
            AimIsPile = false;
            AimIsSwarmNear = false;
            AimIsStain = false;
            AimDoor = null;
            AimDroppedBag = null;
            NeedMouseShakeHint = false;
            AimProgress01 = -1f;
            _scrubMotionWeight = Mathf.MoveTowards(_scrubMotionWeight, 0f, 6f * Time.deltaTime);
            _scrubSway = Vector2.Lerp(_scrubSway, Vector2.zero, 8f * Time.deltaTime);

            if (cameraTransform == null || Cursor.lockState != CursorLockMode.Locked)
            {
                ClearSwarmSelection();
                return;
            }

            bool lmbHeld = false, lmbDown = false, rmbDown = false, fDown = false;
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            if (mouse != null)
            {
                lmbHeld = mouse.leftButton.isPressed;
                lmbDown = mouse.leftButton.wasPressedThisFrame;
                rmbDown = mouse.rightButton.wasPressedThisFrame;
            }
            if (kb != null) fDown = kb.fKey.wasPressedThisFrame;
#else
            lmbHeld = Input.GetMouseButton(0);
            lmbDown = Input.GetMouseButtonDown(0);
            rmbDown = Input.GetMouseButtonDown(1);
            fDown = Input.GetKeyDown(KeyCode.F);
#endif

            Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
            RaycastHit[] hits = Physics.RaycastAll(ray, 5.8f);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            CleanableTarget hitPile = null;
            CleanableTarget hitStain = null;
            InteractiveDoor hitDoor = null;
            ThrownTrashBag hitDroppedBag = null;
            Vector3 worldAimPoint = Vector3.zero;
            bool hasWorldPoint = false;
            bool blockedByDoor = false;

            foreach (var h in hits)
            {
                if (h.collider.GetComponentInParent<FirstPersonCleanerController>() != null) continue;

                var dropped = h.collider.GetComponentInParent<ThrownTrashBag>();
                if (dropped != null && dropped.IsRetrievable && hitDroppedBag == null && h.distance <= 4.0f)
                {
                    hitDroppedBag = dropped;
                    continue;
                }

                var door = h.collider.GetComponentInParent<InteractiveDoor>();
                if (door != null)
                {
                    if (hitDoor == null && h.distance <= 3.0f) hitDoor = door;
                    if (!door.IsOpen) { blockedByDoor = true; break; }
                    continue;
                }

                var c = h.collider.GetComponentInParent<CleanableTarget>();
                if (c != null && !c.IsCleaned)
                {
                    if (c.kind == CleanableKind.TrashPile && hitPile == null) hitPile = c;
                    else if (c.kind == CleanableKind.FloorStain && hitStain == null) hitStain = c;
                }

                // 바닥 쓰레기 오라용 월드 포인트: 큰 봉투에 가려지지 않고 실제 바닥/환경 좌표도 잡도록 우선 기록
                if (!hasWorldPoint && (c == null || c.kind != CleanableKind.TrashPile))
                {
                    worldAimPoint = h.point;
                    hasWorldPoint = true;
                }
            }

            // 큰 봉투만 맞았다면 그 지점이라도 기준점으로 사용
            if (!hasWorldPoint && hitPile != null)
            {
                worldAimPoint = hitPile.transform.position;
                hasWorldPoint = true;
            }

            // v1.1.2 디자인: 바닥을 조준하면 흡입 반경(SweepRadius)을 원형 링으로 항상 보여준다.
            // (쓰레기가 반경 밖에 있어도 보여서 "어디까지 빨려오는지"가 한눈에 읽힌다)
            if (manager.trashSwarm != null)
            {
                bool showRing = hasWorldPoint &&
                                manager.Phase == GamePhase.Phase1_TrashRemoval &&
                                worldAimPoint.y <= 0.95f &&
                                Vector3.Distance(cameraTransform.position, worldAimPoint) <= 5.8f &&
                                !manager.HasCarriedBag;
                manager.trashSwarm.SetCursorRing(showRing, worldAimPoint, manager.SweepRadius);
            }

            // 0) [RMB] 통일: 손에 쥔 봉지 투하 / 바닥 봉지 회수
            if (hitDroppedBag != null)
            {
                _lastDroppedBag = hitDroppedBag;
                hitDroppedBag.SetHighlighted(true);
                AimDroppedBag = hitDroppedBag;
            }

            if (rmbDown)
            {
                Vector3 origin = cameraTransform.position + cameraTransform.forward * 0.55f - cameraTransform.right * 0.14f;
                if (manager.HasCarriedBag)
                {
                    manager.ThrowCarriedBag(origin, cameraTransform.forward);
                    _grabSwingTimer = 0.22f;
                    return;
                }
                else if (hitDroppedBag != null)
                {
                    hitDroppedBag.RetrieveIntoHand();
                    _grabSwingTimer = 0.16f;
                    return;
                }
                else if (manager.CanTieBagEarly)
                {
                    // 요청 #5: 쓰레기 절반(50%) 이상 모으면 [RMB]로 즉시 봉지 묶기!
                    manager.TieBagEarlyIntoHand();
                    _grabSwingTimer = 0.18f;
                    return;
                }
                else if (manager.CollectingCount > 0)
                {
                    manager.ShowToast($"절반 이상 필요 ({manager.CollectingCount}/{manager.HalfBagThreshold})");
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

            if (manager.IsTooSuffocatedToWork)
            {
                ClearSwarmSelection();
                if (lmbDown) { manager.sfx.PlayAlert(); manager.ShowToast("☣ 작업 불가 · 문 밖 환기"); }
                return;
            }

            // 손에 완성 봉지 1개 쥐고 있으면 바닥 작업 차단
            if (manager.HasCarriedBag)
            {
                ClearSwarmSelection();
                // v1.1 UX: 봉투가 차서 쓸기가 막힌 이유를 알 수 있게, 꾹 누르고 있는 동안에도 안내(1.4초 간격)
                if (lmbHeld && _bagHintCooldown <= 0f)
                {
                    _bagHintCooldown = 1.4f;
                    manager.ShowToast($"봉투 가득 (${manager.CarriedBagValue}) · [RMB] {manager.CurrentVehicleName}에 투하");
                }
                return;
            }

            float workMult = Mathf.Max(0.35f, manager.StenchWorkSpeedMult);

            // ===== PHASE 1 =====
            if (manager.Phase == GamePhase.Phase1_TrashRemoval)
            {
                // 요청 #4: 큰 쓰레기 봉투/박스 타격은 오직 [LMB 단타 클릭(lmbDown)] 전용!
                // (꾹 누르고 있을 때는 큰 봉투에 데미지가 절대 들어가지 않음!)
                if (hitPile != null && Vector3.Distance(cameraTransform.position, hitPile.transform.position) <= 4.0f)
                {
                    _lastTarget = hitPile;
                    hitPile.SetHighlighted(true);
                    AimIsPile = true;
                    AimProgress01 = hitPile.CleanProgress01;

                    if (lmbDown && _punchCooldown <= 0f)
                    {
                        _punchCooldown = 0.12f;
                        _punchTimer = 0.15f;
                        float dmg = manager.FistDamage;
                        manager.ApplyShockwaveToNearbyPiles(hitPile, dmg);
                        if (hitPile.ApplyCleanDamage(dmg, null)) manager.OnBigPileBurst(hitPile);
                        else manager.sfx.PlayScrub();
                        ClearSwarmSelection();
                        return;
                    }
                }

                // 요청 #1: [LMB 꾹(HOLD)] = 한번 락온된 쓰레기는 커서 원 반경 밖으로 나가기 전까지 절대 초기화 X!
                if (manager.trashSwarm != null && hasWorldPoint && Vector3.Distance(cameraTransform.position, worldAimPoint) <= 5.0f)
                {
                    float r = manager.SweepRadius;
                    int grabLimit = manager.AutoGrabPerTick;
                    manager.trashSwarm.UpdateHoverSelection(true, lmbHeld, worldAimPoint, r, grabLimit);
                    AimIsSwarmNear = manager.trashSwarm.HoveredCount > 0;

                    Transform dest = manager.ToolTier >= 2 ? rightToolPivot : (leftBagPivot != null ? leftBagPivot : rightToolPivot);
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
                        // v1.1.2: 차지 중 반복 PlayPop 제거 — 실제 쓰레기가 뽑혀 들어갈 때의 PlayNote(도레미파솔)만 남긴다.
                    }

                    if (didCrit) manager.SpawnFloatingText(fxPos + Vector3.up * 0.25f, "CRIT ×2!", new Color(1f, 0.88f, 0.22f), 0.9f);
                    if (didChain) manager.SpawnFloatingText(fxPos + Vector3.up * 0.45f, "CHAIN!", new Color(1f, 0.56f, 0.22f), 0.9f);

                    if (picked > 0 && manager.ToolTier < 2 && _punchTimer <= 0f)
                    {
                        _grabSwingTimer = manager.ToolTier == 1 ? 0.14f : 0.18f;
                    }
                }
                else
                {
                    ClearSwarmSelection();
                }
            }
            // ===== PHASE 2: 밀대/걸레 바닥 닦기 =====
            else if (manager.Phase == GamePhase.Phase2_FloorMopping)
            {
                ClearSwarmSelection();
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

        private void AnimateHandsAndBag()
        {
            if (rightToolPivot == null) return;
            Vector3 targetPos = _rightBasePos;
            Quaternion targetRot = _rightBaseRot;

            // v1.2: 진공 단계에서만 노즐 앞 소용돌이가 보이도록 (차징이 오를수록 조금 더 세짐)
            if (_vacuumVortex != null)
            {
                bool sucking = _isVacuumingNow && manager.ToolTier >= 2 && !manager.HasCarriedBag;
                float charge = manager.trashSwarm != null ? Mathf.Clamp01(manager.trashSwarm.MaxSelectedCharge01) : 0f;
                _vacuumVortex.SetIntensity(sucking ? 0.75f + charge * 0.25f : 0f);
            }

            if (_scrubMotionWeight > 0.05f && manager.Phase == GamePhase.Phase2_FloorMopping)
            {
                // v1.1.2: 마우스로 문지르는 방향(좌우/앞뒤)이 손에 든 걸레에 그대로 실리게 한다 (카메라는 고정)
                float swayX = Mathf.Clamp(_scrubSway.x * 0.010f, -0.10f, 0.10f);
                float swayZ = Mathf.Clamp(-_scrubSway.y * 0.006f, -0.06f, 0.06f);
                if (manager.HasMopUnlocked)
                {
                    float s = Mathf.Sin(_scrubPhase * 1.4f);
                    targetPos += new Vector3(s * 0.20f - 0.12f + swayX, -0.05f, 0.16f + Mathf.Cos(_scrubPhase * 1.4f) * 0.05f + swayZ);
                    targetRot = Quaternion.Euler(18f, s * 22f + swayX * 130f, -s * 16f + swayZ * 70f);
                }
                else
                {
                    float cx = Mathf.Cos(_scrubPhase * 2.4f) * 0.09f;
                    float cz = Mathf.Sin(_scrubPhase * 2.4f) * 0.07f;
                    targetPos += new Vector3(cx - 0.10f + swayX, -0.11f, 0.18f + cz + swayZ);
                    targetRot = Quaternion.Euler(42f, cx * 80f + swayX * 130f, -cx * 90f + swayZ * 70f);
                }
            }
            else if (_isVacuumingNow)
            {
                targetPos += new Vector3(0f, 0.01f, 0.08f) + UnityEngine.Random.insideUnitSphere * 0.012f;
                targetRot = Quaternion.Euler(-5f, 0f, 0f);
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
            else if (_isHoldingTrashNow)
            {
                // 1초 HOLD 진동 차징 중 손도 미세하게 힘을 주며 떨리는 손맛!
                float ch = manager.trashSwarm != null ? manager.trashSwarm.MaxSelectedCharge01 : 0.3f;
                Vector3 vibe = UnityEngine.Random.insideUnitSphere * (0.004f + ch * 0.009f);
                targetPos += new Vector3(-0.06f, -0.04f, 0.10f) + vibe;
                targetRot = Quaternion.Euler(14f, -12f, 0f);
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
            }

            rightToolPivot.localPosition = Vector3.Lerp(rightToolPivot.localPosition, targetPos, 22f * Time.deltaTime);
            rightToolPivot.localRotation = Quaternion.Slerp(rightToolPivot.localRotation, targetRot, 22f * Time.deltaTime);

            if (leftBagPivot != null && leftBagPivot.gameObject.activeSelf)
            {
                float bounce = Mathf.Abs(Mathf.Sin(Time.time * 5.2f)) * 0.018f;
                leftBagPivot.localPosition = _leftBasePos + new Vector3(0f, bounce, 0f);
            }
        }

        private void ClearHighlight(bool clearSwarmLock = true)
        {
            if (_lastTarget != null) { _lastTarget.SetHighlighted(false); _lastTarget = null; }
            if (_lastDoor != null) { _lastDoor.SetHighlighted(false); _lastDoor = null; }
            if (_lastDroppedBag != null) { _lastDroppedBag.SetHighlighted(false); _lastDroppedBag = null; }
            if (clearSwarmLock) ClearSwarmSelection();
        }

        private void ClearSwarmSelection()
        {
            if (manager != null && manager.trashSwarm != null)
                manager.trashSwarm.UpdateHoverSelection(false, false, Vector3.zero, 0f, 0);
        }
    }
}
