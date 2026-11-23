using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TrashCleaner3D
{
    /// <summary>
    /// 1인칭 이동/시점 + 장비 3종 전환(1/2/3) + 쓰레기 흡입/얼룩 닦기/CCTV 거품 가리기 +
    /// 보물 슈킹(E)/보관(Q) + 현장 즉시 강화(Z/X/C/V) 컨트롤러.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonCleanerController : MonoBehaviour
    {
        public GamePrototypeManager manager;
        public Transform cameraTransform;
        public Transform toolHoldPoint;
        public Light flashlight;
        public float moveSpeed = 3.4f;
        public float mouseSensitivity = 0.14f;

        public int ActiveSlot { get; private set; } = 1; // 1:수거/흡입, 2:얼룩/거품, 3:생존/탐지
        public string AimTitle { get; private set; } = "";
        public string AimHint { get; private set; } = "";
        public bool AimIsDanger { get; private set; }

        private CharacterController _cc;
        private float _pitch;
        private CleanableTarget _lastCleanable;
        private TreasureTarget _lastTreasure;
        private float _clickCooldown;
        private Vector3 _toolBaseLocalPos;
        private Renderer _toolRenderer;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            if (toolHoldPoint != null)
            {
                _toolBaseLocalPos = toolHoldPoint.localPosition;
                _toolRenderer = toolHoldPoint.GetComponentInChildren<Renderer>();
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

            HandleMovementAndLook();
            HandleHotkeys();
            HandleInteraction();
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

            // 안전장치: 어떤 이유로든 바닥 아래로 추락하면 즉시 스폰 위치로 복구
            if (transform.position.y < -1.2f && manager != null && manager.playerSpawnPoint != null)
            {
                TeleportTo(manager.playerSpawnPoint.position + Vector3.up * 0.2f, manager.playerSpawnPoint.rotation);
            }

            // 미세한 걷기 손 흔들림
            if (toolHoldPoint != null)
            {
                float bob = (Mathf.Abs(mx) + Mathf.Abs(mz) > 0.1f) ? Mathf.Sin(Time.time * 10f) * 0.012f : 0f;
                toolHoldPoint.localPosition = _toolBaseLocalPos + new Vector3(0f, bob, 0f);
            }
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
            if (_toolRenderer == null) return;
            Color c = ActiveSlot == 1
                ? (manager != null && manager.SuctionTier >= 3 ? new Color(0.95f, 0.72f, 0.15f) : new Color(0.85f, 0.65f, 0.45f))
                : ActiveSlot == 2
                    ? new Color(0.25f, 0.70f, 0.92f)
                    : new Color(0.40f, 0.85f, 0.50f);
            _toolRenderer.material.SetColor("_BaseColor", c);
        }

        private void HandleInteraction()
        {
            if (_clickCooldown > 0f) _clickCooldown -= Time.deltaTime;
            ClearHighlights();
            AimTitle = "";
            AimHint = "";
            AimIsDanger = false;

            if (cameraTransform == null || Cursor.lockState != CursorLockMode.Locked) return;

            bool lmbHeld = false, lmbDown = false, pressE = false, pressQ = false;
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
                pressQ = kb.qKey.wasPressedThisFrame;
            }
#else
            lmbHeld = Input.GetMouseButton(0);
            lmbDown = Input.GetMouseButtonDown(0);
            pressE = Input.GetKeyDown(KeyCode.E);
            pressQ = Input.GetKeyDown(KeyCode.Q);
#endif

            Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
            float reach = Mathf.Max(2.6f, manager.CleanRange);
            if (!Physics.Raycast(ray, out RaycastHit hit, reach)) return;

            // 1. Check Treasure Target (Priority)
            TreasureTarget treasure = hit.collider.GetComponentInParent<TreasureTarget>();
            if (treasure != null && treasure.State == TreasureState.HiddenInRoom)
            {
                _lastTreasure = treasure;
                treasure.SetHighlighted(true);
                bool seen = manager.cctv != null && manager.cctv.CanSeePosition(transform.position);
                float nextDet = manager.PreviewDetectionIfSteal(treasure, seen);

                if (treasure.isDesignatedLostItem)
                {
                    AimTitle = $"[의뢰인 지정 분실물] {treasure.itemName}";
                    AimHint = $"[Q] 선반 보관 (팁 +${manager.lostItemTip})   |   [E] 슈킹 (경고: 100% 발각!)";
                    AimIsDanger = true;
                }
                else
                {
                    AimTitle = $"[보물] {treasure.itemName} (가치 ${treasure.value})";
                    string cctvWarn = seen ? " [CCTV 목격 중!]" : "";
                    AimHint = $"[E] 몰래 슈킹 (예상 발각 {nextDet:0}%{cctvWarn})   |   [Q] 의뢰인 선반 보관";
                    AimIsDanger = seen || nextDet >= 35f;
                }

                if (pressQ) manager.ReturnTreasure(treasure);
                else if (pressE) manager.StealTreasure(treasure, seen);
                return;
            }

            // 2. Check CCTV Target (Foam Spray blinding with Slot 2 T2+)
            SecurityCameraController camTarget = hit.collider.GetComponentInParent<SecurityCameraController>();
            if (camTarget != null)
            {
                AimTitle = camTarget.IsBlinded
                    ? $"CCTV (거품으로 가려짐 · {camTarget.BlindRemaining:0.0}초)"
                    : "감시 CCTV (작동 중)";
                if (ActiveSlot == 2 && manager.StainTier >= 2)
                {
                    AimHint = camTarget.IsBlinded ? "현재 시야 차단됨" : "[좌클릭 홀드] 세제 거품으로 렌즈 가리기 (8초)";
                    if (lmbHeld && !camTarget.IsBlinded && camTarget.ApplyFoamSpray(Time.deltaTime, 8f))
                    {
                        manager.sfx.PlayScrub();
                        manager.ShowToast("CCTV 렌즈를 거품으로 가렸습니다! (8초간 시야 차단)");
                    }
                }
                else
                {
                    AimHint = "트럭에서 [2번 장비: 세제 스프레이] 구매 후 거품으로 가릴 수 있습니다";
                }
                return;
            }

            // 3. Check Cleanable Target (TrashPile / SmallTrash / FloorStain)
            CleanableTarget cleanable = hit.collider.GetComponentInParent<CleanableTarget>();
            if (cleanable != null && !cleanable.IsCleaned)
            {
                _lastCleanable = cleanable;
                cleanable.SetHighlighted(true);
                AimTitle = cleanable.displayName;

                if (cleanable.kind == CleanableKind.FloorStain)
                {
                    if (ActiveSlot != 2)
                    {
                        AimHint = "[2]번 걸레/세제 장비로 전환해서 닦으세요";
                        return;
                    }
                    AimHint = "[좌클릭 홀드] 얼룩 닦아내기";
                    if (lmbHeld)
                    {
                        float mopPower = (manager.StainTier == 1 ? 0.65f : 1.45f) * (1f + manager.LvSpeed * 0.18f);
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
                        AimHint = "[1]번 수거/청소기 장비로 전환해서 치우세요";
                        return;
                    }

                    if (!manager.CanPickUpMoreTrash(cleanable.bagCost))
                    {
                        AimHint = "가방 가득 참! [V] 가방 강화 또는 [R] 트럭 복귀";
                        AimIsDanger = true;
                        return;
                    }

                    bool isVacuum = manager.SuctionTier >= 3;
                    AimHint = isVacuum
                        ? "[좌클릭 홀드] 청소기 연속 흡입"
                        : "[좌클릭] 쓰레기 수거 (트럭에서 청소기 구매 시 홀드 흡입!)";

                    if (isVacuum && lmbHeld)
                    {
                        // 흡입 진동 + 범위 흡입
                        if (toolHoldPoint != null)
                        {
                            toolHoldPoint.localPosition = _toolBaseLocalPos + Random.insideUnitSphere * 0.008f;
                        }
                        float dps = 1.6f * manager.CleanSpeedMultiplier;
                        if (cleanable.ApplyCleanDamage(dps * Time.deltaTime, toolHoldPoint))
                        {
                            manager.OnCleanableFinished(cleanable);
                        }
                        // 주변 소형 쓰레기도 범위(X강화)로 같이 빨아들이기
                        Collider[] near = Physics.OverlapSphere(hit.point, manager.AreaRadius);
                        foreach (var col in near)
                        {
                            var other = col.GetComponentInParent<CleanableTarget>();
                            if (other != null && other != cleanable && !other.IsCleaned &&
                                other.kind == CleanableKind.SmallTrash && manager.CanPickUpMoreTrash(other.bagCost))
                            {
                                if (other.ApplyCleanDamage(dps * 0.75f * Time.deltaTime, toolHoldPoint))
                                {
                                    manager.OnCleanableFinished(other);
                                }
                            }
                        }
                    }
                    else if (!isVacuum && (lmbDown || (lmbHeld && _clickCooldown <= 0f)))
                    {
                        _clickCooldown = manager.SuctionTier == 1 ? 0.42f : 0.22f;
                        float dmg = 0.65f * manager.CleanSpeedMultiplier;
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
        }

        private void ClearHighlights()
        {
            if (_lastCleanable != null) { _lastCleanable.SetHighlighted(false); _lastCleanable = null; }
            if (_lastTreasure != null) { _lastTreasure.SetHighlighted(false); _lastTreasure = null; }
        }
    }
}
