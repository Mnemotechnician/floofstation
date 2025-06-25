using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Content.Server._Floof.NebulaComputing.Components;
using Content.Server._Floof.NebulaComputing.VirtualCPU;
using Content.Server._Floof.NebulaComputing.VirtualCPU.Assembly;
using Content.Server.GameTicking;
using Content.Shared._Floof.NebulaComputing.UI;
using Content.Shared.GameTicking;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.CPUJob.JobQueues.Queues;
using Robust.Shared.Utility;


namespace Content.Server._Floof.NebulaComputing.Systems;


public sealed partial class ProgrammableComputerHostSystem : EntitySystem
{
    [Robust.Shared.IoC.Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Robust.Shared.IoC.Dependency] private readonly SharedPopupSystem _popup = default!;
    [Robust.Shared.IoC.Dependency] private readonly SharedContainerSystem _container = default!;

    private readonly VirtualCPUExecutorThread _executorThread = new();
    private readonly VirtualCPUAsmCompilerThread _asmCompilerThread = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<PostGameMapLoad>(OnGameStart);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnGameRestart);

        SubscribeLocalEvent<CPUComponent, ComponentShutdown>(OnCPUShuttingDown);
        SubscribeLocalEvent<CPUComponent, EntGotRemovedFromContainerMessage>(OnCPURemoved);
        SubscribeLocalEvent<ProgrammableComputerHostComponent, AfterInteractEvent>(OnComputerClicked);
        SubscribeLocalEvent<ProgrammableComputerHostComponent, GetVerbsEvent<InteractionVerb>>(OnGetComputerVerbs);

        InitializePortsHandling();
        InitializeUI();
    }

    public override void Update(float frameTime)
    {
        _asmCompilerThread.Update();

        UpdatePorts(frameTime);
        UpdateUI(frameTime);
    }

    private void OnGameStart(PostGameMapLoad ev)
    {
        if (_executorThread.Running || _asmCompilerThread.Running)
        {
            Log.Warning("Executor or asm thread is already running? Attempting to stop it.");
            _executorThread.Stop();
            _asmCompilerThread.Stop();
        }

        _executorThread.Start();
        _asmCompilerThread.Start();
    }

    private void OnGameRestart(RoundRestartCleanupEvent ev)
    {
        _executorThread.Stop();
        _asmCompilerThread.Stop();
    }

    private void OnCPUShuttingDown(Entity<CPUComponent> ent, ref ComponentShutdown args)
    {
        // This should ideally never happen
        if (ent.Comp.Executor is { } executor)
            _executorThread.RemoveProcessedCPU(executor);
    }

    private void OnCPURemoved(Entity<CPUComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        if (ent.Comp.Executor is { } executor)
            _executorThread.RemoveProcessedCPU(executor);
    }

    private void OnComputerClicked(Entity<ProgrammableComputerHostComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.CanReach)
            return;

        TryOpenUI(args.User, ent);
        args.Handled = true;
    }

    private void OnGetComputerVerbs(Entity<ProgrammableComputerHostComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        var verb = new InteractionVerb()
        {
            Act = () => TryOpenUI(user, ent),
            Text = Loc.GetString("programmable-computer-verb-open"),
            IconEntity = GetNetEntity(ent),
            Priority = 5
        };
        args.Verbs.Add(verb);
    }

    /// <summary>
    ///     Tries to set up the computer memory and other things.
    /// </summary>
    /// <param name="ent"></param>
    /// <param name="error"></param>
    /// <param name="resetNonPersistent">Whether to reset non-persistent memory (even if it's already set up).</param>
    /// <param name="resetPersistent">Whether to reset persistent memory (even if it's already set up).</param>
    public bool TrySetUpComputer(
        Entity<ProgrammableComputerHostComponent> ent,
        [NotNullWhen(false)] out string? error,
        bool resetNonPersistent = false,
        bool resetPersistent = false
    )
    {
        bool TryGetContainerSlot(string name, [NotNullWhen(true)] out EntityUid? result)
        {
            result = null;
            if (!_container.TryGetContainer(ent, name, out var maybeSlot))
                return false;

            result = (maybeSlot as ContainerSlot)?.ContainedEntity;
            return result is not null;
        }

        // Step 1. Resolve the CPU specs
        if (TryComp<CPUComponent>(ent, out var builtInCPU)
            && TryComp<MemoryComponent>(ent, out var builtInStack)
            && builtInStack.Kind.HasFlag(MemoryType.CPUOperationStack))
        {
            ent.Comp.CPU = (ent.Owner, builtInCPU, builtInStack);
        }
        else
        {
            if (!TryGetContainerSlot(ProgrammableComputerHostComponent.CPUContainerName, out var externalCPUEnt))
            {
                error = "programmable-computer-error-no-cpu";
                return false;
            }

            if (!TryComp<CPUComponent>(externalCPUEnt, out var externalCPU)
                || !TryComp<MemoryComponent>(externalCPUEnt, out var externalStack)
                || !externalStack.Kind.HasFlag(MemoryType.CPUOperationStack))
            {
                error = "programmable-computer-error-bad-cpu";
                return false;
            }

            ent.Comp.CPU = (externalCPUEnt.Value, externalCPU, externalStack);
        }

        // Step 2. Resolve the memory specs
        if (TryComp<MemoryComponent>(ent, out var builtInMemory) && builtInMemory.Kind.HasFlag(MemoryType.RandomAccess))
            ent.Comp.Memory = (ent.Owner, builtInMemory);
        else
        {
            if (!TryGetContainerSlot(ProgrammableComputerHostComponent.MemoryContainerName, out var externalMemoryEnt))
            {
                error = "programmable-computer-error-no-memory";
                return false;
            }

            // Praying an hoping the ItemSlots whitelist will prevent this from ever throwing an exception
            ent.Comp.Memory = (externalMemoryEnt.Value, Comp<MemoryComponent>(externalMemoryEnt.Value));
        }

        // Step 3. Resolve the storage specs
        if (TryComp<MemoryComponent>(ent, out var builtInStorage) && builtInStorage.Kind.HasFlag(MemoryType.Persistent))
            ent.Comp.Storage = (ent.Owner, builtInStorage);
        else
        {
            if (!TryGetContainerSlot(ProgrammableComputerHostComponent.CPUContainerName, out var externalStorageEnt))
            {
                error = "programmable-computer-error-no-storage";
                return false;
            }

            // See above
            ent.Comp.Storage = (externalStorageEnt.Value, Comp<MemoryComponent>(externalStorageEnt.Value));
        }

        // Step 4. Reset the memory if requested or necessary
        ref CPUMemoryCell[]?
            cpuStack = ref ent.Comp.CPU.Value.Comp2.StackData,
            memory = ref ent.Comp.Memory.Value.Comp.RandomAccessData,
            storage = ref ent.Comp.Storage.Value.Comp.PersistentData;

        ref var executor = ref ent.Comp.CPU.Value.Comp1.Executor;
        var firstRun = !ent.Comp.SetupDone;

        if (resetNonPersistent || firstRun || cpuStack is null || memory is null)
        {
            // The garbage collector will take care of the rest
            cpuStack = new CPUMemoryCell[ent.Comp.CPU.Value.Comp2.Capacity];
            memory = new CPUMemoryCell[ent.Comp.Memory.Value.Comp.Capacity];
        }

        ent.Comp.IOProvider ??= new VirtualCPUECSIOProvider(ent, this);

        if (executor is null || resetNonPersistent)
        {
            if (executor is {} oldExecutor)
                _executorThread.RemoveProcessedCPU(oldExecutor);

            firstRun = true;
            executor = new(
                new VirtualCPUECSDataProvider(ent.Comp),
                ent.Comp.IOProvider,
                cpuStack);
            executor.Halted = true;
            executor.InstructionRate = ent.Comp.CPU.Value.Comp1.InstructionRate;
            executor.ErrorHandler += (code, pos) =>
            {
                WriteLog(ent, $"[E] CPU encountered error: {code} @ 0x{pos:x8}");
            };

            _executorThread.AddProcessedCPU(executor);
        }

        if (resetPersistent || firstRun || storage is null)
            storage = new CPUMemoryCell[ent.Comp.Storage.Value.Comp.Capacity];

        SetupPorts(ent);

        ent.Comp.SetupDone = true;
        error = null;
        return true;
    }

    public bool TryOpenUI(EntityUid user, Entity<ProgrammableComputerHostComponent> computer)
    {
        if (!TrySetUpComputer(computer, out var error))
        {
            _popup.PopupEntity(Loc.GetString(error, ("uid", computer)), computer, PopupType.Medium);
            return false;
        }

        return _ui.TryOpenUi(computer.Owner, ProgrammableComputerUiKey.Key, user, false);
    }

    public void CompileAndSetAssembly(Entity<ProgrammableComputerHostComponent> ent, string code, bool toRun, bool saveCode = true)
    {
        WriteLog(ent, $"[I] Compiling... {code.Length} bytes. {(toRun ? "Will run the code afterwards." : "")}");

        if (saveCode)
            ent.Comp.AssemblerCode = code;

        ent.Comp.IsActivelyAssembling = true;
        _asmCompilerThread.EnqueueJob(ent, code, job =>
        {
            ent.Comp.IsActivelyAssembling = false;
            if (TerminatingOrDeleted(ent))
                return;

            // This should be resumed on the game thread, so this is fine
            if (job.Result is not { } result || ent.Comp.MemoryData is null)
            {
                WriteLog(ent, "[E] Compilation failed (unknown error)");
                return;
            }

            if (!result.success || result.code is null)
            {
                WriteLog(ent, $"[E] Compilation failed:");
                foreach (var error in job.Compiler.Errors ?? [])
                    WriteLog(ent, $"[E] {error}");
                return;
            }

            if (result.code.Length > ent.Comp.MemoryData.Length)
                WriteLog(ent, "[W] Program completed successfully, but the result is too long to fit in the memory! Result will be truncated; code integrity is not guaranteed.");

            MemCopy(ent.Comp.MemoryData, 0, result.code, 0);

            if (ent.Comp.CPU?.Comp1 is {} cpu)
            {
                cpu.Executor?.Reset(result.entryPoint);
                cpu.EntryPoint = result.entryPoint;

                WriteLog(ent, "Compilation successful!");

                if (toRun && cpu.Executor is {} executor)
                    _executorThread.AddProcessedCPU(executor);
            }
        });
    }

    private void MemCopy(CPUMemoryCell[] dst, int dstOffset, int[] src, int srcOffset)
    {
        var num = Math.Min(dst.Length - dstOffset, src.Length - srcOffset);
        // Fast memcopy when? C# doesn't allow it due to array type mismatch
        // Would need to do a reinterpret cast or something similarly unsafe, or change the compiler to use CPUMemoryCell
        for (var i = 0; i < num; ++i)
            dst[dstOffset + i].Int32 = src[srcOffset + i];
    }

    public void WriteLog(Entity<ProgrammableComputerHostComponent> ent, string data, bool trailingNewline = true)
    {
        if (ent.Comp.IOProvider is not { } provider)
            return;

        if (trailingNewline)
            data += '\n';

        provider.WriteConsoleOutput(data);
    }
}
