using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TrashCleaner3D
{
    /// <summary>
    /// v0.2 1인칭 컨트롤러:
    /// - Phase 1: 오른손 주먹 펀치(큰 봉투 터뜨리기) / 오른손 줍기(작은 쓰레기 → 왼손 봉투 빵빵하게 성장) / 우클릭(트럭 슛!)
    /// - Phase 2: 마우스 실제 이동량(mouseDelta)으로 문질러 닦기 (물티슈 원형 박박 vs 대걸레 좌우 광역 스윙)
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonCleanerController : MonoBehaviour
    {
        public GamePrototypeManager manager;
        public Transform cameraTransform;
        public Transform rightToolPivot;
        public Transform leftBagPivot;
        public GameObject fistHandVisual;
        public GameObject tissueVisual;
        public GameObject mopVisual;
        public Light flashlight;
        public float moveSpeed = 3.5f;
        public float mouseSensitivity = 0.14f;

        // Visual HUD feedback (zero verbose text!)
        public bool AimIsPile { get; private set; }
        public bool AimIsSmallTrash { get; private set; }
        public bool AimIsStain { get; private set; }
        public bool NeedMouseShakeHint { get; private set; }
        public float AimProgress01 { get; private set; } = -1f;
        public bool ShowHelp { get; private set; }

        private CharacterController _cc;
        private float _pitch;
        private CleanableTarget _lastTarget;
        private float _actionCooldown;

        // Distinct procedural 1st-person animation timers
        private Vector3 _rightBasePos;
        private Quaternion _rightBaseRot;
        private Vector3 _leftBasePos;
        private float _punchTimer;
        private float _grabTimer;
        private float _scrubMotionWeight;
        private float _scrubPhase;
        private float _scrubSoundCooldown;

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

            // 가챠 본능 선택 중에는 Q(보관) / E(슈킹) 단축키 즉시 반응!
            if (manager.Phase == GamePhase.GachaStealPopup)
            {
                HandleGachaHotkeys();
                ClearHighlight();
                return;
            }

            if (manager.Phase != GamePhase.Phase1_TrashRemoval && manager.Phase != GamePhase.Phase2_FloorMopping)
            {
                ClearHighlight();
                return;
            }

            UpdateToolVisualMode();
            Vector2 mouseDelta = HandleMovementAndLook();
            HandleInteraction(mouseDelta);
            AnimateHandsAndBag();
        }

        private void HandleGachaHotkeys()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.qKey.wasPressedThisFrame) manager.ResolveGachaChoice(false);
                else if (kb.eKey.wasPressedThisFrame) manager.ResolveGachaChoice(true);
            }
#else
            if (Input.GetKeyDown(KeyCode.Q)) manager.ResolveGachaChoice(false);
            else if (Input.GetKeyDown(KeyCode.E)) manager.ResolveGachaChoice(true);
#endif
        }

        private void UpdateToolVisualMode()
        {
            bool isP2 = manager.Phase == GamePhase.Phase2_FloorMopping;
            if (fistHandVisual != null) fistHandVisual.SetActive(!isP2);
            if (tissueVisual != null) tissueVisual.SetActive(isP2 && !manager.HasMopUnlocked);
            if (mopVisual != null) mopVisual.SetActive(isP2 && manager.HasMopUnlocked);

            // 왼손 쓰레기봉투: 담은 개수에 따라 실시간으로 빵빵하게 부풀어 오름!
            if (leftBagPivot != null)
            {
                bool showBag = !isP2 && manager.HandBagCount > 0;
                leftBagPivot.gameObject.SetActive(showBag);
                if (showBag)
                {
                    float fill = Mathf.Clamp01(manager.HandBagCount / (float)manager.HandBagCapacity);
                    float s = Mathf.Lerp(0.14f, 0.36f, fill);
                    leftBagPivot.localScale = Vector3.Lerp(leftBagPivot.localScale, new Vector3(s, s * 1.15f, s), 12f * Time.deltaTime);
                }
            }
        }

        private Vector2 HandleMovementAndLook()
        {
            float mx = 0f, mz = 0f;
            Vector2 lookDelta = Vector2.zero;
            bool toggleFlash = false, openMindMap = false, unlock = false;

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
                if (kb.rKey.wasPressedThisFrame) openMindMap = true;
                if (kb.hKey.wasPressedThisFrame) ShowHelp = !ShowHelp;
                if (kb.escapeKey.wasPressedThisFrame) unlock = true;
            }
            if (mouse != null)
            {
                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    lookDelta = mouse.delta.ReadValue();
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
                lookDelta = new Vector2(Input.GetAxis("Mouse X") * 12f, Input.GetAxis("Mouse Y") * 12f);
            }
            if (Input.GetKeyDown(KeyCode.F)) toggleFlash = true;
            if (Input.GetKeyDown(KeyCode.R)) openMindMap = true;
            if (Input.GetKeyDown(KeyCode.H)) ShowHelp = !ShowHelp;
            if (Input.GetKeyDown(KeyCode.Escape)) unlock = true;
