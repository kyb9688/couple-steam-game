using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TrashCleaner3D
{
    /// <summary>
    /// 1인칭 이동/시점 + 장비 3종 전환(1/2/3) + 걸레질/줍기/흡입 1인칭 손 모션 +
    /// 우선순위 RaycastAll(쓰레기가 바닥 얼룩에 가려지지 않음) + 최소 텍스트 직관 상호작용 컨트롤러.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonCleanerController : MonoBehaviour
    {
        public GamePrototypeManager manager;
        public Transform cameraTransform;
        public Transform toolHoldPoint;
        public GameObject slot1Visual;
        public GameObject slot2Visual;
        public GameObject slot3Visual;
        public Light flashlight;
        public float moveSpeed = 3.4f;
        public float mouseSensitivity = 0.14f;

        public int ActiveSlot { get; private set; } = 1; // 1:수거/흡입, 2:얼룩/거품, 3:생존/탐지
        public string AimTitle { get; private set; } = "";
        public string AimHint { get; private set; } = "";
        public bool AimIsDanger { get; private set; }
        public bool AimIsTreasure { get; private set; }
        public bool AimIsCleanable { get; private set; }
        public bool AimIsStain { get; private set; }
        public int SuggestedSlot { get; private set; } = 0; // 잘못된 장비일 때 우상단 슬롯 반짝임 유도
        public float AimCleanProgress01 { get; private set; } = -1f;
        public bool ShowHelpOverlay { get; private set; } = false;

        private CharacterController _cc;
        private float _pitch;
        private CleanableTarget _lastCleanable;
        private TreasureTarget _lastTreasure;
        private float _clickCooldown;
        private Vector3 _toolBaseLocalPos;
        private Quaternion _toolBaseLocalRot;
        private float _grabPunchTimer;
        private bool _isMoppingNow;
        private bool _isVacuumingNow;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            if (toolHoldPoint != null)
            {
                _toolBaseLocalPos = toolHoldPoint.localPosition;
                _toolBaseLocalRot = toolHoldPoint.localRotation;
            }
        }

        private void Start()
        {
            if (manager == null) manager = FindFirstObjectByType<GamePrototypeManager>();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            RefreshToolVisual();
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
            if (manager == null || manager.Phase != GamePhase.PlayingInRoom)
            {
                ClearHighlights();
                return;
            }

            _isMoppingNow = false;
            _isVacuumingNow = false;

            HandleMovementAndLook();
            HandleHotkeys();
            HandleInteraction();
            AnimateHeldTool();
        }

        private void HandleMovementAndLook()
        {
            float mx = 0f, mz = 0f, lx = 0f, ly = 0f;
            bool toggleFlash = false;
            bool unlock = false;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed) mz += 1f;
                if (kb.sKey.isPressed) mz -= 1f;
                if (kb.dKey.isPressed) mx += 1f;
                if (kb.aKey.isPressed) mx -= 1f;
                if (kb.fKey.wasPressedThisFrame) toggleFlash = true;
                if (kb.hKey.wasPressedThisFrame) ShowHelpOverlay = !ShowHelpOverlay;
                if (kb.escapeKey.wasPressedThisFrame) unlock = true;
            }
            if (mouse != null)
            {
                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    Vector2 d = mouse.delta.ReadValue();
                    lx = d.x * mouseSensitivity;
                    ly = d.y * mouseSensitivity;
                }
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
            {
                lx = Input.GetAxis("Mouse X") * 2f;
                ly = Input.GetAxis("Mouse Y") * 2f;
            }
            if (Input.GetKeyDown(KeyCode.F)) toggleFlash = true;
            if (Input.GetKeyDown(KeyCode.H)) ShowHelpOverlay = !ShowHelpOverlay;
            if (Input.GetKeyDown(KeyCode.Escape)) unlock = true;
