using UnityEngine;

namespace DDT
{
    public class DDTCamera : MonoBehaviour
    {
        private float m_DebugCameraMovSpeedMultiplier = 0.1f;
        private Vector2 m_DebugCameraMovSpeedMultiplierRange = new Vector2(0.01f, 10.0f);
        private float m_DebugCameraPitch = 0.0f;
        private float m_DebugCameraYaw = 0.0f;

        void Start()
        {
            if (Camera.main == null)
                return;

            transform.position = Camera.main.transform.position;
            transform.rotation = Camera.main.transform.rotation;
        }

        public void InitializeCamRotation()
        {
            if (DrawDebugTools.Instance != null && DrawDebugTools.Instance.m_DDTSettings != null)
                m_DebugCameraMovSpeedMultiplierRange = DrawDebugTools.Instance.m_DDTSettings.m_DebugCamSpeedMultiplierRange;

            m_DebugCameraYaw = transform.eulerAngles.y;
            m_DebugCameraPitch = transform.eulerAngles.x;
        }

        void Update()
        {
            DDTSettings Settings = DrawDebugTools.Instance != null ? DrawDebugTools.Instance.m_DDTSettings : null;
            KeyCode MoveUpKey = Settings != null ? Settings.m_DebugCamMoveUpKey : KeyCode.E;
            KeyCode MoveDownKey = Settings != null ? Settings.m_DebugCamMoveDownKey : KeyCode.Q;

            // Change camera movement speed
            float SpeedMultiplierSensitivity = m_DebugCameraMovSpeedMultiplier < 1.0f ? 2.0f : 10.0f;
            m_DebugCameraMovSpeedMultiplier += DDTInput.GetScrollDelta() * SpeedMultiplierSensitivity * Time.unscaledDeltaTime;
            m_DebugCameraMovSpeedMultiplier = Mathf.Clamp(m_DebugCameraMovSpeedMultiplier, m_DebugCameraMovSpeedMultiplierRange.x, m_DebugCameraMovSpeedMultiplierRange.y);

            // Camera translation and rotation
            float MoveSpeed = 50.0f * m_DebugCameraMovSpeedMultiplier;
            float RotateSpeed = 100.0f;

            Vector2 MoveAxes = DDTInput.GetMoveAxes();
            Vector3 DirectionSpeed = Vector3.zero;
            DirectionSpeed.z = MoveAxes.y * MoveSpeed * Time.unscaledDeltaTime;
            DirectionSpeed.x = MoveAxes.x * MoveSpeed * Time.unscaledDeltaTime;
            if (DDTInput.GetKey(MoveUpKey))
                DirectionSpeed.y = 0.8f * MoveSpeed * Time.unscaledDeltaTime;
            if (DDTInput.GetKey(MoveDownKey))
                DirectionSpeed.y = -0.8f * MoveSpeed * Time.unscaledDeltaTime;

            // Set debug cam position
            transform.position += transform.right * DirectionSpeed.x + transform.forward * DirectionSpeed.z + Vector3.up * DirectionSpeed.y;

            // Set debug cam rotation
            if (DDTInput.GetLookButton())
            {
                Vector2 LookDelta = DDTInput.GetLookDelta();
                m_DebugCameraYaw += LookDelta.x * RotateSpeed * Time.unscaledDeltaTime;
                m_DebugCameraPitch += -LookDelta.y * RotateSpeed * Time.unscaledDeltaTime;
                transform.eulerAngles = new Vector3(m_DebugCameraPitch, m_DebugCameraYaw, 0.0f);
            }
        }

        public float GetDebugCameraMovSpeedMultiplier() { return m_DebugCameraMovSpeedMultiplier; }
    }
}
