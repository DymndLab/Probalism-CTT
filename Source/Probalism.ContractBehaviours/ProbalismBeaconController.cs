using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Probalism.ContractBehaviours
{
    public class ProbalismBeaconController : PartModule
    {
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Activation Altitude", guiUnits = " m")]
        [UI_FloatRange(minValue = 100f, maxValue = 250000f, stepIncrement = 100f)]
        public float activationAltitude = 18000f;

        [KSPField(guiActive = true, guiName = "Beacon Status")]
        public string beaconStatus = "Idle";

        [KSPField(guiActiveEditor = true, guiName = "Beacon Package")]
        public string payloadTier = "Ping";

        [KSPField(guiActiveEditor = true, guiName = "Beacon Link")]
        public string linkTier = "PBM-075 Modem";

        [KSPField(guiActiveEditor = true, guiName = "Package Data")]
        public string payloadData = "0.03 Mb";

        [KSPField(guiActiveEditor = true, guiName = "Package Mass")]
        public string payloadMass = "40.00 kg";

        [KSPField(guiActiveEditor = true, guiName = "Package Cost")]
        public string payloadCost = "25 $";

        [KSPField(guiActiveEditor = true, guiName = "Link Rate")]
        public string linkRate = "0.75 kB/s";

        [KSPField(guiActiveEditor = true, guiName = "Link Range")]
        public string linkRange = "250 km";

        [KSPField(guiActiveEditor = true, guiName = "Link EC/Packet")]
        public string linkEcCost = "0.006 EC";

        [KSPField(guiActiveEditor = true, guiName = "Modem Mass")]
        public string linkMass = "20.00 kg";

        [KSPField(guiActiveEditor = true, guiName = "Modem Cost")]
        public string linkCost = "25 $";

        [KSPField]
        public string experimentId = "pbm_beacon";

        [KSPField]
        public double lowBandwidthPower = 250000;

        [KSPField]
        public double fullBandwidthPower = 250001;

        [KSPField]
        public float completeBlinkRate = 1.25f;

        [KSPField]
        public float transmittingBlinkRate = 0.35f;

        [KSPField]
        public string triggerSoundPath = "Probalism-CTT/Audio/modem-us-robotics";

        [KSPField]
        public float triggerSoundVolume = 1.0f;

        [KSPField(isPersistant = true)]
        public bool testTriggered;

        [KSPField(isPersistant = true)]
        public bool testComplete;

        [KSPField(isPersistant = true)]
        public bool sawBeaconData;

        [KSPField(isPersistant = true)]
        public bool queuedTransmission;

        [KSPField(isPersistant = true)]
        public string activeExperimentId = string.Empty;

        [KSPField(isPersistant = true)]
        public string trackedSubjectId = string.Empty;

        [KSPField(isPersistant = true)]
        public double lastAslAltitude = double.NaN;

        [KSPField(isPersistant = true)]
        public double triggerUniversalTime = -1;

        [KSPField(isPersistant = true)]
        public double fallbackCompleteUniversalTime = -1;

        [KSPField(isPersistant = true)]
        public double triggerAslAltitude = double.NaN;

        [KSPField(isPersistant = true)]
        public double transmittedDataSize = 0;

        [KSPField(isPersistant = true)]
        public string transmittedPackageId = string.Empty;

        [KSPField(isPersistant = true)]
        public double transmittedPacketSize = 0;

        [KSPField(isPersistant = true)]
        public string transmittedLinkProfileId = string.Empty;

        private ModuleLight lightModule;
        private PartModule activeDataProfileModule;
        private ModuleDataTransmitter activeTransmitterModule;
        private PartModule activeLinkProfileModule;
        private AudioSource triggerAudioSource;
        private AudioClip triggerAudioClip;
        private bool uiConfigured;
        private int currentLightMode = -1;
        private float currentLightBlinkRate = -1f;
        private static bool kerbalismReflectionResolved;
        private static bool kerbalismReflectionFailed;
        private static MethodInfo kerbalismDataMethod;
        private static MethodInfo driveGetDrivesMethod;
        private static MethodInfo driveFileDriveMethod;
        private static MethodInfo driveRecordFileMethod;
        private static MethodInfo driveSendMethod;
        private static FieldInfo driveFilesField;
        private static MethodInfo scienceDbGetExperimentInfoMethod;
        private static MethodInfo scienceDbGetSubjectDataMethod;
        private static PropertyInfo vesselSituationsProperty;
        private static MethodInfo vesselSituationsGetExperimentSituationMethod;
        private static PropertyInfo subjectExpInfoProperty;
        private static PropertyInfo subjectIdProperty;
        private static PropertyInfo expInfoExperimentIdProperty;

        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            ResolveModules();
            ResolveAudio();
            ConfigureUi();
            RefreshSelections();

            if (HighLogic.LoadedSceneIsFlight)
            {
                if (testComplete)
                {
                    SetSolidLight();
                    beaconStatus = "Complete";
                }
                else if (testTriggered)
                {
                    SetBlinkingLight(transmittingBlinkRate);
                    beaconStatus = "Transmitting";
                }
                else
                {
                    SetBlinkingLight(completeBlinkRate);
                    beaconStatus = "Armed";
                }
            }
            else
            {
                SetLightOff();
            }
        }

        public void Update()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                ResolveModules();
                ConfigureUi();
                RefreshSelections();
            }
        }

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || part == null || vessel == null)
            {
                return;
            }

            ResolveModules();
            ConfigureUi();
            RefreshSelections();

            double currentAltitude = GetAslAltitude();

            if (!testTriggered && ShouldTrigger(currentAltitude))
            {
                StartBeaconTest();
            }

            if (testTriggered && !testComplete)
            {
                if (!string.IsNullOrEmpty(trackedSubjectId))
                {
                    bool filePresent = HasSubjectFile(trackedSubjectId);
                    EnsureSubjectQueued(trackedSubjectId);

                    if (filePresent)
                    {
                        beaconStatus = "Transmitting";
                        SetBlinkingLight(transmittingBlinkRate);
                    }
                    else
                    {
                        testComplete = true;
                        beaconStatus = "Complete";
                        SetSolidLight();
                    }
                }
                else if (fallbackCompleteUniversalTime > 0 && Planetarium.GetUniversalTime() >= fallbackCompleteUniversalTime)
                {
                    testComplete = true;
                    beaconStatus = "Complete";
                    SetSolidLight();
                }
                else
                {
                    beaconStatus = "No Data";
                    SetLightOff();
                }
            }
            else if (!testTriggered)
            {
                beaconStatus = "Armed";
                SetBlinkingLight(completeBlinkRate);
            }

            lastAslAltitude = currentAltitude;
        }

        private void ResolveModules()
        {
            if (lightModule == null)
            {
                lightModule = part.FindModuleImplementing<ModuleLight>();
            }

            if (activeDataProfileModule == null || !IsModuleEnabled(activeDataProfileModule))
            {
                activeDataProfileModule = GetConfiguredDataProfileModule();
            }

            if (activeTransmitterModule == null || !IsModuleEnabled(activeTransmitterModule))
            {
                activeTransmitterModule = part.FindModuleImplementing<ModuleDataTransmitter>();
            }

            if (activeLinkProfileModule == null || !IsModuleEnabled(activeLinkProfileModule))
            {
                activeLinkProfileModule = GetConfiguredLinkProfileModule();
            }
        }

        private void ConfigureUi()
        {
            if (uiConfigured)
            {
                return;
            }

            if (lightModule != null)
            {
                foreach (BaseEvent baseEvent in lightModule.Events)
                {
                    baseEvent.guiActive = false;
                    baseEvent.guiActiveEditor = false;
                }

                foreach (BaseField field in lightModule.Fields)
                {
                    field.guiActive = false;
                    field.guiActiveEditor = false;
                }
            }

            foreach (PartModule dataProfileModule in GetDataProfileModules())
            {
                foreach (BaseEvent baseEvent in dataProfileModule.Events)
                {
                    baseEvent.guiActive = false;
                    baseEvent.guiActiveEditor = false;
                }

                foreach (BaseField field in dataProfileModule.Fields)
                {
                    field.guiActive = false;
                    field.guiActiveEditor = false;
                }
            }

            if (Fields["activationAltitude"] != null)
            {
                Fields["activationAltitude"].guiActive = false;
            }

            uiConfigured = true;
        }

        private void RefreshSelections()
        {
            activeDataProfileModule = GetConfiguredDataProfileModule() ?? activeDataProfileModule;
            activeLinkProfileModule = GetConfiguredLinkProfileModule() ?? activeLinkProfileModule;
            ApplyConfiguredTransmitterProfile();

            payloadTier = GetStringFieldValue(activeDataProfileModule, "packageName", "Ping");
            linkTier = GetStringFieldValue(activeLinkProfileModule, "profileName", "PBM-075 Modem");
            payloadData = FormatDataSize(GetSelectedDataSize());
            payloadMass = FormatMass(GetDoubleFieldValue(activeDataProfileModule, "packageMass", 0.04));
            payloadCost = FormatCost(GetDoubleFieldValue(activeDataProfileModule, "packageCost", 25));
            linkRate = FormatDataRate(GetDoubleFieldValue(activeLinkProfileModule, "packetSize", 0.00075));
            linkRange = FormatRange(GetDoubleFieldValue(activeLinkProfileModule, "antennaPower", lowBandwidthPower));
            linkEcCost = FormatElectricCost(GetDoubleFieldValue(activeLinkProfileModule, "packetResourceCost", 0.006));
            linkMass = FormatMass(GetDoubleFieldValue(activeLinkProfileModule, "profileMass", 0.02));
            linkCost = FormatCost(GetDoubleFieldValue(activeLinkProfileModule, "profileCost", 25));
        }

        private bool ShouldTrigger(double currentAltitude)
        {
            if (testTriggered || testComplete)
            {
                return false;
            }

            if (double.IsNaN(currentAltitude) || currentAltitude < activationAltitude)
            {
                return false;
            }

            return double.IsNaN(lastAslAltitude) || lastAslAltitude < activationAltitude;
        }

        private void StartBeaconTest()
        {
            activeDataProfileModule = GetConfiguredDataProfileModule();
            activeExperimentId = experimentId;

            if (activeDataProfileModule == null || string.IsNullOrEmpty(activeExperimentId))
            {
                beaconStatus = "No package";
                Debug.LogWarning("[Probalism] Beacon could not find an enabled data package.");
                return;
            }

            double selectedDataSize = GetSelectedDataSize();
            string subjectId;
            if (!InjectBeaconData(selectedDataSize, out subjectId))
            {
                beaconStatus = "No Data";
                Debug.LogWarning("[Probalism] Beacon could not inject Kerbalism science data.");
                return;
            }

            testTriggered = true;
            testComplete = false;
            sawBeaconData = true;
            queuedTransmission = true;
            trackedSubjectId = subjectId;
            triggerUniversalTime = Planetarium.GetUniversalTime();
            triggerAslAltitude = GetAslAltitude();
            transmittedDataSize = selectedDataSize;
            transmittedPackageId = GetStringFieldValue(activeDataProfileModule, "packageId", string.Empty);
            transmittedPacketSize = GetDoubleFieldValue(activeLinkProfileModule, "packetSize", 0.00075);
            transmittedLinkProfileId = GetLinkProfileId(activeLinkProfileModule);
            fallbackCompleteUniversalTime = triggerUniversalTime + EstimateTransmissionDuration() + 2.0;
            beaconStatus = "Transmitting";
            SetBlinkingLight(transmittingBlinkRate);
            PlayTriggerSound();
        }

        private void ResolveAudio()
        {
            if (string.IsNullOrEmpty(triggerSoundPath))
            {
                return;
            }

            if (triggerAudioSource == null)
            {
                triggerAudioSource = part.gameObject.AddComponent<AudioSource>();
                triggerAudioSource.playOnAwake = false;
                triggerAudioSource.loop = false;
                triggerAudioSource.spatialBlend = 1.0f;
                triggerAudioSource.minDistance = 1.0f;
                triggerAudioSource.maxDistance = 250.0f;
                triggerAudioSource.dopplerLevel = 0.0f;
            }

            if (triggerAudioClip == null && GameDatabase.Instance != null)
            {
                triggerAudioClip = GameDatabase.Instance.GetAudioClip(triggerSoundPath);
                if (triggerAudioClip == null)
                {
                    Debug.LogWarning("[Probalism] Beacon trigger sound could not be loaded: " + triggerSoundPath);
                }
            }
        }

        private void PlayTriggerSound()
        {
            ResolveAudio();

            if (triggerAudioSource == null || triggerAudioClip == null)
            {
                return;
            }

            triggerAudioSource.PlayOneShot(triggerAudioClip, triggerSoundVolume);
        }

        private bool InvokeAction(PartModule module, string actionName)
        {
            if (module == null || string.IsNullOrEmpty(actionName))
            {
                return false;
            }

            BaseAction action = module.Actions[actionName];
            if (action != null)
            {
                action.Invoke(new KSPActionParam(KSPActionGroup.None, KSPActionType.Activate));
                return true;
            }

            MethodInfo method = module.GetType().GetMethod(actionName, BindingFlags.Instance | BindingFlags.Public);
            if (method != null)
            {
                method.Invoke(module, new object[] { new KSPActionParam(KSPActionGroup.None, KSPActionType.Activate) });
                return true;
            }

            return false;
        }

        private bool InjectBeaconData(double dataSize, out string subjectId)
        {
            subjectId = string.Empty;

            if (!ResolveKerbalismReflection())
            {
                return false;
            }

            try
            {
                object vesselData = kerbalismDataMethod.Invoke(null, new object[] { vessel });
                if (vesselData == null)
                {
                    return false;
                }

                object expInfo = scienceDbGetExperimentInfoMethod.Invoke(null, new object[] { activeExperimentId });
                if (expInfo == null)
                {
                    return false;
                }

                object vesselSituations = vesselSituationsProperty.GetValue(vesselData, null);
                object situation = vesselSituationsGetExperimentSituationMethod.Invoke(vesselSituations, new[] { expInfo });
                object subjectData = scienceDbGetSubjectDataMethod.Invoke(null, new[] { expInfo, situation });
                if (subjectData == null)
                {
                    return false;
                }

                object drive = driveFileDriveMethod.Invoke(null, new[] { vesselData, (object)dataSize });
                if (drive == null)
                {
                    return false;
                }

                object recorded = driveRecordFileMethod.Invoke(drive, new object[] { subjectData, dataSize, true, false });
                if (recorded is bool && !(bool)recorded)
                {
                    return false;
                }

                subjectId = GetSubjectId(subjectData);
                if (!string.IsNullOrEmpty(subjectId))
                {
                    driveSendMethod.Invoke(drive, new object[] { subjectId, true });
                }

                return !string.IsNullOrEmpty(subjectId);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Probalism] Beacon Kerbalism data injection failed: " + ex.Message);
                return false;
            }
        }

        private IEnumerable<PartModule> GetDataProfileModules()
        {
            return part.Modules.OfType<PartModule>()
                .Where(module => string.Equals(module.moduleName, "ProbalismBeaconDataProfile", StringComparison.Ordinal));
        }

        private PartModule GetConfiguredDataProfileModule()
        {
            return GetDataProfileModules().FirstOrDefault(IsModuleEnabled);
        }

        private PartModule GetConfiguredLinkProfileModule()
        {
            return part.Modules.OfType<PartModule>()
                .Where(module => string.Equals(module.moduleName, "ProbalismBeaconLinkProfile", StringComparison.Ordinal))
                .FirstOrDefault(IsModuleEnabled);
        }

        private void ApplyConfiguredTransmitterProfile()
        {
            if (activeTransmitterModule == null || activeLinkProfileModule == null)
            {
                return;
            }

            double antennaPower = GetDoubleFieldValue(activeLinkProfileModule, "antennaPower", lowBandwidthPower);
            double packetSize = GetDoubleFieldValue(activeLinkProfileModule, "packetSize", 0.00075);
            double packetResourceCost = GetDoubleFieldValue(activeLinkProfileModule, "packetResourceCost", 0.006);

            activeTransmitterModule.antennaType = AntennaType.DIRECT;
            activeTransmitterModule.antennaPower = antennaPower;
            activeTransmitterModule.antennaCombinable = false;
            activeTransmitterModule.packetInterval = 1.0f;
            activeTransmitterModule.packetSize = (float)packetSize;
            activeTransmitterModule.packetResourceCost = (float)packetResourceCost;
        }

        private double EstimateTransmissionDuration()
        {
            double packetSize = GetDoubleFieldValue(activeLinkProfileModule, "packetSize", 0.00075);
            if (packetSize <= 0)
            {
                return 10.0;
            }

            return GetSelectedDataSize() / packetSize;
        }

        private double GetSelectedDataSize()
        {
            return GetDoubleFieldValue(activeDataProfileModule, "dataSize", 0.03);
        }

        private static string FormatDataSize(double megabytes)
        {
            return megabytes.ToString("0.00") + " Mb";
        }

        private static string FormatDataRate(double megabytesPerSecond)
        {
            return (megabytesPerSecond * 1000.0).ToString("0.00") + " kB/s";
        }

        private static string FormatMass(double tons)
        {
            return (tons * 1000.0).ToString("0.00") + " kg";
        }

        private static string FormatCost(double funds)
        {
            return funds.ToString("0") + " $";
        }

        private static string FormatRange(double meters)
        {
            return (meters / 1000.0).ToString("0") + " km";
        }

        private static string FormatElectricCost(double electricCharge)
        {
            return electricCharge.ToString("0.###") + " EC";
        }

        private static string GetExperimentId(PartModule module)
        {
            if (module == null || module.Fields == null)
            {
                return string.Empty;
            }

            BaseField experimentField = module.Fields["experiment_id"];
            if (experimentField == null)
            {
                return string.Empty;
            }

            object value = experimentField.GetValue(module);
            return value != null ? value.ToString() : string.Empty;
        }

        private static string GetLinkProfileId(PartModule module)
        {
            if (module == null || module.Fields == null)
            {
                return string.Empty;
            }

            BaseField profileField = module.Fields["profileId"];
            if (profileField == null)
            {
                return string.Empty;
            }

            object value = profileField.GetValue(module);
            return value != null ? value.ToString() : string.Empty;
        }

        private static double GetDoubleFieldValue(PartModule module, string fieldName, double fallbackValue)
        {
            if (module == null || module.Fields == null || string.IsNullOrEmpty(fieldName))
            {
                return fallbackValue;
            }

            BaseField field = module.Fields[fieldName];
            if (field == null)
            {
                return fallbackValue;
            }

            object value = field.GetValue(module);
            if (value is double)
            {
                return (double)value;
            }

            if (value is float)
            {
                return (float)value;
            }

            if (value is int)
            {
                return (int)value;
            }

            double parsedValue;
            return value != null && double.TryParse(value.ToString(), out parsedValue) ? parsedValue : fallbackValue;
        }

        private static string GetStringFieldValue(PartModule module, string fieldName, string fallbackValue)
        {
            if (module == null || module.Fields == null || string.IsNullOrEmpty(fieldName))
            {
                return fallbackValue;
            }

            BaseField field = module.Fields[fieldName];
            if (field == null)
            {
                return fallbackValue;
            }

            object value = field.GetValue(module);
            return value != null ? value.ToString() : fallbackValue;
        }

        private static bool IsModuleEnabled(PartModule module)
        {
            if (module == null)
            {
                return false;
            }

            PropertyInfo property = module.GetType().GetProperty("isEnabled", BindingFlags.Instance | BindingFlags.Public);
            if (property != null && property.PropertyType == typeof(bool))
            {
                object propertyValue = property.GetValue(module, null);
                if (propertyValue is bool)
                {
                    return (bool)propertyValue;
                }
            }

            FieldInfo field = module.GetType().GetField("isEnabled", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null && field.FieldType == typeof(bool))
            {
                object fieldValue = field.GetValue(module);
                if (fieldValue is bool)
                {
                    return (bool)fieldValue;
                }
            }

            return module.enabled;
        }

        private bool IsExperimentRunning(PartModule module)
        {
            if (module == null)
            {
                return false;
            }

            PropertyInfo runningProperty = module.GetType().GetProperty("Running", BindingFlags.Instance | BindingFlags.Public);
            if (runningProperty == null)
            {
                return false;
            }

            object value = runningProperty.GetValue(module, null);
            return value is bool && (bool)value;
        }

        private void SetLightOff()
        {
            if (lightModule == null)
            {
                return;
            }

            if (currentLightMode == 0)
            {
                return;
            }

            currentLightMode = 0;
            currentLightBlinkRate = -1f;
            lightModule.blinkRate = completeBlinkRate;
            lightModule.isBlinking = false;
            lightModule.blinkState = false;
            lightModule.SetBlinkState(false);
            lightModule.LightsOff();
        }

        private void SetBlinkingLight(float rate)
        {
            if (lightModule == null)
            {
                return;
            }

            if (currentLightMode == 2 && Math.Abs(currentLightBlinkRate - rate) < 0.001f)
            {
                return;
            }

            currentLightMode = 2;
            currentLightBlinkRate = rate;
            lightModule.canBlink = true;
            lightModule.blinkRate = rate;
            lightModule.isBlinking = true;
            lightModule.blinkState = true;
            lightModule.LightsOn();
            lightModule.SetBlinkState(true);
        }

        private void SetSolidLight()
        {
            if (lightModule == null)
            {
                return;
            }

            if (currentLightMode == 1)
            {
                return;
            }

            currentLightMode = 1;
            currentLightBlinkRate = -1f;
            lightModule.canBlink = false;
            lightModule.blinkRate = completeBlinkRate;
            lightModule.isBlinking = false;
            lightModule.blinkState = true;
            lightModule.LightsOn();
        }

        private double GetAslAltitude()
        {
            if (vessel == null || vessel.mainBody == null)
            {
                return 0.0;
            }

            return vessel.mainBody.GetAltitude(vessel.GetWorldPos3D());
        }

        private string FindFirstSubjectIdForExperiment(string experimentId)
        {
            if (string.IsNullOrEmpty(experimentId))
            {
                return string.Empty;
            }

            if (!ResolveKerbalismReflection())
            {
                return string.Empty;
            }

            foreach (object drive in GetKerbalismDrives())
            {
                IDictionary files = driveFilesField.GetValue(drive) as IDictionary;
                if (files == null)
                {
                    continue;
                }

                foreach (DictionaryEntry fileEntry in files)
                {
                    object subjectData = fileEntry.Key;
                    if (!string.Equals(GetExperimentIdFromSubject(subjectData), experimentId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string subjectId = GetSubjectId(subjectData);
                    if (!string.IsNullOrEmpty(subjectId))
                    {
                        return subjectId;
                    }
                }
            }

            return string.Empty;
        }

        private bool HasSubjectFile(string subjectId)
        {
            if (string.IsNullOrEmpty(subjectId))
            {
                return false;
            }

            if (!ResolveKerbalismReflection())
            {
                return false;
            }

            foreach (object drive in GetKerbalismDrives())
            {
                IDictionary files = driveFilesField.GetValue(drive) as IDictionary;
                if (files == null)
                {
                    continue;
                }

                foreach (DictionaryEntry fileEntry in files)
                {
                    if (string.Equals(GetSubjectId(fileEntry.Key), subjectId, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void EnsureSubjectQueued(string subjectId)
        {
            if (queuedTransmission || string.IsNullOrEmpty(subjectId))
            {
                return;
            }

            if (!ResolveKerbalismReflection())
            {
                return;
            }

            bool queuedAny = false;
            foreach (object drive in GetKerbalismDrives())
            {
                IDictionary files = driveFilesField.GetValue(drive) as IDictionary;
                if (files == null)
                {
                    continue;
                }

                foreach (DictionaryEntry fileEntry in files)
                {
                    if (!string.Equals(GetSubjectId(fileEntry.Key), subjectId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    driveSendMethod.Invoke(drive, new object[] { subjectId, true });
                    queuedAny = true;
                }
            }

            if (queuedAny)
            {
                queuedTransmission = true;
            }
        }

        private IEnumerable<object> GetKerbalismDrives()
        {
            if (!ResolveKerbalismReflection())
            {
                return Enumerable.Empty<object>();
            }

            object vesselData = kerbalismDataMethod.Invoke(null, new object[] { vessel });
            if (vesselData == null)
            {
                return Enumerable.Empty<object>();
            }

            object drives = driveGetDrivesMethod.Invoke(null, new[] { vesselData, true });
            return drives as IEnumerable<object> ?? ((IEnumerable)drives).Cast<object>();
        }

        private static string GetExperimentIdFromSubject(object subjectData)
        {
            if (subjectData == null || subjectExpInfoProperty == null || expInfoExperimentIdProperty == null)
            {
                return string.Empty;
            }

            object expInfo = subjectExpInfoProperty.GetValue(subjectData, null);
            if (expInfo == null)
            {
                return string.Empty;
            }

            object experimentId = expInfoExperimentIdProperty.GetValue(expInfo, null);
            return experimentId != null ? experimentId.ToString() : string.Empty;
        }

        private static string GetSubjectId(object subjectData)
        {
            if (subjectData == null || subjectIdProperty == null)
            {
                return string.Empty;
            }

            object id = subjectIdProperty.GetValue(subjectData, null);
            return id != null ? id.ToString() : string.Empty;
        }

        private static bool ResolveKerbalismReflection()
        {
            if (kerbalismReflectionResolved)
            {
                return !kerbalismReflectionFailed;
            }

            kerbalismReflectionResolved = true;
            kerbalismReflectionFailed = true;

            try
            {
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

                    Type vesselDataType = types.FirstOrDefault(t => string.Equals(t.FullName, "KERBALISM.VesselData", StringComparison.Ordinal));
                    Type driveType = types.FirstOrDefault(t => string.Equals(t.FullName, "KERBALISM.Drive", StringComparison.Ordinal));
                    Type subjectDataType = types.FirstOrDefault(t => string.Equals(t.FullName, "KERBALISM.SubjectData", StringComparison.Ordinal));
                    Type experimentInfoType = types.FirstOrDefault(t => string.Equals(t.FullName, "KERBALISM.ExperimentInfo", StringComparison.Ordinal));
                    Type scienceDbType = types.FirstOrDefault(t => string.Equals(t.FullName, "KERBALISM.ScienceDB", StringComparison.Ordinal));
                    Type situationType = types.FirstOrDefault(t => string.Equals(t.FullName, "KERBALISM.Situation", StringComparison.Ordinal));
                    Type vesselSituationsType = types.FirstOrDefault(t => string.Equals(t.FullName, "KERBALISM.VesselSituations", StringComparison.Ordinal));

                    if (vesselDataType == null || driveType == null || subjectDataType == null || experimentInfoType == null || scienceDbType == null || situationType == null || vesselSituationsType == null)
                    {
                        continue;
                    }

                    kerbalismDataMethod = types
                        .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                        .FirstOrDefault(m =>
                            string.Equals(m.Name, "KerbalismData", StringComparison.Ordinal) &&
                            m.GetParameters().Length == 1 &&
                            typeof(Vessel).IsAssignableFrom(m.GetParameters()[0].ParameterType));

                    driveGetDrivesMethod = driveType.GetMethod("GetDrives", BindingFlags.Public | BindingFlags.Static, null, new[] { vesselDataType, typeof(bool) }, null);
                    driveFileDriveMethod = driveType.GetMethod("FileDrive", BindingFlags.Public | BindingFlags.Static, null, new[] { vesselDataType, typeof(double) }, null);
                    driveRecordFileMethod = driveType
                        .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                        .FirstOrDefault(method =>
                            string.Equals(method.Name, "Record_file", StringComparison.Ordinal) &&
                            method.GetParameters().Length == 4 &&
                            method.GetParameters()[0].ParameterType == subjectDataType);
                    driveSendMethod = driveType.GetMethod("Send", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(string), typeof(bool) }, null);
                    driveFilesField = driveType.GetField("files", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (driveFilesField == null)
                    {
                        driveFilesField = driveType.GetField("files", BindingFlags.Public | BindingFlags.Instance);
                    }

                    scienceDbGetExperimentInfoMethod = scienceDbType.GetMethod("GetExperimentInfo", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
                    scienceDbGetSubjectDataMethod = scienceDbType.GetMethod("GetSubjectData", BindingFlags.Public | BindingFlags.Static, null, new[] { experimentInfoType, situationType }, null);
                    vesselSituationsProperty = vesselDataType.GetProperty("VesselSituations", BindingFlags.Public | BindingFlags.Instance);
                    vesselSituationsGetExperimentSituationMethod = vesselSituationsType.GetMethod("GetExperimentSituation", BindingFlags.Public | BindingFlags.Instance, null, new[] { experimentInfoType }, null);
                    subjectExpInfoProperty = subjectDataType.GetProperty("ExpInfo", BindingFlags.Public | BindingFlags.Instance);
                    subjectIdProperty = subjectDataType.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
                    expInfoExperimentIdProperty = experimentInfoType.GetProperty("ExperimentId", BindingFlags.Public | BindingFlags.Instance);

                    if (kerbalismDataMethod != null &&
                        driveGetDrivesMethod != null &&
                        driveFileDriveMethod != null &&
                        driveRecordFileMethod != null &&
                        driveSendMethod != null &&
                        driveFilesField != null &&
                        scienceDbGetExperimentInfoMethod != null &&
                        scienceDbGetSubjectDataMethod != null &&
                        vesselSituationsProperty != null &&
                        vesselSituationsGetExperimentSituationMethod != null &&
                        subjectExpInfoProperty != null &&
                        subjectIdProperty != null &&
                        expInfoExperimentIdProperty != null)
                    {
                        kerbalismReflectionFailed = false;
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Probalism] Beacon Kerbalism reflection failed: " + ex.Message);
            }

            return false;
        }
    }
}
