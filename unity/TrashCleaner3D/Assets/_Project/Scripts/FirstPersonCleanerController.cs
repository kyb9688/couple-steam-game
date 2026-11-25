using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TrashCleaner3D
{
    /// <summary>
    /// v0.9 1인칭 컨트롤러:
    /// - [LMB 꾹(HOLD)] = 바닥 쓰레기 전용! (맨손 1.0초 진동 차징 후 쏙 / 집게 0.40초 / 진공 0.10초 — 큰 쓰레기 봉지에는 데미지 X!)
    /// - [LMB 단타 클릭] = 큰 쓰레기 봉투/박스 타격 전용!
    /// - 봉투 100% 정직 원복 (벽 충돌 과적 폭발 제거), 완성 시 [RMB] 투하 / 바닥 봉지 [RMB] 회수 통일!
    /// - [F] 현관문 / [K] 80% 조기 정산 / [Space] 점프 / [Tab] 스킬트리 / [ESC] 설정
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

        private CharacterController _cc;
        private Camera _cam;
        private float _pitch;
        private float _verticalVel;
        private CleanableTarget _lastTarget;
        private InteractiveDoor _lastDoor;
        private ThrownTrashBag _lastDroppedBag;
        private float _punchCooldown;
        private float _holdVibeSfxTimer;

        private Vector3 _rightBasePos;
        private Quaternion _rightBaseRot;
        private Vector3 _leftBasePos;
        private float _punchTimer;
        private float _grabSwingTimer;
        private float _scrubMotionWeight;
        private float _scrubPhase;
        private float _scrubSoundCooldown;
        private bool _isHoldingTrashNow;
        private bool _isVacuumingNow;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            if (cameraTransform != null) _cam = cameraTransform.GetComponent<Camera>();
            if (rightToolPivot != null) { _rightBasePos = rightToolPivot.localPosition; _rightBaseRot = rightToolPivot.localRotation; }
            if (leftBagPivot != null) _leftBasePos = leftBagPivot.localPosition;
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

        private void Update()
        {
            if (manager == null) return;

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

            if (Application.isFocused && Cursor.lockState != CursorLockMode.Locked) LockCursorStrict();

            _isHoldingTrashNow = false;
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

            if (leftBagPivot != null) leftBagPivot.gameObject.SetActive(manager.HasCarriedBag);
        }

        private void UpdateCameraEffects()
        {
            if (_cam == null) return;
            int st = manager.StenchStage;
            float baseFov = manager.IsAdrenalineZone ? 75f : (manager.HasFeverEngine && manager.IsFeverHot) ? 73f : 70f;
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
            if (_scrubSoundCooldown > 0f) _scrubSoundCooldown -= Time.deltaTime;
            if (_holdVibeSfxTimer > 0f) _holdVibeSfxTimer -= Time.deltaTime;

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
            RaycastHit[] hits = Physics.RaycastAll(ray, 5.4f);
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
                if (dropped != null && dropped.IsRetrievable && hitDroppedBag == null && h.distance <= 3.8f)
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
                if (lmbDown) manager.ShowToast("보유 봉지 [RMB] 투하");
                return;
            }

            float workMult = Mathf.Max(0.35f, manager.StenchWorkSpeedMult);

            // ===== PHASE 1 =====
            if (manager.Phase == GamePhase.Phase1_TrashRemoval)
            {
                // 요청 #4: 큰 쓰레기 봉투/박스 타격은 오직 [LMB 단타 클릭(lmbDown)] 전용!
                // (꾹 누르고 있을 때는 큰 봉투에 데미지가 절대 들어가지 않음!)
                if (hitPile != null && Vector3.Distance(cameraTransform.position, hitPile.transform.position) <= 3.7f)
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
                if (manager.trashSwarm != null && hasWorldPoint && Vector3.Distance(cameraTransform.position, worldAimPoint) <= 4.6f)
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
                        if (charge01 > 0.01f)
                        {
                            AimProgress01 = charge01;
                            if (_holdVibeSfxTimer <= 0f)
                            {
                                _holdVibeSfxTimer = manager.ToolTier == 0 ? 0.22f : 0.14f;
                                manager.sfx.PlayPop(0.55f + charge01 * 0.45f);
                            }
                        }
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
