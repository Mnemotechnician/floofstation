# Introduction
This is the specification the NebulaComputing CPU architecture.

For computer workflow, see [computer workflow](../computer%20workflow.md)

For NebulaASM specification, see [specification](Assembly/specification.md)

# Memory overview
Memory is an array of 32-bit values.

The CPU has a set of registers used to store intermediate and operational values. The architecture is similar to x86.

# Instruction Set Overview

Operands can be:
0. Immediate - constant data.
1. Register - data is stored in a register like RAX, RSP, RIP.
2. Static - data is stored in a static memory location.
3. Dynamic - data is stored in a memory location specified by a register.

The opcode is encoded as the first byte of an instruction
The types of operands are encoded as byte 2 of the opcode, 2 bits per operand.
The remaining 3rd and 4th bytes are reserved for per-instruction use (the jmp and binary commands use it to encode their types)

| Instruction | Arguments   | Description                                                                                                                           |
|-------------|-------------|---------------------------------------------------------------------------------------------------------------------------------------|
| NOP         |             | No operation; does nothing (useful for timing or alignment).                                                                          |
| MOV         | dst, src    | Copies data from operand 2 to operand 1. Both operands can be of any location type.                                                   |
| PUSH        | src         | Push src onto the stack.                                                                                                              |
| POP         | dst         | Pop an operand from the stack and copy its value to dst If location is an immediate value, drop the popped value.                     |
| BINARY      | dst, a, b   | See below: binary operations. This instruction is bytecode only, assembly uses mnemonics for each operation kind.                     |
| UNARY       | dst, a      | See below: unary operations. This instruction is bytecode only, assembly uses mnemonics for each operation kind.                      |
| JMP         | addr        | Jump to the instruction at the specified address.                                                                                     |
| JMPC        | addr        | Jump to the instruction at the specified address if the condition specified in the reserved byte passess. See jump types below.       |
| CMP         | a, b        | Compare the values of the two arguments and set the RFLAG register for use by the JMPC instruction.                                   |
| OUT         | 0x00, src   | Output src to the virtual console (typically 0th port is the virtual console).                                                        |
| OUT         | port, value | Output src to the specified port. This MAY block if the output queue is full.                                                         |
| IN          | port, dst   | Read input from the virtual console or port and copy it to dst. This MAY block until there is any input. See IO remarks.              |
| CALL        | addr        | Push the instruction pointer onto the stack and jump to the instruction at memory address addr.                                       |
| RET         | num         | Pop the stack and return from the function, then reduce the stack counter by num (clearing num arguments).                            |
| ENTER       | num         | Enter a new stack frame (pushing RBP on the stack and copying RSP to RBP) and allocate num variables on the stack.                    |
| LEAVE       |             | Leave the current stack frame (setting the RSP register to RBP and popping RBP from the stack) and thus dropping all local variables. |
| HALT        |             | Stop execution of the program.                                                                                                        |

## IO remarks
In ECS mode (in-game), the in/out instructions do different things depending on the number of the port.

- Port 0 typically corresponds to the terminal console.
- Port 1 typically corresponds to the storage drive interface (TOOD)
- Ports 11..14 correspond to the 1..4 device links (pins you can connect to other devices via a multitool). Be careful, reading from those may block!
- Ports 21..24 return whether the respective 1..4 device links have any ready inputs.

Computers can send numerical data over those ports. Other computers will be able to read the exact data sent,
HOWEVER, due to the limitations of the game, non-computer devices (e.g. airlocks, logic gates) will typically interpret them as boolean values (1 or 0, HIGH or LOW).

## Binary operations
The binary instruction distinguishes the binary operation type and operand types by the value of its reserved field (4th byte).
Whether the instruction operates on floating-point values or on integer values is signified by the last bit of that field.
If it is set, the instruction is floating-point.

All binary operations take three arguments: the destination and two inputs.

| Instruction | Value of the reserved field | Description                                           |
|-------------|-----------------------------|-------------------------------------------------------|
| ADD or FADD | 0x00 or 0x80                | Addition                                              |
| SUB or FSUB | 0x01 or 0x81                | Subtraction                                           |
| MUL or FMUL | 0x02 or 0x82                | Multiplication                                        |
| DIV or FDIV | 0x03 or 0x83                | Division (unlike x86, does not calculate the modulus) |
| MOD or FMOD | 0x04 or 0x84                | Modulus                                               |
| OR          | 0x05                        | Bitwise OR (only integers)                            |
| AND         | 0x06                        | Bitwise AND (only integers)                           |
| XOR         | 0x07                        | Bitwise XOR (only integers)                           |
| FPOW        | 0x88                        | Raise to power (only floats)                          |

