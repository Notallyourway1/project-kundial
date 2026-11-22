using UnityEngine;

namespace ProjectSundial
{
    public sealed class SundialCameraZoom : MonoBehaviour
    {
        private const float RetargetWindow = 1.25f;

        private FlightCamera flightCamera;
        private float originalMin;
        private float originalMax;
        private float appliedMin;
        private float appliedMax;
        private float until;

        internal static Vessel WatchedVessel()
        {
            var manager = CameraManager.Instance;
            var camera = FlightCamera.fetch;
            if (manager == null || manager.currentCameraMode != CameraManager.CameraMode.Flight || camera == null)
                return null;

            if (camera.targetMode == FlightCamera.TargetMode.Part && camera.partTarget != null)
                return camera.partTarget.vessel;
            if (camera.targetMode == FlightCamera.TargetMode.Vessel && camera.vesselTarget != null)
                return camera.vesselTarget;
            return camera.targetMode == FlightCamera.TargetMode.Transform ? null : FlightGlobals.ActiveVessel;
        }

        internal static void Preserve()
        {
            var camera = FlightCamera.fetch;
            if (camera == null)
                return;

            float distance = camera.Distance;
            var guard = camera.GetComponent<SundialCameraZoom>();
            if (guard == null)
            {
                if (distance >= camera.minDistOnDestroy && distance <= camera.maxDistOnDestroy)
                    return;

                guard = camera.gameObject.AddComponent<SundialCameraZoom>();
            }
            guard.Extend(camera, distance);
        }

        private void Extend(FlightCamera camera, float distance)
        {
            if (flightCamera == null)
            {
                flightCamera = camera;
                originalMin = camera.minDistOnDestroy;
                originalMax = camera.maxDistOnDestroy;
            }
            else
            {
                if (camera.minDistOnDestroy != appliedMin)
                    originalMin = camera.minDistOnDestroy;
                if (camera.maxDistOnDestroy != appliedMax)
                    originalMax = camera.maxDistOnDestroy;
            }

            // Stock SetTarget clamps the current zoom to these bounds when a target dies.
            appliedMin = Mathf.Min(camera.minDistOnDestroy, distance);
            appliedMax = Mathf.Max(camera.maxDistOnDestroy, distance);
            camera.minDistOnDestroy = appliedMin;
            camera.maxDistOnDestroy = appliedMax;
            until = Time.unscaledTime + RetargetWindow;
        }

        private void Update()
        {
            if (Time.unscaledTime >= until)
                Destroy(this);
        }

        private void OnDestroy()
        {
            if (flightCamera == null)
                return;
            if (flightCamera.minDistOnDestroy == appliedMin)
                flightCamera.minDistOnDestroy = originalMin;
            if (flightCamera.maxDistOnDestroy == appliedMax)
                flightCamera.maxDistOnDestroy = originalMax;
        }
    }
}
