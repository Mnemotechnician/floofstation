using System.Threading.Tasks;


namespace Content.Server.FloofStation.SpaceComputer.VirtualCPU;

using IS = InstructionSet;

// Shut up! If I follow all of those, I will never finish writing this.
// ReSharper disable BadControlBracesLineBreaks
// ReSharper disable InconsistentNaming
// ReSharper disable MissingLinebreak

/// <summary>
///     A single computing unit.
/// </summary>
public sealed class VirtualCPU(
    VirtualCPUDataProvider dataProvider,
    VirtualCPUIOProvider ioProvider,
    CPUMemoryCell[] operationStack)
{
    public VirtualCPUDataProvider DataProvider = dataProvider;
    public VirtualCPUIOProvider IOProvider = ioProvider;
    /// <summary>
    ///     A function to handle errors in the CPU.
    ///     Receives the error code and program counter as parameters.
    /// </summary>
    public event Action<CPUErrorCode, int>? ErrorHandler;
    /// <summary>
    ///     A function to debug the CPU. Invoked on each tick with the current opcode as the parameter.
    ///     If it returns true, the processor waits on the instruction until it returns false.
    /// </summary>
    public Func<IS, bool>? Debugger;

    private CPUMemoryCell[] _operationStack = operationStack;
    private int _operationStackTop = 0;
    private int _operationStackBottom = 0;

    public int ProgramCounter { get; set; }

    public bool Halted { get; private set; } = true;
    /// <summary>
    ///     If set to true during the execution of an instruction,
    ///     the CPU will not proceed and will resume from the same instruction later.
    /// </summary>
    public bool Waiting { get; private set; }

    /// <summary>
    ///     Resets the CPU and sets the program counter to the specified value.
    /// </summary>
    public void Reset(int pc)
    {
        Halted = Waiting = false;
        ProgramCounter = pc;
        _operationStackBottom = _operationStackTop = _operationStack.Length;
    }

    public void ProcessTicks(int ticks)
    {
        while (ticks > 0 && !Halted)
        {
            Waiting = false;

            var previousPC = ProgramCounter;
            try {
                ticks -= ExecuteInstruction();
            } catch (CPUExecutionException e) {
                Halted = true;
                ErrorHandler?.Invoke(e.ErrorCode, previousPC);

                // The error handler can in theory reset the halted state and continue execution
                if (Halted)
                    return;
            }

            // If the CPU is waiting, doesn't make any sense to process further. It means the CPU is waiting for something like player input,
            // Which is obviously not going to come out of the blue.
            // Also, we reset the PC of the CPU, So that on the next call of this method it executes the same instruction again.
            if (Waiting)
            {
                ProgramCounter = previousPC;
                return;
            }
        }
    }

    /// <summary>
    ///     Executes the instruction at the program counter and increases it respectively.
    ///     Returns the instruction cost.
    /// </summary>
    public int ExecuteInstruction()
    {
        var nextOpCode = ReadNext().Int32;
        if (nextOpCode == (int) IS.HALT)
        {
            Halted = true;
            return 1;
        }

        if (nextOpCode is < 0 or > (int) IS.MAX_EXCEPT_HALT)
            throw new CPUExecutionException(CPUErrorCode.IllegalInstruction);

        // Debug
        if (Debugger is not null && Debugger.Invoke((IS) nextOpCode))
        {
            Waiting = true;
            return 1;
        }

        switch ((IS) nextOpCode)
        {
            case IS.NOP:
                return 1;

            case IS.HALT:
                Halted = true;
                return 1;

            case IS.LOAD:
            {
                var addr = ReadNext().Int32;
                if (addr == -1)
                    addr = Peek().Int32; // Hacky

                var value = DataProvider.GetValue(addr);
                Push(value);
                return 1;
            }

            case IS.PUSH:
                Push(ReadNext());
                return 1;

            case IS.STORE:
            {
                var addr = ReadNext().Int32;
                var value = Peek();
                if (addr == -1)
                    addr = Peek(1).Int32;

                DataProvider.SetValue(addr, value);
                return 1;
            }

            case IS.DUP:
                Push(Peek());
                return 1;

            case IS.DROP:
                Pop();
                return 1;

            case IS.BINARY:
            {
                var type = ReadNext().Int32;
                var operation = ReadNext().Int32;
                return ExecuteBinaryOperation(type, operation);
            }

            case IS.JMP:
                // Sigsegv go brrrr
                ProgramCounter = ReadNext().Int32;
                return 1;

            case IS.JMPC:
            {
                var type = ReadNext().Int32;
                var addr = ReadNext().Int32;

                if (CheckConditionalJump(type, addr))
                    ProgramCounter = addr;

                return 3; // Conditional jumps are expensive duh
            }

            case IS.OUT:
            {
                var port = ReadNext().Int32;
                var valueOut = Peek();
                // Assuming the I/O provider will handle the console/port output separation
                var success = ioProvider.TryWrite(port, valueOut);
                Waiting = !success;
                return 1;
            }

            case IS.IN:
            {
                var port = ReadNext().Int32;
                var (success, value) = ioProvider.TryRead(port);
                Waiting = !success;

                if (success)
                    Push(value);

                return 1;
            }
        }

        // SHould never happen
        throw new CPUExecutionException(CPUErrorCode.IllegalInstruction);
    }

    /// <summary>
    ///     Executes a binary op and returns its instruction cost.
    /// </summary>
    private int ExecuteBinaryOperation(int type, int operation)
    {
        if (type is < 0 or > (int) OperandType.Max)
            throw new CPUExecutionException(CPUErrorCode.InvalidType);

        if (operation is < 0 or > (int) OperationType.Max)
            throw new CPUExecutionException(CPUErrorCode.InvalidType);

        switch ((OperationType) operation)
        {
            // Genuinely don't know how to implement it better other than maybe making a function matrix.
            // Though that won't make it less messy, only less readable but faster.
            case OperationType.Add:
                if (type == (int) OperandType.Integer) {
                    var b = Pop().Int32;
                    var a = Pop().Int32;
                    Push(CPUMemoryCell.FromInt32(a + b));
                } else if (type == (int) OperandType.Float) {
                    var b = Pop().Single;
                    var a = Pop().Single;
                    Push(CPUMemoryCell.FromSingle(a + b));
                }
                return 1;

            case OperationType.Subtract:
                if (type == (int) OperandType.Integer) {
                    var b = Pop().Int32;
                    var a = Pop().Int32;
                    Push(CPUMemoryCell.FromInt32(a - b));
                } else if (type == (int) OperandType.Float) {
                    var b = Pop().Single;
                    var a = Pop().Single;
                    Push(CPUMemoryCell.FromSingle(a - b));
                }
                return 1;

            case OperationType.Multiply:
                if (type == (int) OperandType.Integer) {
                    var b = Pop().Int32;
                    var a = Pop().Int32;
                    Push(CPUMemoryCell.FromInt32(a * b));
                } else if (type == (int) OperandType.Float) {
                    var b = Pop().Single;
                    var a = Pop().Single;
                    Push(CPUMemoryCell.FromSingle(a * b));
                }

                return 5; // Multiplication is more expensive: 5 ticks

            case OperationType.Divide:
                if (type == (int) OperandType.Integer) {
                    var b = Pop().Int32;
                    var a = Pop().Int32;
                    // Integer division has a special case
                    if (b == 0)
                        throw new CPUExecutionException(CPUErrorCode.DivisionByZero);

                    Push(CPUMemoryCell.FromInt32(a / b));
                } else if (type == (int) OperandType.Float) {
                    var b = Pop().Single;
                    var a = Pop().Single;
                    Push(CPUMemoryCell.FromSingle(a / b));
                }

                return 11; // 11 ticks - don't do division unless you have to

            case OperationType.Modulus:
                if (type == (int) OperandType.Integer) {
                    var b = Pop().Int32;
                    var a = Pop().Int32;
                    // Integer division has a special case
                    if (b == 0)
                        throw new CPUExecutionException(CPUErrorCode.DivisionByZero);

                    Push(CPUMemoryCell.FromInt32(a % b));
                } else if (type == (int) OperandType.Float) {
                    var b = Pop().Single;
                    var a = Pop().Single;
                    Push(CPUMemoryCell.FromSingle(a % b));
                }

                return 1; // 11 ticks - don't do division unless you have to
        }

        // Should never happen
        throw new CPUExecutionException(CPUErrorCode.IllegalInstruction);
    }

    public IEnumerable<CPUMemoryCell> DumpStack()
    {
        for (var i = _operationStackTop; i < _operationStack.Length; i++)
            yield return _operationStack[i];
    }

    private bool CheckConditionalJump(int type, int addr)
    {
        var value = Peek().Int32;
        switch ((JumpType) type)
        {
            case JumpType.Zero:
                return value == 0;

            case JumpType.NonZero:
                return value != 0;
        }

        throw new CPUExecutionException(CPUErrorCode.InvalidType);
    }

    private void Push(CPUMemoryCell value)
    {
        if (_operationStackTop > _operationStackBottom ||_operationStackTop <= 0)
            throw new CPUExecutionException(CPUErrorCode.StackOverflow);

        _operationStack[--_operationStackTop] = value;
    }

    private CPUMemoryCell Pop()
    {
        if (_operationStackTop >= _operationStackBottom || _operationStackTop < 0)
            throw new CPUExecutionException(CPUErrorCode.StackUnderflow);

        return _operationStack[_operationStackTop++];
    }

     private CPUMemoryCell Peek(int offset = 0)
     {
         if (_operationStackTop + offset >= _operationStackBottom || _operationStackTop + offset < 0)
             throw new CPUExecutionException(CPUErrorCode.StackUnderflow);

         return _operationStack[_operationStackTop + offset];
     }

     private CPUMemoryCell ReadNext() => DataProvider.GetValue(ProgramCounter++);
}

// ReSharper disable once InconsistentNaming
/// <summary>
///     Provides a CPU with methods to write/read memory.
/// </summary>
public abstract class VirtualCPUDataProvider
{
    /// <summary>
    ///     Reads the memory at the specified address or throws a CPUExecutionException.
    /// </summary>
    public abstract CPUMemoryCell GetValue(int address);

    /// <summary>
    ///     Writes the memory at the specified address or throws a CPUExecutionException.
    /// </summary>
    public abstract void SetValue(int address, CPUMemoryCell value);
}

// ReSharper disable once InconsistentNaming
/// <summary>
///     Provides a CPU with methods to write/read to a virtual terminal or similar.
///
///     Unlike memory I/O, console I/O can take time, so methods here can return a negative success,
///     which indicates that the CPU should pause until it is done.
/// </summary>
public abstract class VirtualCPUIOProvider
{
    public abstract bool TryWrite(int port, CPUMemoryCell message);

    public abstract (bool success, CPUMemoryCell data) TryRead(int port);
}