#endif

            if (unlock)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (toggleFlash && flashlight != null) flashlight.enabled = !flashlight.enabled;

            transform.Rotate(Vector3.up * lx);
            if (cameraTransform != null)
            {
                _pitch = Mathf.Clamp(_pitch - ly, -82f, 82f);
                cameraTransform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }

            Vector3 move = (transform.right * mx + transform.forward * mz).normalized * moveSpeed;
            move.y = _cc.isGrounded ? -1f : -9.81f;
            _cc.Move(move * Time.deltaTime);

            if (transform.position.y < -1.2f && manager != null && manager.playerSpawnPoint != null)
            {
                TeleportTo(manager.playerSpawnPoint.position + Vector3.up * 0.2f, manager.playerSpawnPoint.rotation);
            }
        }

        private void AnimateHeldTool()
        {
            if (toolHoldPoint == null) return;

            Vector3 targetPos = _toolBaseLocalPos;
            Quaternion targetRot = _toolBaseLocalRot;

            if (_isMoppingNow && ActiveSlot == 2)
            {
                // 쓱싹쓱싹 좌우 대걸레 문지르기 모션!
                float sweep = Mathf.Sin(Time.time * 16f);
                float push = Mathf.Cos(Time.time * 16f) * 0.04f;
                targetPos += new Vector3(sweep * 0.16f - 0.12f, -0.04f, 0.14f + push);
                targetRot = Quaternion.Euler(14f, sweep * 18f, -sweep * 14f);
            }
            else if (_isVacuumingNow && ActiveSlot == 1)
            {
                // 청소기 흡입 진동 모션
                targetPos += new Vector3(0f, 0.01f, 0.08f) + UnityEngine.Random.insideUnitSphere * 0.011f;
                targetRot = Quaternion.Euler(-6f, 0f, 0f);
            }
            else if (_grabPunchTimer > 0f)
            {
                // 맨손/집게 줍기 탁! 뻗기 모션
                _grabPunchTimer -= Time.deltaTime;
                float t = Mathf.Sin(Mathf.Clamp01(_grabPunchTimer / 0.18f) * Mathf.PI);
                targetPos += new Vector3(-0.08f * t, -0.02f * t, 0.18f * t);
                targetRot = Quaternion.Euler(18f * t, -12f * t, 0f);
            }
            else
            {
                // 평상시 미세한 호흡 흔들림
                float idle = Mathf.Sin(Time.time * 2.4f) * 0.006f;
                targetPos += new Vector3(0f, idle, 0f);
            }

            toolHoldPoint.localPosition = Vector3.Lerp(toolHoldPoint.localPosition, targetPos, 20f * Time.deltaTime);
            toolHoldPoint.localRotation = Quaternion.Slerp(toolHoldPoint.localRotation, targetRot, 20f * Time.deltaTime);
        }

        private void HandleHotkeys()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) SetSlot(1);
                if (kb.digit2Key.wasPressedThisFrame) SetSlot(2);
                if (kb.digit3Key.wasPressedThisFrame) SetSlot(3);
                if (kb.zKey.wasPressedThisFrame) manager.TryUpgradeSpeed();
                if (kb.xKey.wasPressedThisFrame) manager.TryUpgradeRange();
                if (kb.cKey.wasPressedThisFrame) manager.TryUpgradeBreath();
                if (kb.vKey.wasPressedThisFrame) manager.TryUpgradeBag();
                if (kb.rKey.wasPressedThisFrame) manager.EnterTruckShop(0);
            }
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll > 0.1f) SetSlot(ActiveSlot == 1 ? 3 : ActiveSlot - 1);
                else if (scroll < -0.1f) SetSlot(ActiveSlot == 3 ? 1 : ActiveSlot + 1);
            }
#else
            if (Input.GetKeyDown(KeyCode.Alpha1)) SetSlot(1);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SetSlot(2);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SetSlot(3);
            if (Input.GetKeyDown(KeyCode.Z)) manager.TryUpgradeSpeed();
            if (Input.GetKeyDown(KeyCode.X)) manager.TryUpgradeRange();
            if (Input.GetKeyDown(KeyCode.C)) manager.TryUpgradeBreath();
            if (Input.GetKeyDown(KeyCode.V)) manager.TryUpgradeBag();
            if (Input.GetKeyDown(KeyCode.R)) manager.EnterTruckShop(0);
