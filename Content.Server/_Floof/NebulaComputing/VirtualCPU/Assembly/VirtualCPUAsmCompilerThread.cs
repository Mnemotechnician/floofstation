using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server._Floof.NebulaComputing.VirtualCPU.Util;
using Serilog;
using ThreadState = System.Threading.ThreadState;


namespace Content.Server._Floof.NebulaComputing.VirtualCPU.Assembly;


// TODO: code duplication? perhaps extract a base class
public sealed class VirtualCPUAsmCompilerThread
{
    public const int MaxTimeToCompile = 1000;

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
            Name = "VCPU Asm Compiler",
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
            job.Callback(job);
    }

    /// <summary>
    ///     Enqueues a compiler job. The callback will be invoked on the game thread.
    /// </summary>
    public void EnqueueJob(EntityUid ent, string source, Action<AssemblerJob> callback)
    {
        lock (_jobs)
        {
            // Cancel all pending jobs on this entity
            _jobs.RemoveAll(it => it.Entity == ent);
            _jobs.Add(new(ent, source, callback));
        }
    }

    /// <summary>
    ///     Stops all jobs for the given entity.
    /// </summary>
    public void StopAllJobs(EntityUid ent)
    {
        AssemblerJob[] jobs;
        lock (_jobs)
        {
            jobs = _jobs.Where(it => it.Entity == ent).ToArray();
            _jobs.RemoveAll(it => it.Entity == ent);
        }

        foreach (var job in jobs)
        {
            job.Result = null;
            job.Compiler.Error("Compilation has been aborted.");
        }

        lock (_finishedJobs)
            _finishedJobs.AddRange(jobs);
    }

    private void DoWorkSync()
    {
        try
        {
            while (_running && ProcessStep()) { }
        }
        catch (ThreadInterruptedException e)
        {
            return; // Finish gracefully
        }
    }

    private bool ProcessStep()
    {
        var jobs = ConcurrencyUtils.CopyAndClear(ref _jobs);
        foreach (var job in jobs)
        {
            if (!_running)
                return false;

            try
            {
                // This is... kinda bad? We do this to limit the maximum amount of time a single compilation can take.
                // It does lead to us spawning new virtual threads though. Wonder if there's a better way.
                var task = Task.Run(() => job.Compiler.Compile(job.Source));
                if (!task.IsCompleted)
                    task.Wait(MaxTimeToCompile);

                #pragma warning disable RA0004
                job.Result = task.IsCompletedSuccessfully
                    ? task.Result
                    : VCPUAssemblyCompiler.Result.Failure;
                #pragma warning restore RA0004

                lock (_finishedJobs)
                    _finishedJobs.Add(job);

                Thread.Sleep(50); // Arbitrary value to ensure we don't clog up the CPU and check for interrupts
            }
            catch (ThreadInterruptedException) { throw; }
            catch (Exception e)
            {
                _log.Error($"Caught exception while processing step for {job.Entity}: {e.Message}\n{e.StackTrace}");
                // Long sleep to avoid spamming the logs too much in case of repeated errors
                Thread.Sleep(5000);
            }
        }

        return true;
    }


    public sealed class AssemblerJob(
        EntityUid ent,
        string source,
        Action<AssemblerJob> callback)
    {
        public EntityUid Entity => ent;
        public string Source => source;
        public VCPUAssemblyCompiler Compiler = new();
        internal Action<AssemblerJob> Callback => callback;

        public VCPUAssemblyCompiler.Result? Result;
    }
}
