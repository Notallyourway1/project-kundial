using UnityEngine;

namespace ProjectSundial
{
    // When the camera is following a vessel that the blast destroys, stock KSP drops the camera's
    // pivot and re-aims it, which looks like a teleport. Moving the pivot to the burst point first,
    // while keeping the camera's world position, leaves the view where it was.
    //
    // The pivot is a separate, empty anchor object rather than the effect itself. Releasing the
    // camera (ClearTarget) when the effect ended made the planet lose its atmosphere rendering, so
    // the anchor stays put until something else (a vessel switch, a scene change) takes the camera.
    internal sealed class SundialCameraHold : MonoBehaviour
    {
        private Transform follow;

        internal static void Pin(Transform burst)
        {
            var camera = FlightCamera.fetch;
            if (camera == null || burst == null)
                return;

            var anchor = new GameObject("SundialCameraAnchor");
            anchor.transform.SetPositionAndRotation(burst.position, burst.rotation);
            anchor.AddComponent<SundialCameraHold>().follow = burst;

            camera.SetTarget(anchor.transform, true, FlightCamera.TargetMode.Transform);
        }

        private void Update()
        {
            var camera = FlightCamera.fetch;
            if (camera == null || camera.Target != transform)
            {
                // Something else took the camera, so the anchor has done its job.
                Destroy(gameObject);
                return;
            }

            if (follow != null)
            {
                // Stay on the burst, which tracks the planet's surface across floating-origin shifts.
                transform.SetPositionAndRotation(follow.position, follow.rotation);
                return;
            }

            // The effect is over. Hand the camera back only to a vessel that is really alive. After the
            // blast the game can still report the destroyed vessel as active, and pointing the camera at
            // it made the planet lose its atmosphere rendering. Otherwise stay put: do not clear the target.
            Vessel active = FlightGlobals.ActiveVessel;
            bool alive = active != null && active.loaded && active.state != Vessel.State.DEAD
                && active.parts != null && active.parts.Count > 0;
            Debug.Log("[ProjectSundial] Camera hold ended. Active vessel: "
                + (active != null ? active.vesselName + " (state " + active.state + ", loaded " + active.loaded
                    + ", parts " + (active.parts != null ? active.parts.Count : -1) + ")" : "none")
                + (alive ? ", handing the camera back." : ", leaving the camera on the anchor."));
            if (alive)
                FlightCamera.SetTarget(active);
            enabled = false;
        }
    }
}
