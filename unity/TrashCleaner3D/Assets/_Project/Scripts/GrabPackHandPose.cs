using UnityEngine;

namespace TrashCleaner3D
{
    /// <summary>
    /// [v2.4] 그랩팩(GrabPack) 손가락 2단 관절 포즈 컨트롤러.
    /// open01 = 0f : 꽉 쥔 주먹 (발사/회수/대기)
    /// open01 = 1f : 다섯 손가락을 활짝 펼친 보자기 (목표 도착 직후 집기 준비)
    /// </summary>
    public class GrabPackHandPose : MonoBehaviour
    {
        public Transform[] fingerBases;
        public Transform[] fingerMids;
        public Transform thumbBase;
        public Transform thumbMid;

        private static readonly float[] OpenSpreadY = { -19f, -6f, 6f, 19f };
        private float _currentOpen01 = -1f;

        /// <summary>0 = 주먹, 1 = 보자기. 값이 같으면 불필요한 Transform 갱신을 건너뛴다.</summary>
        public void SetOpen01(float open01)
        {
            open01 = Mathf.Clamp01(open01);
            if (Mathf.Abs(open01 - _currentOpen01) < 0.0005f) return;
            _currentOpen01 = open01;

            if (fingerBases != null)
            {
                for (int i = 0; i < fingerBases.Length; i++)
                {
                    if (fingerBases[i] == null) continue;
                    float spread = i < OpenSpreadY.Length ? OpenSpreadY[i] : 0f;
                    float pitch = Mathf.Lerp(84f, -10f, open01);
                    float yaw = Mathf.Lerp(0f, spread, open01);
                    fingerBases[i].localRotation = Quaternion.Euler(pitch, yaw, 0f);
                }
            }

            if (fingerMids != null)
            {
                float midPitch = Mathf.Lerp(80f, 4f, open01);
                Quaternion midRot = Quaternion.Euler(midPitch, 0f, 0f);
                for (int i = 0; i < fingerMids.Length; i++)
                {
                    if (fingerMids[i] != null) fingerMids[i].localRotation = midRot;
                }
            }

            if (thumbBase != null)
            {
                thumbBase.localRotation = Quaternion.Euler(
                    Mathf.Lerp(26f, -8f, open01),
                    Mathf.Lerp(62f, -42f, open01),
                    Mathf.Lerp(-22f, 12f, open01));
            }

            if (thumbMid != null)
            {
                thumbMid.localRotation = Quaternion.Euler(
                    Mathf.Lerp(46f, -4f, open01),
                    Mathf.Lerp(18f, -6f, open01),
                    0f);
            }
        }
    }
}
