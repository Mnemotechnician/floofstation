using Content.Server.FloofStation.NebulaComputing.Components;
using Content.Server.FloofStation.NebulaComputing.VirtualCPU;
using Content.Shared.FloofStation.NebulaComputing.UI;
using Content.Shared.FloofStation.NebulaComputing.Util;


namespace Content.Server.FloofStation.NebulaComputing.Systems;

/// <summary>
///     Provides a bridge between a virutal CPU I/O and the entity-component system.
/// </summary>
[Serializable]
public sealed class VirtualCPUECSIOProvider(ProgrammableComputerHostComponent comp, ProgrammableComputerHostSystem system) : VirtualCPUIOProvider
{
    /// <summary>How many inputs a port can queue up before they start overwriting each other.</summary>
    /// <remarks>This is very unrealistic, but it's a necessary sacrifice because CPUs aren't updated on each game tick.</remarks>
    public const int PortQueueSize = 16;

    private CircularQueue<char> _consoleOutput = new(ProgrammableComputerBUIState.MaxOutputChars);
    private CircularQueue<int> _consoleInputKeyCodes = new(ProgrammableComputerBUIState.MaxInputKeyCodes);

    private Dictionary<int, CircularQueue<CPUMemoryCell>> _inputPortQueues = new();
    private Dictionary<int, CircularQueue<CPUMemoryCell>> _outputPortQueues = new();

    // The below methods may be called from different threads. I should be concerned about thread safety.
    public override bool TryWrite(int port, CPUMemoryCell message)
    {
        switch (port)
        {
            case ConsolePort:
                lock (_consoleOutput)
                    _consoleOutput.Enqueue((char)message.Int32);
                return true;

            case DiskPort:
                // Disk IO is currently not implemented.
                return false;

            default:
                var portNumber = port - FirstPinPort;
                if (portNumber < 0 || portNumber >= comp.OutputPorts)
                    throw new CPUExecutionException(CPUErrorCode.InvalidPort);

                lock (_outputPortQueues)
                {
                    var queue = GetPortQueueOrDefault(_outputPortQueues, portNumber);
                    if (queue.IsFull())
                        return false; // Make the cpu wait

                    queue.Enqueue(message);
                }

                return true;

        }
    }

    public override (bool success, CPUMemoryCell data) TryRead(int port)
    {
        switch (port)
        {
            case ConsolePort:
                lock (_consoleInputKeyCodes)
                {
                    if (_consoleInputKeyCodes.IsEmpty())
                        return (false, CPUMemoryCell.Zero);

                    return (true, CPUMemoryCell.FromInt32(_consoleInputKeyCodes.Dequeue()));
                }

            case DiskPort:
                // Disk IO is currently not implemented.
                return (false, CPUMemoryCell.Zero);

            default:
                var portNumber = port - FirstPinPort;
                if (portNumber < 0 || portNumber >= comp.InputPorts)
                    throw new CPUExecutionException(CPUErrorCode.InvalidPort);

                lock (_consoleInputKeyCodes)
                {
                    if (!_inputPortQueues.TryGetValue(portNumber, out var queue) || queue.IsEmpty())
                        return (false, CPUMemoryCell.Zero);

                    return (true, queue.Dequeue());
                }
        }
    }

    // Api methods. They may be slow, but they are thread-safe. Hopefully.
    public string GetConsoleOutput()
    {
        lock (_consoleOutput)
            return _consoleOutput.AsString();
    }

    [Access(typeof(ProgrammableComputerHostSystem), Other = AccessPermissions.None)]
    public Dictionary<int, CPUMemoryCell[]> GetAndClearPortOutputs()
    {
        lock (_outputPortQueues)
        {
            var res = new Dictionary<int, CPUMemoryCell[]>(_outputPortQueues.Count);
            foreach (var (port, queue) in _outputPortQueues)
            {
                res[port] = queue.ToArray();
                queue.ClearFast();
            }

            return res;
        }
    }

    public void WriteConsoleInput(int keyCode)
    {
        lock (_consoleInputKeyCodes)
            _consoleInputKeyCodes.Enqueue(keyCode);
    }

    public bool TryWritePinInput(int port, CPUMemoryCell data)
    {
        if (port < 0 || port >= comp.InputPorts)
            return false;

        lock (_inputPortQueues)
            GetPortQueueOrDefault(_inputPortQueues, port).Enqueue(data);

        return true;
    }

    private CircularQueue<T> GetPortQueueOrDefault<T>(Dictionary<int, CircularQueue<T>> queues, int index)
    {
        if (!queues.TryGetValue(index, out var queue))
            queues[index] = queue = new(PortQueueSize);
        return queue;
    }
}
