namespace Warp.Workers.Scheduling
{
    /// <summary>Cluster mode: Relay provisions workers, so this does nothing (spec §7).</summary>
    public class ExternalProvisioner : IWorkerProvisioner
    {
        public void EnsureWorkers(int target) { }
        public int LiveWorkerCount() => 0;
        public void Shutdown() { }
        public bool IsSingleHost => false;   // the external system may place workers anywhere
    }
}
