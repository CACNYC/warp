namespace Warp.Workers.Scheduling
{
    /// <summary>
    /// Strategy for keeping the worker pool populated. Local mode spawns
    /// processes; cluster mode is a no-op because Relay provisions workers (spec §7).
    /// </summary>
    public interface IWorkerProvisioner
    {
        /// <summary>Ensure ~target workers exist. Implementation decides how.</summary>
        void EnsureWorkers(int target);

        /// <summary>How many workers this provisioner currently believes are alive.</summary>
        int LiveWorkerCount();

        /// <summary>Tear down any workers this provisioner owns.</summary>
        void Shutdown();

        /// <summary>
        /// True when every worker in the pool necessarily runs on this machine. The
        /// Scheduler uses this to decide whether host blacklisting is meaningful:
        /// with one host there is no other node to fall back on, so excluding it
        /// would just stall the run.
        /// </summary>
        bool IsSingleHost { get; }
    }
}
