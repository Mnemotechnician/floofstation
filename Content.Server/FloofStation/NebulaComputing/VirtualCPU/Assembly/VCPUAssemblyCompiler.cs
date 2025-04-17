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
    private List<(int addressIntegerPosition, string targetLabel)> _labelledAddresses = new();

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
    public List<int>? Output;

    public bool Errored => Errors is not null && Errors.Count > 0;

    public (bool success, int[]? code, int entryPoint) Compile(string input)
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
            return (false, null, -1);

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
            return (false, null, -1);

        return (true, _output.ToArray(), startSection!.StartAddress);
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
            {
                Error("Section has no body (missing opening brace)");
                return;
            }

            var section = new Section(name, CurrentAddress);
            _sections.Add(section);

            // Pass 1 - read everything within
            _labelledAddresses.Clear();
            while (!Match("}"))
            {
                CurrentSection = section; // Inside of the loop in case of nested sections if those are ever added
                SectionStatement();

                if (_pos < _input.Length)
                    continue;

                Error("Unexpected end of input");
                break;
            }


            // Pass 2 - resolve labels
            CurrentSection = null;
            foreach (var (jumpAddr, label) in _labelledAddresses)
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
            var name = Identifier();
            _startSectionName = name;
            EndOfStatement();
            return;
        } else if (Match(";")) {
            return; // I don't know why it happens
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

        if (Identifier() is not {} name)
            return;

        // Identifier followed by a colon is a label
        if (Match(":"))
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
            WriteInstruction(IS.BINARY);
            Write((int) OperandTypeOrError());
            Write((int) operationType);
            EndOfStatement();
            return;
        }

        switch (name)
        {
            // Instructions with one integer argument
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

            // Instructions with integer arguments that allow referencing labels
            case "store":
            case "push":
            case "load":
            {
                WriteInstruction(Enum.Parse<IS>(name.ToUpper()));

                if (Integer() is { } constant) {
                    Write(constant);
                } else if (Identifier() is { } label) {
                    _labelledAddresses.Add((CurrentAddress, label));
                    Write(int.MaxValue);
                } else {
                    Error("Expected address or label");
                }

                break;
            }

            case "jmp":
            case "jmpc":
            {
                WriteInstruction(Enum.Parse<IS>(name.ToUpper()));
                if (name == "jmpc")
                    Write((int) JumpTypeOrError());

                if (Integer() is { } absoluteAddr)
                    Write(absoluteAddr);
                else if (Identifier() is { } label) {
                    _labelledAddresses.Add((CurrentAddress, label));
                    Write(int.MaxValue);
                } else {
                    Error("Expected jump address or label");
                    SkipUntilTerminator();
                }

                break;
            }

            case "int":
            case "char":
            case "float":
            {
                // Optional label
                if (Identifier() is {} label)
                    _labels.Add(new Label(label, CurrentAddress, CurrentSection));

                // First initializer always exists, rest way or may not
                // ARRAY SUPPORT HOLY SHIT
                do {
                    if (name == "int") {
                        var init = CPUMemoryCell.FromInt32(Integer() ?? 0);
                        Write(init.Int32);
                    } else if (name == "char") {
                        // This one supports both strings and single characters
                        if (AnyString() is { } str) {
                            foreach (var c in str)
                                Write(c);
                        } else if (Integer() is {} c) {
                            Write(c);
                        } else {
                            Error("Expected a string or integer initializer");
                            str = "???";
                        }
                    } else {
                        var init = CPUMemoryCell.FromSingle(Float() ?? 0f);
                        Write(init.Int32);
                    }
                } while (Match(","));

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

        // Special case: if it is 0 followed by an "x", it's a hex number
        var number = 0;
        if (Match("0x")) {
            ch = char.ToLower(Peek());
            while (char.IsDigit(ch) || ch is >= 'a' and <= 'f')
            {
                number = 16 * number + (char.IsDigit(ch) ? ch - '0' : ch - 'a' + 10);
                ch = char.ToLower(Next());
            }
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

    private void WriteInstruction(IS instruction) => _output.Add((int) instruction);

    private void Write(int value) => _output.Add(value);

    private void Error(string message, int addr = -1)
    {
        if (addr == -1)
            addr = _pos;

        var (line, pos) = GetLineNumberAndPosAt(addr);
        _errors.Add($"{message} at {line}:{pos}.");
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