#endif
        }

        public void SetSlot(int slot)
        {
            ActiveSlot = Mathf.Clamp(slot, 1, 3);
            RefreshToolVisual();
        }

        private void RefreshToolVisual()
        {
            if (slot1Visual != null) slot1Visual.SetActive(ActiveSlot == 1);
            if (slot2Visual != null) slot2Visual.SetActive(ActiveSlot == 2);
            if (slot3Visual != null) slot3Visual.SetActive(ActiveSlot == 3);
        }

        private void HandleInteraction()
        {
            if (_clickCooldown > 0f) _clickCooldown -= Time.deltaTime;
            ClearHighlights();
            AimTitle = "";
            AimHint = "";
            AimIsDanger = false;
            AimIsTreasure = false;
            AimIsCleanable = false;
            AimIsStain = false;
            SuggestedSlot = 0;
            AimCleanProgress01 = -1f;

            if (cameraTransform == null) return;
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                AimTitle = "화면 클릭 → 조작 시작";
                return;
            }

            bool lmbHeld = false, lmbDown = false, pressE = false, holdE = false, pressQ = false;
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            if (mouse != null)
            {
                lmbHeld = mouse.leftButton.isPressed;
                lmbDown = mouse.leftButton.wasPressedThisFrame;
            }
            if (kb != null)
            {
                pressE = kb.eKey.wasPressedThisFrame;
                holdE = kb.eKey.isPressed;
                pressQ = kb.qKey.wasPressedThisFrame;
            }
#else
            lmbHeld = Input.GetMouseButton(0);
            lmbDown = Input.GetMouseButtonDown(0);
            pressE = Input.GetKeyDown(KeyCode.E);
            holdE = Input.GetKey(KeyCode.E);
            pressQ = Input.GetKeyDown(KeyCode.Q);
