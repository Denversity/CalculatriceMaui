using System.Globalization;

namespace CalculatriceMaui;

/// <summary>
/// État de la calculatrice : l'expression en cours de saisie, l'historique, la mémoire
/// Ans, le mode degrés/radians et la gestion des erreurs. Ne dépend pas de l'interface.
/// </summary>
public sealed class CalculatorEngine
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private bool _justEvaluated;

    public string Expression { get; private set; } = "";
    public string History { get; private set; } = "";
    public double Ans { get; private set; }
    public bool DegreeMode { get; private set; } = true;
    public string? Error { get; private set; }

    public string DisplayText => Error is not null ? "Erreur" : (Expression.Length == 0 ? "0" : Expression);

    public void ToggleDegreeMode() => DegreeMode = !DegreeMode;

    public void Clear()
    {
        Expression = "";
        History = "";
        Error = null;
        _justEvaluated = false;
    }

    public void Backspace()
    {
        if (Error is not null) { Clear(); return; }
        if (Expression.Length > 0) Expression = Expression[..^1];
        _justEvaluated = false;
    }

    /// <summary>Ajoute un jeton (chiffre, opérateur ou fonction) à l'expression en cours.</summary>
    public void Insert(string token)
    {
        if (Error is not null) Clear();

        if (_justEvaluated)
        {
            // Après un résultat, taper un opérateur (y compris x², xʸ, !, %...) poursuit le
            // calcul à partir de Ans ; taper un chiffre ou une fonction démarre une nouvelle expression.
            Expression = StartsWithOperator(token) ? "Ans" + token : "";
            History = "";
            _justEvaluated = false;
        }

        Expression += token;
    }

    private static bool StartsWithOperator(string token)
    {
        if (token.Length == 0) return false;
        if (token is "mod" or "div") return true;
        return "+-×÷^!%".IndexOf(token[0]) >= 0;
    }

    public void Evaluate()
    {
        if (Error is not null || Expression.Length == 0) return;
        try
        {
            var result = ExpressionEvaluator.Evaluate(Expression, Ans, DegreeMode);
            History = Expression + " =";
            Ans = result;
            Expression = FormatNumber(result);
            _justEvaluated = true;
        }
        catch (CalcException ex)
        {
            Error = ex.Message;
        }
        catch (Exception)
        {
            Error = "Expression invalide";
        }
    }

    /// <summary>Formate un résultat : notation scientifique pour les valeurs très grandes/petites.</summary>
    public static string FormatNumber(double v)
    {
        if (double.IsNaN(v) || double.IsInfinity(v)) return "Erreur";
        if (v != 0 && (Math.Abs(v) < 1e-9 || Math.Abs(v) >= 1e15))
            return v.ToString("0.##########E+0", Inv).Replace(".", ",");

        var rounded = Math.Round(v, 12, MidpointRounding.AwayFromZero);
        if (rounded == 0) rounded = 0; // évite l'affichage "-0"
        return rounded.ToString("0.############", Inv).Replace(".", ",");
    }
}
