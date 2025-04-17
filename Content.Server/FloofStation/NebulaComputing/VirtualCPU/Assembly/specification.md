# Introduction
This is a specification for the NebulaASM assembly language.

NebulaASM is a primitive assembly language. It does not feature advanced capabilities like multi-file processing,
functions, structures, and other features modern assembly languages have.

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
drop
```

# Program example
```adm
.start code
.section data {
    int counter 0
    int a 6
    int b 2
}

.section code {
    push counter
    push 0
    // Loop on counter until 10. Each time, multiply a by b and add a to b, and output a.
    loop:
        drop
        load a
        load b
        mul int
        store a
        load b
        add int
        store b

        drop
        load a
        out 0
        drop

        push 10
        jmpc lower loop
    end:
    halt
}
```
