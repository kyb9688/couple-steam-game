using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TrashCleaner3D
{
    /// <summary>
    /// v0.7 1인칭 컨트롤러:
    /// - [RMB] 우클릭 통일: 손 비었으면 바닥 완성 봉지 들기 / 들고 있으면 던지기!
    /// - 손에 이미 완성 봉지를 들고 있으면(HasCarriedBag) 바닥 쓰레기 줍기 차단!
    /// - 크리티컬 즉사(×2) · 연쇄 도미노 폭발 · 충격파 펀치 · 운동화/피버 이속 반영!
    /// - [F] 문 열기/닫기 · [Space] 점프 · [ESC] 감도/볼륨 설정
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonCleanerController : MonoBehaviour
    {
        public GamePrototypeManager manager;
        public Transform cameraTransform;
        public Transform rightToolPivot;
        public Transform leftBagPivot;
        public GameObject fistHandVisual;
        public GameObject vacuumVisual;
        public GameObject tissueVisual;
        public GameObject mopVisual;
        public Light flashlight;
        public float baseMoveSpeed = 3.8f;
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
        private float _sweepTickTimer;

        private Vector3 _rightBasePos;
        private Quaternion _rightBaseRot;
        private Vector3 _leftBasePos;
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

            if (Application.isFocused && Cursor.lockState != CursorLockMode.Locked) LockCursorStrict();

            _isVacuumingNow = false;
            UpdateToolVisuals();
            UpdateStenchCameraEffects();
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

        private void UpdateToolVisuals()
        {
            bool isP2 = manager.Phase == GamePhase.Phase2_FloorMopping;
            if (fistHandVisual != null) fistHandVisual.SetActive(!isP2 && !manager.HasVacuumUnlocked);
            if (vacuumVisual != null) vacuumVisual.SetActive(!isP2 && manager.HasVacuumUnlocked);
            if (tissueVisual != null) tissueVisual.SetActive(isP2 && !manager.HasMopUnlocked);
            if (mopVisual != null) mopVisual.SetActive(isP2 && manager.HasMopUnlocked);

            if (leftBagPivot != null) leftBagPivot.gameObject.SetActive(manager.HasCarriedBag);
        }

        private void UpdateStenchCameraEffects()
        {
            if (_cam == null) return;
            int st = manager.StenchStage;
            float baseFov = manager.IsFeverActive ? 74f : 70f;
            float targetFov = st == 4 ? 60f : st == 3 ? 64f : st == 2 ? 67f : baseFov;
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, targetFov, 4f * Time.deltaTime);
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
            if (_sweepTickTimer > 0f) _sweepTickTimer -= Time.deltaTime;
            if (_scrubSoundCooldown > 0f) _scrubSoundCooldown -= Time.deltaTime;

            ClearHighlight();
            AimIsPile = false;
            AimIsSwarmNear = false;
            AimIsStain = false;
            AimDoor = null;
            AimDroppedBag = null;
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
            RaycastHit[] hits = Physics.RaycastAll(ray, 5.2f);
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

                if (!hasWorldPoint) { worldAimPoint = h.point; hasWorldPoint = true; }

                var c = h.collider.GetComponentInParent<CleanableTarget>();
                if (c != null && !c.IsCleaned)
                {
                    if (c.kind == CleanableKind.TrashPile && hitPile == null) hitPile = c;
                    else if (c.kind == CleanableKind.FloorStain && hitStain == null) hitStain = c;
                }
            }

            // 0) 요청 d: 완성 봉지 들기 & 던지기 모두 [RMB] 우클릭 하나로 완전 통일!
            if (hitDroppedBag != null)
            {
                _lastDroppedBag = hitDroppedBag;
                hitDroppedBag.SetHighlighted(true);
                AimDroppedBag = hitDroppedBag;
            }

            if (rmbDown)
            {
                if (manager.HasCarriedBag)
                {
                    // 손에 들고 있으면 → 투하!
                    Vector3 origin = cameraTransform.position + cameraTransform.forward * 0.55f - cameraTransform.right * 0.14f;
                    manager.ThrowCarriedBag(origin, cameraTransform.forward);
                    _grabSwingTimer = 0.22f;
                    return;
                }
                else if (hitDroppedBag != null)
                {
                    // 손이 비었고 바닥 봉지를 조준 중이면 → 우클릭으로 바로 들기!
                    hitDroppedBag.RetrieveIntoHand();
                    _grabSwingTimer = 0.16f;
                    return;
                }
                else if (manager.CollectingCount > 0)
                {
                    manager.ShowToast($"봉투 미달 ({manager.CollectingCount}/{manager.HandBagCapacity})");
                }
            }

            // 1) [F] 현관문 열기/닫기 전용
            if (hitDoor != null)
            {
                _lastDoor = hitDoor;
                hitDoor.SetHighlighted(true);
                AimDoor = hitDoor;
                if (fDown) { hitDoor.ToggleDoor(manager.sfx); return; }
            }

            if (blockedByDoor) return;

            // 악취 100% 작업 불가
            if (manager.IsTooSuffocatedToWork)
            {
                if (lmbDown) { manager.sfx.PlayAlert(); manager.ShowToast("☣ 작업 불가 · 문 밖 환기"); }
                return;
            }

            // 요청 a: 손에 이미 완성 봉지를 들고 있으면(HasCarriedBag) 바닥 줍기·타격 차단!
            if (manager.HasCarriedBag)
            {
                if (lmbDown) manager.ShowToast($"보유 봉지 [RMB] 투하");
                return;
            }

            float workMult = Mathf.Max(0.25f, manager.StenchWorkSpeedMult);

            // ===== PHASE 1: 크리티컬·연쇄 도미노 클릭 줍기 & 충격파 펀치 =====
            if (manager.Phase == GamePhase.Phase1_TrashRemoval)
            {
                if (hitPile != null && Vector3.Distance(cameraTransform.position, hitPile.transform.position) <= 3.6f)
                {
                    _lastTarget = hitPile;
                    hitPile.SetHighlighted(true);
                    AimIsPile = true;
                    AimProgress01 = hitPile.CleanProgress01;

                    bool triggerPunch = manager.HasVacuumUnlocked ? lmbHeld : lmbDown;
                    if (triggerPunch && _punchCooldown <= 0f)
                    {
                        _punchCooldown = manager.HasVacuumUnlocked ? (0.14f / workMult) : 0.10f;
                        if (manager.HasVacuumUnlocked) _isVacuumingNow = true;
                        else _punchTimer = 0.16f;

                        float dmg = manager.FistDamage * (manager.HasVacuumUnlocked ? 0.55f : 1f);
                        manager.ApplyShockwaveToNearbyPiles(hitPile, dmg);
                        if (hitPile.ApplyCleanDamage(dmg, null)) manager.OnBigPileBurst(hitPile);
                        else manager.sfx.PlayScrub();
                        if (!manager.HasVacuumUnlocked) return;
                    }
                }

                if (manager.trashSwarm != null && hasWorldPoint && Vector3.Distance(cameraTransform.position, worldAimPoint) <= 4.2f)
                {
                    float r = manager.SweepRadius;
                    int pickLimit = manager.HasVacuumUnlocked ? manager.SweepPerTick : manager.PickCountPerClick;
                    manager.trashSwarm.UpdateHoverSelection(true, worldAimPoint, r, pickLimit);
                    AimIsSwarmNear = manager.trashSwarm.HoveredCount > 0;

                    if (AimIsSwarmNear && !AimIsPile) AimProgress01 = manager.trashSwarm.SelectedPeelProgress01;

                    bool triggerPick = manager.HasVacuumUnlocked ? lmbHeld : lmbDown;
                    if (triggerPick && AimIsSwarmNear)
                    {
                        if (!manager.HasVacuumUnlocked && !manager.IsFeverActive && manager.StenchNauseaFailChance > 0f && UnityEngine.Random.value < manager.StenchNauseaFailChance)
                        {
                            _grabSwingTimer = 0.16f;
                            manager.sfx.PlayScrub();
                            manager.ShowToast("☣ 헛손질 · 환기 필요");
                            return;
                        }

                        if (manager.HasVacuumUnlocked) _isVacuumingNow = true;

                        if (!manager.HasVacuumUnlocked || _sweepTickTimer <= 0f)
                        {
                            if (manager.HasVacuumUnlocked) _sweepTickTimer = 0.055f / workMult;

                            Transform dest = manager.HasVacuumUnlocked ? rightToolPivot : (leftBagPivot != null ? leftBagPivot : rightToolPivot);
                            manager.trashSwarm.HitOrPickSelectedUnits(
                                dest, pickLimit, manager.EffectivePeelDamage,
                                manager.CritChance, manager.ChainChance, manager.ChainExtraCount,
                                out int peeled, out int picked, out bool didCrit, out bool didChain, out Vector3 fxPos);

                            if (didCrit) manager.SpawnFloatingText(fxPos + Vector3.up * 0.25f, "CRIT ×2!", new Color(1f, 0.88f, 0.22f), 0.9f);
                            if (didChain) manager.SpawnFloatingText(fxPos + Vector3.up * 0.45f, "CHAIN!", new Color(1f, 0.56f, 0.22f), 0.9f);

                            if (peeled > 0 && picked == 0)
                            {
                                manager.sfx.PlayPop(0.58f);
                                _grabSwingTimer = 0.12f;
                            }
                            else if (picked > 0 && !manager.HasVacuumUnlocked && _punchTimer <= 0f)
                            {
                                _grabSwingTimer = 0.16f;
                            }
                        }
                    }
                }
            }
            // ===== PHASE 2: 바닥 걸레질 (마우스를 흔들어야만 닦임!) =====
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
                        float power = Mathf.Min(shake, 50f) * 0.14f * manager.ScrubSpeedMult * workMult;
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
                targetPos += new Vector3(0f, 0.01f, 0.07f) + UnityEngine.Random.insideUnitSphere * 0.01f;
                targetRot = Quaternion.Euler(-5f, 0f, 0f);
            }
            else if (_punchTimer > 0f)
            {
                _punchTimer -= Time.deltaTime;
                float t = Mathf.Sin(Mathf.Clamp01(_punchTimer / 0.16f) * Mathf.PI);
                targetPos += new Vector3(-0.14f * t, 0.04f * t, 0.28f * t);
                targetRot = Quaternion.Euler(-20f * t, -16f * t, 14f * t);
            }
            else if (_grabSwingTimer > 0f)
            {
                _grabSwingTimer -= Time.deltaTime;
                float t = Mathf.Sin(Mathf.Clamp01(_grabSwingTimer / 0.16f) * Mathf.PI);
                targetPos += new Vector3(-0.20f * t, -0.12f * t, 0.18f * t);
                targetRot = Quaternion.Euler(28f * t, -32f * t, 16f * t);
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

        private void ClearHighlight()
        {
            if (_lastTarget != null) { _lastTarget.SetHighlighted(false); _lastTarget = null; }
            if (_lastDoor != null) { _lastDoor.SetHighlighted(false); _lastDoor = null; }
            if (_lastDroppedBag != null) { _lastDroppedBag.SetHighlighted(false); _lastDroppedBag = null; }
            if (manager != null && manager.trashSwarm != null) manager.trashSwarm.UpdateHoverSelection(false, Vector3.zero, 0f, 0);
        }
    }
}
