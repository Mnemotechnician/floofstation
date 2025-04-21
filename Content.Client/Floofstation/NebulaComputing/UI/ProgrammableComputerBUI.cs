using Robust.Client.UserInterface;


namespace Content.Client.Floofstation.NebulaComputing.UI;


public sealed class ProgrammableComputerBUI : BoundUserInterface
{
    [ViewVariables] public ProgrammableComputerWindow? Window;

    public ProgrammableComputerBUI(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        if (Window == null)
            Window = this.CreateWindow<ProgrammableComputerWindow>();

        Window.Open();
    }
}
