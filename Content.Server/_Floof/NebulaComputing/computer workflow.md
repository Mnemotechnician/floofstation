# TODO not yet implemented

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
