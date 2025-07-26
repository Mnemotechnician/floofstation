using System;
using Content.Server._Floof.NebulaComputing.VirtualCPU;
using Content.Server._Floof.NebulaComputing.VirtualCPU.Assembly;
using Content.Server._Floof.NebulaComputing.VirtualCPU.Cpu;
using NUnit.Framework;


namespace Content.Tests.Server._Floof.NebulaComputing;


public sealed partial class VirtualCPUTest
{
    public const int DefaultStackSize = 50;

    /// Convenience function to compose an opcode.
    public int Opcode(
        InstructionSet op,
        CPUInstructionCell.DataLocation arg1 = CPUInstructionCell.DataLocation.Immediate,
        CPUInstructionCell.DataLocation arg2 = CPUInstructionCell.DataLocation.Immediate,
        CPUInstructionCell.DataLocation arg3 = CPUInstructionCell.DataLocation.Immediate,
        CPUInstructionCell.DataLocation arg4 = CPUInstructionCell.DataLocation.Immediate,
        byte type = 0)
    {
        var loc = CPUInstructionCell.EncodeDataLocations(arg1, arg2, arg3, arg4);
        return (int) new CPUInstructionCell { RawOpcode = (byte) op, DataLocationSpecifiers = loc, Reserved2 = type }.UInt32;
    }

    /// Convenience function to compose an address considering the default stack size.
    public int Addr(int addr, int offset = DefaultStackSize) => addr + offset;

    public (int[] code, uint entryPoint) Assemble(string asm)
    {
        var assembler = new VCPUAssemblyCompiler();
        var (success, code, entryPoint) = assembler.Compile(asm);

        Assert.That(assembler.Errors, Is.Null, "Found errors while assembling:\n" + string.Join('\n', assembler.Errors ?? []));
        Assert.That(success, Is.True, "No errors but failed to assemble");

        return (code, entryPoint);
    }

    /// <summary>
    ///     Run a simple CPU with fixed input and expected output.
    /// </summary>
    public void TestSimpleCPU(uint entryPoint, int[] program, string inputData = "", string expectedOutput = "", bool debug = false)
    {
        var cpu = CreateTestCPU(entryPoint, program, inputData);
        var assumedTicks = 0;

        // Simple debugger to log executed instructions
        var instructionLog = "";
        if (debug)
        {
            cpu.Debugger = (instruction) =>
            {
                instructionLog += $"{instruction} ";
                return false;
            };
        }

        CPUErrorCode? ec = null;
        cpu.ErrorHandler += (errorCode, programCounter) => { ec = errorCode; };

        Console.WriteLine("CPU has started.");

        while (!cpu.Halted)
        {
            cpu.ProcessTicks(10);
            assumedTicks += 10;

            Assert.That(assumedTicks < 10000, "Endless loop in CPU detected.");
        }
        Console.WriteLine("CPU has halted.");

        if (debug)
            Console.WriteLine($"Executed the following:\n{instructionLog}");

        Assert.That(ec, Is.Null, $"CPU encountered error {ec}");
        Assert.That((cpu.IOProvider as ConsoleIOProvider)?.CombinedOutput,
            Is.EqualTo(expectedOutput));
    }

    public VirtualCPU CreateTestCPU(uint entryPoint, int[] program, string inputData = "", uint stackSize = DefaultStackSize)
    {
        // Stack has to be allocated manually since we aren't using a compiler here.
        var memory = new int[stackSize + program.Length];
        Array.Copy(program, 0, memory, stackSize, program.Length);

        var cpu = new VirtualCPU(
            new StaticDataProvider(memory),
            new ConsoleIOProvider(inputData));

        cpu.ErrorHandler += (errorCode, programCounter) =>
        {
            Console.WriteLine($"CPU encountered error {errorCode}. PC={programCounter}");
            Console.WriteLine("Stack dump (top-to-bottom):");

            var i = 0;
            foreach (var value in cpu.DumpStack())
            {
                Console.WriteLine($"{i++}. Numeric {value.Int32}, char {value.Char}");
            }
        };
        cpu.Reset(entryPoint + stackSize);
        cpu.StackSize = stackSize;
        cpu.StackBeginAddr = 0;
        return cpu;
    }

    public sealed class StaticDataProvider(int[] data) : VirtualCPUDataProvider
    {
        public int[] Data => data;

        public override CPUMemoryCell GetValue(uint address)
        {
            if (address < 0 || address >= Data.Length)
                throw new CPUExecutionException(CPUErrorCode.SegmentationFault);

            return CPUMemoryCell.FromInt32(Data[address]);
        }

        public override void SetValue(uint address, CPUMemoryCell value)
        {
            if (address < 0 || address >= Data.Length)
                throw new CPUExecutionException(CPUErrorCode.SegmentationFault);

            Data[address] = value.Int32;
        }
    }

    public sealed class ConsoleIOProvider(string constantInput) : VirtualCPUIOProvider
    {
        public string ConstantInput = constantInput;
        public int InputPos = 0;
        public string CombinedOutput;

        public override bool TryWrite(int port, CPUMemoryCell message)
        {
            Console.WriteLine($"On port {port}: {message.Char}  | {message.Int32}");
            CombinedOutput += message.Char;
            return true;
        }

        public override (bool success, CPUMemoryCell data) TryRead(int port)
        {
            return (true, CPUMemoryCell.FromInt32(ConstantInput[InputPos++]));
        }
    }
}
