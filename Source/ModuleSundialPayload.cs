using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

[assembly: AssemblyVersion("0.8.2.0")]
[assembly: KSPAssembly("ProjectSundial", 0, 8)]

namespace ProjectSundial
{
    public sealed class ModuleSundialPayload : PartModule, IPartMassModifier, IPartCostModifier
    {
        [KSPField] public float countdownSeconds = 10f;
        [KSPField] public float blastRadius = 2200f;
        [KSPField] public float visualRadius = 12000f;
        [KSPField] public float fireballRadius = 2800f;

        // Above the atmosphere there is no blast wave to blunt the X-rays, so the damage reaches
        // much further, but only parts with a clear line of sight to the burst are destroyed.
        [KSPField] public float vacuumBlastRadius = 100000f;
        [KSPField] public bool vacuumShielding = true;

        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Proposed yield")]
        public string proposedYield = "10,000 Mt TNT (10 Gt)";

        [KSPField(isPersistant = true)] public bool armed;
        [KSPField(isPersistant = true)] public double detonationTime = -1d;

        [KSPField(guiActive = true, guiName = "Sundial")]
        public string status = "Safe";

        // Optional steel frame around the casing, switched on in the VAB.
        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "Structural cage"),
            UI_Toggle(disabledText = "Off", enabledText = "On", scene = UI_Scene.Editor)]
        public bool cage;

        [KSPField] public float cageMass = 3f;
        [KSPField] public float cageCost = 2500f;

        private static readonly RaycastHit[] ShieldHits = new RaycastHit[256];

        private bool detonated;
        private int displayedSeconds = -1;

        private bool CountingDown => detonationTime >= 0d;
        private bool InFlight => HighLogic.LoadedSceneIsFlight && !detonated && vessel != null;

        public override string GetInfo()
        {
            string cageInfo = $"\nOptional structural cage (VAB): +{cageMass:0.#} t, +{cageCost:0} funds";
            return $"July 1954 proposal. Sundial was never built or tested.\n" +
                $"Proposed yield: {proposedYield}\n\n" +
                $"KSP countdown: {countdownSeconds:0} s\n" +
                $"KSP fireball radius in atmosphere: {fireballRadius / 1000f:0.#} km\n" +
                $"KSP damage radius in atmosphere: {blastRadius:0} m\n" +
                $"KSP damage radius in vacuum: {vacuumBlastRadius:0} m" +
                (vacuumShielding ? " (parts behind other parts are shielded)" : "") + cageInfo;
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if (HighLogic.LoadedSceneIsFlight)
                RefreshControls();
            if (HighLogic.LoadedSceneIsFlight || HighLogic.LoadedSceneIsEditor)
                SundialCage.Attach(this);
        }

        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit) => cage ? cageMass : 0f;
        public ModifierChangeWhen GetModuleMassChangeWhen() => ModifierChangeWhen.FIXED;
        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit) => cage ? cageCost : 0f;
        public ModifierChangeWhen GetModuleCostChangeWhen() => ModifierChangeWhen.FIXED;

        public override void OnUpdate()
        {
            if (!InFlight || !armed || !CountingDown)
                return;

            double remaining = detonationTime - Planetarium.GetUniversalTime();
            if (vessel.packed)
            {
                // Detonation waits for physics, but keep the readout current.
                if (remaining > 0d)
                    ShowTime(remaining);
                else
                {
                    displayedSeconds = -1;
                    status = "Detonation pending";
                }
                return;
            }

            if (remaining <= 0d)
                Detonate();
            else
                ShowTime(remaining);
        }

        [KSPEvent(guiActive = true, guiName = "Arm Sundial")]
        public void Arm()
        {
            if (!InFlight || vessel.packed || armed)
                return;

            armed = true;
            RefreshControls();
        }

        [KSPEvent(guiActive = true, guiName = "Start Detonation Countdown")]
        public void StartCountdown()
        {
            if (!InFlight || vessel.packed || CountingDown)
                return;

            if (!armed)
            {
                ScreenMessages.PostScreenMessage(
                    "Arm the Sundial before starting the countdown.",
                    3f, ScreenMessageStyle.UPPER_CENTER);
                return;
            }

            detonationTime = Planetarium.GetUniversalTime() + countdownSeconds;
            RefreshControls();
            ScreenMessages.PostScreenMessage(
                $"Sundial countdown started ({countdownSeconds:0} s).",
                3f, ScreenMessageStyle.UPPER_CENTER);
        }

        [KSPEvent(guiActive = true, guiName = "Disarm / Cancel Countdown")]
        public void Disarm()
        {
            if (!InFlight || !armed)
                return;

            armed = false;
            detonationTime = -1d;
            RefreshControls();
        }

        [KSPAction("Arm Sundial")]
        public void ArmAction(KSPActionParam parameter) => Arm();

        [KSPAction("Start Sundial Countdown")]
        public void CountdownAction(KSPActionParam parameter) => StartCountdown();

        [KSPAction("Disarm Sundial")]
        public void DisarmAction(KSPActionParam parameter) => Disarm();

        private void RefreshControls()
        {
            Events[nameof(Arm)].active = !armed;
            Events[nameof(StartCountdown)].active = armed && !CountingDown;
            Events[nameof(Disarm)].active = armed;
            if (armed && CountingDown)
                ShowTime(detonationTime - Planetarium.GetUniversalTime());
            else
            {
                displayedSeconds = -1;
                status = armed ? "Armed" : "Safe";
            }
        }

        private void ShowTime(double remaining)
        {
            int seconds = Mathf.CeilToInt(Mathf.Max(0f, (float)remaining));
            if (seconds == displayedSeconds)
                return;

            displayedSeconds = seconds;
            status = $"Detonation in {seconds} s";
        }

        // True when the planet itself sits between the burst and the target, as for a vessel on the
        // far side of the world. The X-rays travel in a straight line, so the planet shields it.
        private static bool BlockedByPlanet(CelestialBody body, Vector3d from, Vector3d to)
        {
            Vector3d along = to - from;
            double lengthSquared = along.sqrMagnitude;
            if (lengthSquared < 1d)
                return false;

            double t = System.Math.Max(0d, System.Math.Min(1d, Vector3d.Dot(body.position - from, along) / lengthSquared));
            Vector3d closest = from + along * t;
            return (closest - body.position).magnitude < body.Radius - 5d;
        }

        // True when another part sits between the burst and the target and would absorb the X-rays.
        private bool IsShielded(Vector3 center, Part target)
        {
            Vector3 toTarget = target.transform.position - center;
            float distance = toTarget.magnitude;
            if (distance < 0.5f)
                return false;

            int hitCount = Physics.RaycastNonAlloc(center, toTarget / distance, ShieldHits, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = ShieldHits[i];
                Part blocker = hit.collider != null ? hit.collider.GetComponentInParent<Part>() : null;
                if (blocker != null && blocker != part && blocker != target)
                    return true;
            }

            return false;
        }

        private void Detonate()
        {
            if (detonated || vessel == null)
                return;

            detonated = true;
            CelestialBody body = vessel.mainBody;
            Vector3 center = part.transform.position;
            bool vacuum = SundialDetonationEffect.IsVacuum(body, body.GetAltitude(center));

            // Everything inside the blast radius is destroyed. In vacuum the X-ray radius is larger.
            float radius = Mathf.Max(1f, blastRadius);
            float radiusSquared = radius * radius;
            float reach = vacuum ? Mathf.Max(radius, vacuumBlastRadius) : radius;
            float reachSquared = reach * reach;

            // Cache the targets before explosions split or remove vessels.
            Vessel watchedVessel = SundialCameraZoom.WatchedVessel();
            bool watchedVesselHit = watchedVessel != null && watchedVessel == vessel;
            var targets = new List<Part>();
            var chained = new List<ModuleSundialPayload>();
            foreach (Vessel candidate in FlightGlobals.VesselsLoaded)
            {
                if (candidate == null || candidate.packed || candidate.mainBody != body)
                    continue;

                foreach (Part target in candidate.parts)
                {
                    if (target == null || target == part)
                        continue;

                    float distanceSquared = (target.transform.position - center).sqrMagnitude;
                    if (distanceSquared > reachSquared)
                        continue;

                    if (distanceSquared > radiusSquared && vacuumShielding && IsShielded(center, target))
                        continue;

                    watchedVesselHit |= candidate == watchedVessel;

                    // Other payloads in range detonate with their own effect instead of
                    // being destroyed silently as collateral.
                    var other = target.FindModuleImplementing<ModuleSundialPayload>();
                    if (other != null && !other.detonated)
                        chained.Add(other);
                    else
                        targets.Add(target);
                }
            }

            // Vessels outside physics range are not simulated part by part, so out to the full vacuum
            // radius each one is destroyed whole, unless the planet is in the way. There is no
            // line-of-sight check for these because their colliders do not exist.
            var distantVessels = new List<Vessel>();
            if (vacuum)
            {
                foreach (Vessel candidate in FlightGlobals.Vessels)
                {
                    if (candidate == null || candidate == vessel || candidate.mainBody != body
                        || candidate.state == Vessel.State.DEAD || (candidate.loaded && !candidate.packed)
                        || candidate.vesselType == VesselType.Flag || candidate.vesselType == VesselType.SpaceObject)
                        continue;

                    Vector3d position = candidate.GetWorldPos3D();
                    if ((position - (Vector3d)center).sqrMagnitude > reachSquared)
                        continue;
                    if (BlockedByPlanet(body, (Vector3d)center, position))
                        continue;

                    distantVessels.Add(candidate);
                }
            }

            if (watchedVesselHit)
                SundialCameraZoom.Preserve();

            // Spawn the burst first so the camera can be pinned to it before its vessel is destroyed.
            SundialDetonationEffect effect = SundialDetonationEffect.Spawn(center, body, visualRadius, fireballRadius);
            if (effect != null && watchedVessel != null && watchedVessel == vessel)
                SundialCameraHold.Pin(effect.transform);

            part.explode();
            foreach (Part target in targets)
            {
                if (target != null && target.State != PartStates.DEAD)
                    target.explode();
            }

            int destroyedDistant = 0;
            foreach (Vessel far in distantVessels)
            {
                try
                {
                    if (far != null && far.state != Vessel.State.DEAD)
                    {
                        far.Die();
                        destroyedDistant++;
                    }
                }
                catch (System.Exception error)
                {
                    Debug.LogWarning("[ProjectSundial] Could not destroy distant vessel: " + error.Message);
                }
            }

            string message = vacuum ? "Sundial detonated in vacuum" : "Sundial detonated";
            if (destroyedDistant > 0)
                message += " (" + destroyedDistant + " distant vessel" + (destroyedDistant == 1 ? "" : "s") + " destroyed)";
            ScreenMessages.PostScreenMessage(message, 4f, ScreenMessageStyle.UPPER_CENTER);

            foreach (ModuleSundialPayload other in chained)
            {
                if (other != null && other.part != null && other.part.State != PartStates.DEAD)
                    other.Detonate();
            }
        }
    }
}
