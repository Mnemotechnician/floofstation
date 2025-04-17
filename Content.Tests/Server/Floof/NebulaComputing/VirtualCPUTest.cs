using System;
using System.Collections.Generic;
using Content.Server.FloofStation.SpaceComputer.VirtualCPU;
using NUnit.Framework;

namespace Content.Tests.Server.Floof.NebulaComputing;

using IS = InstructionSet;


public sealed class VirtualCPUTest
{
    [Test]
    public void TestSimpleProgramWithOutput()
    {
        int[] program =
        [
            // Data section
            'h', 'e', 'l', 'l', 'o', ' ', 'w', 'o', 'r', 'l', 'd', '!', // Ints 0-11 - phrase to output
            0, 0, 0, 0, 0, 0, 0, 0, // 14-19 - padding ints

            // Code section - int 20
            (int) IS.PUSH, 0,     // Push the address of the string onto the stack, acting as the counter
            // Label 22 - begin loop
            (int) IS.LOAD, -1,    // Load value from memory via address on the stack
            (int) IS.JMPC, 0, 37, // Jump to end if the value is zero
            (int) IS.OUT, 0,      // Otherwise print it to the console
            (int) IS.DROP,        // And pop it
            (int) IS.PUSH, 1,     // Increment the address
            (int) IS.BINARY, 0, 1,// ...
            (int) IS.JMP, 22,     // and repeat
            // Label 37 - end loop
            (int) IS.HALT
        ];

        Console.WriteLine("CPU has started.");

        var cpu = CreateTestCPU(20, program, "");
        var assumedTicks = 0;
        // Simple debugger to log executed instructions
        // var instructionLog = "";
        // cpu.Debugger = (instruction) =>
        // {
        //     instructionLog += $"({instruction}) ";
        //     return false;
        // };

        while (!cpu.Halted)
        {
            cpu.ProcessTicks(10);
            assumedTicks += 10;

            Assert.That(assumedTicks < 10000, "Endless loop in CPU detected.");
        }
        Console.WriteLine("CPU has halted.");
        //Console.WriteLine($"Executed the following:\n{instructionLog}");

        Assert.That((cpu.IOProvider as ConsoleIOProvider)?.CombinedOutput, Is.EqualTo("hello world!"));
    }

    [Test]
    public void TestEchoProgramWithIO()
    {
        int[] program =
        {
            // Data section
            'w', 'r', 'i', 't', 'e', ' ', 's', 't', 'r', 'i', 'n', 'g', '\n', 0, // Ints 0-13 - phrase to output
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // Ints 15-29 - empty string buffer
            // Note: with the current input, the code will overflow and write over the code section
            // Very safe innit?

            // Code section - int 30
            (int) IS.PUSH, 0,                       // Push the address of the string onto the stack, acting as the counter
            // Label 32 - begin print loop
            (int) IS.LOAD, -1,                      // Load value from memory via address on the stack
            (int) IS.JMPC, 0, 47, // Jump to next step if value is 0
            (int) IS.OUT, 0,                        // Otherwise print it to the console
            (int) IS.DROP,                          // And pop it
            (int) IS.PUSH, 1,                       // Increment the address
            (int) IS.BINARY, 0, 1,                  // ...
            (int) IS.JMP, 32,                       // and repeat
            // Label 47 - prepare input loop
            (int) IS.DROP,                          // Drop the address
            (int) IS.PUSH, 15,                      // Push the address of the buffer onto the stack
            // Label 50 - begin input loop
            (int) IS.IN, 0,                         // Read a char onto the stack
            (int) IS.STORE, -1,                     // Store it in the buffer
            (int) IS.JMPC, 0, 65,                   // Jump to next step if value is 0
            (int) IS.DROP,                          // Otherwise pop it
            (int) IS.PUSH, 1,                       // Increment the address
            (int) IS.BINARY, 0, 1,                  // ...
            (int) IS.JMP, 50,                       // and repeat
            // Label 65 - prepare echo loop
            (int) IS.PUSH, 15,                      // Push the address of the buffer onto the stack
            // Label 67 - begin echo loop
            (int) IS.LOAD, -1,                      // Load value from memory via address on the stack
            (int) IS.JMPC, 0, 82,                   // Jump to end if value is 0
            (int) IS.OUT, 0,                        // Otherwise print it to the console
            (int) IS.DROP,                          // And pop it
            (int) IS.PUSH, 1,                       // Increment the address
            (int) IS.BINARY, 0, 1,                  // ...
            (int) IS.JMP, 67,                       // and repeat
            // Label 82 - end
            (int) IS.HALT
        };

        Console.WriteLine("CPU has started.");

        var cpu = CreateTestCPU(30, program, "chicken nuggies\0");
        var assumedTicks = 0;
        // Simple debugger to log executed instructions
        var instructionLog = "";
        cpu.Debugger = (instruction) =>
        {
            instructionLog += $"({instruction}) ";
            return false;
        };

        while (!cpu.Halted)
        {
            cpu.ProcessTicks(10);
            assumedTicks += 10;

            Assert.That(assumedTicks < 10000, "Endless loop in CPU detected.");
        }
        Console.WriteLine("CPU has halted.");
        Console.WriteLine($"Executed the following:\n{instructionLog}");

        Assert.That((cpu.IOProvider as ConsoleIOProvider)?.CombinedOutput,
            Is.EqualTo("write string\nchicken nuggies"));
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
