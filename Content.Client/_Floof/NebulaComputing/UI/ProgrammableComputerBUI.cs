using Content.Shared._Floof.NebulaComputing.UI.Events;
using Robust.Client.UserInterface;
using Serilog;


namespace Content.Client._Floof.NebulaComputing.UI;


public sealed class ProgrammableComputerBUI : BoundUserInterface
{
    [ViewVariables] public ProgrammableComputerWindow? Window;

    public ProgrammableComputerBUI(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        if (Window == null)
        {
            Window = this.CreateWindow<ProgrammableComputerWindow>();
            Window.OnAction += OnAction;
            Window.Assembler.OnRunRequested += OnAssemblerRun;
        }

        Window.Open();
    }

    private void OnAction(PCActionRequest.Action action) => SendMessage(new PCActionRequest { Act = action });

    private void OnAssemblerRun(string code, bool toRun) => SendMessage(new AssemblerRunRequest { Code = code, Run = toRun });
}
