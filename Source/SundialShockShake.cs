using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectSundial
{
    internal sealed class SundialShockShake : MonoBehaviour
    {
        private const float DefaultDuration = 1.05f;
        private const float DefaultPeakAngle = 1.6f;
        private static SundialShockShake active;
        private readonly Dictionary<Camera, Quaternion> rotations = new Dictionary<Camera, Quaternion>();
        private float start;
        private float strength;
        private float duration = DefaultDuration;
        private float peakAngle = DefaultPeakAngle;

        internal static void Play(float strength, float duration = DefaultDuration, float peakAngle = DefaultPeakAngle)
        {
            if (strength <= 0f || active != null)
                return;

            var node = new GameObject("Sundial camera tremor");
            active = node.AddComponent<SundialShockShake>();
            active.start = Time.unscaledTime;
            active.strength = Mathf.Clamp01(strength);
            active.duration = Mathf.Max(0.1f, duration);
            active.peakAngle = peakAngle;
        }

        private void OnEnable()
        {
            Camera.onPreCull += BeforeRender;
            Camera.onPostRender += AfterRender;
        }

        private void Update()
        {
            if (Time.unscaledTime - start >= duration)
                Destroy(gameObject);
        }

        private void BeforeRender(Camera camera)
        {
            var manager = CameraManager.Instance;
            var flight = FlightCamera.fetch;
            if (manager == null || manager.currentCameraMode != CameraManager.CameraMode.Flight || flight == null)
                return;
            if (camera != flight.mainCamera && (flight.cameras == null || Array.IndexOf(flight.cameras, camera) < 0))
                return;
            if (rotations.ContainsKey(camera))
                return;

            float time = Time.unscaledTime - start;
            float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / duration));
            float amount = peakAngle * strength * fade * Mathf.Clamp01(time / 0.045f);
            if (amount <= 0f)
                return;

            float pitch = Mathf.Sin(time * 34f) * amount;
            float yaw = Mathf.Sin(time * 47f) * amount * 0.69f;
            float roll = Mathf.Sin(time * 27f) * amount * 0.36f;
            Transform cameraTransform = camera.transform;
            rotations.Add(camera, cameraTransform.localRotation);
            cameraTransform.localRotation *= Quaternion.Euler(pitch, yaw, roll);
        }

        private void AfterRender(Camera camera)
        {
            Quaternion rotation;
            if (!rotations.TryGetValue(camera, out rotation))
                return;
            if (camera != null)
                camera.transform.localRotation = rotation;
            rotations.Remove(camera);
        }

        private void OnDisable()
        {
            Camera.onPreCull -= BeforeRender;
            Camera.onPostRender -= AfterRender;
            foreach (var before in rotations)
            {
                if (before.Key != null)
                    before.Key.transform.localRotation = before.Value;
            }
            rotations.Clear();
            if (active == this)
                active = null;
        }
    }
}
