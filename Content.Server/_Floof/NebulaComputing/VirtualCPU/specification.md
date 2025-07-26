# Introduction
This is the specification the NebulaComputing CPU architecture.

For computer workflow, see [computer workflow](../computer%20workflow.md)

For NebulaASM specification, see [specification](Assembly/specification.md)

# Memory overview
Memory is an array of 32-bit values.

# Instruction Set Overview
Binary format: numbers like 0x04 specify the exact binary value that the integer at the instruction pointer must have.
Symbols like int specify the instruction pointer must be followed by a 32-bit value, aka an integer.

All operations that mention taking something from the stack, pop the stack.

All operations that take an address or a port as a parameter can be given the value of -1 if the address is to be read from the stack.

When the stack is popped multiple times, address arguments take precedence over non-address ones (i.e. the value is popped second, and the address is popped first).

| Instruction | Arguments  | Binary format | Description                                                                                                                 |
|-------------|------------|---------------|-----------------------------------------------------------------------------------------------------------------------------|
| NOP         |            | 0x00          | No operation; does nothing (useful for timing or alignment).                                                                |
| LOAD        | addr       | 0x01 int      | Load data from memory address addr onto the stack. If addr is -1, then stack is popped and value is used as address.        |
| PUSH        | value      | 0x02 int      | Push a constant value onto the operation stack, OR push the address of the specified label.                                 |
| STORE       | addr       | 0x03 int      | Store data from the top of the stack into \[addr\].                                                                         |
| DUP         | rel        | 0x04 int      | Read the value at (stack top + rel) without removing it, and push it onto the stack.                                        |
| DROP        | rel        | 0x05 int      | Remove the value at (stack top + rel). Removing non-top elements has greater performance cost.                              |
| BINARY      | OTYPE KIND | 0x06 int int  | See below: binary operations. This instruction is bytecode only, assembly uses mnemonics for each operation kind.           |
| JMP         | addr       | 0x07          | Jump to the instruction at memory address addr.                                                                             |
| JMPC        | CTYPE addr | 0x08 int int  | Pops the stack and performs a conditional jump based on it.                                                                 |
| OUT         | 0x00       | 0x09 0x00     | Pop the stack and output to the virtual console (typically 0th port is the virtual console).                                |
| OUT         | port       | 0x09 int      | Pop the stack and output to the specified port. This MAY block if the output queue is full.                                 |
| IN          | port       | 0x0A int      | Read input from the virtual console or port onto the stack. This MAY block until there is any input, depending on the port. |
| CALL        | addr       | 0x0B          | Push the instruction pointer onto the stack and jump to the instruction at memory address addr.                             |
| RET         | num1 num2  | 0x0C int int  | Pop num1 stack frames, pop the stack and return from the function, pop num2 more frames.                                    |
| HALT        |            | 0x0D          | Stop execution of the program.                                                                                              |

Any command can include one of the following flags in its instruction code (combine via binary OR):
- PRESERVE_STACK (0x100000) - Do not modify the stack. This is useful for commands like JMPC which normally pop the stack regardless of which branch is taken.

## IO remarks
In ECS mode (in-game), the in/out instructions do different things depending on the number of the port.

- Port 0 typically corresponds to the terminal console.
- Port 1 typically corresponds to the storage drive interface (TOOD)
- Ports 11..14 correspond to the 1..4 device links (pins you can connect to other devices via a multitool). Be careful, reading from those may block!
- Ports 21..24 return whether the respective 1..4 device links have any ready inputs.

Computers can send numerical data over those ports. Other computers will be able to read the exact data sent,
HOWEVER, due to the limitations of the game, non-computer devices (e.g. airlocks, logic gates) will typically interpret them as boolean values (1 or 0, HIGH or LOW).

## Assembly pseudo-instructions. They do not have a binary equivalent.
### Binary operations

| Instruction | Arguments  | Binary format | Description                                                                                         |
|-------------|------------|---------------|-----------------------------------------------------------------------------------------------------|
| ADD         | TYPE       | 0x06 int 0x01 | Add the two top values on the stack together, and store the result on the stack.                    |
| SUB         | TYPE       | 0x06 int 0x02 | Subtract the topmost value on the stack from the value below it, and store the result on the stack. |
| MUL         | TYPE       | 0x06 int 0x03 | Multiply the two top values on the stack together, and store the result on the stack.               |
| DIV         | TYPE       | 0x06 int 0x04 | Subtract the value below the top by the top of the stack, and store the result on the stack.        |
| MOD         | TYPE       | 0x06 int 0x05 | Calculate the modulus between the two topmost values, and store the result on the stack.            |

### Data definitions
| Instruction | Arguments           | Description                                                                        |
|-------------|---------------------|------------------------------------------------------------------------------------|
| int         | label initial_value | Define a new integer value with label "label".                                     |
| float       | label initial_value | Define a new floating point value with label "label".                              |
| char        | label initial_value | Define a new character value with label "label". Supports multi-character strings. |

Those translate to a literal byte sequence in the memory.
Placing those definitions inside a code segment must be done with care as the CPU will attempt to execute the stored data as instructions if it ever encounters it.

An array can be defined by separating initial values with commas.

## Remarks
### Binary operation types
The binary operation instructions accept a type argument. The argument can be either int (0) or float (1).

int indicates that operands are integers and a respective integer binary operation is to be applied between them.

float indicates that operands are floating point numbers and a respective floating point binary operation is to be applied between them.

### Conditional jump types
Zero (0) performs a jump if the topmost value is zero.

NonZero (1) performs a jump if the topmost value is non-zero.

# Errors
If the CPU encounters an error, it will output the error code to the virtual console and stop execution.

| Code | Name               |                                                                           |
|------|--------------------|---------------------------------------------------------------------------|
| 0x01 | IllegalInstruction | The processor encountered an unknown opcode.                              |
| 0x02 | SegmentationFault  | An instruction has attempted to access an out-of-bounds memory region.    |
| 0x03 | StackOverflow      | The stack has overflowed.                                                 |
| 0x04 | StackUnderflow     | The stack has underflowed.                                                |
| 0x05 | DivisionByZero     | An integer division by zero has occurred.                                 |
| 0x06 | InvalidType        | A type argument was not a valid values (for jump and binary instructions) |
