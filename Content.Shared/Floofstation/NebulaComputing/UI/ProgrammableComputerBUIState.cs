using Content.Shared.FloofStation.NebulaComputing.Util;
using Robust.Shared.Serialization;


namespace Content.Shared.FloofStation.NebulaComputing.UI;


[Serializable, NetSerializable]
public sealed class ProgrammableComputerBUIState : BoundUserInterfaceState
{
    public const int MaxOutputChars = 2000, MaxInputKeyCodes = 100;

    public bool IsRunning;
    public CircularQueue<char> ConsoleOutput;

    public ProgrammableComputerBUIState(bool isRunning, CircularQueue<char> consoleOutput)
    {
        IsRunning = isRunning;
        ConsoleOutput = consoleOutput;
    }
}