#endif

            if (unlock) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            if (toggleFlash && flashlight != null) flashlight.enabled = !flashlight.enabled;
            if (openMindMap) { manager.OpenMindMap(); return Vector2.zero; }

            // Phase 2 바닥 문지를 때는 시점이 덜 어지럽게 감도 살짝 낮춤
            float sens = (_scrubMotionWeight > 0.2f) ? mouseSensitivity * 0.55f : mouseSensitivity;
            transform.Rotate(Vector3.up * (lookDelta.x * sens));
            if (cameraTransform != null)
            {
                _pitch = Mathf.Clamp(_pitch - lookDelta.y * sens, -82f, 82f);
                cameraTransform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }

            Vector3 move = (transform.right * mx + transform.forward * mz).normalized * moveSpeed;
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
            if (_actionCooldown > 0f) _actionCooldown -= Time.deltaTime;
            if (_scrubSoundCooldown > 0f) _scrubSoundCooldown -= Time.deltaTime;
            ClearHighlight();
            AimIsPile = false;
            AimIsSmallTrash = false;
            AimIsStain = false;
            NeedMouseShakeHint = false;
            AimProgress01 = -1f;
            _scrubMotionWeight = Mathf.MoveTowards(_scrubMotionWeight, 0f, 6f * Time.deltaTime);

            if (cameraTransform == null || Cursor.lockState != CursorLockMode.Locked) return;

            bool lmbHeld = false, lmbDown = false, rmbDown = false;
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null)
            {
                lmbHeld = mouse.leftButton.isPressed;
                lmbDown = mouse.leftButton.wasPressedThisFrame;
                rmbDown = mouse.rightButton.wasPressedThisFrame;
            }
#else
            lmbHeld = Input.GetMouseButton(0);
            lmbDown = Input.GetMouseButtonDown(0);
            rmbDown = Input.GetMouseButtonDown(1);
