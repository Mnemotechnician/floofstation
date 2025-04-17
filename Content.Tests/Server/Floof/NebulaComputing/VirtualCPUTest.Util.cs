using System;
using Content.Server.FloofStation.NebulaComputing.VirtualCPU;
using Content.Server.FloofStation.NebulaComputing.VirtualCPU.Assembly;
using NUnit.Framework;


namespace Content.Tests.Server.Floof.NebulaComputing;


public sealed partial class VirtualCPUTest
{
    public (int[] code, int entryPoint) Assemble(string asm)
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
    public void TestSimpleCPU(int entryPoint, int[] program, string inputData = "", string expectedOutput = "", bool debug = false)
    {
        var cpu = CreateTestCPU(entryPoint, program, inputData);
        var assumedTicks = 0;

        // Simple debugger to log executed instructions
        var instructionLog = "";
        if (debug)
        {
            cpu.Debugger = (instruction) =>
            {
                instructionLog += $"({instruction}) ";
                return false;
            };
        }

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


        Assert.That((cpu.IOProvider as ConsoleIOProvider)?.CombinedOutput,
            Is.EqualTo(expectedOutput));
    }

    public VirtualCPU CreateTestCPU(int entryPoint, int[] program, string inputData = "")
    {
        var cpu = new VirtualCPU(
            new StaticDataProvider(program),
            new ConsoleIOProvider(inputData),
            new CPUMemoryCell[100]);

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
        cpu.Reset(entryPoint);
        return cpu;
    }

    public sealed class StaticDataProvider(int[] data) : VirtualCPUDataProvider
    {
        public int[] Data => data;

        public override CPUMemoryCell GetValue(int address)
        {
            if (address < 0 || address >= Data.Length)
                throw new CPUExecutionException(CPUErrorCode.SegmentationFault);

            return CPUMemoryCell.FromInt32(Data[address]);
        }

        public override void SetValue(int address, CPUMemoryCell value)
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
