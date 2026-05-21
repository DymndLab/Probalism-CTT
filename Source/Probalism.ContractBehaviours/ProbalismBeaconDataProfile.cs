namespace Probalism.ContractBehaviours
{
    public class ProbalismBeaconDataProfile : PartModule
    {
        [KSPField]
        public string packageId = string.Empty;

        [KSPField]
        public string packageName = string.Empty;

        [KSPField]
        public double dataSize = 0.03;

        [KSPField]
        public double packageMass = 0.04;

        [KSPField]
        public double packageCost = 25;
    }
}
