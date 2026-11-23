using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

[assembly: AssemblyVersion("0.6.2.0")]
[assembly: KSPAssembly("ProjectSundial", 0, 6)]

namespace ProjectSundial
{
    public sealed class ModuleSundialPayload : PartModule
    {
        [KSPField] public float countdownSeconds = 10f;
        [KSPField] public float blastRadius = 2200f;
        [KSPField] public float visualRadius = 12000f;
        [KSPField] public float fireballRadius = 2800f;

        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Proposed yield")]
        public string proposedYield = "10,000 Mt TNT (10 Gt)";

        [KSPField(isPersistant = true)] public bool armed;
        [KSPField(isPersistant = true)] public double detonationTime = -1d;

        [KSPField(guiActive = true, guiName = "Sundial")]
        public string status = "Safe";

        private bool detonated;
        private int displayedSeconds = -1;

        private bool CountingDown => detonationTime >= 0d;
        private bool InFlight => HighLogic.LoadedSceneIsFlight && !detonated && vessel != null;

        public override string GetInfo()
        {
            return $"July 1954 proposal. Sundial was never built or tested.\n" +
                $"Proposed yield: {proposedYield}\n\n" +
                $"KSP countdown: {countdownSeconds:0} s\n" +
                $"KSP fireball radius in atmosphere: {fireballRadius / 1000f:0.#} km\n" +
                $"KSP damage radius: {blastRadius:0} m";
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if (HighLogic.LoadedSceneIsFlight)
                RefreshControls();
        }

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

        private void Detonate()
        {
            if (detonated || vessel == null)
                return;

            detonated = true;
            CelestialBody body = vessel.mainBody;
            Vector3 center = part.transform.position;
            float radius = Mathf.Max(1f, blastRadius);
            float radiusSquared = radius * radius;

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
                    if (target == null || target == part ||
                        (target.transform.position - center).sqrMagnitude > radiusSquared)
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

            if (watchedVesselHit)
                SundialCameraZoom.Preserve();

            part.explode();
            foreach (Part target in targets)
            {
                if (target != null && target.State != PartStates.DEAD)
                    target.explode();
            }

            ScreenMessages.PostScreenMessage("Sundial detonated", 4f, ScreenMessageStyle.UPPER_CENTER);
            SundialDetonationEffect.Spawn(center, body, visualRadius, fireballRadius);

            foreach (ModuleSundialPayload other in chained)
            {
                if (other != null && other.part != null && other.part.State != PartStates.DEAD)
                    other.Detonate();
            }
        }
    }
}
