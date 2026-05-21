using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ContractConfigurator;
using UnityEngine;

namespace Probalism.ContractBehaviours
{

    internal sealed class FacilityRequest
    {
        internal FacilityRequest()
        {
        }

        internal FacilityRequest(string facilityId, int minLevel)
        {
            FacilityId = facilityId;
            MinLevel = minLevel;
        }

        internal string FacilityId { get; set; }
        internal int MinLevel { get; set; }
    }

    public sealed class SetKSCFacilityMinLevelFactory : BehaviourFactory
    {
        private static readonly HashSet<string> SupportedFacilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Administration",
            "AstronautComplex",
            "LaunchPad",
            "MissionControl",
            "ResearchAndDevelopment",
            "Runway",
            "TrackingStation",
            "SpaceplaneHangar",
            "VehicleAssemblyBuilding",
            "Observatory",
        };

        private readonly List<FacilityRequest> requests = new List<FacilityRequest>();

        public override bool Load(ConfigNode configNode)
        {
            bool valid = base.Load(configNode);

            string onState = "CONTRACT_ACCEPTED";
            ConfigNodeUtil.ParseValue(configNode, "onState", x => onState = x, this, "CONTRACT_ACCEPTED");
            if (!string.Equals(onState, "CONTRACT_ACCEPTED", StringComparison.OrdinalIgnoreCase))
            {
                LoggingUtil.LogError(this, "SetKSCFacilityMinLevel only supports onState = CONTRACT_ACCEPTED.");
                valid = false;
            }

            foreach (ConfigNode facilityNode in configNode.GetNodes("FACILITY"))
            {
                string facilityId = string.Empty;
                int minLevel = 0;

                valid &= ConfigNodeUtil.ParseValue<string>(facilityNode, "facility", x => facilityId = x, this);
                valid &= ConfigNodeUtil.ParseValue<int>(facilityNode, "minLevel", x => minLevel = x, this);

                if (string.IsNullOrWhiteSpace(facilityId))
                {
                    LoggingUtil.LogError(this, "FACILITY node is missing a facility identifier.");
                    valid = false;
                    continue;
                }

                if (!SupportedFacilities.Contains(facilityId))
                {
                    LoggingUtil.LogError(this, "Unsupported facility '{0}'.", facilityId);
                    valid = false;
                    continue;
                }

                if (minLevel < 1)
                {
                    LoggingUtil.LogError(this, "Facility '{0}' must use minLevel >= 1.", facilityId);
                    valid = false;
                    continue;
                }

                requests.Add(new FacilityRequest(facilityId, minLevel));
            }

            if (requests.Count == 0)
            {
                LoggingUtil.LogError(this, "SetKSCFacilityMinLevel requires at least one FACILITY node.");
                valid = false;
            }

            return valid;
        }

