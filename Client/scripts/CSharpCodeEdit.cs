using Godot;
using Godot.Collections;

namespace TTRpgClient.scripts;

public partial class CSharpCodeEdit : CodeEdit
{
    private static readonly Color purple = Color.FromHtml("#644AC9");
    private static readonly Color cyan = Color.FromHtml("#036A96");
    private static readonly Color yellow = Color.FromHtml("#F1FA8C");
    private static readonly CodeHighlighter codeHighlighter = new CodeHighlighter()
    {
        NumberColor = Color.FromHtml("#FFB86C"),
        FunctionColor = Color.FromHtml("#50FA7B"),
        SymbolColor = Color.FromHtml("#FFEE0F"),
        MemberVariableColor = Color.FromHtml("#FFB86C"),
        ColorRegions = new Dictionary()
        {
            {"\" \"", yellow},
            {"\' \'", Color.FromHtml("#FF5555")}
        },
        KeywordColors = new Dictionary()
        {
            {"void", purple},
            {"string", purple},
            {"static", purple},
            {"int", purple},
            {"var", purple},
            {"float", purple},
            {"bool", purple},
            {"if", purple},
            {"else", purple},
            {"return", purple},
            {"Creature", cyan},
            {"Entity", cyan}
        },
    };

    public CSharpCodeEdit()
    {
        HighlightCurrentLine = true;
        HighlightAllOccurrences = true;
        GuttersDrawLineNumbers = true;
        GuttersDrawFoldGutter = true;
        SymbolLookupOnClick = true;
        SymbolTooltipOnHover = true;
        AutoBraceCompletionEnabled = true;
        AutoBraceCompletionHighlightMatching = true;
        AddCommentDelimiter("//", string.Empty, true);
        AddCommentDelimiter("/*", "*/");
        SyntaxHighlighter = codeHighlighter;
        AddThemeColorOverride("background_color", Color.FromHtml("#282A36"));
    }
}