#endif

            Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
            float reach = Mathf.Max(4.2f, manager.CleanRange + 1.5f);

            // RaycastAll로 정렬해 '바닥 얼룩' 위에 놓인 쓰레기봉투·박스·보물을 항상 최우선 조준!
            RaycastHit[] hits = Physics.RaycastAll(ray, reach);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            TreasureTarget hitTreasure = null;
            SecurityCameraController hitCctv = null;
            CleanableTarget hitTrash = null;
            CleanableTarget hitStain = null;
            Vector3 hitPoint = Vector3.zero;

            foreach (var h in hits)
            {
                if (h.collider.GetComponentInParent<FirstPersonCleanerController>() != null) continue;

                var tr = h.collider.GetComponentInParent<TreasureTarget>();
                if (tr != null && tr.State == TreasureState.HiddenInRoom && hitTreasure == null)
                {
                    hitTreasure = tr;
                    hitPoint = h.point;
                    break;
                }

                var cc = h.collider.GetComponentInParent<SecurityCameraController>();
                if (cc != null && hitCctv == null)
                {
                    hitCctv = cc;
                    hitPoint = h.point;
                    break;
                }

                var cl = h.collider.GetComponentInParent<CleanableTarget>();
                if (cl != null && !cl.IsCleaned)
                {
                    if (cl.kind == CleanableKind.FloorStain)
                    {
                        if (hitStain == null) { hitStain = cl; if (hitTrash == null) hitPoint = h.point; }
                    }
                    else
                    {
                        if (hitTrash == null) { hitTrash = cl; hitPoint = h.point; }
                        break; // 쓰레기 덩어리/소품이 있으면 밑의 바닥 얼룩보다 무조건 우선!
                    }
                }
                else
                {
                    // 벽·가구(냉장고/매트리스)에 막히면 탐색 중단
                    break;
                }
            }

            // 1. Treasure (최소 텍스트 키 배지)
            if (hitTreasure != null)
            {
                _lastTreasure = hitTreasure;
                hitTreasure.SetHighlighted(true);
                AimIsTreasure = true;
                bool seen = manager.cctv != null && manager.cctv.CanSeePosition(transform.position);
                float nextDet = manager.PreviewDetectionIfSteal(hitTreasure, seen);

                if (hitTreasure.isDesignatedLostItem)
                {
                    AimTitle = $"★ {hitTreasure.itemName}";
                    AimHint = $"[Q] 보관 (+${manager.lostItemTip})   |   [E] 슈킹 (100% 발각)";
                    AimIsDanger = true;
                }
                else
                {
                    AimTitle = $"{hitTreasure.itemName} (${hitTreasure.value})";
                    AimHint = $"[E] 슈킹 ({nextDet:0}%{(seen ? " CCTV!" : "")})   |   [Q] 선반 보관";
                    AimIsDanger = seen || nextDet >= 35f;
                }

                if (pressQ) manager.ReturnTreasure(hitTreasure);
                else if (pressE) manager.StealTreasure(hitTreasure, seen);
                return;
            }

            // 2. CCTV (거품 가리기)
            if (hitCctv != null)
            {
                if (ActiveSlot == 2 && manager.StainTier >= 2)
                {
                    AimTitle = hitCctv.IsBlinded ? $"CCTV 가려짐 ({hitCctv.BlindRemaining:0.0}s)" : "CCTV [좌클릭 꾹] 거품";
                    if (lmbHeld && !hitCctv.IsBlinded)
                    {
                        _isMoppingNow = true;
                        if (hitCctv.ApplyFoamSpray(Time.deltaTime, 8f))
                        {
                            manager.sfx.PlayScrub();
                            manager.ShowToast("CCTV 거품 가리기 완료 (8초)");
                        }
                    }
                }
                else
                {
                    AimTitle = "CCTV";
                    SuggestedSlot = 2;
                }
                return;
            }

            // 3. Cleanable (쓰레기 우선, 없으면 바닥 얼룩) — 긴 설명 글자 제거! 시각적 조준점+게이지만 표시
            CleanableTarget cleanable = hitTrash != null ? hitTrash : hitStain;
            if (cleanable == null) return;

            _lastCleanable = cleanable;
            cleanable.SetHighlighted(true);
            AimIsCleanable = true;
            AimIsStain = (cleanable.kind == CleanableKind.FloorStain);
            AimCleanProgress01 = cleanable.CleanProgress01;

            bool actionHeld = lmbHeld || holdE;
            bool actionDown = lmbDown || pressE;

            if (cleanable.kind == CleanableKind.FloorStain)
            {
                if (ActiveSlot != 2)
                {
                    // 글자 문장 대신 아주 작은 슬롯 뱃지 + 우상단 [2]번 반짝임
                    SuggestedSlot = 2;
                    AimTitle = "[2] 걸레";
                    if (actionDown) SetSlot(2); // 얼룩 클릭 시 아예 자연스럽게 2번 걸레로 자동 전환!
                    return;
                }

                if (actionHeld)
                {
                    _isMoppingNow = true;
                    float mopPower = (manager.StainTier == 1 ? 0.9f : 1.75f) * (1f + manager.LvSpeed * 0.18f);
                    if (cleanable.ApplyCleanDamage(mopPower * Time.deltaTime, toolHoldPoint))
                    {
                        manager.OnCleanableFinished(cleanable);
                    }
                }
            }
            else
            {
                if (ActiveSlot != 1)
                {
                    SuggestedSlot = 1;
                    AimTitle = "[1] 수거";
                    if (actionDown) SetSlot(1); // 쓰레기 클릭 시 자연스럽게 1번 수거 장비로 자동 전환!
                    return;
                }

                if (!manager.CanPickUpMoreTrash(cleanable.bagCost))
                {
                    AimTitle = "가방 가득 참! ([V] / [R])";
                    AimIsDanger = true;
                    return;
                }

                bool isVacuum = manager.SuctionTier >= 3;
                if (isVacuum && actionHeld)
                {
                    _isVacuumingNow = true;
                    float dps = 1.9f * manager.CleanSpeedMultiplier;
                    if (cleanable.ApplyCleanDamage(dps * Time.deltaTime, toolHoldPoint))
                    {
                        manager.OnCleanableFinished(cleanable);
                    }
                    Collider[] near = Physics.OverlapSphere(hitPoint, manager.AreaRadius);
                    foreach (var col in near)
                    {
                        var other = col.GetComponentInParent<CleanableTarget>();
                        if (other != null && other != cleanable && !other.IsCleaned &&
                            other.kind == CleanableKind.SmallTrash && manager.CanPickUpMoreTrash(other.bagCost))
                        {
                            if (other.ApplyCleanDamage(dps * 0.8f * Time.deltaTime, toolHoldPoint))
                            {
                                manager.OnCleanableFinished(other);
                            }
                        }
                    }
                }
                else if (!isVacuum && (actionDown || (actionHeld && _clickCooldown <= 0f)))
                {
                    _clickCooldown = manager.SuctionTier == 1 ? 0.30f : 0.16f;
                    _grabPunchTimer = 0.18f;
                    float dmg = 0.80f * manager.CleanSpeedMultiplier;
                    if (cleanable.ApplyCleanDamage(dmg, toolHoldPoint))
                    {
                        manager.OnCleanableFinished(cleanable);
                    }
                    else
                    {
                        manager.sfx.PlayScrub();
                    }
                }
            }
        }

        private void ClearHighlights()
        {
            if (_lastCleanable != null) { _lastCleanable.SetHighlighted(false); _lastCleanable = null; }
            if (_lastTreasure != null) { _lastTreasure.SetHighlighted(false); _lastTreasure = null; }
        }
    }
}