#endif

            // 우클릭: 왼손에 담은 봉투를 열린 문 밖 쓰레기 트럭으로 농구 슛!
            if (manager.Phase == GamePhase.Phase1_TrashRemoval && rmbDown && manager.HandBagCount > 0)
            {
                Vector3 origin = cameraTransform.position + cameraTransform.forward * 0.55f - cameraTransform.right * 0.15f;
                manager.ThrowHandBag(origin, cameraTransform.forward);
                _grabTimer = 0.2f;
                return;
            }

            Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
            RaycastHit[] hits = Physics.RaycastAll(ray, 4.2f);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            CleanableTarget target = null;
            foreach (var h in hits)
            {
                if (h.collider.GetComponentInParent<FirstPersonCleanerController>() != null) continue;
                var c = h.collider.GetComponentInParent<CleanableTarget>();
                if (c != null && !c.IsCleaned)
                {
                    if (manager.Phase == GamePhase.Phase1_TrashRemoval)
                    {
                        if (c.kind == CleanableKind.FloorStain) continue; // Phase 1에선 얼룩 완전 무시!
                        target = c;
                        break;
                    }
                    else if (manager.Phase == GamePhase.Phase2_FloorMopping)
                    {
                        if (c.kind == CleanableKind.FloorStain)
                        {
                            target = c;
                            break;
                        }
                    }
                }
                else
                {
                    // 벽/가구에 가로막힘
                    break;
                }
            }

            if (target == null) return;

            _lastTarget = target;
            target.SetHighlighted(true);
            AimProgress01 = target.CleanProgress01;

            if (target.kind == CleanableKind.TrashPile)
            {
                AimIsPile = true;
                if ((lmbDown || (lmbHeld && _actionCooldown <= 0f)))
                {
                    _actionCooldown = 0.28f;
                    _punchTimer = 0.22f; // 확실한 주먹 펀치 모션!
                    manager.ConsumeStamina(1.5f);
                    if (target.ApplyCleanDamage(manager.FistDamage, null))
                    {
                        manager.OnTrashPileBurst(target);
                    }
                    else
                    {
                        manager.sfx.PlayScrub();
                    }
                }
            }
            else if (target.kind == CleanableKind.SmallTrash)
            {
                AimIsSmallTrash = true;
                if (manager.IsHandBagFull)
                {
                    if (lmbDown) manager.ShowToast("봉투가 꽉 찼습니다! 열린 문 밖 트럭에 [우클릭] 슛 하세요!");
                    return;
                }

                if (lmbDown || (lmbHeld && _actionCooldown <= 0f))
                {
                    _actionCooldown = 0.18f;
                    _grabTimer = 0.18f; // 확실한 줍기(Grab) 모션!
                    manager.ConsumeStamina(0.9f);
                    Transform dest = leftBagPivot != null ? leftBagPivot : rightToolPivot;
                    if (target.ApplyCleanDamage(99f, dest))
                    {
                        manager.OnSmallTrashPicked(target);
                    }
                }
            }
            else if (target.kind == CleanableKind.FloorStain)
            {
                AimIsStain = true;
                if (lmbHeld)
                {
                    float shake = mouseDelta.magnitude;
                    // 마우스를 실제로 흔들수록 5배 빠르게 닦이고, 가만히 누르기만 하면 아주 느림!
                    float power = (0.12f + Mathf.Min(shake, 45f) * 0.085f) * manager.ScrubSpeedMult;
                    if (shake < 1.2f) NeedMouseShakeHint = true;

                    _scrubMotionWeight = 1f;
                    _scrubPhase += (6f + Mathf.Min(shake, 30f) * 0.8f) * Time.deltaTime;
                    manager.ConsumeStamina(0.65f * Time.deltaTime);

                    if (shake > 2.5f && _scrubSoundCooldown <= 0f)
                    {
                        _scrubSoundCooldown = 0.22f;
                        manager.sfx.PlayScrub();
                    }

                    if (target.ApplyCleanDamage(power * Time.deltaTime, null))
                    {
                        manager.OnStainCleaned(target);
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
                    // T2 대걸레: 크고 시원하게 좌우 쓱싹쓱싹 스윙 모션!
                    float s = Mathf.Sin(_scrubPhase * 1.4f);
                    targetPos += new Vector3(s * 0.20f - 0.12f, -0.05f, 0.16f + Mathf.Cos(_scrubPhase * 1.4f) * 0.05f);
                    targetRot = Quaternion.Euler(18f, s * 22f, -s * 16f);
                }
                else
                {
                    // T1 물티슈/맨손: 바닥 쪽으로 내려가 작고 빠르게 원형 박박 문지르기 모션!
                    float cx = Mathf.Cos(_scrubPhase * 2.4f) * 0.09f;
                    float cz = Mathf.Sin(_scrubPhase * 2.4f) * 0.07f;
                    targetPos += new Vector3(cx - 0.10f, -0.11f, 0.18f + cz);
                    targetRot = Quaternion.Euler(42f, cx * 80f, -cx * 90f);
                }
            }
            else if (_punchTimer > 0f)
            {
                // 주먹 펀치 모션: 앞으로 쾅! 뻗고 강하게 내려침
                _punchTimer -= Time.deltaTime;
                float t = Mathf.Sin(Mathf.Clamp01(_punchTimer / 0.22f) * Mathf.PI);
                targetPos += new Vector3(-0.14f * t, 0.05f * t, 0.30f * t);
                targetRot = Quaternion.Euler(-22f * t, -18f * t, 14f * t);
            }
            else if (_grabTimer > 0f)
            {
                // 쓰레기 줍기 모션: 아래로 슥 뻗어 왼손 봉투 쪽으로 낚아채기
                _grabTimer -= Time.deltaTime;
                float t = Mathf.Sin(Mathf.Clamp01(_grabTimer / 0.18f) * Mathf.PI);
                targetPos += new Vector3(-0.22f * t, -0.12f * t, 0.14f * t);
                targetRot = Quaternion.Euler(28f * t, -35f * t, 20f * t);
            }
            else
            {
                float idle = Mathf.Sin(Time.time * 2.5f) * 0.006f;
                targetPos += new Vector3(0f, idle, 0f);
            }

            rightToolPivot.localPosition = Vector3.Lerp(rightToolPivot.localPosition, targetPos, 22f * Time.deltaTime);
            rightToolPivot.localRotation = Quaternion.Slerp(rightToolPivot.localRotation, targetRot, 22f * Time.deltaTime);

            // 왼손 봉투가 꽉 차면 통통 살짝 뛰어서 "던져줘!" 시각적 신호
            if (leftBagPivot != null && leftBagPivot.gameObject.activeSelf)
            {
                float bounce = manager.IsHandBagFull ? Mathf.Abs(Mathf.Sin(Time.time * 7f)) * 0.035f : 0f;
                leftBagPivot.localPosition = _leftBasePos + new Vector3(0f, bounce, 0f);
            }
        }

        private void ClearHighlight()
        {
            if (_lastTarget != null) { _lastTarget.SetHighlighted(false); _lastTarget = null; }
        }
    }
}
