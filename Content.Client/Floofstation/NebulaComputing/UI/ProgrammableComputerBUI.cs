using Content.Shared.FloofStation.NebulaComputing.UI.Events;
using Robust.Client.UserInterface;


namespace Content.Client.Floofstation.NebulaComputing.UI;


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
        }

        Window.Open();
    }

    private void OnAction(PCActionRequest.Action action) => SendMessage(new PCActionRequest { Act = action });

    private void OnAssemblerRun(string code) => SendMessage(new AssemblerRunRequest { Code = code });
}
