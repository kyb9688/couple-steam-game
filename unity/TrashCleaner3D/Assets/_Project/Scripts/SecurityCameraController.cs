using UnityEngine;

namespace TrashCleaner3D
{
    /// <summary>
    /// 방 구석 고정/회전 CCTV.
    /// 플레이어가 붉은 시야각 안에 있을 때 슈킹하면 목격 페널티(+35%) 부여.
    /// 2번 장비(세제 스프레이 이상)로 조준해 좌클릭하면 거품에 덮여 일정 시간 무력화.
    /// </summary>
    public class SecurityCameraController : MonoBehaviour
    {
        public Transform pivotTransform;
        public Renderer visionConeRenderer;
        public Light statusLight;
        public float sweepAngle = 38f;
        public float sweepSpeed = 0.7f;
        public float viewRange = 6.0f;
        public float viewHalfAngle = 30f;

        public bool IsBlinded => _blindTimer > 0f;
        public float BlindRemaining => Mathf.Max(0f, _blindTimer);

        private Quaternion _baseRot;
        private float _blindTimer;
        private float _sprayProgress;
        private float _phase;
        private Material _coneMat;

        private void Awake()
        {
            if (pivotTransform == null) pivotTransform = transform;
            _baseRot = pivotTransform.localRotation;
            if (visionConeRenderer != null)
            {
                _coneMat = visionConeRenderer.material;
            }
        }

        private void Update()
        {
            if (_blindTimer > 0f)
            {
                _blindTimer -= Time.deltaTime;
                if (visionConeRenderer != null) visionConeRenderer.enabled = false;
                if (statusLight != null) statusLight.color = new Color(0.55f, 0.85f, 1.0f);
                return;
            }

            if (visionConeRenderer != null) visionConeRenderer.enabled = true;
            if (statusLight != null) statusLight.color = Color.red;

            _phase += Time.deltaTime * sweepSpeed;
            float yaw = Mathf.Sin(_phase) * sweepAngle;
            pivotTransform.localRotation = _baseRot * Quaternion.Euler(0f, yaw, 0f);
        }

        public bool CanSeePosition(Vector3 targetPos)
        {
            if (IsBlinded) return false;
            Vector3 origin = pivotTransform.position;
            Vector3 toTarget = targetPos - origin;
            float dist = toTarget.magnitude;
            if (dist > viewRange) return false;

            float angle = Vector3.Angle(pivotTransform.forward, toTarget.normalized);
            if (angle > viewHalfAngle) return false;

            if (Physics.Raycast(origin, toTarget.normalized, out RaycastHit hit, dist - 0.15f))
            {
                // 벽/냉장고에 가려졌는지 확인 (플레이어 자신이나 보물이면 보임)
                if (!hit.collider.GetComponentInParent<FirstPersonCleanerController>() &&
                    !hit.collider.GetComponentInParent<TreasureTarget>())
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 2번 장비(세제 스프레이 이상)로 거품을 뿌려 CCTV 무력화
        /// </summary>
        public bool ApplyFoamSpray(float dt, float blindDuration)
        {
            if (IsBlinded) return false;
            _sprayProgress += dt;
            if (_sprayProgress >= 0.45f)
            {
                _sprayProgress = 0f;
                _blindTimer = blindDuration;
                return true;
            }
            return false;
        }

        public void ResetCamera()
        {
            _blindTimer = 0f;
            _sprayProgress = 0f;
        }
    }
}
