using Content.Server._Floof.NebulaComputing.Components;
using Content.Server._Floof.NebulaComputing.VirtualCPU;
using Content.Server._Floof.NebulaComputing.VirtualCPU.Cpu;
using Content.Shared._Floof.NebulaComputing.UI;
using Content.Shared._Floof.NebulaComputing.Util;


namespace Content.Server._Floof.NebulaComputing.Systems;

/// <summary>
///     Provides a bridge between a virutal CPU I/O and the entity-component system.
/// </summary>
[Serializable]
public sealed class VirtualCPUECSIOProvider(Entity<ProgrammableComputerHostComponent> ent, ProgrammableComputerHostSystem system) : VirtualCPUIOProvider
{
    /// <summary>How many inputs a port can queue up before they start overwriting each other.</summary>
    /// <remarks>This is very unrealistic, but it's a necessary sacrifice because CPUs aren't updated on each game tick.</remarks>
    public const int PortQueueSize = 16;

    [ViewVariables]
    private CharCircularQueue _consoleOutput = new(ProgrammableComputerBUIState.MaxOutputChars);
    [ViewVariables]
    private IntCircularQueue _consoleInputKeyCodes = new(ProgrammableComputerBUIState.MaxInputKeyCodes);

    [ViewVariables]
    private Dictionary<int, IntCircularQueue> _inputPortQueues = new();
    [ViewVariables]
    private Dictionary<int, IntCircularQueue> _outputPortQueues = new();

    // The below methods may be called from different threads. I should be concerned about thread safety.
    public override bool TryWrite(int port, CPUMemoryCell message)
    {
        switch (port)
        {
            case ConsolePort:
                lock (_consoleOutput)
                    _consoleOutput.Enqueue((char) message.Int32);
                return true;

            case DiskPort:
                // Disk IO is currently not implemented.
                return false;

            case >=FirstPinPort:
            {
                var portNumber = port - FirstPinPort;
                if (portNumber >= ent.Comp.OutputPorts)
                    throw new CPUExecutionException(CPUErrorCode.InvalidPort);

                lock (_outputPortQueues)
                {
                    var queue = GetPortQueueOrDefault(_outputPortQueues, portNumber);
                    if (queue.IsFull())
                        return false; // Make the cpu wait

                    queue.Enqueue(message.Int32);
                    system.MarkForPortsUpdate(ent);
                }

                return true;
            }

            default:
                throw new CPUExecutionException(CPUErrorCode.InvalidPort);
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

            // Ports 21..24 - check if pin 1..4 has inputs
            case >= FirstCheckInputPort:
            {
                var portNumber = port - FirstCheckInputPort;
                if (portNumber >= ent.Comp.InputPorts)
                    throw new CPUExecutionException(CPUErrorCode.InvalidPort);

                lock (_inputPortQueues)
                {
                    var hasInput = _inputPortQueues.TryGetValue(portNumber, out var queue) && !queue.IsEmpty();
                    return (true, hasInput ? CPUMemoryCell.One : CPUMemoryCell.Zero);
                }
            }

            // Ports 11.14 - read pins 1..4
            case >= FirstPinPort:
            {
                var portNumber = port - FirstPinPort;
                if (portNumber >= ent.Comp.InputPorts)
                    throw new CPUExecutionException(CPUErrorCode.InvalidPort);

                lock (_inputPortQueues)
                {
                    if (!_inputPortQueues.TryGetValue(portNumber, out var queue) || queue.IsEmpty())
                        return (false, CPUMemoryCell.Zero);

                    return (true, CPUMemoryCell.FromInt32(queue.Dequeue()));
                }
            }

            default:
                throw new CPUExecutionException(CPUErrorCode.InvalidPort);
        }
    }

    // Api methods. They may be slow, but they are thread-safe. Hopefully.
    public CharCircularQueue GetConsoleOutput()
    {
        lock (_consoleOutput)
            return _consoleOutput;
    }

    /// <summary>
    ///     Write a key code into the console output queue of the computer, showing it on the terminal.
    /// </summary>
    public void WriteConsoleOutput(char c)
    {
        lock (_consoleOutput)
            _consoleOutput.Enqueue(c);
    }

    /// <summary>
    ///     Write a string into the console output queue of the computer, showing it on the terminal.
    /// </summary>
    public void WriteConsoleOutput(string str)
    {
        lock (_consoleOutput)
            _consoleOutput.Append(str);
    }

    [Access(typeof(ProgrammableComputerHostSystem), Other = AccessPermissions.None)]
    public Dictionary<int, int[]> GetAndClearPortOutputs()
    {
        lock (_outputPortQueues)
        {
            var res = new Dictionary<int, int[]>(_outputPortQueues.Count);
            foreach (var (port, queue) in _outputPortQueues)
            {
                res[port] = queue.ToArray();
                queue.ClearFast();
            }

            return res;
        }
    }

    /// <summary>
    ///     Write a key code to the input queue for the computer to read.
    /// </summary>
    public void WriteConsoleInput(int keyCode)
    {
        lock (_consoleInputKeyCodes)
            _consoleInputKeyCodes.Enqueue(keyCode);
    }


    /// <summary>
    ///     Write a string to the input queue for the computer to read.
    /// </summary>
    public void WriteConsoleInput(string text)
    {
        lock (_consoleInputKeyCodes)
            foreach (var c in text)
                _consoleInputKeyCodes.Enqueue(c);
    }

    /// <summary>
    ///     Try to write into a specific port. Note: this uses "raw" port numbers, which does not include console/disk ports (everything below FirstPinPort).
    /// </summary>
    public bool TryWritePortInput(int port, CPUMemoryCell data)
    {
        if (port < 0 || port >= ent.Comp.InputPorts)
            return false;

        lock (_inputPortQueues)
            GetPortQueueOrDefault(_inputPortQueues, port).Enqueue(data.Int32);

        return true;
    }

    /// <summary>
    ///     Try to get a queue for a specific port. Note: this uses "raw" port numbers, which does not include console/disk ports (everything below FirstPinPort).
    /// </summary>
    private IntCircularQueue GetPortQueueOrDefault(Dictionary<int, IntCircularQueue> queues, int index)
    {
        if (!queues.TryGetValue(index, out var queue))
            queues[index] = queue = new(PortQueueSize);

        return queue;
    }
}
