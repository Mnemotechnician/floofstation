using System.Diagnostics.CodeAnalysis;
using Content.Server.FloofStation.NebulaComputing.Components;
using Content.Server.FloofStation.NebulaComputing.VirtualCPU;
using Content.Server.GameTicking;
using Content.Shared.FloofStation.NebulaComputing.UI;
using Content.Shared.GameTicking;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Verbs;
using Robust.Shared.Containers;


namespace Content.Server.FloofStation.NebulaComputing.Systems;


public sealed partial class ProgrammableComputerHostSystem : EntitySystem
{
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    private readonly VirtualCPUExecutorThread executorThread = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<PostGameMapLoad>(OnGameStart);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnGameRestart);
        SubscribeLocalEvent<ProgrammableComputerHostComponent, AfterInteractEvent>(OnComputerClicked);
        SubscribeLocalEvent<ProgrammableComputerHostComponent, GetVerbsEvent<InteractionVerb>>(OnGetComputerVerbs);

        InitializePortsHandling();
        InitializeUI();
    }

    private void OnGameStart(PostGameMapLoad ev)
    {
        if (executorThread.Running)
        {
            Log.Warning("Executor thread is already running? Attempting to stop it.");
            executorThread.Stop();
        }

        executorThread.Start();
    }

    private void OnGameRestart(RoundRestartCleanupEvent ev)
    {
        executorThread.Stop();
    }

    private void OnComputerClicked(Entity<ProgrammableComputerHostComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
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
                executorThread.RemoveProcessedCPU(oldExecutor);

            firstRun = true;
            executor = new(
                new VirtualCPUECSDataProvider(ent.Comp),
                ent.Comp.IOProvider,
                cpuStack);
            executor.Halted = true;

            executorThread.AddProcessedCPU(executor);
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
}
