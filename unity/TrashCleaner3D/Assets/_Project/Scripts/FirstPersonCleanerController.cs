using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TrashCleaner3D
{
    /// <summary>
    /// v0.8 1인칭 컨트롤러:
    /// - 클릭 연타 완전 제거! [LMB 홀드]로 1단계 맨손(톡톡 1개) → 2단계 집게/빗자루(묶음 쓸기) → 3단계 진공청소기(광역 회오리 흡입) 통일!
    /// - 지층 발굴 보물(BuriedTreasureObject) 조준 시 하얀 외곽선 + [LMB / F] 발굴!
    /// - [RMB] 우클릭 통일: 보유/100~150% 봉투 묶어 던지기 & 바닥 봉지 줍기!
    /// - 150% 과적 상태에서 벽·가구 충돌 시(OnControllerColliderHit) 봉투 폭발!
    /// - [K] 80% 조기 정산 / [F] 현관문 / [Space] 점프 / [Tab] 스킬트리 / [ESC] 설정
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
        public BuriedTreasureObject AimTreasure { get; private set; }
        public bool NeedMouseShakeHint { get; private set; }
        public float AimProgress01 { get; private set; } = -1f;
        public bool ShowHelp { get; private set; }

        private CharacterController _cc;
        private Camera _cam;
        private float _pitch;
        private float _verticalVel;
        private CleanableTarget _lastTarget;
        private InteractiveDoor _lastDoor;
        private ThrownTrashBag _lastDroppedBag;
        private BuriedTreasureObject _lastTreasure;
        private float _punchCooldown;
        private float _autoTickTimer;
        private float _burstCooldown;

        private Vector3 _rightBasePos;
        private Quaternion _rightBaseRot;
        private Vector3 _leftBasePos;
        private Vector3 _leftBaseScale = Vector3.one;
        private float _punchTimer;
        private float _grabSwingTimer;
        private float _scrubMotionWeight;
        private float _scrubPhase;
        private float _scrubSoundCooldown;
        private bool _isVacuumingNow;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            if (cameraTransform != null) _cam = cameraTransform.GetComponent<Camera>();
            if (rightToolPivot != null) { _rightBasePos = rightToolPivot.localPosition; _rightBaseRot = rightToolPivot.localRotation; }
            if (leftBagPivot != null) { _leftBasePos = leftBagPivot.localPosition; _leftBaseScale = leftBagPivot.localScale; }
        }

        private void Start()
        {
            if (manager == null) manager = FindFirstObjectByType<GamePrototypeManager>();
            if (manager != null) mouseSensitivity = manager.MouseSensitivity;
            LockCursorStrict();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && manager != null &&
                (manager.Phase == GamePhase.Phase1_TrashRemoval || manager.Phase == GamePhase.Phase2_FloorMopping))
            {
                LockCursorStrict();
            }
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

        /// <summary>
        /// 150% 과적 상태에서 벽·가구에 부딪히면 봉투가 터져 쓰레기가 바닥으로 쏟아짐!
        /// </summary>
        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (manager == null || _burstCooldown > 0f) return;
            if (!manager.IsBagOverfilled) return;
            if (manager.Phase != GamePhase.Phase1_TrashRemoval && manager.Phase != GamePhase.Phase2_FloorMopping) return;

            // 바닥(수평면)이 아닌 벽·가구(수직면)에 일정 속도로 충돌했을 때만 판정
            if (Mathf.Abs(hit.normal.y) > 0.38f) return;
            Vector3 horizVel = new Vector3(_cc.velocity.x, 0f, _cc.velocity.z);
            if (horizVel.magnitude < 0.85f) return;

            // 바닥에 떨어진 봉투와의 가벼운 스침은 제외
            if (hit.collider.GetComponentInParent<ThrownTrashBag>() != null) return;

            _burstCooldown = 1.6f;
            Vector3 spillOrigin = hit.point + hit.normal * 0.42f;
            manager.OnOverfillWallBump(spillOrigin);
        }

        private void Update()
        {
            if (manager == null) return;
            if (_burstCooldown > 0f) _burstCooldown -= Time.deltaTime;

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

            // 80% 조기 정산 단축키 [K]
            if (manager.CanEarlyCheckout && WasKeyKPressed())
            {
                ClearHighlight();
                manager.TriggerEarlyCheckout();
                return;
            }

            if (Application.isFocused && Cursor.lockState != CursorLockMode.Locked) LockCursorStrict();

            _isVacuumingNow = false;
            UpdateToolVisuals();
            UpdateCameraEffects();
            Vector2 mouseDelta = HandleMovementAndLook();
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

        private void UpdateToolVisuals()
        {
            bool isP2 = manager.Phase == GamePhase.Phase2_FloorMopping;
            int tier = manager.ToolTier;

            if (fistHandVisual != null) fistHandVisual.SetActive(!isP2 && tier == 0);
            if (tongsBroomVisual != null) tongsBroomVisual.SetActive(!isP2 && tier == 1);
            if (vacuumVisual != null) vacuumVisual.SetActive(!isP2 && tier >= 2);
            if (tissueVisual != null) tissueVisual.SetActive(isP2 && !manager.HasMopUnlocked);
            if (mopVisual != null) mopVisual.SetActive(isP2 && manager.HasMopUnlocked);

            if (leftBagPivot != null)
            {
                bool showBag = manager.HasCarriedBag || manager.IsBagFullReady;
                leftBagPivot.gameObject.SetActive(showBag);
            }
        }

        private void UpdateCameraEffects()
        {
            if (_cam == null) return;
            int st = manager.StenchStage;
            float baseFov = manager.IsAdrenalineZone ? 76f : (manager.HasFeverEngine && manager.IsFeverHot) ? 73f : 70f;
            float targetFov = st == 4 ? 60f : st == 3 ? 64f : st == 2 ? 67f : baseFov;
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, targetFov, 4.5f * Time.deltaTime);
        }

        private Vector2 HandleMovementAndLook()
        {
            float mx = 0f, mz = 0f;
            Vector2 lookDelta = Vector2.zero;
            bool toggleFlash = false, jumpDown = false;

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
            if (mouse != null && Cursor.lockState == CursorLockMode.Locked) lookDelta = mouse.delta.ReadValue();
#else
            mx = Input.GetAxis("Horizontal");
            mz = Input.GetAxis("Vertical");
            if (Cursor.lockState == CursorLockMode.Locked)
                lookDelta = new Vector2(Input.GetAxis("Mouse X") * 12f, Input.GetAxis("Mouse Y") * 12f);
            if (Input.GetKeyDown(KeyCode.Space)) jumpDown = true;
            if (Input.GetKeyDown(KeyCode.T)) toggleFlash = true;
            if (Input.GetKeyDown(KeyCode.H)) ShowHelp = !ShowHelp;
#endif

            if (toggleFlash && flashlight != null) flashlight.enabled = !flashlight.enabled;

            float activeSens = manager.MouseSensitivity;
            float sens = (_scrubMotionWeight > 0.2f) ? activeSens * 0.55f : activeSens;
            transform.Rotate(Vector3.up * (lookDelta.x * sens));
            if (cameraTransform != null)
            {
                _pitch = Mathf.Clamp(_pitch - lookDelta.y * sens, -82f, 82f);
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
            if (_autoTickTimer > 0f) _autoTickTimer -= Time.deltaTime;
            if (_scrubSoundCooldown > 0f) _scrubSoundCooldown -= Time.deltaTime;

            ClearHighlight();
            AimIsPile = false;
            AimIsSwarmNear = false;
            AimIsStain = false;
            AimDoor = null;
            AimDroppedBag = null;
            AimTreasure = null;
            NeedMouseShakeHint = false;
            AimProgress01 = -1f;
            _scrubMotionWeight = Mathf.MoveTowards(_scrubMotionWeight, 0f, 6f * Time.deltaTime);

            if (cameraTransform == null || Cursor.lockState != CursorLockMode.Locked) return;

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
            RaycastHit[] hits = Physics.RaycastAll(ray, 5.4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            CleanableTarget hitPile = null;
            CleanableTarget hitStain = null;
            InteractiveDoor hitDoor = null;
            ThrownTrashBag hitDroppedBag = null;
            BuriedTreasureObject hitTreasure = null;
            Vector3 worldAimPoint = Vector3.zero;
            bool hasWorldPoint = false;
            bool blockedByDoor = false;

            foreach (var h in hits)
            {
                if (h.collider.GetComponentInParent<FirstPersonCleanerController>() != null) continue;

                var dropped = h.collider.GetComponentInParent<ThrownTrashBag>();
                if (dropped != null && dropped.IsRetrievable && hitDroppedBag == null && h.distance <= 3.8f)
                {
                    hitDroppedBag = dropped;
                    continue;
                }

                var bt = h.collider.GetComponentInParent<BuriedTreasureObject>();
                if (bt != null && bt.IsUncovered && !bt.IsCollected && hitTreasure == null && h.distance <= 3.6f)
                {
                    hitTreasure = bt;
                    continue;
                }

                var door = h.collider.GetComponentInParent<InteractiveDoor>();
                if (door != null)
                {
                    if (hitDoor == null && h.distance <= 3.0f) hitDoor = door;
                    if (!door.IsOpen) { blockedByDoor = true; break; }
                    continue;
                }

                if (!hasWorldPoint) { worldAimPoint = h.point; hasWorldPoint = true; }

                var c = h.collider.GetComponentInParent<CleanableTarget>();
                if (c != null && !c.IsCleaned)
                {
                    if (c.kind == CleanableKind.TrashPile && hitPile == null) hitPile = c;
                    else if (c.kind == CleanableKind.FloorStain && hitStain == null) hitStain = c;
                }
            }

            // 0) [RMB] 통일: 손에 쥔 봉지 투하 / 100~150% 수집 봉지 묶어 투하 / 바닥 봉지 회수
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
                else if (manager.CanTieAndThrowCollectingBag)
                {
                    manager.TieAndThrowCollectingBag(origin, cameraTransform.forward);
                    _grabSwingTimer = 0.22f;
                    return;
                }
                else if (hitDroppedBag != null)
                {
                    hitDroppedBag.RetrieveIntoHand();
                    _grabSwingTimer = 0.16f;
                    return;
                }
                else if (manager.CollectingCount > 0)
                {
                    manager.ShowToast($"봉투 미달 ({manager.CollectingCount}/{manager.HandBagCapacity})");
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

            if (blockedByDoor) return;

            // 2) 노출된 지층 보물 발굴 ([LMB] 또는 [F])
            if (hitTreasure != null)
            {
                _lastTreasure = hitTreasure;
                hitTreasure.SetHighlighted(true);
                AimTreasure = hitTreasure;
                if (lmbDown || fDown)
                {
                    hitTreasure.Collect();
                    return;
                }
            }

            // 악취 100% 작업 불가
            if (manager.IsTooSuffocatedToWork)
            {
                if (lmbDown) { manager.sfx.PlayAlert(); manager.ShowToast("☣ 작업 불가 · 문 밖 환기"); }
                return;
            }

            // 손에 이미 묶은 완성 봉지(150% 자동 묶기 or 회수)를 들고 있으면 바닥 작업 차단
            if (manager.HasCarriedBag)
            {
                if (lmbDown) manager.ShowToast("보유 봉지 [RMB] 투하");
                return;
            }

            float workMult = Mathf.Max(0.28f, manager.StenchWorkSpeedMult);

            // ===== PHASE 1: 커서 반경 호버/홀드 자동 처리 (1단계 맨손 → 2단계 집게/빗자루 → 3단계 진공청소기) =====
            if (manager.Phase == GamePhase.Phase1_TrashRemoval)
            {
                // 큰 쓰레기 더미도 연타 없이 [LMB 홀드]로 자동 타격!
                if (hitPile != null && Vector3.Distance(cameraTransform.position, hitPile.transform.position) <= 3.7f)
                {
                    _lastTarget = hitPile;
                    hitPile.SetHighlighted(true);
                    AimIsPile = true;
                    AimProgress01 = hitPile.CleanProgress01;

                    if (lmbHeld && _punchCooldown <= 0f)
                    {
                        float interval = manager.ToolTier >= 2 ? 0.12f : manager.ToolTier == 1 ? 0.18f : 0.24f;
                        _punchCooldown = interval / (workMult * manager.AdrenalineSpeedMult);
                        if (manager.ToolTier >= 2) _isVacuumingNow = true;
                        else _punchTimer = 0.14f;

                        float dmg = manager.FistDamage * (manager.ToolTier >= 2 ? 0.65f : 1f);
                        manager.ApplyShockwaveToNearbyPiles(hitPile, dmg);
                        if (hitPile.ApplyCleanDamage(dmg, null)) manager.OnBigPileBurst(hitPile);
                        else manager.sfx.PlayScrub();
                    }
                }

                // 바닥 쓰레기: 커서 반경 오라 + 호버/홀드 자동 흡입!
                if (manager.trashSwarm != null && hasWorldPoint && Vector3.Distance(cameraTransform.position, worldAimPoint) <= 4.4f)
                {
                    float r = manager.SweepRadius;
                    int grabLimit = manager.AutoGrabPerTick;
                    manager.trashSwarm.UpdateHoverSelection(true, worldAimPoint, r, grabLimit);
                    AimIsSwarmNear = manager.trashSwarm.HoveredCount > 0;

                    if (lmbHeld && AimIsSwarmNear)
                    {
                        if (manager.ToolTier >= 2) _isVacuumingNow = true;

                        if (_autoTickTimer <= 0f)
                        {
                            _autoTickTimer = manager.EffectiveAutoTickInterval;

                            Transform dest = manager.ToolTier >= 2
                                ? rightToolPivot
                                : (leftBagPivot != null && leftBagPivot.gameObject.activeSelf ? leftBagPivot : rightToolPivot);

                            int picked = manager.trashSwarm.AutoProcessSelected(
                                dest, grabLimit, manager.CritChance, manager.ChainChance, manager.ChainExtraCount,
                                out bool didCrit, out bool didChain, out Vector3 fxPos);

                            if (didCrit) manager.SpawnFloatingText(fxPos + Vector3.up * 0.25f, "CRIT ×2!", new Color(1f, 0.88f, 0.22f), 0.9f);
                            if (didChain) manager.SpawnFloatingText(fxPos + Vector3.up * 0.45f, "CHAIN!", new Color(1f, 0.56f, 0.22f), 0.9f);

                            if (picked > 0 && manager.ToolTier < 2 && _punchTimer <= 0f)
                            {
                                _grabSwingTimer = manager.ToolTier == 1 ? 0.13f : 0.16f;
                            }
                        }
                    }
                }
            }
            // ===== PHASE 2: 밀대/걸레 바닥 닦기 (좌클릭 + 마우스 문지르기) =====
            else if (manager.Phase == GamePhase.Phase2_FloorMopping && hitStain != null)
            {
                _lastTarget = hitStain;
                hitStain.SetHighlighted(true);
                AimIsStain = true;
                AimProgress01 = hitStain.CleanProgress01;

                if (lmbHeld)
                {
                    float shake = mouseDelta.magnitude;
                    if (shake < 1.8f) NeedMouseShakeHint = true;
                    else
                    {
                        float power = Mathf.Min(shake, 50f) * 0.145f * manager.ScrubSpeedMult * workMult;
                        _scrubMotionWeight = 1f;
                        _scrubPhase += (6f + Mathf.Min(shake, 32f) * 0.9f) * Time.deltaTime;
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

            if (_scrubMotionWeight > 0.05f && manager.Phase == GamePhase.Phase2_FloorMopping)
            {
                if (manager.HasMopUnlocked)
                {
                    float s = Mathf.Sin(_scrubPhase * 1.4f);
                    targetPos += new Vector3(s * 0.20f - 0.12f, -0.05f, 0.16f + Mathf.Cos(_scrubPhase * 1.4f) * 0.05f);
                    targetRot = Quaternion.Euler(18f, s * 22f, -s * 16f);
                }
                else
                {
                    float cx = Mathf.Cos(_scrubPhase * 2.4f) * 0.09f;
                    float cz = Mathf.Sin(_scrubPhase * 2.4f) * 0.07f;
                    targetPos += new Vector3(cx - 0.10f, -0.11f, 0.18f + cz);
                    targetRot = Quaternion.Euler(42f, cx * 80f, -cx * 90f);
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
                float t = Mathf.Sin(Mathf.Clamp01(_punchTimer / 0.14f) * Mathf.PI);
                targetPos += new Vector3(-0.14f * t, 0.04f * t, 0.28f * t);
                targetRot = Quaternion.Euler(-20f * t, -16f * t, 14f * t);
            }
            else if (_grabSwingTimer > 0f)
            {
                _grabSwingTimer -= Time.deltaTime;
                float dur = manager.ToolTier == 1 ? 0.13f : 0.16f;
                float t = Mathf.Sin(Mathf.Clamp01(_grabSwingTimer / dur) * Mathf.PI);
                if (manager.ToolTier == 1)
                {
                    // 2단계 집게/빗자루 좌우 쓸어담기 모션
                    float sweep = Mathf.Sin(Time.time * 24f) * 0.16f;
                    targetPos += new Vector3(sweep - 0.08f * t, -0.08f * t, 0.22f * t);
                    targetRot = Quaternion.Euler(20f * t, sweep * 90f, -15f * t);
                }
                else
                {
                    // 1단계 맨손 톡톡 집기 모션
                    targetPos += new Vector3(-0.18f * t, -0.11f * t, 0.18f * t);
                    targetRot = Quaternion.Euler(26f * t, -30f * t, 14f * t);
                }
            }
            else
            {
                targetPos += new Vector3(0f, Mathf.Sin(Time.time * 2.4f) * 0.005f, 0f);
            }

            rightToolPivot.localPosition = Vector3.Lerp(rightToolPivot.localPosition, targetPos, 22f * Time.deltaTime);
            rightToolPivot.localRotation = Quaternion.Slerp(rightToolPivot.localRotation, targetRot, 22f * Time.deltaTime);

            // 왼손 봉투: 100~150% 과적일수록 부풀고 떨림!
            if (leftBagPivot != null && leftBagPivot.gameObject.activeSelf)
            {
                float over01 = manager.IsBagOverfilled
                    ? Mathf.Clamp01((manager.BagFillRatio - 1f) / 0.5f)
                    : 0f;
                float bounce = Mathf.Abs(Mathf.Sin(Time.time * (5.2f + over01 * 12f))) * (0.018f + over01 * 0.025f);
                Vector3 shake = over01 > 0.01f ? UnityEngine.Random.insideUnitSphere * (0.014f * over01) : Vector3.zero;
                leftBagPivot.localPosition = _leftBasePos + new Vector3(0f, bounce, 0f) + shake;
                leftBagPivot.localScale = _leftBaseScale * (1f + over01 * 0.36f);
            }
        }

        private void ClearHighlight()
        {
            if (_lastTarget != null) { _lastTarget.SetHighlighted(false); _lastTarget = null; }
            if (_lastDoor != null) { _lastDoor.SetHighlighted(false); _lastDoor = null; }
            if (_lastDroppedBag != null) { _lastDroppedBag.SetHighlighted(false); _lastDroppedBag = null; }
            if (_lastTreasure != null) { _lastTreasure.SetHighlighted(false); _lastTreasure = null; }
            if (manager != null && manager.trashSwarm != null) manager.trashSwarm.UpdateHoverSelection(false, Vector3.zero, 0f, 0);
        }
    }
}
