using Content.Server._Floof.NebulaComputing.VirtualCPU;
using Content.Server._Floof.NebulaComputing.VirtualCPU.Assembly;
using Content.Server._Floof.NebulaComputing.VirtualCPU.Cpu;
using Iced.Intel;
using NUnit.Framework;

namespace Content.Tests.Server._Floof.NebulaComputing;

using IS = InstructionSet;
using DL = CPUInstructionCell.DataLocation;
using Reg = CPURegister;
using JT = JumpType;
using UOT = UnaryOperationType;

[TestFixture]
[TestOf(typeof(VirtualCPU))]
[TestOf(typeof(VCPUAssemblyCompiler))]
public sealed partial class VirtualCPUTest
{
    // This is beatiful. We have a total of 4 layers between the CPU and our code here.
    // The first layer is machine code. The second layer is C#'s IL running the program with our virtual CPU.
    // The third layer is the bytecode of the CPU. Finally, the fourth layer is this... pseudo-assembly language we are using here.
    // If aliens exist and they find this project, our race won't survive their judgment.
    [Test]
    public void TestSimpleProgramWithOutput()
    {
        var unaryAddInPlace = Opcode(IS.Unary, DL.Register, DL.Register, type: (byte) UOT.Inc);
        int[] program =
        [
            // Data section
            'h', 'e', 'l', 'l', 'o', ' ', 'w', 'o', 'r', 'l', 'd', '!', // Ints 0-11 - phrase to output
            0, 0, 0, 0, 0, 0, 0, 0, // 14-19 - padding ints

            // Code section - int 20
            Opcode(IS.Mov, DL.Register),               (int) Reg.RSRC, Addr(0),                     // mov rsrc, text
            // Label 23 - loop until the null character, output chars
            Opcode(IS.Mov, DL.Register, DL.Dynamic),   (int) Reg.RAX,  (int) Reg.RSRC,              // mov rax, [rsrc]
            Opcode(IS.Cmp, DL.Register),               (int) Reg.RAX,  0,                           // cmp rax, 0
            Opcode(IS.JmpC, type: (byte) JT.Equal),    Addr(39),                                    // jeq 36
            Opcode(IS.Out, DL.Immediate, DL.Register), 0,              (int) Reg.RAX,               // out 0, rax
            unaryAddInPlace,                              (int) Reg.RSRC, (int) Reg.RSRC,              // inc rsrc, rsrc
            Opcode(IS.Jmp),                            Addr(23),                                    // jmp 23
            // Label 39: end of loop
            Opcode(IS.Halt)                                                                         // halt
        ];

        TestSimpleCPU(20, program, "", "hello world!", debug: false);
    }

    [Test]
    public void TestEchoProgramWithIO()
    {
        // int[] program =
        // {
        //     // Data section
        //     'w', 'r', 'i', 't', 'e', ' ', 's', 't', 'r', 'i', 'n', 'g', '\n', 0, // Ints 0-13 - phrase to output
        //     0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // Ints 14-29 - empty string buffer
        //     // Note: with long enough input, this will overflow and write over the code section
        //     // Very safe innit? Smth smth buffer overflow attacks in ss14
        //
        //     // Code section - int 30
        //     // Jump over the function
        //     (int) IS.Jmp, 60,
        //
        //     // Label 32 - FUNCTION - write a null-terminated string to console specified by the address on the stack.
        //     (int) IS.DUP, 1,                            // Copy the argument
        //     // Label 34 - begin loop
        //     (int) IS.LOAD_PSP, -1,                      // Load value from memory via address on the stack
        //     (int) IS.JMPC_PSP, 48, (int) JumpType.Zero, // Jump to end if the value is zero
        //     (int) IS.Out, 0,                            // Otherwise print it to the console
        //     (int) IS.Push, 1,                           // Increment the address
        //     (int) IS.Binary, 0, 1,                      // ...
        //     (int) IS.Jmp, 34,                           // and repeat
        //     // label 48: function end
        //     (int) IS.Ret, 2, 1,                         // Clear 2 items from the stack, return, and clear 1 more value from the stack
        //
        //     (int) IS.SectionBoundary,  0, 0, 0, 0, 0,  0, 0, 0, // Padding
        //
        //     // Label 60 - main
        //     (int) IS.Push, 0,             // Push the address of the string onto the stack, acting as the counter
        //     (int) IS.Call, 32,            // Print the string
        //
        //     // Label 64 - Prepare read loop
        //     (int) IS.Push, 14,                      // Push the address of the string onto the stack, acting as the counter
        //     // Label 66 - read loop
        //     (int) IS.In, 0,                         // Read a character
        //     (int) IS.DUP, 0,                        // Duplicate the character
        //     (int) IS.DUP, 2,                        // Duplicate the address TODO THIS SUCKS ASS, ADD AN SWP INSTRUCTION OR SMTH!!!
        //     (int) IS.STORE, -1,                     // Store the character at the address
        //     (int) IS.JmpC, 90, (int) JumpType.Zero, // Jump if the character is null
        //     (int) IS.Push, 1,                       // Otherwise increment the address
        //     (int) IS.Binary, 0, 1,                  // ...
        //     (int) IS.Jmp, 66,                       // and repeat
        //     0, 0, 0, 0, // Padding
        //
        //     // Label 90 - end read loop
        //     (int) IS.DROP, 0,  // Drop the address
        //     (int) IS.DROP, 0,  // Drop the character
        //     (int) IS.Push, 14, // Push the address of the string onto the stack
        //     (int) IS.Call, 32, // Print the string
        //     (int) IS.Halt
        // };
        //
        // TestSimpleCPU(30, program, "chicken nuggies\0", "write string\nchicken nuggies", debug: false);
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
