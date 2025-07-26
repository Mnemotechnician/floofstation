using System.Text;
using Content.Server._Floof.NebulaComputing.VirtualCPU.Cpu;


namespace Content.Server._Floof.NebulaComputing.VirtualCPU.Assembly;

using IS = InstructionSet;

// Shush
// ReSharper disable BadControlBracesLineBreaks
// ReSharper disable MissingLinebreak
// TODO PENDING REWRITE

/// <summary>
///    Compiles assembly code into the binary format.
///    Not thread-safe. Use sparingly.
/// </summary>
public sealed class VCPUAssemblyCompiler
{
    private static readonly Dictionary<string, byte> SimpleInstructions = new()
    {
        {"nop",     (byte) IS.Nop},
        {"halt",    (byte) IS.Halt},
        {"invalid", (byte) IS.Invalid}
    };

    private string _input = default!;
    private int _pos;
    private List<int> _output = new();
    private List<string> _errors = new();
    private List<Section> _sections = new();
    private List<Label> _labels = new();
    private StringBuilder _idBuilder = new();
    private List<(int addressIntegerPosition, string targetLabel)> _labelledAddresses = new();

    /// <summary>Address at which the next written byte will appear. </summary>
    private uint CurrentAddress => (uint) _output.Count;
    private string? _startSectionName;
    private Section? CurrentSection = null;

    /// <summary>True if the current instruction is a "preserve PS" instruction. </summary>
    private bool _inPSPInstruction = false;

    /// <summary>
    ///     Not null after Compile() is called if there were any errors during compilation.
    /// </summary>
    public List<string>? Errors;
    /// <summary>
    ///     Not null after Compile() is called.
    /// </summary>
    public List<int>? Output;

    public bool Errored => Errors is not null && Errors.Count > 0;

    public Result Compile(string input)
    {
        _input = input;
        _pos = 0;
        _output.Clear();
        _errors.Clear();
        _sections.Clear();
        _labels.Clear();
        _labelledAddresses.Clear();
        _startSectionName = null;

        // Read the input and parse it
        while (_pos < _input.Length) {
            TopLevelStatement();

            if (_errors.Count > 9) {
                _errors.Add("Too many errors. Stopping compilation.");
                break;
            }
        }

        if (Errored)
            return Result.Failure;

        // Resolve labels
        foreach (var (jumpAddr, label) in _labelledAddresses)
        {
            // Jump to label is only possible within the same section. Jump to section is allowed everywhere.
            // TODO: disabled. Find a way to support referencing data labels everywhere?
            if (_labels.Find(it => it.Name == label/* && it.inSection == CurrentSection*/) is { } targetLabel)
                _output[jumpAddr] = (int) targetLabel.StartAddress;
            else if (_sections.Find(it => it.Name == label) is { } targetSection)
                _output[jumpAddr] = (int) targetSection.StartAddress;
            else
                Error($"Label {label} does not exist", jumpAddr);
        }

        if (Errored)
            return Result.Failure;

        // Find the main section
        Section? startSection = null;
        if (_startSectionName == null)
            _errors.Add("No start section specified. Specify one with .start <name>");
        else if (_sections.Find(it => it.Name == _startSectionName) is not {} _startSection)
            _errors.Add($"Start section {_startSectionName} does not exist.");
        else
            startSection = _startSection;

        Output = _output;
        Errors = _errors.Count > 0 ? _errors : null;

        if (Errored)
            return Result.Failure;

        return new(true, _output.ToArray(), startSection!.StartAddress);
    }

    /// <summary>
    ///     Compiler instruction or section definition.
    /// </summary>
    private void TopLevelStatement()
    {
        var mark = _pos;
        int startSection = 0;

        if (Match(".section")) {
            if (Identifier() is not { } name) {
                Error("Expected a section name");
                name = "ERROR";
            }

            if (!MatchOrError("{"))
                return;

            // Output invalid instructions before and after the section
            // This is to ensure the executor will never reach the end of section
            // and immediately start executing the next section (which may be a procedure etc)
            Write((int) IS.SectionBoundary);

            var section = new Section(name, CurrentAddress);
            _sections.Add(section);

            // Pass 1 - read everything within
            while (!Match("}"))
            {
                CurrentSection = section; // Inside of the loop in case of nested sections if those are ever added
                SectionStatement();

                _inPSPInstruction = false; // In case SectionStatement() set it

                if (_pos < _input.Length)
                    continue;

                Error("Expected a closing brace");
                break;
            }

            Write((int) IS.SectionBoundary);

            return;
        } else if (Match(".start")) {
            var name = Identifier();
            _startSectionName = name;
            EndOfStatement();
            return;
        } else if (Match(";")) {
            return;
        }

        Error("Expected a top-level statement");
        SkipUntilTerminator();
    }

