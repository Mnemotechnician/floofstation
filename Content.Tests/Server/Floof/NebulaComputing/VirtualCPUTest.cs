using System;
using System.Collections.Generic;
using Content.Server.FloofStation.NebulaComputing.VirtualCPU;
using Content.Server.FloofStation.NebulaComputing.VirtualCPU.Assembly;
using JetBrains.Annotations;
using NUnit.Framework;

namespace Content.Tests.Server.Floof.NebulaComputing ;

using IS = InstructionSet;


public sealed partial class VirtualCPUTest
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

        TestSimpleCPU(20, program, "", "hello world!", false);
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

        TestSimpleCPU(30, program, "chicken nuggies\0", "write string\nchicken nuggies");
    }

    [Test]
    public void TestSimpleAssembly()
    {
        var asm = """
            .start code;
            .section data {
                int wowie 25;
                int 10;
            }
            .section code {
                nop; nop; nop;

                push 1;
                push 2;
                push 3;
                add int; // 2 + 3

                load wowie;
                mul int; // * 25

                out 0; out 0; out 0; // Print char 125 3 times
                drop;

                halt;
            }
            """.Trim();

        var assembler = new VCPUAssemblyCompiler();
        var (success, code, entryPoint) = assembler.Compile(asm);

        Assert.That(assembler.Errors, Is.Null, "Found errors while assembling:\n" + string.Join('\n', assembler.Errors ?? []));
        Assert.That(success, Is.True, "No errors but failed to assemble");

        char c125 = (char) 125;
        TestSimpleCPU(entryPoint, code, expectedOutput: $"{c125}{c125}{c125}", debug: true);
    }
}
