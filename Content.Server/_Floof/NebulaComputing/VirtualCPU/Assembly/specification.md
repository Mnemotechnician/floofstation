# Introduction
This is a specification for the NebulaASM assembly language.

NebulaASM is a primitive assembly language. It does not feature advanced capabilities like multi-file processing,
structures, and other features modern assembly languages have.

# Syntax
A NebulaASM program is a sequence of statements.

Statements are separated by semicolons.

Each statement can either instruct the compiler to do something (e.g. create a new label or section)
or specify an instruction to output to the binary file.

# File format
Each file must begin with some compiler instructions.

Until a section is made, no assembly instructions can be specified.

# Sections
A section is created using the following syntax:
```asm
.section name {
    // statements
}
```
Everything inside the curly brackets belongs to that section.

# Labels
A label is created using the following syntax:
```asm
label:
    // statements
```

# Instructions
An instruction is created using the following syntax:
```asm
instruction [arguments]

// For example, add 1 and 2 together and output the sum:
push 1
push 2
add int
out 0
```

Some instructions support prefixes. For example, the PRESERVE_STACK flag will prevent the instruction
from popping the stack. The list of all can be found in the CPU specification.
```asm
push charArrayLabel     // Load the address of a char array
psp load onstack        // Load the character from it without popping the address
psp jmpc someLabel zero // Jump if zero, do not pop the stack
```

# Numbers
Numbers in NebulaASM have the following syntaxes:

## Integer
- 123
- -123
- 0x16DEC13A1
- -0xff
- onstack // Constant -1, some commands use that to indicate that the value is to be popped from the stack.

## Float
- 1.0
- -1.0
- 345

## Character arrays
- "Hello, world!"
- 'Hello, world!'
- "Hello world!", 0x0A, "Newline", 0x00
- 123

# Data
Data embeddings are created using the following syntax:
```asm
.section data {
    int a 123
    int b 456
    char c "Hello, world!"
    float pi 3.14
}
```

You can also declare variables, arrays, and unnamed data inside other sections, but that is not recommended.

# Arrays
Arrays are created using the following syntax:
- int array_name 1, 2, 3, 4, 5, 6
- char array_name "Hello, world!", 0x0A, "Newline", 0x00

# Program example
```adm
.start code
.section data {
    int counter 0
    int a 6
    int b 2
}

.section code {
    // Loop on counter until 10. Each time, multiply a by b and add a to b, and output a.
    loop:
        load a
        load b
        mul int
        psp store a
        load b
        add int
        store b

        load a
        out 0

        load counter
        push 1
        add int
        dup
        store counter

        push 10
        cmp lower
        jmpc loop one
    end:
    halt
}
```

# Functions
You can implement functions by wrapping the function code in a section and using a `ret` instruction at its end.
You can then call the function by using the `call` command with the section name as the argument.

Arguments can be passed through the stack by `push`ing them on the stack before `call`ing the function.
Note that when you use the `call` instruction, the CPU puts the return address on the stack.

You can access arguments inside the function using the `dup <arg_count - arg_idx + 1>` syntax.
This will read the `arg_idx`th argument and put it on top of the stack.

The `ret <after call pops> <before call pops>` instruction accepts two arguments:
- `<after call pops>` is the number of values the CPU must clear from the stack before reading the return address.
  This is effectively the amount of stack memory the function has reserved.
- `<before call pops>` is the amount of values the CPU must clear from the stack AFTER reading the return address.
  This is effectively the number of arguments the function accepts, allows the callee to do post-call cleanup.