## Unary operations
The unary instruction distinguishes the unary operation type and operand type by the value of its reserved field (4th byte).
Whether the instruction operates on floating-point values or on integer values is signified by the last bit of that field.
If it is set, the instruction is floating-point.

All unary operations take two arguments: the destination and one input.

| Instruction | Value of the reserved field | Description                               |
|-------------|-----------------------------|-------------------------------------------|
| NEG or FNEG | 0x00 or 0x80                | Unary negation                            |
| ABS or FABS | 0x01 or 0x81                | Absolute value                            |
| NOT         | 0x02                        | Bitwise NOT (only integers)               |
| FSQRT       | 0x83                        | Square root (only floats)                 |
| FSIN        | 0x84                        | Sine (only floats)                        |
| FCOS        | 0x85                        | Cosine (only floats)                      |
| FTAN        | 0x86                        | Tangent (only floats)                     |
| FASIN       | 0x87                        | Arcsine (only floats)                     |
| FACOS       | 0x88                        | Arccosine (only floats)                   |
| FATAN       | 0x89                        | Arctangent (only floats)                  |
| INC or FINC | 0x0A or 0x8A                | Increment (only floats)                   |
| DEC or FDEC | 0x0B or 0x8B                | Decrement (only floats)                   |
| FLOOR       | 0x8C                        | Floor (only floats)                       |
| CEIL        | 0x8D                        | Ceil (only floats)                        |
| ROUND       | 0x8E                        | Round (only floats)                       |
| TRUNC       | 0x8F                        | Truncate (remove integer part from float) |
| LOG         | 0x80                        | Base-e logarithm (only floats)            |

## Conditional jumps
The jmpc instruction distinguishes the jump type by the value of its reserved field (4th byte).
The condition is evaluated based on the value of the RFLAG register, which is set by invoking the CMP instruction.

| Instruction | Value of the reserved field | Description                                         |
|-------------|-----------------------------|-----------------------------------------------------|
| JMPE        | 0x00                        | Jump if values are equal.                           |
| JMPNE       | 0x01                        | Jump if values are not equal                        |
| JMPL        | 0x02                        | Jump if value 1 is less than value 2                |
| JMPLE       | 0x03                        | Jump if value 1 is less than or equal to value 2    |
| JMPG        | 0x04                        | Jump if value 1 is greater than value 2             |
| JMPGE       | 0x05                        | Jump if value 1 is greater than or equal to value 2 |

## Comparisons
The cmp instruction compares the values of its two operands and sets the RFLAG register.
The reserved field (4th byte) is used to distinguish the comparison type (0 is int, 1 is float, 2 is unsigned int).

| Instruction | Value of the reserved byte | Description                                           |
|-------------|---------------------------|-------------------------------------------------------|
| CMP         | 0x00                      | Compare two integer values                            |
| FCMP        | 0x01                      | Compare two floating point values                     |
| UCMP        | 0x02                      | Compare two unsigned integer values                   |

### Data definitions
| Instruction | Arguments           | Description                                                                        |
|-------------|---------------------|------------------------------------------------------------------------------------|
| int         | label initial_value | Define a new integer value with label "label".                                     |
| float       | label initial_value | Define a new floating point value with label "label".                              |
| char        | label initial_value | Define a new character value with label "label". Supports multi-character strings. |

Those translate to a literal byte sequence in the memory.
Placing those definitions inside a code segment must be done with care as the CPU will attempt to execute the stored data as instructions if it ever encounters it.

An array can be defined by separating initial values with commas.


# Errors
If the CPU encounters an error, it will output the error code to the virtual console and stop execution. Some examples are:

| Code | Name               |                                                                                                                     |
|------|--------------------|---------------------------------------------------------------------------------------------------------------------|
| 0x01 | IllegalInstruction | The processor encountered an unknown opcode.                                                                        |
| 0x02 | SegmentationFault  | An instruction has attempted to access an out-of-bounds memory region.                                              |
| 0x03 | StackOverflow      | The stack has overflowed.                                                                                           |
| 0x04 | StackUnderflow     | The stack has underflowed.                                                                                          |
| 0x05 | DivisionByZero     | An integer division by zero has occurred.                                                                           |
| 0x06 | InvalidType        | A type argument was not a valid values (for jmpc, binary, unary instructions).                                      |
| 0x07 | InvalidPort        | A read or write operation has been attempted on an invalid port.                                                    |
| 0x08 | SectionBoundary    | A subset of IllegalInstruction, the CPU has encountered a section boundary (missing a `ret` or `halt` instruction). |