    private void SectionStatement()
    {
        SkipWS();
        if (Peek() == '.')
        {
            // Nested sections???
            // Don't try this at home
            TopLevelStatement();
            // This will expect two semicolons at the end. Maybe just don't use nested sections??? To do: fix it, or don't.
            return;
        }

        if (Peek() == ';')
        {
            Next();
            return;
        }

        // Prefixes
        _inPSPInstruction = Match("psp");

        // Instrcution itself
        if (Identifier() is not {} name)
            return;

        // Identifier followed by a colon is a label
        // God, this is turning into such a mess
        if (Match(":") && !_inPSPInstruction)
        {
            _labels.Add(new Label(name, CurrentAddress, CurrentSection));
            return;
        }

        // Simple instructions with no arguments
        name = name.ToLower();
        if (SimpleInstructions.TryGetValue(name, out var simpleId))
        {
            Write(simpleId);
            EndOfStatement();
            return;
        }

        // Binary operations
        if (Enum.TryParse<BinaryOperationType>(name, true, out var operationType))
        {
            WriteInstruction(IS.Binary);
            Write((int) OperandTypeOrError());
            Write((int) operationType);
            EndOfStatement();
            return;
        }

        switch (name)
        {
            // Instructions with one relative/port argument
            case "out":
            case "in":
            case "drop":
            case "dup":
            {
                // TODO REWRITE
                break;
            }

            // Instructions with integer arguments that allow referencing labels
            case "push":
            case "load":
            case "store":
            case "call":
            {
                // TODO REWRITE
                break;
            }

            case "jmp":
            case "jmpc":
            {
                // TODO REWRITE
                break;
            }

            case "int":
            case "char":
            case "float":
            {
                // TODO REWRITE
                // Optional label
                // if (Identifier() is {} label)
                //     _labels.Add(new Label(label, CurrentAddress, CurrentSection));
                //
                // // First initializer always exists, rest way or may not
                // // ARRAY SUPPORT HOLY SHIT
                // do {
                //     if (name == "int") {
                //         var init = CPUMemoryCell.FromInt32(Integer() ?? 0);
                //         Write(init.Int32);
                //     } else if (name == "char") {
                //         // This one supports both strings and single characters
                //         if (AnyString() is { } str) {
                //             foreach (var c in str)
                //                 Write(c);
                //         } else if (Integer() is {} c) {
                //             Write(c);
                //         } else {
                //             Error("Expected a string or integer initializer");
                //             str = "???";
                //         }
                //     } else {
                //         var init = CPUMemoryCell.FromSingle(Float() ?? 0f);
                //         Write(init.Int32);
                //     }
                // } while (Match(","));

                break;
            }

            case "ret": {
                int numBefore = 0, numAfter = 0;
                if (Integer() is { } tmpNumBefore) {
                    numBefore = tmpNumBefore;
                    numAfter = Integer() ?? 0;
                }

                // TODO REWRITE

                break;
            }
        }
        EndOfStatement();
    }

    private void EndOfStatement()
    {
        SkipWS();
        if (!Match(";"))
        {
            Error("Expected a semicolon after statement");
            SkipUntilTerminator();
        }

        SkipWS();
    }

