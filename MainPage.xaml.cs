namespace CalculatriceMaui;

public partial class MainPage : ContentPage
{
    private const double PagePadding = 10;
    private const double CardPadding = 14;
    private static readonly Color ErrorColor = Color.FromArgb("#EF4444");
    private static readonly Color OkColor = Color.FromArgb("#10B981");
    private static readonly Color MutedColor = Color.FromArgb("#8B93A5");

    private readonly CalculatorEngine _engine = new();
    private bool? _isLandscape;
    private double _resultWidth = 300;
    private double _maxResultFont = 44;

    public MainPage()
    {
        InitializeComponent();
        Refresh();
    }

    // ---------- Adaptation portrait / paysage / petits écrans ----------

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0 || height <= 0) return;

        var landscape = width > height;
        if (_isLandscape != landscape)
        {
            _isLandscape = landscape;
            ApplyOrientationLayout(landscape);
        }

        var leftColumnWidth = landscape ? (width - 2 * PagePadding - 8) * 0.4 : width - 2 * PagePadding;
        _resultWidth = Math.Max(80, leftColumnWidth - 2 * CardPadding);
        ResultLabel.MinimumWidthRequest = _resultWidth;
        ExpressionLabel.MinimumWidthRequest = _resultWidth;

        var keypadHeight = landscape ? height - 2 * PagePadding : (height - 2 * PagePadding - 16) * 4.4 / 8;
        var keyFont = Math.Clamp(keypadHeight / 5 * 0.34, 14, 30);
        foreach (var button in KeypadGrid.Children.OfType<Button>())
            button.FontSize = keyFont;

        _maxResultFont = landscape ? Math.Clamp(height * 0.14, 24, 40) : 44;
        Refresh();
    }

    private void ApplyOrientationLayout(bool landscape)
    {
        RootGrid.RowDefinitions.Clear();
        RootGrid.ColumnDefinitions.Clear();

        if (landscape)
        {   // affichage + panneau scientifique à gauche (empilés), clavier à droite
            RootGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(4, GridUnitType.Star)));
            RootGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(6, GridUnitType.Star)));
            RootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            RootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));

            Grid.SetRow(DisplayCard, 0); Grid.SetColumn(DisplayCard, 0); Grid.SetColumnSpan(DisplayCard, 1); Grid.SetRowSpan(DisplayCard, 1);
            Grid.SetRow(SciCard, 1); Grid.SetColumn(SciCard, 0); Grid.SetColumnSpan(SciCard, 1); Grid.SetRowSpan(SciCard, 1);
            Grid.SetRow(KeypadGrid, 0); Grid.SetColumn(KeypadGrid, 1); Grid.SetRowSpan(KeypadGrid, 2); Grid.SetColumnSpan(KeypadGrid, 1);
        }
        else
        {   // affichage, panneau scientifique, puis clavier, empilés verticalement
            RootGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            RootGrid.RowDefinitions.Add(new RowDefinition(new GridLength(2, GridUnitType.Star)));
            RootGrid.RowDefinitions.Add(new RowDefinition(new GridLength(1.6, GridUnitType.Star)));
            RootGrid.RowDefinitions.Add(new RowDefinition(new GridLength(4.4, GridUnitType.Star)));

            Grid.SetRow(DisplayCard, 0); Grid.SetColumn(DisplayCard, 0); Grid.SetColumnSpan(DisplayCard, 1); Grid.SetRowSpan(DisplayCard, 1);
            Grid.SetRow(SciCard, 1); Grid.SetColumn(SciCard, 0); Grid.SetColumnSpan(SciCard, 1); Grid.SetRowSpan(SciCard, 1);
            Grid.SetRow(KeypadGrid, 2); Grid.SetColumn(KeypadGrid, 0); Grid.SetRowSpan(KeypadGrid, 1); Grid.SetColumnSpan(KeypadGrid, 1);
        }
    }

    // ---------- Gestionnaires d'événements ----------

    /// <summary>Chiffres, opérateurs et fonctions scientifiques insèrent tous du texte dans l'expression.</summary>
    private void OnFunctionClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string token })
            _engine.Insert(token);
        Refresh();
    }

    private void OnActionClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string p })
        {
            switch (p)
            {
                case "CLEAR": _engine.Clear(); break;
                case "BACK": _engine.Backspace(); break;
                case "EQUALS": _engine.Evaluate(); break;
            }
        }
        Refresh();
    }

    private void OnDegRadClicked(object? sender, EventArgs e)
    {
        _engine.ToggleDegreeMode();
        Refresh();
    }

    // ---------- Rafraîchissement de l'affichage ----------

    private void Refresh()
    {
        var text = _engine.DisplayText;
        var error = _engine.Error is not null;

        ResultLabel.Text = text;
        ExpressionLabel.Text = error ? _engine.Error : _engine.History;

        ResultLabel.TextColor = error ? ErrorColor : Colors.White;
        ExpressionLabel.TextColor = error ? ErrorColor : MutedColor;

        StatusLabel.Text = error ? "Erreur" : "Prêt";
        StatusLabel.TextColor = error ? ErrorColor : OkColor;

        DegRadButton.Text = _engine.DegreeMode ? "DEG" : "RAD";

        // Réduit la police pour que les longs nombres/expressions tiennent sur l'écran
        var fitted = _resultWidth / Math.Max(1, text.Length) / 0.55;
        ResultLabel.FontSize = Math.Clamp(fitted, 18, _maxResultFont);

        _ = ResultScroll.ScrollToAsync(ResultLabel, ScrollToPosition.End, false);
        _ = ExpressionScroll.ScrollToAsync(ExpressionLabel, ScrollToPosition.End, false);
    }
}
