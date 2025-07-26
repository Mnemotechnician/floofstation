namespace Content.Server._Floof.NebulaComputing.VirtualCPU;

using IS = InstructionSet;

// Shut up! If I follow all of those, I will never finish writing this.
// ReSharper disable BadControlBracesLineBreaks
// ReSharper disable InconsistentNaming
// ReSharper disable MissingLinebreak

// TODO this must be refactored to use registers like EAX, EBX, ECX, EDX... Stack-based computation SUCKS
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
    ///     Instructions per second, for use in <see cref="VirtualCPUExecutorThread"/>
    /// </summary>
    public int InstructionRate = 1;
    /// <summary>
    ///     A function to handle errors in the CPU.
    ///     Receives the error code and program counter as parameters.
    /// </summary>
    /// <remarks>Not guaranteed to be executed on the main game thread! Use with caution!</remarks>
    public event Action<CPUErrorCode, int>? ErrorHandler;
    /// <summary>
    ///     A function to debug the CPU. Invoked on each tick with the current opcode as the parameter.
    ///     If it returns true, the processor waits on the instruction until it returns false.
    /// </summary>
    /// <remarks>Not guaranteed to be executed on the main game thread! Use with caution!</remarks>
    public Func<IS, bool>? Debugger;

    private CPUMemoryCell[] _operationStack = operationStack;
    private int _operationStackTop = 0;
    private int _operationStackBottom = 0;
    /// <summary>
    ///     Whether the CPU should revert the stack to its previous state at the end of the instruction. Set before a tick.
    ///     Some instructions can manually handle this.
    /// </summary>
    private bool _shouldPreserveStack = false;
    /// <summary>
    ///     The previous stack top value, for use with the preserve stack flag.
    /// </summary>
    private int _previousTickStackTop = -1;

    public int ProgramCounter { get; set; }

    public volatile bool Halted = true;
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

            TryRevertStack();

            // If the CPU is waiting, doesn't make any sense to process further. It means the CPU is waiting for something like player input,
            // Which is obviously not going to come out of the blue.
            // Also, we reset the PC of the CPU, So that on the next call of this method it executes the same instruction again.
            if (Waiting)
            {
                ProgramCounter = previousPC;
                break;
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

        // Preserve stack
        _shouldPreserveStack = false;
        if ((nextOpCode & (int) IS.PRESERVE_STACK_MASK) != 0) {
            _shouldPreserveStack = true;
            _previousTickStackTop = _operationStackTop;
            nextOpCode &= ~(int) IS.PRESERVE_STACK_MASK;
        }

        if (nextOpCode is < 0 or > (int) IS.MAX_OPCODE)
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

            case IS.LOAD: {
                var addr = ReadOrPopAddr().Int32;
                var value = DataProvider.GetValue(addr);

                TryRevertStack(); // Make sure the push is saved even if preserving stack.
                Push(value);
                return 2;
            }

            case IS.PUSH:
                Push(ReadNext());
                return 1;

            case IS.STORE: {
                var addr = ReadOrPopAddr().Int32;
                var value = Pop();

                DataProvider.SetValue(addr, value);
                return 2;
            }

            case IS.DUP: {
                var relative = ReadNext().Int32;
                if (relative < 0)
                    throw new CPUExecutionException(CPUErrorCode.SegmentationFault);

                Push(Peek(relative));
                return 1;
            }

            case IS.DROP: {
                var relative = ReadNext().Int32;
                if (relative < 0)
                    throw new CPUExecutionException(CPUErrorCode.SegmentationFault);

                if (relative + _operationStackTop >= _operationStack.Length)
                    throw new CPUExecutionException(CPUErrorCode.StackUnderflow);

                if (relative == 0)
                    Pop();
                else {
                    // This one is tricky - we have to remove a middle element
                    for (var i = _operationStackTop + relative; i > _operationStackTop; i--)
                        _operationStack[i] = _operationStack[i - 1];
                }

                return 1 + relative; // Don't do relative drops unless you must
            }

            case IS.BINARY:
            {
                var type = ReadNext().Int32;
                var operation = ReadNext().Int32;
                return ExecuteBinaryOperation(type, operation);
            }

            case IS.JMP:
            {
                // Sigsegv go brrrr
                ProgramCounter = ReadOrPopAddr().Int32;
                return 1;
            }

            case IS.JMPC:
            {
                var addr = ReadOrPopAddr().Int32;
                var type = ReadNext().Int32;

                if (CheckConditionalJump(type, addr))
                    ProgramCounter = addr;

                return 3; // Conditional jumps are expensive duh
            }

            case IS.OUT:
            {
                var port = ReadOrPopAddr().Int32;
                var valueOut = Pop();
                // Assuming the I/O provider will handle the console/port output separation
                var success = ioProvider.TryWrite(port, valueOut);
                Waiting = !success;
                return 1;
            }

            case IS.IN:
            {
                var port = ReadOrPopAddr().Int32;
                var (success, value) = ioProvider.TryRead(port);
                Waiting = !success;

                if (success)
                    Push(value);

                return 1;
            }

            case IS.CALL:
            {
                var addr = ReadOrPopAddr().Int32;
                Push(CPUMemoryCell.FromInt32(ProgramCounter)); // After the current instruction
                ProgramCounter = addr;
                return 1;
            }

            case IS.RET:
            {
                var numFramesBefore = ReadNext().Int32;
                for (var i = 0; i < numFramesBefore; i++)
                    Pop();

                var retTarget = Pop().Int32;

                var numFramesAfter = ReadNext().Int32;
                for (var i = 0; i < numFramesAfter; i++)
                    Pop();

                ProgramCounter = retTarget;
                return 1 + Math.Max(numFramesBefore, 0) + Math.Max(numFramesAfter, 0);
            }
        }

        // Should never happen
        throw new CPUExecutionException(CPUErrorCode.IllegalInstruction);
    }

    /// <summary>
    ///     Executes a binary op and returns its instruction cost.
    /// </summary>
    private int ExecuteBinaryOperation(int type, int operation)
    {
        if (type is < 0 or > (int) OperandType.Max)
            throw new CPUExecutionException(CPUErrorCode.InvalidType);

        if (operation is < 0 or > (int) BinaryOperationType.MaxValue)
            throw new CPUExecutionException(CPUErrorCode.InvalidType);

        switch ((BinaryOperationType) operation)
        {
            // Genuinely don't know how to implement it better other than maybe making a function matrix.
            // Though that won't make it less messy, only less readable but faster.
            case BinaryOperationType.Add:
                if (type == (int) OperandType.Int) {
                    var b = Pop().Int32;
                    var a = Pop().Int32;
                    Push(CPUMemoryCell.FromInt32(a + b));
                } else if (type == (int) OperandType.Float) {
                    var b = Pop().Single;
                    var a = Pop().Single;
                    Push(CPUMemoryCell.FromSingle(a + b));
                }
                return 1;

            case BinaryOperationType.Sub:
                if (type == (int) OperandType.Int) {
                    var b = Pop().Int32;
                    var a = Pop().Int32;
                    Push(CPUMemoryCell.FromInt32(a - b));
                } else if (type == (int) OperandType.Float) {
                    var b = Pop().Single;
                    var a = Pop().Single;
                    Push(CPUMemoryCell.FromSingle(a - b));
                }
                return 1;

            case BinaryOperationType.Mul:
                if (type == (int) OperandType.Int) {
                    var b = Pop().Int32;
                    var a = Pop().Int32;
                    Push(CPUMemoryCell.FromInt32(a * b));
                } else if (type == (int) OperandType.Float) {
                    var b = Pop().Single;
                    var a = Pop().Single;
                    Push(CPUMemoryCell.FromSingle(a * b));
                }

                return 5; // Multiplication is more expensive: 5 ticks

            case BinaryOperationType.Div:
                if (type == (int) OperandType.Int) {
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

            case BinaryOperationType.Mod:
                if (type == (int) OperandType.Int) {
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
        var value = Pop().Int32;
        switch ((JumpType) type)
        {
            case JumpType.Zero:
                return value == 0;

            case JumpType.NonZero:
                return value != 0;
        }

        throw new CPUExecutionException(CPUErrorCode.InvalidType);
    }

    /// <summary>
    ///     Reads an address parameter from the memory. If it is -1, pops it from the stack.
    /// </summary>
    /// <returns></returns>
    private CPUMemoryCell ReadOrPopAddr()
    {
        var addr = ReadNext().Int32;
        if (addr == -1)
            return Pop();

        return CPUMemoryCell.FromInt32(addr);
    }

    /// <summary>
    ///     If we are preserving the stack, revert it to how it was before this tick.
    /// </summary>
    private void TryRevertStack()
    {
        if (!_shouldPreserveStack)
            return;

        _operationStackTop = _previousTickStackTop;
        _shouldPreserveStack = false;
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
         if (_operationStackTop + offset >= _operationStackBottom)
             throw new CPUExecutionException(CPUErrorCode.StackOverflow);

         if (_operationStackTop + offset < 0)
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
    public const int ConsolePort = 0,
                     DiskPort = 1,
                     Reserved = 10,
                     FirstPinPort = Reserved + 1,
                     FirstCheckInputPort = FirstPinPort + 10;

    public abstract bool TryWrite(int port, CPUMemoryCell message);

    public abstract (bool success, CPUMemoryCell data) TryRead(int port);
}
