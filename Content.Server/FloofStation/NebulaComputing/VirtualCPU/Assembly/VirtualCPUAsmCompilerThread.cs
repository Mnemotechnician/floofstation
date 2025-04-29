using System.Diagnostics;
using System.Threading;
using Content.Server.FloofStation.NebulaComputing.VirtualCPU.Util;
using ThreadState = System.Threading.ThreadState;


namespace Content.Server.FloofStation.NebulaComputing.VirtualCPU.Assembly;


// TODO: code duplication? perhaps extract a base class
public sealed class VirtualCPUAsmCompilerThread
{
    public bool Running => _running && _executorThread?.ThreadState.HasFlag(ThreadState.Running) == true;
    private bool _running;

    private Thread? _executorThread;
    private List<AssemblerJob> _jobs = new(), _finishedJobs = new();

    private ISawmill _log = Logger.GetSawmill("virtual-cpu-asm-compiler");

    public void Start()
    {
        if (_running)
            Stop();

        _executorThread = new Thread(DoWorkSync)
        {
            IsBackground = true,
            Priority = ThreadPriority.BelowNormal
        };

        _running = true;
        _executorThread.Start();
    }

    public void Stop()
    {
        _running = false;
        _executorThread?.Interrupt();

        ConcurrencyUtils.Clear(ref _jobs);
        ConcurrencyUtils.Clear(ref _finishedJobs);
    }

    /// <summary>
    ///     Must be invoked on the game thread.
    /// </summary>
    public void Update()
    {
        var finishedJobs = ConcurrencyUtils.CopyAndClear(ref _finishedJobs);
        foreach (var job in finishedJobs)
            if (job.Result is {} result)
                job.Callback(result);
    }

    /// <summary>
    ///     Enqueues a compiler job. The callback will be invoked on the game thread.
    /// </summary>
    public void EnqueueJob(EntityUid target, string source, Action<VCPUAssemblyCompiler.Result> callback)
    {
        lock (_jobs)
            _jobs.Add(new(target, source, callback));
    }

    private void DoWorkSync()
    {
        while (_running) {
            if (!_running)
                return;

            try
            {
                ProcessStep();
                Thread.Sleep(50); // Arbitrary value to ensure we don't clog up the CPU
            }
            catch (ThreadInterruptedException)
            {
                return;
            }
            catch (Exception e)
            {
                _log.Error($"Caught exception while processing step: {e.Message}\n{e.StackTrace}");
                // Long sleep to avoid spamming the logs too much in case of repeated errors
                Thread.Sleep(5000);
            }
        }
    }

    private void ProcessStep()
    {
        var jobs = ConcurrencyUtils.Copy(ref _jobs);
        foreach (var job in jobs)
        {
            if (!_running)
                return;
        }
    }


    private class AssemblerJob(
        EntityUid ent,
        string source,
        Action<VCPUAssemblyCompiler.Result> callback)
    {
        public EntityUid Entity => ent;
        internal string Source => source;
        internal Action<VCPUAssemblyCompiler.Result> Callback => callback;

        internal VCPUAssemblyCompiler.Result? Result;
    }
}