    /// <summary>
    ///     Read a literal address or a label.
    ///     Returns int.MaxValue and queues dereferencing the address if it's a label
    /// </summary>
    /// <returns></returns>
    private int LabelOrAddress()
    {
        if (Integer() is {} constant)
            return constant;

        if (Identifier() is { } label) {
            _labelledAddresses.Add(((int) CurrentAddress, label));
            return int.MaxValue;
        }

        Error("Expected address or label");
        return int.MaxValue;
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
        SkipWS();

        var ch = Peek();
        if (!char.IsLetter(ch))
            return null;

        _idBuilder.Clear();
        do
        {
            _idBuilder.Append(ch);
            _pos++;
            ch = Peek();
        } while (char.IsLetter(ch) || char.IsNumber(ch) || ch is '_' or '-' or '?' or '@');

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

    /// <summary>
    ///     Matches a string in single or double quotes.
    /// </summary>
    private string? AnyString()
    {
        SkipWS();
        var quoteType = Peek();

        if (quoteType is not '"' and not '\'')
            return null;

        _idBuilder.Clear();
        char ch = Next();

        while (ch != quoteType && ch != '\0')
        {
            _idBuilder.Append(ch);
            ch = Next();
        }

        _pos++; // Skip the quote end
        if (ch != quoteType) {
            Error("Unterminated string");
            return null;
        }

        return _idBuilder.ToString();
    }

    /// <see cref="Identifier"/>
    private int? Integer()
    {
        SkipWS();

        var sign = Match("-");
        var ch = Peek();
        if (!char.IsDigit(ch))
            return null;

        var number = 0;
        if (Match("0x")) {
            // Special case: if it is 0 followed by an "x", it's a hex number
            ch = char.ToLower(Peek());
            while (char.IsDigit(ch) || ch is >= 'a' and <= 'f')
            {
                number = 16 * number + (char.IsDigit(ch) ? ch - '0' : ch - 'a' + 10);
                ch = char.ToLower(Next());
            }
        } else if (Match("onstack")) {
            // Special case: the constant "onstack" is -1
            // This should really be handled by the relevant instructions, but oh well
            return -1;
        } else {
            while (char.IsDigit(ch))
            {
                number = 10 * number + (ch - '0');
                ch = Next();
            }
        }

        return sign ? -number : number;
    }

    private float? Float()
    {
        SkipWS();
        if (Integer() is not { } integerPart)
            return null;

        var mark = _pos;
        if (!Match(".") || Integer() is not { } fractionalPart)
            return (float) integerPart;

        if (fractionalPart < 0) {
            Error("What the fuck are you on about?");
            fractionalPart = int.Abs(fractionalPart) * 10;
        }

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
            return JumpType.Equal; // Error recovery
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
        SkipWS();

        var success = Match(seq);
        if (!success)
            Error($"Expected {seq}");

        return success;
    }

    /// <summary>
    ///     Tries to match a given sequences. Returns true and advances on success.
    /// </summary>
    private bool Match(string seq, bool skipWS = true)
    {
        if (skipWS)
            SkipWS();

        var mark = _pos;
        var seqPos = 0;
        while (seqPos < seq.Length && Peek() == seq[seqPos])
        {
            seqPos++;
            _pos++;
        }

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
        if (Match("//", false))
        {
            while (Peek() is not '\n' and not '\0')
                _pos++;
            SkipWS();
        } else if (Match("/*", false)) {
            while (!Match("*/", false) && Peek() is not '\0')
                _pos++;
            SkipWS();
        }
    }

    private void SkipUntilTerminator()
    {
        while (Peek() is not ';' and not '\0')
            _pos++;
    }

    /// <returns>Next char or '\0' if end of input. Advances one char forward.</returns>
    private char Next() => _pos >= _input.Length ? '\0' : _input[++_pos];

    /// <returns>Current char or '\0' if end of input.</returns>
    private char Peek() => _pos >= _input.Length ? '\0' : _input[_pos];

    private void WriteInstruction(IS instruction)
    {
        byte value = (byte) instruction;
        Write(value);
    }

    private void Write(int value) => _output.Add(value);

    public void Error(string message, int addr = -1)
    {
        if (addr == -1)
            addr = _pos;

        var (line, pos) = GetLineNumberAndPosAt(addr);
        _errors.Add($"{message} at {line}:{pos}.");
    }

    private class Section(string name, uint startAddress)
    {
        public string Name = name;
        public uint StartAddress = startAddress;
    }

    private class Label(string name, uint startAddress, Section? inSection)
    {
        public string Name = name;
        public uint StartAddress = startAddress;
        public Section? inSection = inSection;
    }

    public record struct Result(bool success,int[]? code, uint entryPoint)
    {
        public bool success = success;
        public int[]? code = code;
        public uint entryPoint = entryPoint;

        public static Result Failure => new(false, null, 0);
    }
}
