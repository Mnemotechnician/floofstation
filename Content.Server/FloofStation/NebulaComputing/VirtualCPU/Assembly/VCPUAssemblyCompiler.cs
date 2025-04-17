using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using YamlDotNet.Core.Tokens;


namespace Content.Server.FloofStation.NebulaComputing.VirtualCPU.Assembly;

using IS = InstructionSet;

// Shush
// ReSharper disable BadControlBracesLineBreaks
// ReSharper disable MissingLinebreak

/// <summary>
///    Compiles assembly code into the binary format.
///    Not thread-safe. Use sparingly.
/// </summary>
public sealed class VCPUAssemblyCompiler
{
    private static readonly Dictionary<string, int> SimpleInstructions = new()
    {
        {"nop", 0x00},
        {"dup", 0x04},
        {"drop", 0x05},
        {"halt", 0xff},
    };

    private string _input = default!;
    private int _pos;
    private List<int> _output = new();
    private List<string> _errors = new();
    private List<Section> _sections = new();
    private List<Label> _labels = new();
    private StringBuilder _idBuilder = new();
    private List<(int addressIntegerPosition, string targetLabel)> _labeledJumps = new();

    /// <summary>Address at which the next written byte will appear. </summary>
    private int CurrentAddress => _output.Count;
    private string? _startSectionName;
    private Section? CurrentSection = null;

    /// <summary>
    ///     Not null after Compile() is called if there were any errors during compilation.
    /// </summary>
    public List<string>? Errors;
    /// <summary>
    ///     Not null after Compile() is called.
    /// </summary>
    private List<int>? Output;

    public bool Errored => Errors is not null && Errors.Count > 0;

    public bool Compile(string input)
    {
        _input = input;
        _pos = 0;
        _output.Clear();
        _errors.Clear();
        _sections.Clear();
        _labels.Clear();
        _labeledJumps.Clear();
        _startSectionName = null;

        // Output a jump to the starting section
        WriteInstruction(IS.JMP);
        Write(int.MaxValue);

        while (_pos < _input.Length) {
            TopLevelStatement();

            if (_errors.Count > 9) {
                _errors.Append("Too many errors. Stopping compilation.");
                break;
            }
        }

        if (_startSectionName == null) {
            _errors.Append("No start section specified. Specify one with .start <name>");
        } else if (_sections.Find(it => it.Name == _startSectionName) is not {} startSection) {
            _errors.Append($"Start section {_startSectionName} does not exist.");
        } else {
            _output[1] = startSection.StartAddress;
        }


        Output = _output;
        Errors = _errors.Count > 0 ? _errors : null;

        return Errored;
    }

    /// <summary>
    ///     Compiler instruction or section definition.
    /// </summary>
    private void TopLevelStatement()
    {
        SkipWS();
        var mark = _pos;
        int startSection = 0;

        if (Match(".section")) {
            SkipWS();
            if (Identifier() is not { } name) {
                Error("Expected an identier");
                name = "ERROR";
            }
            SkipWS();

            if (!MatchOrError("{"))
                return;

            var section = new Section(name, CurrentAddress);
            _sections.Add(section);

            // Pass 1 - read everything within
            _labeledJumps.Clear();
            SkipWS();
            while (!Match("}"))
            {
                CurrentSection = section; // Inside of the loop in case of nested sections if those are ever added
                SectionStatement();
                EndOfStatement();

                if (_pos < _input.Length)
                    continue;

                Error("Unexpected end of input");
                break;
            }

            CurrentSection = null;
            EndOfStatement();

            // Pass 2 - resolve labels
            foreach (var (jumpAddr, label) in _labeledJumps)
            {
                if (_labels.Find(it => it.Name == label) is { } targetLabel)
                    _output[jumpAddr] = targetLabel.StartAddress;
                else if (_sections.Find(it => it.Name == label) is { } targetSection)
                    _output[jumpAddr] = targetSection.StartAddress;
                else
                    Error($"Label {label} does not exist", jumpAddr);
            }

            return;
        } else if (Match(".start")) {
            SkipWS();
            var name = Identifier();
            EndOfStatement();
            return;
        }

        Error("Expected a top-level statement");
    }

    private void SectionStatement()
    {
        if (Peek() == '.')
        {
            // Nested sections???
            // Don't try this at home
            TopLevelStatement();
            // This will expect two semicolons at the end. Maybe just don't use nested sections??? To do: fix it, or don't.
            return;
        }

        SkipWS();
        if (Identifier() is not {} name)
            return;

        // Simple instructions with no arguments
        if (SimpleInstructions.TryGetValue(name, out var simpleId))
        {
            Write(simpleId);
            return;
        }

        // Binary operations
        if (Enum.TryParse<BinaryOperationType>(name, true, out var operationType))
        {
            WriteInstruction(IS.BINARY);
            Write((int) OperandTypeOrError());
            Write((int) operationType);
            return;
        }

        switch (name.ToLower())
        {
            // Instructions with one integer argument
            case "load":
            case "push":
            case "out":
            case "in":
            {
                if (Integer() is not { } addr)
                {
                    Error("Expected exactly one integer argument.");
                    break;
                }

                WriteInstruction(Enum.Parse<IS>(name.ToUpper()));
                Write(addr);
                break;
            }

            case "jmp":
            case "jmpc":
            {
                if (Integer() is not { } addr)
                {
                    Error("Expected jump address.");
                    break;
                }

                WriteInstruction(IS.JMPC);
                if (name.Equals("jmpc", StringComparison.OrdinalIgnoreCase))
                    Write((int) JumpTypeOrError());

                if (Integer() is { } absoluteAddr)
                    Write(absoluteAddr);
                else if (Identifier() is { } label) {
                    _labeledJumps.Add((CurrentAddress, label));
                    Write(int.MaxValue);
                } else {
                    Error("Expected jump address or label");
                    SkipUntilTerminator();
                }

                break;
            }

            case "int":
            case "float":
            {
                if (Identifier() is not { } label)
                {
                    Error("Expected a label");
                    label = "???";
                }

                CPUMemoryCell initializer;
                // The below return values are never null even though the compiler thinks otherwise
                if (name == "int")
                    initializer = CPUMemoryCell.FromInt32(Integer() ?? 0);
                else
                    initializer = CPUMemoryCell.FromSingle(Float() ?? 0f);

                _labels.Add(new Label(label, CurrentAddress, CurrentSection));
                Write(initializer.Int32);
                break;
            }
        }
    }

