using Content.Server._Floof.NebulaComputing.Components;
using Content.Shared._Floof.NebulaComputing.UI;
using Content.Shared._Floof.NebulaComputing.UI.Events;
using Content.Shared.Popups;
using Content.Shared.UserInterface;


namespace Content.Server._Floof.NebulaComputing.Systems;


public sealed partial class ProgrammableComputerHostSystem
{
    public static int MaxProgramLength = 100000;
    public static TimeSpan UiUpdateInterval = TimeSpan.FromMilliseconds(200);

    private void InitializeUI()
    {
        SubscribeLocalEvent<ProgrammableComputerHostComponent, ActivatableUIOpenAttemptEvent>(OnUIOpenAttempt);

        Subs.BuiEvents<ProgrammableComputerHostComponent>(ProgrammableComputerUiKey.Key,
        subs =>
        {
            subs.Event<PCActionRequest>(OnActionRequest);
            subs.Event<AssemblerRunRequest>(OnAssemblerRunRequest);
        });
    }

    private void UpdateUI(float frameTime)
    {
        var query = EntityQueryEnumerator<ProgrammableComputerHostComponent, UserInterfaceComponent>();

        while (query.MoveNext(out var ent, out var host, out var ui))
        {
            // Don't update consoles of unused computers, and don't update too often
            if (ui.Actors.Count == 0 || _timing.CurTime < host.NextBUIUpdate)
                continue;

            host.NextBUIUpdate = _timing.CurTime + UiUpdateInterval;

            var executor = host.CPU?.Comp1?.Executor;
            var isRunning = executor?.Halted == false && _executorThread.IsRunning(executor);
            var state = new ProgrammableComputerBUIState(
                isRunning,
                host.IOProvider?.GetConsoleOutput() ?? new(0));

            state.AssemblerCode = host.AssemblerCode;
            state.IsActivelyAssembling = host.IsActivelyAssembling;

            _ui.SetUiState((ent, ui), ProgrammableComputerUiKey.Key, state);
        }

        query.Dispose();
    }

    private void OnUIOpenAttempt(Entity<ProgrammableComputerHostComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (!TryOpenUI(args.User, ent, dryRun: true))
            args.Cancel();
    }

    private void OnActionRequest(Entity<ProgrammableComputerHostComponent> ent, ref PCActionRequest args)
    {
        if (ent.Comp.CPU?.Comp1 is not { } cpu || cpu.Executor is not {} executor)
            return;

        switch (args.Act)
        {
            // Start does nothing if the CPU is already running
            case PCActionRequest.Action.Start:
                if (executor.Halted)
                    executor.Reset(cpu.EntryPoint);

                _executorThread.AddProcessedCPU(executor);
                break;

            case PCActionRequest.Action.Restart:
                executor.Reset(cpu.EntryPoint);
                _executorThread.AddProcessedCPU(executor);
                break;

            // Reset also stops any ongoing assemblies
            case PCActionRequest.Action.Reset:
                _executorThread.RemoveProcessedCPU(executor);
                _asmCompilerThread.StopAllJobs(ent);
                executor.Reset(cpu.EntryPoint);

                break;
        }
    }

    private void OnAssemblerRunRequest(Entity<ProgrammableComputerHostComponent> ent, ref AssemblerRunRequest args)
    {
        if (args.Code.Length > MaxProgramLength)
        {
            WriteLog(ent, $"[E] Input code is too long (max: {MaxProgramLength}!");
            return;
        }

        CompileAndSetAssembly(ent, args.Code, args.Run);
    }

    /// <summary>
    ///     Try to open programmable computer ui. If dryRun is true, only do the preparations needed to open a ui, but do not actually invoke the ui system.
    /// </summary>
    public bool TryOpenUI(EntityUid user, Entity<ProgrammableComputerHostComponent> computer, bool dryRun = false)
    {
        if (!TrySetUpComputer(computer, out var error))
        {
            _popup.PopupEntity(Loc.GetString(error, ("uid", computer)), computer, PopupType.Medium);
            return false;
        }

        return dryRun || _ui.TryOpenUi(computer.Owner, ProgrammableComputerUiKey.Key, user, false);
    }
}
