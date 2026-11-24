using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TrashCleaner3D
{
    /// <summary>
    /// v0.4 1인칭 컨트롤러:
    /// - [F] 현관문 열기/닫기 & 바닥에 떨어진 실패 쓰레기봉지 다시 줍기!
    /// - 1개씩 클릭 줍기 + 하얀 테두리 하이라이트 (진공청소기 해금 시 좌클릭 꾹 연속 흡입)
    /// - 봉투 100% 가득 찼을 때만 왼손 봉투 표시 & [우클릭] 던지기 가능
    /// - 2단계 바닥 걸레질: 가만히 좌클릭만 누르면 절대 안 닦임! 마우스를 흔들어야만 닦임!
    /// - 강력한 악취 체감 이펙트: 이속 대폭 저하 + 시야 FOV 압박 + 기침 반동
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
        public float baseMoveSpeed = 3.6f;
        public float mouseSensitivity = 0.14f;

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
            if (rightToolPivot != null)
            {
                _rightBasePos = rightToolPivot.localPosition;
                _rightBaseRot = rightToolPivot.localRotation;
            }
            if (leftBagPivot != null) _leftBasePos = leftBagPivot.localPosition;
        }

        private void Start()
        {
            if (manager == null) manager = FindFirstObjectByType<GamePrototypeManager>();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void TeleportTo(Vector3 pos, Quaternion rot)
        {
            _cc.enabled = false;
            transform.position = pos;
            transform.rotation = rot;
            _pitch = 0f;
            if (cameraTransform != null) cameraTransform.localRotation = Quaternion.identity;
            _cc.enabled = true;
        }

        private void Update()
        {
            if (manager == null) return;

            // 보물 즉시 결정 모달 중에는 [Q] 보관 / [E] 슈킹 단축키 처리
            if (manager.Phase == GamePhase.TreasureDecision)
            {
                ClearHighlight();
                HandleTreasureDecisionHotkeys();
                return;
            }

            // [Tab] 마인드맵 스킬트리 토글
            if (WasTabPressed())
            {
                manager.ToggleMindMap();
                ClearHighlight();
                return;
            }

            if (manager.Phase != GamePhase.Phase1_TrashRemoval && manager.Phase != GamePhase.Phase2_FloorMopping)
            {
                ClearHighlight();
                return;
            }

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

        private void UpdateToolVisuals()
        {
            bool isP2 = manager.Phase == GamePhase.Phase2_FloorMopping;
            if (fistHandVisual != null) fistHandVisual.SetActive(!isP2 && !manager.HasVacuumUnlocked);
            if (vacuumVisual != null) vacuumVisual.SetActive(!isP2 && manager.HasVacuumUnlocked);
            if (tissueVisual != null) tissueVisual.SetActive(isP2 && !manager.HasMopUnlocked);
            if (mopVisual != null) mopVisual.SetActive(isP2 && manager.HasMopUnlocked);

            // 요청 #9: 봉투가 100% 가득 찼을 때만(또는 방 100% 마무리 잔여분) 왼손에 쓰레기봉투가 나타남!
            if (leftBagPivot != null)
            {
                bool showBag = manager.CanThrowHandBag;
                leftBagPivot.gameObject.SetActive(showBag);
                if (showBag)
                {
                    leftBagPivot.localScale = Vector3.Lerp(leftBagPivot.localScale, new Vector3(0.20f, 0.22f, 0.20f), 14f * Time.deltaTime);
                }
            }
        }

        private void UpdateStenchCameraEffects()
        {
            if (_cam == null) return;
            // 악취 심해질수록 숨 막히는 시야 FOV 압박 (70° → 62°)
            int st = manager.StenchStage;
            float targetFov = st == 4 ? 61f : st == 3 ? 64f : st == 2 ? 67f : 70f;
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, targetFov, 4f * Time.deltaTime);
        }

        private Vector2 HandleMovementAndLook()
        {
            float mx = 0f, mz = 0f;
            Vector2 lookDelta = Vector2.zero;
            bool toggleFlash = false, unlock = false;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed) mz += 1f;
                if (kb.sKey.isPressed) mz -= 1f;
                if (kb.dKey.isPressed) mx += 1f;
                if (kb.aKey.isPressed) mx -= 1f;
                if (kb.tKey.wasPressedThisFrame) toggleFlash = true;
                if (kb.hKey.wasPressedThisFrame) ShowHelp = !ShowHelp;
                if (kb.escapeKey.wasPressedThisFrame) unlock = true;
            }
            if (mouse != null)
            {
                if (Cursor.lockState == CursorLockMode.Locked) lookDelta = mouse.delta.ReadValue();
                else if (mouse.leftButton.wasPressedThisFrame)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
#else
            mx = Input.GetAxis("Horizontal");
            mz = Input.GetAxis("Vertical");
            if (Cursor.lockState == CursorLockMode.Locked)
                lookDelta = new Vector2(Input.GetAxis("Mouse X") * 12f, Input.GetAxis("Mouse Y") * 12f);
            if (Input.GetKeyDown(KeyCode.T)) toggleFlash = true;
            if (Input.GetKeyDown(KeyCode.H)) ShowHelp = !ShowHelp;
            if (Input.GetKeyDown(KeyCode.Escape)) unlock = true;
#endif

            if (unlock) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            if (toggleFlash && flashlight != null) flashlight.enabled = !flashlight.enabled;

            float sens = (_scrubMotionWeight > 0.2f) ? mouseSensitivity * 0.55f : mouseSensitivity;
            transform.Rotate(Vector3.up * (lookDelta.x * sens));
            if (cameraTransform != null)
            {
                _pitch = Mathf.Clamp(_pitch - lookDelta.y * sens, -82f, 82f);
                // 75%+ 악취 기침 시 1회성 수직 반동(멀미 유발 회전 없음)
                float coughOffset = manager.CoughKick01 * 2.4f;
                cameraTransform.localRotation = Quaternion.Euler(Mathf.Clamp(_pitch + coughOffset, -84f, 84f), 0f, 0f);
            }

            float speed = baseMoveSpeed * manager.StenchMoveSpeedMult;
            Vector3 move = (transform.right * mx + transform.forward * mz).normalized * speed;
            move.y = _cc.isGrounded ? -1f : -9.81f;
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

            // 우클릭: 가득 찬 봉투를 멀리 있는 트럭을 향해 던지기!
            if (rmbDown && manager.HandBagCount > 0)
            {
                Vector3 origin = cameraTransform.position + cameraTransform.forward * 0.55f - cameraTransform.right * 0.14f;
                manager.ThrowHandBag(origin, cameraTransform.forward);
                if (manager.HandBagCount == 0) _grabSwingTimer = 0.22f;
                return;
            }

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
                    if (!door.IsOpen)
                    {
                        blockedByDoor = true;
                        break;
                    }
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

            // 0) 바닥에 떨어져 실패한 쓰레기봉지 다시 줍기! ([좌클릭] 또는 [F])
            if (hitDroppedBag != null)
            {
                _lastDroppedBag = hitDroppedBag;
                hitDroppedBag.SetHighlighted(true);
                AimDroppedBag = hitDroppedBag;
                if (lmbDown || fDown)
                {
                    hitDroppedBag.RetrieveIntoHand();
                    _grabSwingTimer = 0.16f;
                    return;
                }
            }

            // 1) [F] 현관문 열기/닫기
            if (hitDoor != null)
            {
                _lastDoor = hitDoor;
                hitDoor.SetHighlighted(true);
                AimDoor = hitDoor;
                if (fDown)
                {
                    hitDoor.ToggleDoor(manager.sfx);
                    return;
                }
            }

            if (blockedByDoor) return;

            float workMult = manager.StenchWorkSpeedMult;

            // ===== PHASE 1: 1개씩 클릭 줍기 + 단단한 큰 봉투 터뜨리기 =====
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
                        _punchCooldown = (manager.HasVacuumUnlocked ? 0.14f : 0.18f) / workMult;
                        if (manager.HasVacuumUnlocked) _isVacuumingNow = true;
                        else _punchTimer = 0.18f;

                        if (hitPile.ApplyCleanDamage(manager.FistDamage * (manager.HasVacuumUnlocked ? 0.55f : 1f), null))
                        {
                            manager.OnBigPileBurst(hitPile);
                        }
                        else
                        {
                            manager.sfx.PlayScrub();
                        }
                        if (!manager.HasVacuumUnlocked) return;
                    }
                }

                if (manager.trashSwarm != null && hasWorldPoint && Vector3.Distance(cameraTransform.position, worldAimPoint) <= 4.2f)
                {
                    float r = manager.SweepRadius;
                    int pickLimit = manager.HasVacuumUnlocked ? manager.SweepPerTick : manager.PickCountPerClick;
                    manager.trashSwarm.UpdateHoverSelection(true, worldAimPoint, r, pickLimit);
                    AimIsSwarmNear = manager.trashSwarm.HoveredCount > 0;

                    bool triggerPick = manager.HasVacuumUnlocked ? lmbHeld : lmbDown;
                    if (triggerPick && AimIsSwarmNear)
                    {
                        if (manager.IsHandBagFull)
                        {
                            if (lmbDown) manager.ShowToast($"봉투 가득 (${manager.HandBagPendingValue})! 밖으로 나가 트럭에 [우클릭] 버리세요");
                            return;
                        }

                        if (manager.HasVacuumUnlocked) _isVacuumingNow = true;

                        if (!manager.HasVacuumUnlocked || _sweepTickTimer <= 0f)
                        {
                            if (manager.HasVacuumUnlocked) _sweepTickTimer = 0.05f / workMult;
                            int spaceLeft = Mathf.Max(0, manager.HandBagCapacity - manager.HandBagCount);
                            int maxGrab = Mathf.Min(pickLimit, spaceLeft);
                            Transform dest = manager.HasVacuumUnlocked ? rightToolPivot : (leftBagPivot != null ? leftBagPivot : rightToolPivot);

                            int grabbed = manager.trashSwarm.PickSelectedUnits(dest, maxGrab);
                            if (grabbed > 0 && !manager.HasVacuumUnlocked && _punchTimer <= 0f)
                            {
                                _grabSwingTimer = 0.16f;
                            }
                        }
                    }
                }
            }
            // ===== PHASE 2: 바닥 걸레질 (요청 #14: 가만히 좌클릭만 누르면 0% 진행! 마우스를 흔들어야만 닦임!) =====
            else if (manager.Phase == GamePhase.Phase2_FloorMopping && hitStain != null)
            {
                _lastTarget = hitStain;
                hitStain.SetHighlighted(true);
                AimIsStain = true;
                AimProgress01 = hitStain.CleanProgress01;

                if (lmbHeld)
                {
                    float shake = mouseDelta.magnitude;
                    if (shake < 1.8f)
                    {
                        // 마우스를 안 움직이면 절대 닦이지 않음!
                        NeedMouseShakeHint = true;
                    }
                    else
                    {
                        float power = Mathf.Min(shake, 50f) * 0.14f * manager.ScrubSpeedMult * workMult;
                        _scrubMotionWeight = 1f;
                        _scrubPhase += (6f + Mathf.Min(shake, 32f) * 0.9f) * Time.deltaTime;
                        if (_scrubSoundCooldown <= 0f)
                        {
                            _scrubSoundCooldown = 0.18f;
                            manager.sfx.PlayScrub();
                        }
                        if (hitStain.ApplyCleanDamage(power * Time.deltaTime, null))
                        {
                            manager.OnStainCleaned(hitStain);
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
                targetPos += new Vector3(0f, 0.01f, 0.07f) + UnityEngine.Random.insideUnitSphere * 0.01f;
                targetRot = Quaternion.Euler(-5f, 0f, 0f);
            }
            else if (_punchTimer > 0f)
            {
                _punchTimer -= Time.deltaTime;
                float t = Mathf.Sin(Mathf.Clamp01(_punchTimer / 0.18f) * Mathf.PI);
                targetPos += new Vector3(-0.14f * t, 0.04f * t, 0.28f * t);
                targetRot = Quaternion.Euler(-20f * t, -16f * t, 14f * t);
            }
            else if (_grabSwingTimer > 0f)
            {
                _grabSwingTimer -= Time.deltaTime;
                float t = Mathf.Sin(Mathf.Clamp01(_grabSwingTimer / 0.16f) * Mathf.PI);
                targetPos += new Vector3(-0.18f * t, -0.10f * t, 0.12f * t);
                targetRot = Quaternion.Euler(24f * t, -30f * t, 16f * t);
            }
            else
            {
                targetPos += new Vector3(0f, Mathf.Sin(Time.time * 2.4f) * 0.005f, 0f);
            }

            rightToolPivot.localPosition = Vector3.Lerp(rightToolPivot.localPosition, targetPos, 22f * Time.deltaTime);
            rightToolPivot.localRotation = Quaternion.Slerp(rightToolPivot.localRotation, targetRot, 22f * Time.deltaTime);

            if (leftBagPivot != null && leftBagPivot.gameObject.activeSelf)
            {
                float bounce = Mathf.Abs(Mathf.Sin(Time.time * 6.5f)) * 0.024f;
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
