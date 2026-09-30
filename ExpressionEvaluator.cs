using System.Globalization;

namespace CalculatriceMaui;

/// <summary>Erreur de calcul destinée à être affichée telle quelle à l'utilisateur.</summary>
public sealed class CalcException : Exception
{
    public CalcException(string message) : base(message) { }
}

internal enum TokenType { Number, Op, Ident }

internal readonly record struct Token(TokenType Type, string Text, double Value);

/// <summary>
/// Évaluateur d'expressions mathématiques complètes (nombres, parenthèses, priorité des
/// opérations, fonctions scientifiques). Entièrement indépendant de l'interface : testable seul.
/// </summary>
public static class ExpressionEvaluator
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static readonly HashSet<string> WordOperators = new() { "mod", "div" };
    private static readonly HashSet<string> Constants = new() { "π", "e", "Ans" };

    public static double Evaluate(string expression, double ans, bool degreeMode)
    {
        if (string.IsNullOrWhiteSpace(expression))
            throw new CalcException("Expression vide");

        var tokens = Tokenize(expression);
        tokens = InsertImplicitMultiplication(tokens);
        tokens = AutoCloseParentheses(tokens);

        var parser = new Parser(tokens, ans, degreeMode);
        var result = parser.ParseExpression();

        if (!parser.IsAtEnd)
            throw new CalcException("Expression invalide");
        if (double.IsNaN(result) || double.IsInfinity(result))
            throw new CalcException("Résultat indéfini");

        return result;
    }

    // ---------- Analyse lexicale ----------

    private static List<Token> Tokenize(string s)
    {
        var tokens = new List<Token>();
        int i = 0;
        while (i < s.Length)
        {
            char c = s[i];

            if (char.IsWhiteSpace(c)) { i++; continue; }

            if (char.IsDigit(c) || c == '.')
            {
                int start = i;
                while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;

                // Notation scientifique : 1.5E3 ou 1.5E-3
                if (i < s.Length && s[i] == 'E')
                {
                    int j = i + 1;
                    if (j < s.Length && (s[j] == '+' || s[j] == '-')) j++;
                    int k = j;
                    while (k < s.Length && char.IsDigit(s[k])) k++;
                    if (k > j) i = k;
                }

                var numText = s[start..i];
                if (!double.TryParse(numText, NumberStyles.Float, Inv, out var val))
                    throw new CalcException("Nombre invalide");
                tokens.Add(new Token(TokenType.Number, numText, val));
                continue;
            }

            if (c == 'π') { tokens.Add(new Token(TokenType.Ident, "π", 0)); i++; continue; }
            if (c == '√') { tokens.Add(new Token(TokenType.Ident, "√", 0)); i++; continue; }

            if (char.IsLetter(c))
            {
                int start = i;
                while (i < s.Length && char.IsLetter(s[i])) i++;
                tokens.Add(new Token(TokenType.Ident, s[start..i], 0));
                continue;
            }

            switch (c)
            {
                case '+': case '-': case '*': case '/': case '^':
                case '!': case '%': case '(': case ')': case ',':
                    tokens.Add(new Token(TokenType.Op, c.ToString(), 0)); i++; continue;
                case '×': tokens.Add(new Token(TokenType.Op, "*", 0)); i++; continue;
                case '÷': tokens.Add(new Token(TokenType.Op, "/", 0)); i++; continue;
                case '−': tokens.Add(new Token(TokenType.Op, "-", 0)); i++; continue;
            }

            throw new CalcException($"Caractère invalide : {c}");
        }
        return tokens;
    }

    /// <summary>Insère un '*' implicite : 2π, 3(4+5), 2sin(30), (2)(3), πe, etc.</summary>
    private static List<Token> InsertImplicitMultiplication(List<Token> tokens)
    {
        var result = new List<Token>(tokens.Count);
        foreach (var t in tokens)
        {
            if (result.Count > 0)
            {
                var prev = result[^1];
                bool prevEndsValue =
                    prev.Type == TokenType.Number ||
                    (prev.Type == TokenType.Op && (prev.Text == ")" || prev.Text == "!" || prev.Text == "%")) ||
                    (prev.Type == TokenType.Ident && Constants.Contains(prev.Text));

                bool curStartsValue =
                    t.Type == TokenType.Number ||
                    (t.Type == TokenType.Op && t.Text == "(") ||
                    (t.Type == TokenType.Ident && !WordOperators.Contains(t.Text));

                if (prevEndsValue && curStartsValue)
                    result.Add(new Token(TokenType.Op, "*", 0));
            }
            result.Add(t);
        }
        return result;
    }

    /// <summary>Ferme automatiquement les parenthèses oubliées, par tolérance de saisie.</summary>
    private static List<Token> AutoCloseParentheses(List<Token> tokens)
    {
        int open = tokens.Count(t => t.Type == TokenType.Op && t.Text == "(")
                 - tokens.Count(t => t.Type == TokenType.Op && t.Text == ")");
        if (open <= 0) return tokens;
        var copy = new List<Token>(tokens);
        for (int i = 0; i < open; i++) copy.Add(new Token(TokenType.Op, ")", 0));
        return copy;
    }

    // ---------- Analyse syntaxique + évaluation (descente récursive) ----------
    // Priorités, de la plus faible à la plus forte :
    //   Addition/soustraction < Multiplication/division/mod/div < Unaire (-x)
    //   < Puissance (^, associative à droite) < Postfixe (!, %) < Primaire

    private sealed class Parser
    {
        private readonly List<Token> _tokens;
        private readonly double _ans;
        private readonly bool _degrees;
        private int _pos;

        public Parser(List<Token> tokens, double ans, bool degrees)
        {
            _tokens = tokens; _ans = ans; _degrees = degrees;
        }

        public bool IsAtEnd => _pos >= _tokens.Count;
        private Token Current => _pos < _tokens.Count ? _tokens[_pos] : new Token(TokenType.Op, "", 0);
        private void Advance() => _pos++;

        private bool IsOp(string text) => Current.Type == TokenType.Op && Current.Text == text;

        private void Expect(string text)
        {
            if (!IsOp(text)) throw new CalcException($"« {text} » attendu");
            Advance();
        }

        public double ParseExpression() => ParseAdd();

        private double ParseAdd()
        {
            var v = ParseMul();
            while (IsOp("+") || IsOp("-"))
            {
                var op = Current.Text; Advance();
                var rhs = ParseMul();
                v = op == "+" ? v + rhs : v - rhs;
            }
            return v;
        }

        private double ParseMul()
        {
            var v = ParseUnary();
            while (true)
            {
                if (IsOp("*") || IsOp("/"))
                {
                    var op = Current.Text; Advance();
                    var rhs = ParseUnary();
                    if (op == "*") v *= rhs;
                    else
                    {
                        if (rhs == 0) throw new CalcException("Division par zéro impossible");
                        v /= rhs;
                    }
                }
                else if (Current.Type == TokenType.Ident && WordOperators.Contains(Current.Text))
                {
                    var op = Current.Text; Advance();
                    var rhs = ParseUnary();
                    if (rhs == 0) throw new CalcException("Division par zéro impossible");
                    v = op == "mod" ? v - rhs * Math.Floor(v / rhs) : Math.Floor(v / rhs);
                }
                else break;
            }
            return v;
        }

        private double ParseUnary()
        {
            if (IsOp("-")) { Advance(); return -ParseUnary(); }
            if (IsOp("+")) { Advance(); return ParseUnary(); }
            return ParsePower();
        }

        private double ParsePower()
        {
            var baseVal = ParsePostfix();
            if (IsOp("^"))
            {
                Advance();
                var exp = ParseUnary(); // associatif à droite : 2^3^2 = 2^(3^2)
                var r = Math.Pow(baseVal, exp);
                if (double.IsNaN(r)) throw new CalcException("Puissance indéfinie");
                return r;
            }
            return baseVal;
        }

        private double ParsePostfix()
        {
            var v = ParsePrimary();
            while (true)
            {
                if (IsOp("!")) { Advance(); v = Factorial(v); }
                else if (IsOp("%")) { Advance(); v /= 100.0; }
                else break;
            }
            return v;
        }

        private double ParsePrimary()
        {
            var tok = Current;

            if (tok.Type == TokenType.Number) { Advance(); return tok.Value; }

            if (tok.Type == TokenType.Op && tok.Text == "(")
            {
                Advance();
                var v = ParseAdd();
                Expect(")");
                return v;
            }

            if (tok.Type == TokenType.Ident)
            {
                Advance();
                switch (tok.Text)
                {
                    case "π": return Math.PI;
                    case "e": return Math.E;
                    case "Ans": return _ans;
                }

                Expect("(");
                var args = new List<double> { ParseAdd() };
                while (IsOp(","))
                {
                    Advance();
                    args.Add(ParseAdd());
                }
                Expect(")");
                return EvalFunction(tok.Text == "√" ? "sqrt" : tok.Text, args);
            }

            throw new CalcException("Expression incomplète");
        }

        private double ToRad(double x) => _degrees ? x * Math.PI / 180.0 : x;
        private double FromRad(double x) => _degrees ? x * 180.0 / Math.PI : x;

        private double EvalFunction(string name, List<double> args)
        {
            var a = args[0];
            switch (name)
            {
                case "sin": return Math.Sin(ToRad(a));
                case "cos": return Math.Cos(ToRad(a));
                case "tan": return Math.Tan(ToRad(a));
                case "asin":
                    if (a is < -1 or > 1) throw new CalcException("sin⁻¹ défini sur [-1, 1]");
                    return FromRad(Math.Asin(a));
                case "acos":
                    if (a is < -1 or > 1) throw new CalcException("cos⁻¹ défini sur [-1, 1]");
                    return FromRad(Math.Acos(a));
                case "atan": return FromRad(Math.Atan(a));
                case "sinh": return Math.Sinh(a);
                case "cosh": return Math.Cosh(a);
                case "tanh": return Math.Tanh(a);
                case "log":
                    if (a <= 0) throw new CalcException("log défini pour x > 0");
                    return Math.Log10(a);
                case "ln":
                    if (a <= 0) throw new CalcException("ln défini pour x > 0");
                    return Math.Log(a);
                case "exp": return Math.Exp(a);
                case "sqrt":
                    if (a < 0) throw new CalcException("Racine carrée d'un nombre négatif");
                    return Math.Sqrt(a);
                case "abs": return Math.Abs(a);
                case "root":
                    if (args.Count < 2) throw new CalcException("ⁿ√x nécessite 2 valeurs : root(x,n)");
                    var n = args[1];
                    if (n == 0) throw new CalcException("Indice de racine nul");
                    if (a < 0 && n % 2 == 0) throw new CalcException("Racine paire d'un nombre négatif");
                    return a < 0 ? -Math.Pow(-a, 1.0 / n) : Math.Pow(a, 1.0 / n);
                default: throw new CalcException($"Fonction inconnue : {name}");
            }
        }

        private static double Factorial(double v)
        {
            if (v < 0 || v != Math.Floor(v)) throw new CalcException("n! défini pour un entier ≥ 0");
            if (v > 170) throw new CalcException("Résultat trop grand");
            double r = 1;
            for (var i = 2; i <= (int)v; i++) r *= i;
            return r;
        }
    }
}
