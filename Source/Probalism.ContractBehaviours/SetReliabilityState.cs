using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Contracts;
using ContractConfigurator;
using ContractConfigurator.Behaviour;
using UnityEngine;

namespace Probalism.ContractBehaviours
{
    public class SetReliabilityStateFactory : BehaviourFactory
    {
        private TriggeredBehaviour.State onState;
        private readonly List<string> parameter = new List<string>();
        private string vessel;
        private string partName;
        private int? partIndex;
        private string reliabilityType;
        private bool? broken;
        private bool? critical;
        private bool? quality;
        private bool? needMaintenance;
        private bool? inspectNow;
        private double? lastInspection;
        private int? addIgnitions;
        private string popupTitle;
        private string popupBody;
        private string popupImageURL;

        public override bool Load(ConfigNode configNode)
        {
            bool valid = base.Load(configNode);

            if (configNode.HasValue("onState"))
            {
                try
                {
                    Enum.Parse(typeof(TriggeredBehaviour.LegacyState), configNode.GetValue("onState"));
                    LoggingUtil.LogWarning(this, "Warning, values for onState have changed - attempting to load using obsolete values.");
                    valid &= ConfigNodeUtil.ParseValue<TriggeredBehaviour.LegacyState>(configNode, "onState", x =>
                    {
                        switch (x)
                        {
                            case TriggeredBehaviour.LegacyState.ContractAccepted:
                                onState = TriggeredBehaviour.State.CONTRACT_ACCEPTED;
                                break;
                            case TriggeredBehaviour.LegacyState.ContractCompletedFailure:
                                onState = TriggeredBehaviour.State.CONTRACT_FAILED;
                                break;
                            case TriggeredBehaviour.LegacyState.ContractCompletedSuccess:
                                onState = TriggeredBehaviour.State.CONTRACT_SUCCESS;
                                break;
                            case TriggeredBehaviour.LegacyState.ParameterCompleted:
                                onState = TriggeredBehaviour.State.PARAMETER_COMPLETED;
                                break;
                        }
                    }, this);
                }
                catch
                {
                    valid &= ConfigNodeUtil.ParseValue<TriggeredBehaviour.State>(configNode, "onState", x => onState = x, this);
                }
            }
            else
            {
                valid &= ConfigNodeUtil.ParseValue<TriggeredBehaviour.State>(configNode, "onState", x => onState = x, this);
            }

            valid &= ConfigNodeUtil.ParseValue<string>(configNode, "vessel", x => vessel = x, this);
            valid &= ConfigNodeUtil.ParseValue<string>(configNode, "partName", x => partName = x, this, (string)null);
            valid &= ConfigNodeUtil.ParseValue<int?>(configNode, "partIndex", x => partIndex = x, this, (int?)null);
            valid &= ConfigNodeUtil.ParseValue<string>(configNode, "reliabilityType", x => reliabilityType = x, this, (string)null);

            valid &= ConfigNodeUtil.ParseValue<bool?>(configNode, "broken", x => broken = x, this, (bool?)null);
            valid &= ConfigNodeUtil.ParseValue<bool?>(configNode, "critical", x => critical = x, this, (bool?)null);
            valid &= ConfigNodeUtil.ParseValue<bool?>(configNode, "quality", x => quality = x, this, (bool?)null);
            valid &= ConfigNodeUtil.ParseValue<bool?>(configNode, "needMaintenance", x => needMaintenance = x, this, (bool?)null);
            valid &= ConfigNodeUtil.ParseValue<bool?>(configNode, "inspectNow", x => inspectNow = x, this, (bool?)null);
            valid &= ConfigNodeUtil.ParseValue<double?>(configNode, "lastInspection", x => lastInspection = x, this, (double?)null);
            valid &= ConfigNodeUtil.ParseValue<int?>(configNode, "addIgnitions", x => addIgnitions = x, this, (int?)null);
            valid &= ConfigNodeUtil.ParseValue<string>(configNode, "popupTitle", x => popupTitle = x, this, (string)null);
            valid &= ConfigNodeUtil.ParseValue<string>(configNode, "popupBody", x => popupBody = x, this, (string)null);
            valid &= ConfigNodeUtil.ParseValue<string>(configNode, "popupImageURL", x => popupImageURL = x, this, (string)null);

            if (onState == TriggeredBehaviour.State.PARAMETER_COMPLETED || onState == TriggeredBehaviour.State.PARAMETER_FAILED)
            {
                valid &= ConfigNodeUtil.ParseValue<List<string>>(configNode, "parameter", x =>
                {
                    parameter.Clear();
                    parameter.AddRange(x);
                }, this, new List<string>());
            }

            if (string.IsNullOrEmpty(partName))
            {
                LoggingUtil.LogError(this, "{0}: requires 'partName'.", ErrorPrefix());
                valid = false;
            }

            if (partIndex.HasValue && partIndex.Value < 0)
            {
                LoggingUtil.LogError(this, "{0}: 'partIndex' cannot be negative.", ErrorPrefix());
                valid = false;
            }

            if (configNode.HasValue("inspectNow") && configNode.HasValue("lastInspection"))
            {
                LoggingUtil.LogError(this, "{0}: use either 'inspectNow' or 'lastInspection', not both.", ErrorPrefix());
                valid = false;
            }

            bool hasEdit = configNode.HasValue("broken") ||
                           configNode.HasValue("critical") ||
                           configNode.HasValue("quality") ||
                           configNode.HasValue("needMaintenance") ||
                           configNode.HasValue("inspectNow") ||
                           configNode.HasValue("lastInspection") ||
                           configNode.HasValue("addIgnitions");
            if (!hasEdit)
            {
                LoggingUtil.LogError(this, "{0}: requires at least one reliability state edit field.", ErrorPrefix());
                valid = false;
            }

            if ((onState == TriggeredBehaviour.State.PARAMETER_COMPLETED || onState == TriggeredBehaviour.State.PARAMETER_FAILED) && parameter.Count == 0)
            {
                LoggingUtil.LogError(this, "{0}: parameter-based triggers require at least one 'parameter' entry.", ErrorPrefix());
                valid = false;
            }

            return valid;
        }

