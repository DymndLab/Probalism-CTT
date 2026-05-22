using System;
using System.Linq;
using Contracts;
using Contracts.Parameters;
using ContractConfigurator;
using ContractConfigurator.Parameters;
using UnityEngine;

namespace Probalism.ContractBehaviours
{
    public class BeaconTransmissionFactory : ParameterFactory
    {
        private double minDataSize;
        private double minTriggerAltitude;
        private double maxTriggerAltitude;
        private double minBandwidth;
        private string requiredPackageId;
        private string requiredModemId;
        private bool requireComplete;
        private string experimentId;

        public override bool Load(ConfigNode configNode)
        {
            bool valid = base.Load(configNode);

            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "minDataSize", x => minDataSize = x, this, 0.0, x => Validation.GE(x, 0.0));
            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "minTriggerAltitude", x => minTriggerAltitude = x, this, 0.0, x => Validation.GE(x, 0.0));
            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "maxTriggerAltitude", x => maxTriggerAltitude = x, this, double.MaxValue, x => Validation.GE(x, 0.0));
            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "minBandwidth", x => minBandwidth = x, this, 0.0, x => Validation.GE(x, 0.0));
            valid &= ConfigNodeUtil.ParseValue<string>(configNode, "requiredPackageId", x => requiredPackageId = x, this, string.Empty);
            valid &= ConfigNodeUtil.ParseValue<string>(configNode, "requiredModemId", x => requiredModemId = x, this, string.Empty);
            valid &= ConfigNodeUtil.ParseValue<bool>(configNode, "requireComplete", x => requireComplete = x, this, true);
            valid &= ConfigNodeUtil.ParseValue<string>(configNode, "experimentId", x => experimentId = x, this, "pbm_beacon");

            if (maxTriggerAltitude < minTriggerAltitude)
            {
                LoggingUtil.LogError(this, "maxTriggerAltitude must be greater than or equal to minTriggerAltitude.");
                valid = false;
            }

            return valid;
        }

        public override ContractParameter Generate(Contract contract)
        {
            return new BeaconTransmission(minDataSize, minTriggerAltitude, maxTriggerAltitude, minBandwidth, requiredPackageId, requiredModemId, requireComplete, experimentId, title);
        }
    }

    public class BeaconTransmission : VesselParameter
    {
        private const float UpdateFrequency = 0.25f;
        private double minDataSize;
        private double minTriggerAltitude;
        private double maxTriggerAltitude;
        private double minBandwidth;
        private string requiredPackageId;
        private string requiredModemId;
        private bool requireComplete;
        private string experimentId;
        private float lastUpdate;

        public BeaconTransmission()
            : base(null)
        {
        }

        public BeaconTransmission(double minDataSize, double minTriggerAltitude, double maxTriggerAltitude, double minBandwidth, string requiredPackageId, string requiredModemId, bool requireComplete, string experimentId, string title = null)
            : base(title)
        {
            this.minDataSize = minDataSize;
            this.minTriggerAltitude = minTriggerAltitude;
            this.maxTriggerAltitude = maxTriggerAltitude;
            this.minBandwidth = minBandwidth;
            this.requiredPackageId = requiredPackageId;
            this.requiredModemId = requiredModemId;
            this.requireComplete = requireComplete;
            this.experimentId = experimentId;

            CreateChildParameters();
        }

        protected override string GetParameterTitle()
        {
            if (!string.IsNullOrEmpty(title))
            {
                return title;
            }

            return requireComplete ? "Downlink the required beacon package" : "Trigger the beacon transmission";
        }

        private void CreateChildParameters()
        {
            if (minTriggerAltitude > 0.0 && maxTriggerAltitude < double.MaxValue)
            {
                AddParameter(new BeaconTriggerAltitudeWindowParameter(minTriggerAltitude, maxTriggerAltitude, experimentId));
            }
            else if (minTriggerAltitude > 0.0)
            {
                AddParameter(new BeaconMinTriggerAltitudeParameter(minTriggerAltitude, experimentId));
            }
            else if (maxTriggerAltitude < double.MaxValue)
            {
                AddParameter(new BeaconMaxTriggerAltitudeParameter(maxTriggerAltitude, experimentId));
            }

            if (!string.IsNullOrEmpty(requiredPackageId))
            {
                AddParameter(new BeaconRequiredPackageParameter(requiredPackageId, experimentId));
            }
            else
            {
                if (!string.IsNullOrEmpty(BeaconTransmissionUtil.GetMinimumPackageLabel(minDataSize)))
                {
                    AddParameter(new BeaconMinimumPackageParameter(minDataSize, experimentId));
                }
            }

            if (!string.IsNullOrEmpty(requiredModemId))
            {
                AddParameter(new BeaconRequiredModemParameter(requiredModemId, experimentId));
            }
            else if (minBandwidth > 0.0)
            {
                AddParameter(new BeaconMinimumBandwidthParameter(minBandwidth, experimentId));
            }
        }

        protected override void OnParameterSave(ConfigNode node)
        {
            base.OnParameterSave(node);
            node.AddValue("minDataSize", minDataSize);
            node.AddValue("minTriggerAltitude", minTriggerAltitude);
            if (maxTriggerAltitude != double.MaxValue)
            {
                node.AddValue("maxTriggerAltitude", maxTriggerAltitude);
            }
            node.AddValue("minBandwidth", minBandwidth);
            node.AddValue("requiredPackageId", requiredPackageId);
            node.AddValue("requiredModemId", requiredModemId);
            node.AddValue("requireComplete", requireComplete);
            node.AddValue("experimentId", experimentId);
        }

        protected override void OnParameterLoad(ConfigNode node)
        {
            base.OnParameterLoad(node);
            minDataSize = ConfigNodeUtil.ParseValue<double>(node, "minDataSize", 0.0);
            minTriggerAltitude = ConfigNodeUtil.ParseValue<double>(node, "minTriggerAltitude", 0.0);
            maxTriggerAltitude = ConfigNodeUtil.ParseValue<double>(node, "maxTriggerAltitude", double.MaxValue);
            minBandwidth = ConfigNodeUtil.ParseValue<double>(node, "minBandwidth", 0.0);
            requiredPackageId = ConfigNodeUtil.ParseValue<string>(node, "requiredPackageId", string.Empty);
            requiredModemId = ConfigNodeUtil.ParseValue<string>(node, "requiredModemId", string.Empty);
            requireComplete = ConfigNodeUtil.ParseValue<bool>(node, "requireComplete", true);
            experimentId = ConfigNodeUtil.ParseValue<string>(node, "experimentId", "pbm_beacon");
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();

            if (UnityEngine.Time.fixedTime - lastUpdate <= UpdateFrequency)
            {
                return;
            }

            lastUpdate = UnityEngine.Time.fixedTime;
            CheckVessel(FlightGlobals.ActiveVessel);
        }

        protected override bool VesselMeetsCondition(Vessel vessel)
        {
            if (vessel == null)
            {
                return false;
            }

            return vessel.parts
                .SelectMany(part => part.FindModulesImplementing<ProbalismBeaconController>())
                .Any(BeaconMeetsCondition);
        }

        private bool BeaconMeetsCondition(ProbalismBeaconController beacon)
        {
            if (!BeaconTransmissionUtil.BeaconHasTransmissionData(beacon, experimentId))
            {
                return false;
            }

            if (requireComplete && !beacon.testComplete)
            {
                return false;
            }

            if (beacon.transmittedDataSize < minDataSize)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(requiredPackageId) && !string.Equals(beacon.transmittedPackageId, requiredPackageId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (beacon.transmittedPacketSize < BeaconTransmissionUtil.ConvertKilobytesPerSecondToMegabits(minBandwidth))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(requiredModemId) && !string.Equals(beacon.transmittedLinkProfileId, requiredModemId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (double.IsNaN(beacon.triggerAslAltitude))
            {
                return false;
            }

            return beacon.triggerAslAltitude >= minTriggerAltitude && beacon.triggerAslAltitude <= maxTriggerAltitude;
        }

        private bool ExperimentMatches(ProbalismBeaconController beacon)
        {
            return string.IsNullOrEmpty(experimentId) || string.Equals(beacon.activeExperimentId, experimentId, StringComparison.Ordinal);
        }
    }

    internal static class BeaconTransmissionUtil
    {
        public static bool AnyBeaconMatches(Vessel vessel, string experimentId, Func<ProbalismBeaconController, bool> predicate)
        {
            if (vessel == null)
            {
                return false;
            }

            return vessel.parts
                .SelectMany(part => part.FindModulesImplementing<ProbalismBeaconController>())
                .Where(beacon => BeaconHasTransmissionData(beacon, experimentId))
                .Any(predicate);
        }

        public static bool BeaconHasTransmissionData(ProbalismBeaconController beacon, string experimentId)
        {
            return beacon != null &&
                beacon.testTriggered &&
                beacon.sawBeaconData &&
                ExperimentMatches(beacon, experimentId) &&
                !double.IsNaN(beacon.triggerAslAltitude);
        }

        public static bool ExperimentMatches(ProbalismBeaconController beacon, string experimentId)
        {
            return string.IsNullOrEmpty(experimentId) || string.Equals(beacon.activeExperimentId, experimentId, StringComparison.Ordinal);
        }

        public static string FormatPackageLabel(string packageId)
        {
            switch ((packageId ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "ping":
                    return "Ping";
                case "packet":
                    return "Packet";
                case "burst":
                    return "Burst";
                case "stream":
                    return "Stream";
                default:
                    return packageId;
            }
        }

        public static string GetMinimumPackageLabel(double dataSize)
        {
            if (dataSize <= 0.0)
            {
                return null;
            }
            if (dataSize <= 0.03)
            {
                return "Ping";
            }
            if (dataSize <= 0.07)
            {
                return "Packet";
            }
            if (dataSize <= 0.12)
            {
                return "Burst";
            }
            if (dataSize <= 0.20)
            {
                return "Stream";
            }

            return null;
        }

        public static string FormatModemLabel(string modemId)
        {
            switch ((modemId ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "pbm-075":
                    return "PBM-075";
                case "pbm-144":
                    return "PBM-144";
                case "pbm-288":
                    return "PBM-288";
                case "pbm-336":
                    return "PBM-336";
                case "pbm-560":
                    return "PBM-560";
                default:
                    return modemId;
            }
        }

        public static double ConvertKilobytesPerSecondToMegabits(double bandwidth)
        {
            return bandwidth / 1000.0;
        }
    }

    public abstract class BeaconRequirementParameter : VesselParameter
    {
        protected double minDataSize;
        protected double minTriggerAltitude;
        protected double maxTriggerAltitude = double.MaxValue;
        protected double minBandwidth;
        protected string requiredPackageId = string.Empty;
        protected string requiredModemId = string.Empty;
        protected string experimentId = "pbm_beacon";

        protected BeaconRequirementParameter()
            : base(null)
        {
        }

        protected BeaconRequirementParameter(string title)
            : base(title)
        {
        }

        protected override void OnParameterSave(ConfigNode node)
        {
            base.OnParameterSave(node);
            node.AddValue("minDataSize", minDataSize);
            node.AddValue("minTriggerAltitude", minTriggerAltitude);
            if (maxTriggerAltitude != double.MaxValue)
            {
                node.AddValue("maxTriggerAltitude", maxTriggerAltitude);
            }
            node.AddValue("minBandwidth", minBandwidth);
            node.AddValue("requiredPackageId", requiredPackageId);
            node.AddValue("requiredModemId", requiredModemId);
            node.AddValue("experimentId", experimentId);
        }

        protected override void OnParameterLoad(ConfigNode node)
        {
            base.OnParameterLoad(node);
            minDataSize = ConfigNodeUtil.ParseValue<double>(node, "minDataSize", 0.0);
            minTriggerAltitude = ConfigNodeUtil.ParseValue<double>(node, "minTriggerAltitude", 0.0);
            maxTriggerAltitude = ConfigNodeUtil.ParseValue<double>(node, "maxTriggerAltitude", double.MaxValue);
            minBandwidth = ConfigNodeUtil.ParseValue<double>(node, "minBandwidth", 0.0);
            requiredPackageId = ConfigNodeUtil.ParseValue<string>(node, "requiredPackageId", string.Empty);
            requiredModemId = ConfigNodeUtil.ParseValue<string>(node, "requiredModemId", string.Empty);
            experimentId = ConfigNodeUtil.ParseValue<string>(node, "experimentId", "pbm_beacon");
        }

        protected bool AnyBeaconMatches(Vessel vessel, Func<ProbalismBeaconController, bool> predicate)
        {
            return BeaconTransmissionUtil.AnyBeaconMatches(vessel, experimentId, predicate);
        }
    }

    public class BeaconMinTriggerAltitudeParameter : BeaconRequirementParameter
    {
        public BeaconMinTriggerAltitudeParameter()
        {
        }

        public BeaconMinTriggerAltitudeParameter(double minTriggerAltitude, string experimentId)
        {
            this.minTriggerAltitude = minTriggerAltitude;
            this.experimentId = experimentId;
        }

        protected override string GetParameterTitle()
        {
            return string.Format("Min Trigger Altitude: {0} m", minTriggerAltitude.ToString("N0"));
        }

        protected override bool VesselMeetsCondition(Vessel vessel)
        {
            return AnyBeaconMatches(vessel, beacon => beacon.triggerAslAltitude >= minTriggerAltitude);
        }
    }

    public class BeaconMaxTriggerAltitudeParameter : BeaconRequirementParameter
    {
        public BeaconMaxTriggerAltitudeParameter()
        {
        }

        public BeaconMaxTriggerAltitudeParameter(double maxTriggerAltitude, string experimentId)
        {
            this.maxTriggerAltitude = maxTriggerAltitude;
            this.experimentId = experimentId;
        }

        protected override string GetParameterTitle()
        {
            return string.Format("Max Trigger Altitude: {0} m", maxTriggerAltitude.ToString("N0"));
        }

        protected override bool VesselMeetsCondition(Vessel vessel)
        {
            return AnyBeaconMatches(vessel, beacon => beacon.triggerAslAltitude <= maxTriggerAltitude);
        }
    }

    public class BeaconTriggerAltitudeWindowParameter : BeaconRequirementParameter
    {
        public BeaconTriggerAltitudeWindowParameter()
        {
        }

        public BeaconTriggerAltitudeWindowParameter(double minTriggerAltitude, double maxTriggerAltitude, string experimentId)
        {
            this.minTriggerAltitude = minTriggerAltitude;
            this.maxTriggerAltitude = maxTriggerAltitude;
            this.experimentId = experimentId;
        }

        protected override string GetParameterTitle()
        {
            return string.Format("Trigger Altitude Window: {0} m to {1} m",
                minTriggerAltitude.ToString("N0"), maxTriggerAltitude.ToString("N0"));
        }

        protected override bool VesselMeetsCondition(Vessel vessel)
        {
            return AnyBeaconMatches(vessel, beacon => beacon.triggerAslAltitude >= minTriggerAltitude && beacon.triggerAslAltitude <= maxTriggerAltitude);
        }
    }

    public class BeaconRequiredPackageParameter : BeaconRequirementParameter
    {
        public BeaconRequiredPackageParameter()
        {
        }

        public BeaconRequiredPackageParameter(string requiredPackageId, string experimentId)
        {
            this.requiredPackageId = requiredPackageId;
            this.experimentId = experimentId;
        }

        protected override string GetParameterTitle()
        {
            return string.Format("Required Package: {0}", BeaconTransmissionUtil.FormatPackageLabel(requiredPackageId));
        }

        protected override bool VesselMeetsCondition(Vessel vessel)
        {
            return AnyBeaconMatches(vessel, beacon => string.Equals(beacon.transmittedPackageId, requiredPackageId, StringComparison.OrdinalIgnoreCase));
        }
    }

    public class BeaconMinimumPackageParameter : BeaconRequirementParameter
    {
        public BeaconMinimumPackageParameter()
        {
        }

        public BeaconMinimumPackageParameter(double minDataSize, string experimentId)
        {
            this.minDataSize = minDataSize;
            this.experimentId = experimentId;
        }

        protected override string GetParameterTitle()
        {
            return string.Format("Minimum Package: {0}", BeaconTransmissionUtil.GetMinimumPackageLabel(minDataSize));
        }

        protected override bool VesselMeetsCondition(Vessel vessel)
        {
            return AnyBeaconMatches(vessel, beacon => beacon.transmittedDataSize >= minDataSize);
        }
    }

    public class BeaconRequiredModemParameter : BeaconRequirementParameter
    {
        public BeaconRequiredModemParameter()
        {
        }

        public BeaconRequiredModemParameter(string requiredModemId, string experimentId)
        {
            this.requiredModemId = requiredModemId;
            this.experimentId = experimentId;
        }

        protected override string GetParameterTitle()
        {
            return string.Format("Required Modem: {0}", BeaconTransmissionUtil.FormatModemLabel(requiredModemId));
        }

        protected override bool VesselMeetsCondition(Vessel vessel)
        {
            return AnyBeaconMatches(vessel, beacon => string.Equals(beacon.transmittedLinkProfileId, requiredModemId, StringComparison.OrdinalIgnoreCase));
        }
    }

    public class BeaconMinimumBandwidthParameter : BeaconRequirementParameter
    {
        public BeaconMinimumBandwidthParameter()
        {
        }

        public BeaconMinimumBandwidthParameter(double minBandwidth, string experimentId)
        {
            this.minBandwidth = minBandwidth;
            this.experimentId = experimentId;
        }

        protected override string GetParameterTitle()
        {
            return string.Format("Minimum Modem Rate: {0} kB/s", minBandwidth.ToString("0.##"));
        }

        protected override bool VesselMeetsCondition(Vessel vessel)
        {
            return AnyBeaconMatches(vessel, beacon => beacon.transmittedPacketSize >= BeaconTransmissionUtil.ConvertKilobytesPerSecondToMegabits(minBandwidth));
        }
    }
}