    private void EndOfStatement()
    {
        SkipWS();
        if (!Match(";"))
            Error("Expected a semicolon after statement");

        SkipWS();
    }

    private (int, int) GetLineNumberAndPosAt(int position)
    {
        if (position >= _input.Length)
            return (-1, -1);

        var pos = 0;
        var line = 1;
        var posInLine = 1;
        while (pos < position)
        {
            if (_input[pos] == '\n')
            {
                line++;
                posInLine = 1;
            }
            pos++;
            posInLine++;
        }

        return (line, posInLine);
    }

    /// <summary>
    ///     Tries to match an identifier. Returns it if successful.
    /// </summary>
    /// <returns></returns>
    private string? Identifier()
    {
        var mark = _pos;
        var ch = Next();
        if (!char.IsLetter(ch))
        {
            _pos = mark;
            return null;
        }

        _idBuilder.Clear();
        do
        {
            _idBuilder.Append(ch);
            ch = Next();
        } while (char.IsLetter(ch) || char.IsNumber(ch));

        return _idBuilder.ToString();
    }


    private T DelegateOrError<T>(Func<T?> delegated, string expectation, T fallback = default!) where T : notnull
    {
        SkipWS();
        if (delegated() is not {} result)
        {
            Error($"Expected {expectation}");
            return fallback;
        }

        return result;
    }

    /// <see cref="Identifier"/>
    private int? Integer()
    {
        var mark = _pos;
        var ch = Next();
        if (!char.IsDigit(ch))
        {
            _pos = mark;
            return null;
        }

        var number = 0;
        while (char.IsDigit(ch))
        {
            number = 10 * number + (ch - '0');
            ch = Next();
        }

        return number;
    }

    private float? Float()
    {
        var mark = _pos;
        if (Integer() is not { } integerPart)
            return null;

        mark = _pos;
        if (!Match(".") || Integer() is not { } fractionalPart)
            return (float) integerPart;

        var fractLength = _pos - mark - 1; // This hacky way to get the length of the fractional part
        return integerPart + fractionalPart * float.Pow(0.1f, fractLength);
    }

    // Boilerplate because I can't be bothered to go generic here
    // I just wanna get over with this compiler so I can get to writing the real language
    private JumpType JumpTypeOrError()
    {
        SkipWS();
        if (Identifier() is not { } name || !Enum.TryParse<JumpType>(name, true, out var value))
        {
            Error("Expected a jump type");
            return JumpType.Zero; // Error recovery
        }

        return value;
    }

    private OperandType OperandTypeOrError()
    {
        SkipWS();
        if (Identifier() is not { } name || !Enum.TryParse<OperandType>(name, true, out var value))
        {
            Error("Expected an operand type");
            return OperandType.Int; // Error recovery
        }

        return value;
    }

    private bool MatchOrError(string seq)
    {
        var success = Match(seq);
        if (!success)
            Error($"Expected {seq}");

        return success;
    }

    /// <summary>
    ///     Tries to match a given sequences. Returns true and advances on success.
    /// </summary>
    private bool Match(string seq)
    {
        var mark = _pos;
        var seqPos = 0;
        while (seqPos < seq.Length && Next() == seq[seqPos])
            seqPos++;

        if (seqPos == seq.Length)
            return true;

        _pos = mark;
        return false;
    }

    private void SkipWS()
    {
        while (Peek() is ' ' or '\t' or '\n' or '\r')
            _pos++;

        // Skip comments and recursively skip whitespace after them
        if (Match("//"))
        {
            while (Peek() is not '\n' and not '\0')
                _pos++;
            SkipWS();
        } else if (Match("/*")) {
            while (!Match("*/"))
                _pos++;
            SkipWS();
        }
    }

    private void SkipUntilTerminator()
    {
        while (Peek() != ';')
            _pos++;
    }

    /// <returns>Next char or '\0' if end of input. Advances one char forward.</returns>
    private char Next() => _pos >= _input.Length ? '\0' : _input[_pos++];

    /// <returns>Next char or '\0' if end of input.</returns>
    private char Peek() => _pos >= _input.Length ? '\0' : _input[_pos];

    private void WriteInstruction(IS instruction) => _output.Append((int) instruction);

    private void Write(int value) => _output.Append(value);

    private void Error(string message, int addr = -1)
    {
        if (addr == -1)
            addr = _pos;

        var (line, pos) = GetLineNumberAndPosAt(addr);
        _errors.Append($"{message} at {line}:{pos}.");
    }

    private class Section(string name, int startAddress)
    {
        public string Name = name;
        public int StartAddress = startAddress;
    }

    private class Label(string name, int startAddress, Section? inSection)
    {
        public string Name = name;
        public int StartAddress = startAddress;
        public Section? inSection = inSection;
    }
}
