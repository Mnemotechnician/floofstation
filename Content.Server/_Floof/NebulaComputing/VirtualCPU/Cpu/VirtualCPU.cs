namespace Content.Server._Floof.NebulaComputing.VirtualCPU.Cpu;

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
    VirtualCPUIOProvider ioProvider)
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
    public event Action<CPUErrorCode, uint>? ErrorHandler;

    /// <summary>
    ///     A function to debug the CPU. Invoked on each tick with the current opcode as the parameter.
    ///     If it returns true, the processor waits on the instruction until it returns false.
    /// </summary>
    /// <remarks>Not guaranteed to be executed on the main game thread! Use with caution!</remarks>
    public Func<CPUInstructionCell, bool>? Debugger;

    public CPUMemoryCell[] Registers = new CPUMemoryCell[(int) CPURegister.Count];

    public uint ProgramCounter
    {
        get => Register(CPURegister.RIP).UInt32;
        set => Register(CPURegister.RIP).UInt32 = value;
    }

    public uint StackPos
    {
        get => Register(CPURegister.RSP).UInt32;
        set => Register(CPURegister.RSP).UInt32 = value;
    }

    public uint StackBeginAddr
    {
        get => Register(CPURegister.RSS).UInt32;
        set => Register(CPURegister.RSS).UInt32 = value;
    }

    public uint StackSize
    {
        get => Register(CPURegister.RSZ).UInt32;
        set => Register(CPURegister.RSZ).UInt32 = value;
    }

    public volatile bool Halted = true;

    /// <summary>
    ///     If set to true during the execution of an instruction,
    ///     the CPU will not proceed and will resume from the same instruction later.
    /// </summary>
    public bool Waiting { get; private set; }

    /// <summary>
    ///     Resets the CPU and sets the program counter to the specified value.
    /// </summary>
    public void Reset(uint pc)
    {
        Halted = Waiting = false;
        ProgramCounter = pc;
        StackPos = StackBeginAddr;
    }

    public void ProcessTicks(int ticks)
    {
        while (ticks > 0 && !Halted)
        {
            Waiting = false;

            var previousPC = ProgramCounter;
            try
            {
                ticks -= ExecuteInstruction();
            }
            catch (CPUExecutionException e)
            {
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
        var icell = ReadNext().Instruction;
        if (icell.OpCode > IS.MaxOpcode)
        {
            var err = icell.OpCode == IS.SectionBoundary
                ? CPUErrorCode.SectionBoundary
                : CPUErrorCode.IllegalInstruction;
            throw new CPUExecutionException(err);
        }

        // Debug
        if (Debugger is not null && Debugger.Invoke(icell))
        {
            Waiting = true;
            return 1;
        }

        switch (icell.OpCode)
        {
            case IS.Nop:
                return 1;

            case IS.Halt:
                Halted = true;
                return 1;

            case IS.Mov:
            {
                var dst = ReadNext();
                var src = ResolveMemory(icell.GetArgumentLocation(1), ReadNext());
                WriteMemory(icell.GetArgumentLocation(0), dst, src);
                return 2;
            }

            case IS.Push:
            {
                var srcLoc = icell.GetArgumentLocation(0);
                Push(ResolveMemory(srcLoc, ReadNext()));
                return 1;
            }

            case IS.Pop:
            {
                var dstLoc = icell.GetArgumentLocation(0);
                WriteMemory(dstLoc, ReadNext(), Pop());
                return 1;
            }

            case IS.Binary:
            {
                var dst = ReadNext();
                var src1 = ResolveMemory(icell.GetArgumentLocation(1), ReadNext());
                var src2 = ResolveMemory(icell.GetArgumentLocation(2), ReadNext());

                var result = ExecuteBinaryOperation(icell.Reserved2, src1, src2, out var cost);
                WriteMemory(icell.GetArgumentLocation(0), dst, result);
                return cost;
            }

            case IS.Unary:
            {
                var dst = ReadNext();
                var src = ResolveMemory(icell.GetArgumentLocation(1), ReadNext());

                var result = ExecuteUnaryOperation(icell.Reserved2, src, out var cost);
                WriteMemory(icell.GetArgumentLocation(0), dst, result);
                return cost;
            }

            case IS.Jmp:
            {
                // Sigsegv go brrrr
                ProgramCounter = ResolveMemory(icell.GetArgumentLocation(0), ReadNext()).UInt32;
                return 2;
            }

            case IS.JmpC:
            {
                var addr = ResolveMemory(icell.GetArgumentLocation(0), ReadNext()).UInt32;
                var type = (JumpType) icell.Reserved2;

                if (CheckConditionalJump(type))
                    ProgramCounter = addr;

                return 3; // Conditional jumps are expensive duh
            }

            case IS.Cmp:
            {
                var src1Loc = icell.GetArgumentLocation(0);
                var src2Loc = icell.GetArgumentLocation(1);
                var src1 = ResolveMemory(src1Loc, ReadNext());
                var src2 = ResolveMemory(src2Loc, ReadNext());
                var type = (OperandType) icell.Reserved2;

                Register(CPURegister.RFLAGS).UInt32 = (uint) ExecuteComparison(src1, src2, type);
                return 1;
            }

            case IS.Out:
            {
                var port = ResolveMemory(icell.GetArgumentLocation(0), ReadNext()).Int32;
                var value = ResolveMemory(icell.GetArgumentLocation(1), ReadNext());
                // Assuming the I/O provider will handle the console/port output separation
                var success = ioProvider.TryWrite(port, value);
                Waiting = !success;
                return 1;
            }

            case IS.In:
            {
                var port = ResolveMemory(icell.GetArgumentLocation(0), ReadNext()).Int32;
                var dst = ReadNext();
                var (success, value) = ioProvider.TryRead(port);
                Waiting = !success;

                if (success)
                    WriteMemory(icell.GetArgumentLocation(1), dst, value);

                return 1;
            }

            case IS.Call:
            {
                var addr = ResolveMemory(icell.GetArgumentLocation(0), ReadNext()).UInt32;
                Push(Register(CPURegister.RIP)); // After the current instruction
                ProgramCounter = addr;
                return 1;
            }

            case IS.Ret:
            {
                var numArgs = ResolveMemory(icell.GetArgumentLocation(0), ReadNext()).UInt32;
                ProgramCounter = Pop().UInt32;
                StackPos -= numArgs;
                return 1;
            }

            case IS.Enter:
            {
                // We push the old RBP and set up the new stack frame after it
                Push(Register(CPURegister.RBP));
                Register(CPURegister.RBP) = Register(CPURegister.RSP);
                var numArgs = ResolveMemory(icell.GetArgumentLocation(0), ReadNext()).UInt32;
                StackPos += numArgs;
                return 1;
            }

            case IS.Leave:
            {
                Register(CPURegister.RSP) = Register(CPURegister.RBP);
                Register(CPURegister.RBP) = Pop();
                return 1;
            }
        }

        // Should never happen
        throw new CPUExecutionException(CPUErrorCode.IllegalInstruction);
    }

    /// <summary>
    ///     Executes a binary operation and returns the result.
    /// </summary>
    /// <param name="type">The value of the reserved byte - the <see cref="BinaryOperationType"/> of the operation.</param>
    /// <param name="a">The first operand.</param>
    /// <param name="b">The second operand.</param>
    /// <param name="cost">The operational cost of the instruction.</param>
    private CPUMemoryCell ExecuteBinaryOperation(byte type, CPUMemoryCell a, CPUMemoryCell b, out int cost)
    {
        // 8th bit signifies the floating point mode. Doesn't apply to bitwise operations
        switch (type)
        {
            case (byte) BinaryOperationType.Add:
                cost = 1;
                return CPUMemoryCell.FromInt32(a.Int32 + b.Int32);

            case (byte) BinaryOperationType.Add | 0x80:
                cost = 1;
                return CPUMemoryCell.FromSingle(a.Single + b.Single);

            case (byte) BinaryOperationType.Sub:
                cost = 1;
                return CPUMemoryCell.FromInt32(a.Int32 - b.Int32);

            case (byte) BinaryOperationType.Sub | 0x80:
                cost = 1;
                return CPUMemoryCell.FromSingle(a.Single - b.Single);

            // Multiplication are division are more expensive, especially with floats
            case (byte) BinaryOperationType.Mul:
                cost = 2;
                return CPUMemoryCell.FromInt32(a.Int32 * b.Int32);

            case (byte) BinaryOperationType.Mul | 0x80:
                cost = 3;
                return CPUMemoryCell.FromSingle(a.Single * b.Single);

            case (byte) BinaryOperationType.Div:
                cost = 3;
                return CPUMemoryCell.FromInt32(a.Int32 / b.Int32);

            case (byte) BinaryOperationType.Div | 0x80:
                cost = 5;
                return CPUMemoryCell.FromSingle(a.Single / b.Single);

            // Bitwise
            case (byte) BinaryOperationType.And:
                cost = 1;
                return CPUMemoryCell.FromInt32(a.Int32 & b.Int32);

            case (byte) BinaryOperationType.Or:
                cost = 1;
                return CPUMemoryCell.FromInt32(a.Int32 | b.Int32);

            case (byte) BinaryOperationType.Xor:
                cost = 1;
                return CPUMemoryCell.FromInt32(a.Int32 ^ b.Int32);

            // Float-specific
            case (byte) BinaryOperationType.Pow:
                cost = 8;
                return CPUMemoryCell.FromSingle(MathF.Pow(a.Single, b.Single));
        }

        // Should never happen
        throw new CPUExecutionException(CPUErrorCode.InvalidType);
    }

    /// <summary>
    ///     Executes a unary operation and returns the result.
    /// </summary>
    /// <param name="type">The value of the reserved byte - the <see cref="UnaryOperationType"/> of the operation.</param>
    /// <param name="value">The operand.</param>
    /// <param name="cost">The operational cost of the instruction.</param>
    private CPUMemoryCell ExecuteUnaryOperation(byte type, CPUMemoryCell value, out int cost)
    {
        cost = 1;
        switch (type)
        {
            // Neg
            case (byte) UnaryOperationType.Neg:
                return CPUMemoryCell.FromInt32(-value.Int32);
            case (byte) UnaryOperationType.Neg | 0x80:
                return CPUMemoryCell.FromSingle(-value.Single);

            // Abs
            case (byte) UnaryOperationType.Abs:
                return CPUMemoryCell.FromInt32(Math.Abs(value.Int32));
            case (byte) UnaryOperationType.Abs | 0x80:
                return CPUMemoryCell.FromSingle(MathF.Abs(value.Single));

            // Not
            case (byte) UnaryOperationType.Not:
                return CPUMemoryCell.FromInt32(~value.Int32);

            // Sqrt
            case (byte) UnaryOperationType.Sqrt | 0x80:
                cost = 12; // Ayooo. On Intel those take 6-20 cycles, so this is a good approximation
                return CPUMemoryCell.FromSingle(MathF.Sqrt(value.Single));

            // Trig
            case (byte) UnaryOperationType.Sin | 0x80:
                cost = 2;
                return CPUMemoryCell.FromSingle(MathF.Sin(value.Single));
            case (byte) UnaryOperationType.Cos | 0x80:
                cost = 2;
                return CPUMemoryCell.FromSingle(MathF.Cos(value.Single));
            case (byte) UnaryOperationType.Tan | 0x80:
                cost = 2;
                return CPUMemoryCell.FromSingle(MathF.Tan(value.Single));

            // Inverse trig
            case (byte) UnaryOperationType.Asin | 0x80:
                cost = 2;
                return CPUMemoryCell.FromSingle(MathF.Asin(value.Single));
            case (byte) UnaryOperationType.Acos | 0x80:
                cost = 2;
                return CPUMemoryCell.FromSingle(MathF.Acos(value.Single));
            case (byte) UnaryOperationType.Atan | 0x80:
                cost = 2;
                return CPUMemoryCell.FromSingle(MathF.Atan(value.Single));

            // Rounding
            case (byte) UnaryOperationType.Round | 0x80:
                return CPUMemoryCell.FromInt32((int) MathF.Round(value.Single));
            case (byte) UnaryOperationType.Floor | 0x80:
                return CPUMemoryCell.FromInt32((int) MathF.Floor(value.Single));
            case (byte) UnaryOperationType.Ceil | 0x80:
                return CPUMemoryCell.FromInt32((int) MathF.Ceiling(value.Single));
            case (byte) UnaryOperationType.Trunc | 0x80:
                return CPUMemoryCell.FromInt32((int) MathF.Truncate(value.Single));

            // Increment/decrement
            case (byte) UnaryOperationType.Inc:
                return CPUMemoryCell.FromInt32(value.Int32 + 1);
            case (byte) UnaryOperationType.Inc | 0x80:
                return CPUMemoryCell.FromSingle(value.Single + 1);

            case (byte) UnaryOperationType.Dec:
                return CPUMemoryCell.FromInt32(value.Int32 - 1);
            case (byte) UnaryOperationType.Dec | 0x80:
                return CPUMemoryCell.FromSingle(value.Single - 1);
            // Should never happen
            default:
                throw new CPUExecutionException(CPUErrorCode.InvalidType);
        }
    }

    /// <summary>
    ///     Checks if a conditional jump should be taken, based on the results of the last CMP instruction.
    /// </summary>
    private bool CheckConditionalJump(JumpType type)
    {
        if (type > JumpType.Max)
            throw new CPUExecutionException(CPUErrorCode.InvalidType);

        var flags = (ComparisonStatusFlags) Registers[(byte) CPURegister.RFLAGS].UInt32;
        switch (type) {
            case JumpType.Equal:
                return (flags & ComparisonStatusFlags.Equal) != 0;
            case JumpType.NotEqual:
                return (flags & ComparisonStatusFlags.Equal) == 0;
            case JumpType.Greater:
                return (flags & ComparisonStatusFlags.Greater) != 0;
            case JumpType.Lower:
                return (flags & ComparisonStatusFlags.Greater) == 0 && (flags & ComparisonStatusFlags.Equal) == 0;
            case JumpType.GreaterEq:
                return (flags & ComparisonStatusFlags.Greater) != 0 || (flags & ComparisonStatusFlags.Equal) != 0;
            case JumpType.LowerEq:
                return (flags & ComparisonStatusFlags.Greater) == 0;
        }

        throw new CPUExecutionException(CPUErrorCode.InvalidType);
    }

    private ComparisonStatusFlags ExecuteComparison(CPUMemoryCell a, CPUMemoryCell b, OperandType type)
    {
        if (type > OperandType.MaxValue)
            throw new CPUExecutionException(CPUErrorCode.InvalidType);

        // This is terrible, absolutely horrendous
        var result = ComparisonStatusFlags.None;
        switch (type) {
            case OperandType.Int:
                if (a.Int32 == b.Int32)
                    result |= ComparisonStatusFlags.Equal;
                else if (a.Int32 > b.Int32)
                    result |= ComparisonStatusFlags.Greater;
                break;

            // TODO handle NaN and such?
            case OperandType.Float:
                if (a.Single == b.Single)
                    result |= ComparisonStatusFlags.Equal;
                else if (a.Single > b.Single)
                    result |= ComparisonStatusFlags.Greater;
                break;

            case OperandType.Uint:
                if (a.UInt32 == b.UInt32)
                    result |= ComparisonStatusFlags.Equal;
                else if (a.UInt32 > b.UInt32)
                    result |= ComparisonStatusFlags.Greater;
                break;
        }

        return result;
    }

    public IEnumerable<CPUMemoryCell> DumpStack()
    {
        var end = StackBeginAddr + StackSize;
        for (var i = StackBeginAddr; i < end; i++)
            yield return DataProvider.GetValue(i);
    }

    private void Push(CPUMemoryCell value)
    {
        var end = StackBeginAddr + StackSize;
        if (StackPos > end)
            throw new CPUExecutionException(CPUErrorCode.StackOverflow);

        // Value will be stored in the cell after the current one, and that's where RSP will point at
        DataProvider.SetValue(++StackPos, value);
    }

    private CPUMemoryCell Pop()
    {
        if (StackPos <= StackBeginAddr)
            throw new CPUExecutionException(CPUErrorCode.StackUnderflow);

        // Value is retrieved from the address RSP is pointing at, and then RSP will be decremented
        return DataProvider.GetValue(StackPos--);
    }

    private CPUMemoryCell ReadNext() => DataProvider.GetValue(ProgramCounter++);

    /// <summary>
    ///     Returns a reference to a register.
    /// </summary>
    public ref CPUMemoryCell Register(CPURegister register) => ref Registers[(byte) register];

    /// <summary>
    ///     Attempts to resolve a memory value as specified by the DataLocation of a CPUInstruction cell.
    ///     The argument is the raw value of the parameter as specified in the bytecode, which can be an immediate,
    ///     a static pointer, a register pointer, etc.
    /// </summary>
    private CPUMemoryCell ResolveMemory(CPUInstructionCell.DataLocation location, CPUMemoryCell argument)
    {
        switch (location) {
            case CPUInstructionCell.DataLocation.Immediate:
                return argument;

            case CPUInstructionCell.DataLocation.Register:
                return ResolveComplexRegisterOperand(argument);

            case CPUInstructionCell.DataLocation.Static:
                return DataProvider.GetValue(argument.UInt32);

            case CPUInstructionCell.DataLocation.Dynamic:
                var pointer = ResolveComplexRegisterOperand(argument);
                return DataProvider.GetValue(pointer.UInt32);

            default:
                throw new CPUExecutionException(CPUErrorCode.IllegalInstruction); // Should never happen
        }
    }

    /// <summary>
    ///     Resolves a register value as per specification.
    ///     The bottommost 8 bits indicate the register, the middle 16 bits indicate offset, and topmost 8 bits indicate bitshift.
    ///     The final formula is: (registerValue shl topmostBytes) + middleBytes
    /// </summary>
    /// <returns></returns>
    private CPUMemoryCell ResolveComplexRegisterOperand(CPUMemoryCell argument)
    {
        var register = argument.Byte1;
        if (register > Registers.Length)
            throw new CPUExecutionException(CPUErrorCode.IllegalRegister);

        // Using integers here because using those together with other int32 operands would result in them getting cast to int32 anyway
        int offset = argument.Middle;
        int shift = argument.Byte4;
        return CPUMemoryCell.FromInt32((Registers[register].Int32 << shift) + offset);
    }

    /// <summary>
    ///     Attempts to resolve & write to memory as specified by the DataLocation of a CPUInstruction cell.
    ///     The argument is the raw value of the parameter as specified in the bytecode, which can be an immediate,
    ///     a static pointer, a register pointer, etc.
    /// </summary>
    private void WriteMemory(CPUInstructionCell.DataLocation location, CPUMemoryCell argument, CPUMemoryCell value)
    {
        switch (location) {
            case CPUInstructionCell.DataLocation.Immediate:
                return; // Cannot write to an immediate, assume the code wants to avoid writing

            case CPUInstructionCell.DataLocation.Register:
            {
                if (argument.UInt32 > Registers.Length)
                    throw new CPUExecutionException(CPUErrorCode.IllegalRegister);

                Registers[argument.UInt32] = value;
                break;
            }

            case CPUInstructionCell.DataLocation.Static:
                DataProvider.SetValue(argument.UInt32, value);
                break;

            case CPUInstructionCell.DataLocation.Dynamic:
            {
                // As per specification, the middle bytes signify the offset and the upmost byte signifies the shift
                if (argument.Byte1 > Registers.Length)
                    throw new CPUExecutionException(CPUErrorCode.IllegalRegister);

                var pointer = (Registers[argument.Byte1].UInt32 << argument.Byte4) + (uint) argument.Middle;
                DataProvider.SetValue(pointer, value);
                break;
            }

            default:
                throw new CPUExecutionException(CPUErrorCode.IllegalInstruction); // Should never happen
        }
    }
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
    public abstract CPUMemoryCell GetValue(uint address);

    /// <summary>
    ///     Writes the memory at the specified address or throws a CPUExecutionException.
    /// </summary>
    public abstract void SetValue(uint address, CPUMemoryCell value);
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
