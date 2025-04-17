# Startup
Computer starts by setting the instruction counter at 0, resetting RAM,
and reading the program definition from the first bytes of the storage device.
It then proceeds to load the specified program into RAM and start execution.

## Program definition
```c
struct ProgramDefinition {
    int startAddress, // Address at which the program to load begins

    int size,         // Size of the program

    int entryPoint    // Relative address of the entry point (first intruction)
                      // Must be lower than size
}
```

# Memory overview
Memory is an array of 32-bit values.

# Instruction Set Overview
Binary format: numbers like 0x04 specify the exact binary value that the integer at the instruction pointer must have.
Symbols like int specify the instruction pointer must be followed by a 32-bit value, aka an integer.

| Instruction | Arguments | Binary format | Description                                                                                                                                     |
|-------------|-----------|---------------|-------------------------------------------------------------------------------------------------------------------------------------------------|
| NOP         |           | 0x00          | No operation; does nothing (useful for timing or alignment).                                                                                    |
| LOAD        | addr      | 0x01 int      | Load data from memory address addr onto the operation stack. If addr is -1, then the topmost value on the stack is taken as address.            |
| PUSH        | value     | 0x02 int      | Push a constant value onto the operation stack.                                                                                                 |
| STORE       | addr      | 0x03 int      | Store data from the top of the stack into memory address addr, WITHOUT removing it. If addr is -1, then the value below it is taken as address. |
| DUP         |           | 0x04          | Duplicate the value at the top of the stack.                                                                                                    |
| DROP        |           | 0x05          | Remove the value at the top of the stack.                                                                                                       |
| ADD         | TYPE      | 0x06 int 0x01 | Add the two top values on the stack together, remove them from the stack, and store the result on the stack.                                    |
| SUB         | TYPE      | 0x06 int 0x02 | Subtract the topmost value on the stack from the value below it, remove them from the stack, and store the result on the stack.                 |
| MUL         | TYPE      | 0x06 int 0x03 | Multiply the two top values on the stack together, remove them from the stack, and store the result on the stack.                               |
| DIV         | TYPE      | 0x06 int 0x04 | Subtract the value below the top by the top of the stack, remove them from the stack, and store the result on the stack.                        |
| MOD         | TYPE      | 0x06 int 0x05 | Calculate the modulus between the two topmost values, remove them from the stack, and store the result on the stack.                            |
| JMP         | addr      | 0x07          | Jump to the instruction at memory address addr.                                                                                                 |
| JMPC        | TYPE addr | 0x08 int int  | Conditional jump based on the topmost value on the stack.                                                                                       |
| OUT         | 0x00      | 0x09 0x00     | Output the character value at the top of the stack to the virtual console.                                                                      |
| OUT         | port      | 0x09 int      | Output the value at the top of the stack to the virtual console.                                                                                |
| IN          | port      | 0x0A int      | Read input from the virtual console or port onto the stack.                                                                                     |
| HALT        |           | 0xff          | Stop execution of the program.                                                                                                                  |

## Remarks
### Binary operation types
The binary operation instructions (0x05 0x01-0x05) accept a type argument. The argument can be either 0 or 1.

0 indicates that operands are integers and a respective integer binary operation is to be applied between them.

1 indicates that operands are floating point numbers and a respective floating point binary operation is to be applied between them.

### Conditional jump types
0 performs a jump if the topmost value is zero.

1 performs a jump if the topmost value is non-zero.

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
