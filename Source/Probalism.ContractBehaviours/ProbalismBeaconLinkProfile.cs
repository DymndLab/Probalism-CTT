namespace Probalism.ContractBehaviours
{
    public class ProbalismBeaconLinkProfile : PartModule
    {
        [KSPField]
        public string profileId = string.Empty;

        [KSPField]
        public string profileName = string.Empty;

        [KSPField]
        public double antennaPower = 250000;

        [KSPField]
        public double packetSize = 0.00075;

        [KSPField]
        public double packetResourceCost = 0.006;

        [KSPField]
        public double profileMass = 0.02;

        [KSPField]
        public double profileCost = 25;
    }
}
