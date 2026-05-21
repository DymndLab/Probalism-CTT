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
        }

        protected override string GetParameterTitle()
        {
            if (!string.IsNullOrEmpty(title))
            {
                return title;
            }

            return requireComplete ? "Transmit PROBA beacon data" : "Trigger PROBA beacon transmission";
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
            if (beacon == null || !beacon.testTriggered || !beacon.sawBeaconData)
            {
                return false;
            }

            if (requireComplete && !beacon.testComplete)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(experimentId) && !string.Equals(beacon.activeExperimentId, experimentId, StringComparison.Ordinal))
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

            if (beacon.transmittedPacketSize < ConvertKilobytesPerSecondToMegabits(minBandwidth))
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

        private static double ConvertKilobytesPerSecondToMegabits(double bandwidth)
        {
            return bandwidth / 1000.0;
        }
    }
}
