using Content.Server.FloofStation.NebulaComputing.Components;
using Content.Shared.FloofStation.NebulaComputing.UI;
using Content.Shared.FloofStation.NebulaComputing.UI.Events;


namespace Content.Server.FloofStation.NebulaComputing.Systems;


public sealed partial class ProgrammableComputerHostSystem
{
    private void InitializeUI()
    {
        Subs.BuiEvents<ProgrammableComputerHostComponent>(ProgrammableComputerUiKey.Key,
        subs =>
        {
            subs.Event<PCActionRequest>(OnActionRequest);
            subs.Event<AssemblerRunRequest>(OnAssemblerRunRequest);
        });
    }

    private void OnActionRequest(Entity<ProgrammableComputerHostComponent> ent, ref PCActionRequest args)
    {
        // TODO
    }

    private void OnAssemblerRunRequest(Entity<ProgrammableComputerHostComponent> ent, ref AssemblerRunRequest args)
    {
        // TODO
    }
}
