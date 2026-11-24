using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TrashCleaner3D
{
    /// <summary>
    /// v0.3 1인칭 컨트롤러:
    /// - 동전 줍기식 넓은 반경 훑기(좌클릭 꾹 → 1,000+ GPU-Instanced 쓰레기 촤르르 흡입!)
    /// - 큰 봉투 퍽! 펀치 → 진공청소기 해금 시 광역 폭풍 흡입
    /// - [Tab] 언제든 마인드맵 토글 · 악취 디버프(이속/손놀림 저하) · 문 밖 나가면 환기
    /// - Phase 2 마우스 흔들기 물티슈/대걸레 모션 완전 분리
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
        public bool NeedMouseShakeHint { get; private set; }
        public float AimProgress01 { get; private set; } = -1f;
        public bool ShowHelp { get; private set; }

        private CharacterController _cc;
        private float _pitch;
        private CleanableTarget _lastTarget;
        private InteractiveDoor _lastDoor;
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

            // [Tab] 언제든 마인드맵 스킬트리 토글!
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
            Vector2 mouseDelta = HandleMovementAndLook();
            HandleInteraction(mouseDelta);
            AnimateHandsAndBag();
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

            // 왼손 봉투: 시야 안 가리게 아담한 크기(0.10 → 0.21) 유지
            if (leftBagPivot != null)
            {
                bool showBag = !isP2 && !manager.HasVacuumUnlocked && manager.HandBagCount > 0;
                leftBagPivot.gameObject.SetActive(showBag);
                if (showBag)
                {
                    float fill = Mathf.Clamp01(manager.HandBagCount / (float)manager.HandBagCapacity);
                    float s = Mathf.Lerp(0.10f, 0.21f, fill);
                    leftBagPivot.localScale = Vector3.Lerp(leftBagPivot.localScale, new Vector3(s, s * 1.1f, s), 12f * Time.deltaTime);
                }
            }
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
                cameraTransform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }

            // 악취 디버프(25%+ 이속 -15%, 100% 이속 -35%) 반영!
            float speed = baseMoveSpeed * manager.StenchMoveSpeedMult;
            Vector3 move = (transform.right * mx + transform.forward * mz).normalized * speed;
            move.y = _cc.isGrounded ? -1f : -9.81f;
            _cc.Move(move * Time.deltaTime);

            // 열린 문 밖 주차장(z <= 4.0)까지만 이동 허용, 추락 방지
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

            // 우클릭: 왼손 봉투를 문 밖 트럭 짐칸으로 농구 슛!
            if (manager.Phase == GamePhase.Phase1_TrashRemoval && rmbDown && manager.HandBagCount > 0)
            {
                Vector3 origin = cameraTransform.position + cameraTransform.forward * 0.55f - cameraTransform.right * 0.14f;
                manager.ThrowHandBag(origin, cameraTransform.forward);
                _grabSwingTimer = 0.22f;
                return;
            }

            Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
            RaycastHit[] hits = Physics.RaycastAll(ray, 5.2f);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            CleanableTarget hitPile = null;
            CleanableTarget hitStain = null;
            InteractiveDoor hitDoor = null;
            Vector3 worldAimPoint = Vector3.zero;
            bool hasWorldPoint = false;
            bool blockedByDoor = false;

            foreach (var h in hits)
            {
                if (h.collider.GetComponentInParent<FirstPersonCleanerController>() != null) continue;

                var door = h.collider.GetComponentInParent<InteractiveDoor>();
                if (door != null)
                {
                    if (hitDoor == null && h.distance <= 3.0f) hitDoor = door;
                    if (!door.IsOpen)
                    {
                        blockedByDoor = true;
                        break; // 닫힌 문 너머의 쓰레기는 선택/클릭 불가!
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

            // [F] 현관문 열기 / 닫기 (하얀 테두리 하이라이트!)
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

            // ===== PHASE 1: 하얀 테두리 선택 + 맨손 클릭 줍기 (청소기 해금 시 꾹 흡입!) =====
            if (manager.Phase == GamePhase.Phase1_TrashRemoval)
            {
                // 1) 큰 쓰레기봉투 펀치 (하얀 테두리 표시 · 맨손은 클릭! 청소기는 꾹!)
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

                // 2) 바닥 1,000+ GPU-Instanced 쓰레기: 마우스 올리면 주위 하얀색 테두리 표시 → 클릭으로 줍기!
                if (manager.trashSwarm != null && hasWorldPoint && Vector3.Distance(cameraTransform.position, worldAimPoint) <= 4.2f)
                {
                    float r = manager.SweepRadius;
                    manager.trashSwarm.SetHoverSelection(true, worldAimPoint, r);
                    AimIsSwarmNear = manager.trashSwarm.HasAnyIdleNear(worldAimPoint, r);

                    bool triggerPick = manager.HasVacuumUnlocked ? lmbHeld : lmbDown;
                    if (triggerPick && AimIsSwarmNear)
                    {
                        if (manager.IsHandBagFull)
                        {
                            if (lmbDown) manager.ShowToast("봉투 가득! 문 밖 트럭에 [우클릭] 슛");
                            return;
                        }

                        if (manager.HasVacuumUnlocked) _isVacuumingNow = true;

                        if (!manager.HasVacuumUnlocked || _sweepTickTimer <= 0f)
                        {
                            if (manager.HasVacuumUnlocked) _sweepTickTimer = 0.035f / workMult;
                            int spaceLeft = manager.HasVacuumUnlocked ? 99 : Mathf.Max(0, manager.HandBagCapacity - manager.HandBagCount);
                            int maxGrab = Mathf.Min(manager.SweepPerTick, spaceLeft);
                            Transform dest = manager.HasVacuumUnlocked ? rightToolPivot : (leftBagPivot != null ? leftBagPivot : rightToolPivot);

                            int grabbed = manager.trashSwarm.SweepAt(worldAimPoint, r, maxGrab, dest);
                            if (grabbed > 0 && !manager.HasVacuumUnlocked && _punchTimer <= 0f)
                            {
                                _grabSwingTimer = 0.16f;
                            }
                        }
                    }
                }
            }
            // ===== PHASE 2: 바닥 얼룩 마우스 흔들어 닦기 =====
            else if (manager.Phase == GamePhase.Phase2_FloorMopping && hitStain != null)
            {
                _lastTarget = hitStain;
                hitStain.SetHighlighted(true);
                AimIsStain = true;
                AimProgress01 = hitStain.CleanProgress01;

                if (lmbHeld)
                {
                    float shake = mouseDelta.magnitude;
                    float power = (0.12f + Mathf.Min(shake, 45f) * 0.09f) * manager.ScrubSpeedMult * workMult;
                    if (shake < 1.2f) NeedMouseShakeHint = true;

                    _scrubMotionWeight = 1f;
                    _scrubPhase += (6f + Mathf.Min(shake, 30f) * 0.85f) * Time.deltaTime;
                    if (shake > 2.5f && _scrubSoundCooldown <= 0f)
                    {
                        _scrubSoundCooldown = 0.20f;
                        manager.sfx.PlayScrub();
                    }
                    if (hitStain.ApplyCleanDamage(power * Time.deltaTime, null))
                    {
                        manager.OnStainCleaned(hitStain);
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
                float t = Mathf.Sin(Mathf.Clamp01(_punchTimer / 0.20f) * Mathf.PI);
                targetPos += new Vector3(-0.14f * t, 0.04f * t, 0.28f * t);
                targetRot = Quaternion.Euler(-20f * t, -16f * t, 14f * t);
            }
            else if (_grabSwingTimer > 0f)
            {
                _grabSwingTimer -= Time.deltaTime;
                float t = Mathf.Sin(Mathf.Clamp01(_grabSwingTimer / 0.14f) * Mathf.PI);
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
                float bounce = manager.IsHandBagFull ? Mathf.Abs(Mathf.Sin(Time.time * 7f)) * 0.028f : 0f;
                leftBagPivot.localPosition = _leftBasePos + new Vector3(0f, bounce, 0f);
            }
        }

        private void ClearHighlight()
        {
            if (_lastTarget != null) { _lastTarget.SetHighlighted(false); _lastTarget = null; }
            if (_lastDoor != null) { _lastDoor.SetHighlighted(false); _lastDoor = null; }
            if (manager != null && manager.trashSwarm != null) manager.trashSwarm.SetHoverSelection(false, Vector3.zero, 0f);
        }
    }
}
