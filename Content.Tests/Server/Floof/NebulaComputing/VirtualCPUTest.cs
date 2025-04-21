using System;
using System.Collections.Generic;
using Content.Server.FloofStation.NebulaComputing.VirtualCPU;
using Content.Server.FloofStation.NebulaComputing.VirtualCPU.Assembly;
using JetBrains.Annotations;
using NUnit.Framework;

namespace Content.Tests.Server.Floof.NebulaComputing ;

using IS = InstructionSet;


[TestFixture]
[TestOf(typeof(VirtualCPU))]
[TestOf(typeof(VCPUAssemblyCompiler))]
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
            (int) IS.PUSH, 0,                           // Push the address of the string onto the stack, acting as the counter
            // Label 22 - begin loop
            (int) IS.LOAD_PSP, -1,                      // Load value from memory via address on the stack
            (int) IS.JMPC_PSP, 36, (int) JumpType.Zero, // Jump to end if the value is zero
            (int) IS.OUT, 0,                            // Otherwise print it to the console
            (int) IS.PUSH, 1,                           // Increment the address
            (int) IS.BINARY, 0, 1,                      // ...
            (int) IS.JMP, 22,                           // and repeat
            // Label 36 - end loop
            (int) IS.DROP, 0, // Drop the value
            (int) IS.DROP, 0, // Drop the address
            (int) IS.HALT
        ];

        TestSimpleCPU(20, program, "", "hello world!", debug: false);
    }

    [Test]
    public void TestEchoProgramWithIO()
    {
        int[] program =
        {
            // Data section
            'w', 'r', 'i', 't', 'e', ' ', 's', 't', 'r', 'i', 'n', 'g', '\n', 0, // Ints 0-13 - phrase to output
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // Ints 14-29 - empty string buffer
            // Note: with long enough input, this will overflow and write over the code section
            // Very safe innit? Smth smth buffer overflow attacks in ss14

            // Code section - int 30
            // Jump over the function
            (int) IS.JMP, 60,

            // Label 32 - FUNCTION - write a null-terminated string to console specified by the address on the stack.
            (int) IS.DUP, 1,                            // Copy the argument
            // Label 34 - begin loop
            (int) IS.LOAD_PSP, -1,                      // Load value from memory via address on the stack
            (int) IS.JMPC_PSP, 48, (int) JumpType.Zero, // Jump to end if the value is zero
            (int) IS.OUT, 0,                            // Otherwise print it to the console
            (int) IS.PUSH, 1,                           // Increment the address
            (int) IS.BINARY, 0, 1,                      // ...
            (int) IS.JMP, 34,                           // and repeat
            // label 48: function end
            (int) IS.RET, 2, 1,                         // Clear 2 items from the stack, return, and clear 1 more value from the stack

            (int) IS.INVALID,  0, 0, 0, 0, 0,  0, 0, 0, // Padding

            // Label 60 - main
            (int) IS.PUSH, 0,             // Push the address of the string onto the stack, acting as the counter
            (int) IS.CALL, 32,            // Print the string

            // Label 64 - Prepare read loop
            (int) IS.PUSH, 14,                      // Push the address of the string onto the stack, acting as the counter
            // Label 66 - read loop
            (int) IS.IN, 0,                         // Read a character
            (int) IS.DUP, 0,                        // Duplicate the character
            (int) IS.DUP, 2,                        // Duplicate the address TODO THIS SUCKS ASS, ADD AN SWP INSTRUCTION OR SMTH!!!
            (int) IS.STORE, -1,                     // Store the character at the address
            (int) IS.JMPC, 90, (int) JumpType.Zero, // Jump if the character is null
            (int) IS.PUSH, 1,                       // Otherwise increment the address
            (int) IS.BINARY, 0, 1,                  // ...
            (int) IS.JMP, 66,                       // and repeat
            0, 0, 0, 0, // Padding

            // Label 90 - end read loop
            (int) IS.DROP, 0,  // Drop the address
            (int) IS.DROP, 0,  // Drop the character
            (int) IS.PUSH, 14, // Push the address of the string onto the stack
            (int) IS.CALL, 32, // Print the string
            (int) IS.HALT
        };

        TestSimpleCPU(30, program, "chicken nuggies\0", "write string\nchicken nuggies", debug: false);
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

                // Output char 125 two times
                call printNoStackFree;
                call printNoStackFree;

                // This should output 0 and then 125 again
                push 0;
                call printStackFree;
                call printStackFree;

                halt;
            }

            // Print a character from the stack without removing it
            .section printNoStackFree {
                dup 1; // Copy the argument
                out 0;
                ret;
            }

            // Print a character from the stack and remove it
            .section printStackFree {
                dup 1; // Copy the argument
                out 0;
                ret 0 1;
            }
            """.Trim();

        var (code, entryPoint) = Assemble(asm);
        var c125 = (char) 125;
        TestSimpleCPU(entryPoint, code, expectedOutput: $"{c125}{c125}\0{c125}", debug: false);
    }

    [Test]
    public void TestMeowerAssembly()
    {
        var asm = """
            .start code;
            .section data {
                int repeats 8;
                char word "meow", 0x0A, 0;
            }
            .section code {
                load repeats; // i = 8

                sentenceLoop:
                    push 0; // j = 0
                    wordLoop:
                        dup;
                        push word;
                        add int;
                        load -1;
                        psp jmpc wordLoop_end zero;
                        out 0; // Load word[j] and print it if it isn't 0
                        push 1;
                        add int; // j++
                    jmp wordLoop;

                    wordLoop_end:
                    drop; drop; // Drop j and letter
                    push -1;
                    add int;
                psp jmpc sentenceLoop nonZero;
                drop;

                halt;
            }
        """;

        var (code, entryPoint) = Assemble(asm);
        TestSimpleCPU(entryPoint, code, expectedOutput: $"meow\nmeow\nmeow\nmeow\nmeow\nmeow\nmeow\nmeow\n", debug: false);
    }
}