        public override ContractBehaviour Generate(ConfiguredContract contract)
        {
            return new SetKSCFacilityMinLevel(requests.Select(r => new FacilityRequest(r.FacilityId, r.MinLevel)).ToList());
        }
    }

    public sealed class SetKSCFacilityMinLevel : ContractBehaviour
    {
        private readonly List<FacilityRequest> requests = new List<FacilityRequest>();

        public SetKSCFacilityMinLevel()
        {
        }

        internal SetKSCFacilityMinLevel(List<FacilityRequest> requests)
        {
            this.requests.AddRange(requests);
        }

        protected override void OnAccepted()
        {
            foreach (FacilityRequest request in requests)
            {
                try
                {
                    ApplyRequest(request);
                }
                catch (Exception ex)
                {
                    LoggingUtil.LogError(this, "Failed applying min-level upgrade for {0}: {1}", request.FacilityId, ex);
                }
            }
        }

        protected override void OnSave(ConfigNode configNode)
        {
            base.OnSave(configNode);

            foreach (FacilityRequest request in requests)
            {
                ConfigNode facilityNode = new ConfigNode("FACILITY");
                facilityNode.AddValue("facility", request.FacilityId);
                facilityNode.AddValue("minLevel", request.MinLevel);
                configNode.AddNode(facilityNode);
            }
        }

        protected override void OnLoad(ConfigNode configNode)
        {
            base.OnLoad(configNode);

            requests.Clear();
            foreach (ConfigNode facilityNode in configNode.GetNodes("FACILITY"))
            {
                string facilityId = facilityNode.GetValue("facility");
                if (string.IsNullOrWhiteSpace(facilityId))
                {
                    continue;
                }

                int minLevel;
                if (!int.TryParse(facilityNode.GetValue("minLevel"), out minLevel))
                {
                    continue;
                }

                requests.Add(new FacilityRequest(facilityId, minLevel));
            }
        }

        private void ApplyRequest(FacilityRequest request)
        {
            int maxLevel;
            int currentLevel;
            if (!FacilityInterop.TryGetCurrentLevel(request.FacilityId, out currentLevel, out maxLevel))
            {
                LoggingUtil.LogError(this, "Could not resolve facility '{0}'.", request.FacilityId);
                return;
            }

            int targetLevel = Math.Min(request.MinLevel, maxLevel);
            if (currentLevel >= targetLevel)
            {
                LoggingUtil.LogInfo(this, "Facility '{0}' already satisfies minLevel {1}.", request.FacilityId, targetLevel);
                return;
            }

            if (KctInterop.IsAvailable)
            {
                KctInterop.ResolveQueuedUpgradeImmediately(request.FacilityId, targetLevel, this);
            }

            if (!FacilityInterop.TrySetLevel(request.FacilityId, targetLevel, KctInterop.IsAvailable))
            {
                LoggingUtil.LogError(this, "Failed to raise facility '{0}' to level {1}.", request.FacilityId, targetLevel);
                return;
            }

            LoggingUtil.LogInfo(this, "Raised facility '{0}' to level {1}.", request.FacilityId, targetLevel);
        }
    }

    internal static class FacilityInterop
    {
        private static readonly FieldInfo ProtoLevelField =
            typeof(ScenarioUpgradeableFacilities.ProtoUpgradeable).GetField("level", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static bool TryGetCurrentLevel(string facilityId, out int currentLevel, out int maxLevel)
        {
            currentLevel = 0;
            maxLevel = 0;

            if (string.IsNullOrWhiteSpace(facilityId))
            {
                return false;
            }

            ScenarioUpgradeableFacilities.ProtoUpgradeable proto;
            if (!ScenarioUpgradeableFacilities.protoUpgradeables.TryGetValue(facilityId, out proto))
            {
                return false;
            }

            maxLevel = ScenarioUpgradeableFacilities.GetFacilityLevelCount(facilityId);
            if (maxLevel <= 0)
            {
                return false;
            }

            currentLevel = Mathf.RoundToInt(ScenarioUpgradeableFacilities.GetFacilityLevel(facilityId) * maxLevel);
            return true;
        }

        internal static bool TrySetLevel(string facilityId, int targetLevel, bool kctInstalled)
        {
            ScenarioUpgradeableFacilities.ProtoUpgradeable proto;
            if (!ScenarioUpgradeableFacilities.protoUpgradeables.TryGetValue(facilityId, out proto))
            {
                return false;
            }

            int maxLevel = ScenarioUpgradeableFacilities.GetFacilityLevelCount(facilityId);
            if (maxLevel <= 0)
            {
                return false;
            }

            targetLevel = Mathf.Clamp(targetLevel, 0, maxLevel);
            float normalizedLevel = maxLevel == 0 ? 0.0f : (float)targetLevel / maxLevel;

            if (ProtoLevelField != null)
            {
                ProtoLevelField.SetValue(proto, normalizedLevel);
            }

            Action apply = () =>
            {
                foreach (Upgradeables.UpgradeableFacility facilityRef in proto.facilityRefs)
                {
                    if (facilityRef != null)
                    {
                        facilityRef.SetLevel(targetLevel);
                    }
                }
            };

            if (kctInstalled)
            {
                KctInterop.WithAllowedToUpgrade(apply);
            }
            else
            {
                apply();
            }

            return true;
        }
    }

    internal static class KctInterop
    {
        private static readonly Type GameStatesType = Type.GetType("KerbalConstructionTime.KCT_GameStates, KerbalConstructionTime");
        private static readonly Type EventsType = Type.GetType("KerbalConstructionTime.KCT_Events, KerbalConstructionTime");
        private static readonly FieldInfo AllowedToUpgradeField = EventsType != null
            ? EventsType.GetField("allowedToUpgrade", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            : null;

        internal static bool IsAvailable
        {
            get
            {
                if (GameStatesType == null)
                {
                    return false;
                }

                PropertyInfo enabledProperty = GameStatesType.GetProperty("Enabled", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (enabledProperty == null)
                {
                    return true;
                }

                object value = enabledProperty.GetValue(null, null);
                return value is bool && (bool)value;
            }
        }

        internal static void WithAllowedToUpgrade(Action action)
        {
            if (action == null)
            {
                return;
            }

            bool hadField = AllowedToUpgradeField != null;
            bool oldValue = false;

            try
            {
                if (hadField)
                {
                    oldValue = (bool)AllowedToUpgradeField.GetValue(null);
                    AllowedToUpgradeField.SetValue(null, true);
                }

                action();
            }
            finally
            {
                if (hadField)
                {
                    AllowedToUpgradeField.SetValue(null, oldValue);
                }
            }
        }

        internal static void ResolveQueuedUpgradeImmediately(string facilityId, int minLevel, object logger)
        {
            IList queue;
            object queuedUpgrade;
            if (!TryFindQueuedUpgrade(facilityId, out queue, out queuedUpgrade))
            {
                return;
            }

            FieldInfo upgradeLevelField = queuedUpgrade.GetType().GetField("upgradeLevel", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (upgradeLevelField != null)
            {
                int queuedTarget = (int)upgradeLevelField.GetValue(queuedUpgrade);
                if (queuedTarget < minLevel)
                {
                    upgradeLevelField.SetValue(queuedUpgrade, minLevel);
                    LoggingUtil.LogInfo(logger, "Promoted KCT queued upgrade for '{0}' from level {1} to {2}.", facilityId, queuedTarget, minLevel);
                }
            }

            MethodInfo upgradeMethod = queuedUpgrade.GetType().GetMethod("Upgrade", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (upgradeMethod != null)
            {
                upgradeMethod.Invoke(queuedUpgrade, null);
            }

            queue.Remove(queuedUpgrade);
            LoggingUtil.LogInfo(logger, "Removed KCT queued upgrade for '{0}' after immediate emergency application.", facilityId);
        }

        private static bool TryFindQueuedUpgrade(string facilityId, out IList queue, out object queuedUpgrade)
        {
            queue = null;
            queuedUpgrade = null;

            if (!IsAvailable)
            {
                return false;
            }

            FieldInfo activeKscField = GameStatesType.GetField("ActiveKSC", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            object activeKsc = activeKscField != null ? activeKscField.GetValue(null) : null;
            if (activeKsc == null)
            {
                return false;
            }

            FieldInfo kscTechField = activeKsc.GetType().GetField("KSCTech", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            queue = kscTechField != null ? kscTechField.GetValue(activeKsc) as IList : null;
            if (queue == null)
            {
                return false;
            }

            foreach (object item in queue)
            {
                if (item == null)
                {
                    continue;
                }

                FieldInfo idField = item.GetType().GetField("id", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                string id = idField != null ? idField.GetValue(item) as string : null;
                if (string.Equals(id, facilityId, StringComparison.OrdinalIgnoreCase))
                {
                    queuedUpgrade = item;
                    return true;
                }
            }

            return false;
        }
    }
}