        public override ContractBehaviour Generate(ConfiguredContract contract)
        {
            return new SetReliabilityState(onState, parameter, vessel, partName, partIndex, reliabilityType,
                broken, critical, quality, needMaintenance, inspectNow, lastInspection, addIgnitions,
                popupTitle, popupBody, popupImageURL);
        }
    }

    internal static class BehaviourPopup
    {
        private static readonly Color DefaultTitleColor = Color.white;
        private static readonly Color DefaultBodyColor = new Color(0.8f, 0.8f, 0.8f);
        private static readonly Color DefaultImageNameColor = new Color(0.729f, 0.855f, 0.333f);

        public static void Show(string title, string body, string imageURL, object source)
        {
            if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(body))
            {
                return;
            }

            try
            {
                DialogBox.DialogDetail detail = new DialogBox.DialogDetail
                {
                    condition = DialogBox.TriggerCondition.CONTRACT_ACCEPTED,
                    position = DialogBox.Position.CENTER,
                    width = 0.60f,
                    height = 0.0f,
                    title = title ?? string.Empty,
                    titleColor = DefaultTitleColor
                };

                if (!string.IsNullOrEmpty(imageURL))
                {
                    if (GameDatabase.Instance.ExistsTexture(imageURL) || File.Exists(Path.Combine(KSPUtil.ApplicationRootPath, "GameData", imageURL)))
                    {
                        detail.sections.Add(new DialogBox.ImageSection
                        {
                            imageURL = imageURL,
                            showName = false,
                            characterName = string.Empty,
                            textColor = DefaultImageNameColor
                        });
                    }
                    else
                    {
                        LoggingUtil.LogWarning(source, "SetReliabilityState popup image '{0}' could not be found. Showing popup without image.", imageURL);
                    }
                }

                if (!string.IsNullOrEmpty(body))
                {
                    detail.sections.Add(new DialogBox.TextSection
                    {
                        text = body.Replace("\\n", "\n"),
                        textColor = DefaultBodyColor,
                        fontSize = 20
                    });
                }

                DialogBox dialog = new DialogBox();
                dialog.displayQueue.Enqueue(detail);
                DialogBox.DialogBoxGUI.DisplayMessage(dialog);
            }
            catch (Exception ex)
            {
                LoggingUtil.LogWarning(source, "SetReliabilityState popup could not be shown: {0}", ex.Message);
            }
        }
    }

    public class SetReliabilityState : TriggeredBehaviour
    {
        private struct ApplyResult
        {
            public int MatchingPartCount;
            public int MatchingReliabilityCount;
            public int UpdatedCount;
        }

        private static MethodInfo kerbalismDataMethod;
        private static MethodInfo resetReliabilityStatusMethod;
        private static bool kerbalismReflectionResolved;
        private static bool loggedKerbalismRefreshFailure;

        private string vesselKey;
        private string partName;
        private int? partIndex;
        private string reliabilityType;
        private bool? broken;
        private bool? critical;
        private bool? quality;
        private bool? needMaintenance;
        private bool? inspectNow;
        private double? lastInspection;
        private int? addIgnitions;
        private string popupTitle;
        private string popupBody;
        private string popupImageURL;

        public SetReliabilityState()
        {
        }

        public SetReliabilityState(State onState, List<string> parameter, string vesselKey, string partName,
            int? partIndex, string reliabilityType, bool? broken, bool? critical, bool? quality,
            bool? needMaintenance, bool? inspectNow, double? lastInspection, int? addIgnitions,
            string popupTitle, string popupBody, string popupImageURL)
            : base(onState, new List<string>(parameter ?? new List<string>()))
        {
            this.vesselKey = vesselKey;
            this.partName = partName;
            this.partIndex = partIndex;
            this.reliabilityType = reliabilityType;
            this.broken = broken;
            this.critical = critical;
            this.quality = quality;
            this.needMaintenance = needMaintenance;
            this.inspectNow = inspectNow;
            this.lastInspection = lastInspection;
            this.addIgnitions = addIgnitions;
            this.popupTitle = popupTitle;
            this.popupBody = popupBody;
            this.popupImageURL = popupImageURL;
        }

        protected override void TriggerAction()
        {
            Vessel targetVessel = ContractVesselTracker.Instance == null ? null : ContractVesselTracker.Instance.GetAssociatedVessel(vesselKey);
            if (targetVessel == null)
            {
                LoggingUtil.LogWarning(this, "Unable to find tracked vessel '{0}' for SetReliabilityState.", vesselKey);
                return;
            }

            ApplyResult result = targetVessel.loaded ? ApplyToLoadedVessel(targetVessel) : ApplyToUnloadedVessel(targetVessel);
            if (result.UpdatedCount == 0)
            {
                if (result.MatchingPartCount == 0)
                {
                    LoggingUtil.LogWarning(this,
                        "SetReliabilityState found no matching parts on vessel '{0}' for partName '{1}'.",
                        targetVessel.vesselName, partName);
                }
                else if (result.MatchingReliabilityCount == 0)
                {
                    LoggingUtil.LogWarning(this,
                        "SetReliabilityState found {0} matching part(s) on vessel '{1}' for partName '{2}', but no matching Reliability modules{3}.",
                        result.MatchingPartCount, targetVessel.vesselName, partName, FormatReliabilityTypeSuffix());
                }
                else
                {
                    LoggingUtil.LogWarning(this,
                        "SetReliabilityState matched {0} Reliability module(s) on vessel '{1}' but applied no updates.",
                        result.MatchingReliabilityCount, targetVessel.vesselName);
                }
            }
            else
            {
                LoggingUtil.LogInfo(this,
                    "SetReliabilityState updated {0} Reliability module(s) on vessel '{1}' across {2} matching part(s).",
                    result.UpdatedCount, targetVessel.vesselName, result.MatchingPartCount);
                if (targetVessel.loaded)
                {
                    TryRefreshKerbalismReliabilityStatus(targetVessel);
                }
                BehaviourPopup.Show(popupTitle, popupBody, popupImageURL, this);
            }
        }

        protected override void OnLoad(ConfigNode configNode)
        {
            base.OnLoad(configNode);
            vesselKey = ConfigNodeUtil.ParseValue<string>(configNode, "vessel");
            partName = ConfigNodeUtil.ParseValue<string>(configNode, "partName", (string)null);
            partIndex = ConfigNodeUtil.ParseValue<int?>(configNode, "partIndex", (int?)null);
            reliabilityType = ConfigNodeUtil.ParseValue<string>(configNode, "reliabilityType", (string)null);
            broken = ConfigNodeUtil.ParseValue<bool?>(configNode, "broken", (bool?)null);
            critical = ConfigNodeUtil.ParseValue<bool?>(configNode, "critical", (bool?)null);
            quality = ConfigNodeUtil.ParseValue<bool?>(configNode, "quality", (bool?)null);
            needMaintenance = ConfigNodeUtil.ParseValue<bool?>(configNode, "needMaintenance", (bool?)null);
            inspectNow = ConfigNodeUtil.ParseValue<bool?>(configNode, "inspectNow", (bool?)null);
            lastInspection = ConfigNodeUtil.ParseValue<double?>(configNode, "lastInspection", (double?)null);
            addIgnitions = ConfigNodeUtil.ParseValue<int?>(configNode, "addIgnitions", (int?)null);
            popupTitle = ConfigNodeUtil.ParseValue<string>(configNode, "popupTitle", (string)null);
            popupBody = ConfigNodeUtil.ParseValue<string>(configNode, "popupBody", (string)null);
            popupImageURL = ConfigNodeUtil.ParseValue<string>(configNode, "popupImageURL", (string)null);
        }

        protected override void OnSave(ConfigNode configNode)
        {
            base.OnSave(configNode);
            configNode.AddValue("vessel", vesselKey);
            AddOptionalValue(configNode, "partName", partName);
            AddOptionalValue(configNode, "partIndex", partIndex);
            AddOptionalValue(configNode, "reliabilityType", reliabilityType);
            AddOptionalValue(configNode, "broken", broken);
            AddOptionalValue(configNode, "critical", critical);
            AddOptionalValue(configNode, "quality", quality);
            AddOptionalValue(configNode, "needMaintenance", needMaintenance);
            AddOptionalValue(configNode, "inspectNow", inspectNow);
            AddOptionalValue(configNode, "lastInspection", lastInspection);
            AddOptionalValue(configNode, "addIgnitions", addIgnitions);
            AddOptionalValue(configNode, "popupTitle", popupTitle);
            AddOptionalValue(configNode, "popupBody", popupBody);
            AddOptionalValue(configNode, "popupImageURL", popupImageURL);
        }

        private ApplyResult ApplyToLoadedVessel(Vessel vessel)
        {
            List<Part> matchingParts = vessel.parts.Where(PartMatches).ToList();
            if (partIndex.HasValue)
            {
                matchingParts = matchingParts.Skip(partIndex.Value).Take(1).ToList();
            }

            int matchingReliabilityCount = 0;
            int updated = 0;
            foreach (Part part in matchingParts)
            {
                List<PartModule> reliabilityModules = GetLoadedReliabilityModules(part).ToList();
                matchingReliabilityCount += reliabilityModules.Count;
                foreach (PartModule reliabilityModule in reliabilityModules)
                {
                    ApplyLoadedValues(part, reliabilityModule);
                    updated++;
                }
            }

            return new ApplyResult
            {
                MatchingPartCount = matchingParts.Count,
                MatchingReliabilityCount = matchingReliabilityCount,
                UpdatedCount = updated
            };
        }

        private ApplyResult ApplyToUnloadedVessel(Vessel vessel)
        {
            if (vessel.protoVessel == null)
            {
                return default(ApplyResult);
            }

            List<ProtoPartSnapshot> matchingParts = vessel.protoVessel.protoPartSnapshots.Where(PartMatches).ToList();
            if (partIndex.HasValue)
            {
                matchingParts = matchingParts.Skip(partIndex.Value).Take(1).ToList();
            }

            int matchingReliabilityCount = 0;
            int updated = 0;
            foreach (ProtoPartSnapshot part in matchingParts)
            {
                List<ProtoPartModuleSnapshot> reliabilityModules = GetProtoReliabilityModules(part).ToList();
                matchingReliabilityCount += reliabilityModules.Count;
                foreach (ProtoPartModuleSnapshot reliabilityModule in reliabilityModules)
                {
                    ApplyProtoValues(part, reliabilityModule);
                    updated++;
                }
            }

            return new ApplyResult
            {
                MatchingPartCount = matchingParts.Count,
                MatchingReliabilityCount = matchingReliabilityCount,
                UpdatedCount = updated
            };
        }

        private bool PartMatches(Part part)
        {
            if (!PartNameMatches(GetLoadedPartNames(part)))
            {
                return false;
            }

            return true;
        }

        private static IEnumerable<string> GetLoadedPartNames(Part part)
        {
            if (part == null)
            {
                return Enumerable.Empty<string>();
            }

            return new[]
            {
                part.partInfo != null ? part.partInfo.name : null,
                part.name,
                part.partName
            }
            .Where(name => !string.IsNullOrEmpty(name))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private bool PartMatches(ProtoPartSnapshot part)
        {
            if (!PartNameMatches(new[] { part != null ? part.partName : null }))
            {
                return false;
            }

            return true;
        }

        private bool PartNameMatches(IEnumerable<string> candidateNames)
        {
            if (string.IsNullOrEmpty(partName))
            {
                return false;
            }

            string canonicalTarget = CanonicalizePartIdentifier(partName);
            foreach (string candidateName in candidateNames.Where(name => !string.IsNullOrEmpty(name)))
            {
                if (string.Equals(candidateName, partName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (string.Equals(CanonicalizePartIdentifier(candidateName), canonicalTarget, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string CanonicalizePartIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            char[] canonical = new char[value.Length];
            int length = 0;
            bool lastWasSeparator = false;

            foreach (char c in value)
            {
                char normalized;
                if (char.IsLetterOrDigit(c))
                {
                    normalized = char.ToLowerInvariant(c);
                    lastWasSeparator = false;
                }
                else
                {
                    if (lastWasSeparator)
                    {
                        continue;
                    }

                    normalized = '_';
                    lastWasSeparator = true;
                }

                canonical[length++] = normalized;
            }

            while (length > 0 && canonical[length - 1] == '_')
            {
                length--;
            }

            return new string(canonical, 0, length);
        }

        private string FormatReliabilityTypeSuffix()
        {
            return string.IsNullOrEmpty(reliabilityType)
                ? string.Empty
                : string.Format(CultureInfo.InvariantCulture, " for reliabilityType '{0}'", reliabilityType);
        }

        private IEnumerable<PartModule> GetLoadedReliabilityModules(Part part)
        {
            IEnumerable<PartModule> modules = part.Modules.OfType<PartModule>()
                .Where(module => string.Equals(module.moduleName, "Reliability", StringComparison.Ordinal));

            if (!string.IsNullOrEmpty(reliabilityType))
            {
                modules = modules.Where(module => string.Equals(GetFieldValue<string>(module, "type", string.Empty), reliabilityType, StringComparison.Ordinal));
            }

            return modules;
        }

        private IEnumerable<ProtoPartModuleSnapshot> GetProtoReliabilityModules(ProtoPartSnapshot part)
        {
            IEnumerable<ProtoPartModuleSnapshot> modules = part.modules
                .Where(module => string.Equals(module.moduleName, "Reliability", StringComparison.Ordinal));

            if (!string.IsNullOrEmpty(reliabilityType))
            {
                modules = modules.Where(module => string.Equals(GetProtoValue(module, "type"), reliabilityType, StringComparison.Ordinal));
            }

            return modules;
        }

        private void ApplyLoadedValues(Part part, PartModule reliabilityModule)
        {
            if (broken.HasValue)
            {
                SetFieldValue(reliabilityModule, "broken", broken.Value);
            }
            if (critical.HasValue)
            {
                SetFieldValue(reliabilityModule, "critical", critical.Value);
            }
            if (quality.HasValue)
            {
                SetFieldValue(reliabilityModule, "quality", quality.Value);
            }
            if (needMaintenance.HasValue)
            {
                SetFieldValue(reliabilityModule, "needMaintenance", needMaintenance.Value);
            }
            if (inspectNow.GetValueOrDefault())
            {
                SetFieldValue(reliabilityModule, "last_inspection", Planetarium.GetUniversalTime());
            }
            else if (lastInspection.HasValue)
            {
                SetFieldValue(reliabilityModule, "last_inspection", lastInspection.Value);
            }
            if (addIgnitions.HasValue)
            {
                int currentIgnitions = GetFieldValue(reliabilityModule, "ignitions", 0);
                SetFieldValue(reliabilityModule, "ignitions", Math.Max(0, currentIgnitions + addIgnitions.Value));
            }

            if (broken.HasValue)
            {
                SyncLoadedBrokenState(part, reliabilityModule, GetFieldValue(reliabilityModule, "broken", false));
            }
        }

        private void TryRefreshKerbalismReliabilityStatus(Vessel vessel)
        {
            if (vessel == null)
            {
                return;
            }

            try
            {
                ResolveKerbalismReflection();
                if (kerbalismDataMethod == null || resetReliabilityStatusMethod == null)
                {
                    return;
                }

                object vesselData = kerbalismDataMethod.Invoke(null, new object[] { vessel });
                if (vesselData == null)
                {
                    return;
                }

                resetReliabilityStatusMethod.Invoke(vesselData, null);
            }
            catch (Exception ex)
            {
                if (!loggedKerbalismRefreshFailure)
                {
                    loggedKerbalismRefreshFailure = true;
                    LoggingUtil.LogWarning(this, "SetReliabilityState could not refresh Kerbalism reliability status: {0}", ex.Message);
                }
            }
        }

        private static void ResolveKerbalismReflection()
        {
            if (kerbalismReflectionResolved)
            {
                return;
            }

            kerbalismReflectionResolved = true;

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null).ToArray();
                }

                foreach (Type type in types)
                {
                    MethodInfo candidate = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                        .FirstOrDefault(m =>
                            string.Equals(m.Name, "KerbalismData", StringComparison.Ordinal) &&
                            m.GetParameters().Length == 1 &&
                            typeof(Vessel).IsAssignableFrom(m.GetParameters()[0].ParameterType));

                    if (candidate == null)
                    {
                        continue;
                    }

                    MethodInfo resetCandidate = candidate.ReturnType.GetMethod("ResetReliabilityStatus", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
                    if (resetCandidate == null)
                    {
                        continue;
                    }

                    kerbalismDataMethod = candidate;
                    resetReliabilityStatusMethod = resetCandidate;
                    return;
                }
            }
        }

        private void ApplyProtoValues(ProtoPartSnapshot part, ProtoPartModuleSnapshot reliabilityModule)
        {
            if (broken.HasValue)
            {
                SetProtoValue(reliabilityModule, "broken", broken.Value);
            }
            if (critical.HasValue)
            {
                SetProtoValue(reliabilityModule, "critical", critical.Value);
            }
            if (quality.HasValue)
            {
                SetProtoValue(reliabilityModule, "quality", quality.Value);
            }
            if (needMaintenance.HasValue)
            {
                SetProtoValue(reliabilityModule, "needMaintenance", needMaintenance.Value);
            }
            if (inspectNow.GetValueOrDefault())
            {
                SetProtoValue(reliabilityModule, "last_inspection", Planetarium.GetUniversalTime());
            }
            else if (lastInspection.HasValue)
            {
                SetProtoValue(reliabilityModule, "last_inspection", lastInspection.Value);
            }
            if (addIgnitions.HasValue)
            {
                int currentIgnitions = GetProtoValue(reliabilityModule, "ignitions", 0);
                SetProtoValue(reliabilityModule, "ignitions", Math.Max(0, currentIgnitions + addIgnitions.Value));
            }

            if (broken.HasValue)
            {
                SyncProtoBrokenState(part, reliabilityModule, GetProtoValue(reliabilityModule, "broken", false));
            }
        }

        private void SyncLoadedBrokenState(Part part, PartModule reliabilityModule, bool isBroken)
        {
            string controlledType = GetFieldValue<string>(reliabilityModule, "type", string.Empty);
            foreach (PartModule controlledModule in GetControlledLoadedModules(part, controlledType))
            {
                if (isBroken)
                {
                    StopControlledModuleImmediately(controlledModule);
                }

                controlledModule.enabled = !isBroken;
                SetFieldValue(controlledModule, "isEnabled", !isBroken);

                if (isBroken)
                {
                    ScheduleDeferredControlledModuleStop(part, controlledModule);
                }
            }

            InvokeApply(reliabilityModule, isBroken);

            if (!isBroken)
            {
                RefreshConfigureModules(part);
            }
        }

        private void SyncProtoBrokenState(ProtoPartSnapshot part, ProtoPartModuleSnapshot reliabilityModule, bool isBroken)
        {
            string controlledType = GetProtoValue(reliabilityModule, "type");
            foreach (ProtoPartModuleSnapshot controlledModule in GetControlledProtoModules(part, controlledType))
            {
                SetProtoValue(controlledModule, "isEnabled", !isBroken);
                if (string.Equals(controlledType, "ProcessController", StringComparison.Ordinal))
                {
                    SetProtoValue(controlledModule, "broken", isBroken);
                }
            }
        }

        private IEnumerable<PartModule> GetControlledLoadedModules(Part part, string controlledType)
        {
            if (string.IsNullOrEmpty(controlledType))
            {
                return Enumerable.Empty<PartModule>();
            }

            if (controlledType.StartsWith("ModuleEngines", StringComparison.Ordinal))
            {
                return part.Modules.OfType<PartModule>()
                    .Where(module => module.moduleName != null && module.moduleName.StartsWith("ModuleEngines", StringComparison.Ordinal))
                    .ToList();
            }

            return part.Modules.OfType<PartModule>()
                .Where(module => string.Equals(module.moduleName, controlledType, StringComparison.Ordinal))
                .ToList();
        }

        private IEnumerable<ProtoPartModuleSnapshot> GetControlledProtoModules(ProtoPartSnapshot part, string controlledType)
        {
            if (string.IsNullOrEmpty(controlledType))
            {
                return Enumerable.Empty<ProtoPartModuleSnapshot>();
            }

            if (controlledType.StartsWith("ModuleEngines", StringComparison.Ordinal))
            {
                return part.modules
                    .Where(module => module.moduleName != null && module.moduleName.StartsWith("ModuleEngines", StringComparison.Ordinal))
                    .ToList();
            }

            return part.modules
                .Where(module => string.Equals(module.moduleName, controlledType, StringComparison.Ordinal))
                .ToList();
        }

        private static void StopControlledModuleImmediately(PartModule controlledModule)
        {
            ModuleEngines engine = controlledModule as ModuleEngines;
            if (engine == null)
            {
                return;
            }

            engine.Shutdown();

            SetFieldValue(engine, "EngineIgnited", false);
            SetFieldValue(engine, "engineShutdown", true);
            SetFieldValue(engine, "currentThrottle", 0f);
            SetFieldValue(engine, "requestedThrottle", 0f);

            InvokeOptional(controlledModule, "SetRunningGroupsActive", false);
            InvokeOptional(controlledModule, "SetPowerGroupsActive", false);
            InvokeOptional(controlledModule, "DeactivateRunningFX");
            InvokeOptional(controlledModule, "DeactivateLoopingFX");
            InvokeOptional(controlledModule, "DeactivatePowerFX");
            InvokeOptional(controlledModule, "PlayShutdownFX");

            StopEngineFxGroups(engine);
            StopPartFxGroups(controlledModule, engine);
        }

        private static void ScheduleDeferredControlledModuleStop(Part part, PartModule controlledModule)
        {
            if (part == null || controlledModule == null)
            {
                return;
            }

            part.StartCoroutine(DeferredControlledModuleStop(controlledModule));
        }

        private static System.Collections.IEnumerator DeferredControlledModuleStop(PartModule controlledModule)
        {
            yield return null;
            StopControlledModuleImmediately(controlledModule);

            yield return null;
            StopControlledModuleImmediately(controlledModule);
        }

        private static void StopEngineFxGroups(ModuleEngines engine)
        {
            StopFxGroup(GetFieldValue<FXGroup>(engine, "runningGroup", null));
            StopFxGroup(GetFieldValue<FXGroup>(engine, "powerGroup", null));
            StopFxGroup(GetFieldValue<FXGroup>(engine, "engageGroup", null));
            StopFxGroup(GetFieldValue<FXGroup>(engine, "disengageGroup", null));
            StopFxGroup(GetFieldValue<FXGroup>(engine, "flameoutGroup", null));

            StopFxGroups(GetFieldValue<List<FXGroup>>(engine, "runningGroups", null));
            StopFxGroups(GetFieldValue<List<FXGroup>>(engine, "powerGroups", null));
            StopFxGroups(GetFieldValue<List<FXGroup>>(engine, "flameoutGroups", null));

            AudioSource powerSfx = GetFieldValue<AudioSource>(engine, "powerSfx", null);
            if (powerSfx != null)
            {
                powerSfx.Stop();
                powerSfx.volume = 0f;
                powerSfx.clip = null;
            }
        }

        private static void StopPartFxGroups(PartModule controlledModule, ModuleEngines engine)
        {
            if (engine.part == null)
            {
                return;
            }

            foreach (string effectName in GetEngineEffectNames(controlledModule))
            {
                if (string.IsNullOrEmpty(effectName))
                {
                    continue;
                }

                engine.part.Effect(effectName, 0f, -1);
                StopFxGroup(engine.part.findFxGroup(effectName));
            }
        }

        private static IEnumerable<string> GetEngineEffectNames(PartModule controlledModule)
        {
            yield return GetFieldValue<string>(controlledModule, "runningEffectName", string.Empty);
            yield return GetFieldValue<string>(controlledModule, "powerEffectName", string.Empty);
            yield return GetFieldValue<string>(controlledModule, "directThrottleEffectName", string.Empty);
            yield return GetFieldValue<string>(controlledModule, "flameoutEffectName", string.Empty);
        }

        private static void StopFxGroups(IEnumerable<FXGroup> groups)
        {
            if (groups == null)
            {
                return;
            }

            foreach (FXGroup group in groups)
            {
                StopFxGroup(group);
            }
        }

        private static void StopFxGroup(FXGroup group)
        {
            if (group == null)
            {
                return;
            }

            group.setActiveWithAudioFade(false, 0f);
            SetFieldValue(group, "power", 0f);

            AudioSource audio = GetFieldValue<AudioSource>(group, "audio", null);
            if (audio != null)
            {
                audio.Stop();
                audio.volume = 0f;
                audio.clip = null;
            }
        }

        private static void RefreshConfigureModules(Part part)
        {
            foreach (PartModule module in part.Modules.OfType<PartModule>().Where(m => string.Equals(m.moduleName, "Configure", StringComparison.Ordinal)))
            {
                MethodInfo doConfigure = module.GetType().GetMethod("DoConfigure", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (doConfigure != null && doConfigure.GetParameters().Length == 0)
                {
                    doConfigure.Invoke(module, null);
                }
            }
        }

        private static void InvokeApply(PartModule reliabilityModule, bool value)
        {
            MethodInfo apply = reliabilityModule.GetType().GetMethod("Apply", BindingFlags.Instance | BindingFlags.NonPublic);
            if (apply != null)
            {
                apply.Invoke(reliabilityModule, new object[] { value });
            }
        }

        private static void InvokeOptional(object obj, string methodName, params object[] args)
        {
            if (obj == null)
            {
                return;
            }

            Type[] argumentTypes = args == null || args.Length == 0
                ? Type.EmptyTypes
                : args.Select(arg => arg != null ? arg.GetType() : typeof(object)).ToArray();

            MethodInfo method = obj.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, argumentTypes, null)
                ?? obj.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null)
            {
                return;
            }

            ParameterInfo[] parameters = method.GetParameters();
            if ((args == null ? 0 : args.Length) != parameters.Length)
            {
                return;
            }

            method.Invoke(obj, args);
        }

        private static T GetFieldValue<T>(object obj, string fieldName, T defaultValue)
        {
            if (obj == null)
            {
                return defaultValue;
            }

            FieldInfo field = obj.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
            {
                return defaultValue;
            }

            object value = field.GetValue(obj);
            if (value == null)
            {
                return defaultValue;
            }

            if (value is T)
            {
                return (T)value;
            }

            return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
        }

        private static void SetFieldValue(object obj, string fieldName, object value)
        {
            if (obj == null)
            {
                return;
            }

            FieldInfo field = obj.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
            {
                return;
            }

            object converted = value;
            Type targetType = Nullable.GetUnderlyingType(field.FieldType) ?? field.FieldType;
            if (value != null && !targetType.IsInstanceOfType(value))
            {
                converted = Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            }
            field.SetValue(obj, converted);
        }

        private static string GetProtoValue(ProtoPartModuleSnapshot module, string key, string defaultValue = "")
        {
            if (module == null || module.moduleValues == null || !module.moduleValues.HasValue(key))
            {
                return defaultValue;
            }
            return module.moduleValues.GetValue(key);
        }

        private static T GetProtoValue<T>(ProtoPartModuleSnapshot module, string key, T defaultValue)
        {
            string raw = GetProtoValue(module, key, null);
            if (raw == null)
            {
                return defaultValue;
            }

            Type targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            return (T)Convert.ChangeType(raw, targetType, CultureInfo.InvariantCulture);
        }

        private static void SetProtoValue(ProtoPartModuleSnapshot module, string key, object value)
        {
            if (module == null || module.moduleValues == null)
            {
                return;
            }

            string stringValue = Convert.ToString(value, CultureInfo.InvariantCulture);
            if (module.moduleValues.HasValue(key))
            {
                module.moduleValues.SetValue(key, stringValue, true);
            }
            else
            {
                module.moduleValues.AddValue(key, stringValue);
            }
        }

        private static void AddOptionalValue(ConfigNode configNode, string key, object value)
        {
            if (value != null)
            {
                configNode.AddValue(key, value);
            }
        }
    }
}
