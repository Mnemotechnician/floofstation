using System.Diagnostics;
using System.Threading;
using Content.Server._Floof.NebulaComputing.VirtualCPU.Util;
using ThreadState = System.Threading.ThreadState;


namespace Content.Server._Floof.NebulaComputing.VirtualCPU.Cpu;


public sealed class VirtualCPUExecutorThread
{
    public const long TickDuration = 1000L;

    public bool Running => _running && _executorThread?.ThreadState.HasFlag(ThreadState.Running) == true;
    private bool _running;

    private Thread? _executorThread;
    private List<VirtualCPU> _cpus = new();

    private ISawmill _log = Logger.GetSawmill("virtual-cpu");

    public void Start()
    {
        if (_running)
            Stop();

        _executorThread = new Thread(DoWorkSync)
        {
            Name = "VCPU Executor",
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

        lock (_cpus)
            _cpus.Clear();
    }

    public void AddProcessedCPU(VirtualCPU cpu)
    {
        lock (_cpus)
            if (!_cpus.Contains(cpu))
                _cpus.Add(cpu);
    }

    public void RemoveProcessedCPU(VirtualCPU cpu)
    {
        lock (_cpus)
            _cpus.Remove(cpu);
    }

    // TODO this is terrible. Use a RW lock?
    public bool IsRunning(VirtualCPU cpu)
    {
        lock (_cpus)
            return _cpus.Contains(cpu);
    }

    private void DoWorkSync()
    {
        while (true)
        {
            if (!_running)
                return;

            try
            {
                var stopwatch = new Stopwatch();
                ProcessStep();
                stopwatch.Stop();

                if (stopwatch.ElapsedMilliseconds > TickDuration)
                {
                    // This should IDEALLY never happen because virtual CPUs run on an extremely low frequency
                    _log.Warning($"Can't keep up: spent {stopwatch.ElapsedMilliseconds}ms in a {TickDuration}ms tick!");
                    Thread.Sleep(100);
                }
                else
                {
                    Thread.Sleep((int) (TickDuration - stopwatch.ElapsedMilliseconds));
                }
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
        var cpus = ConcurrencyUtils.Copy(ref _cpus);
        foreach (var cpu in cpus)
        {
            if (!_running)
                return;

            if (cpu.Halted)
                continue;

            // TODO if Reset() is called in the middle, we may get fucked
            cpu.ProcessTicks(cpu.InstructionRate);
        }
    }
}
