using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
namespace type_deinference;

public class SymbolFinder : ISymbolFinder {
    private readonly Func<ExpressionSyntax, ISymbol?> _fnFindSymbol;

    public SymbolFinder(Func<ExpressionSyntax, ISymbol?> fnFindSymbol)
    {
        _fnFindSymbol = fnFindSymbol;
    }

    public ISymbol? Find(ExpressionSyntax syntax) {
        return _fnFindSymbol(syntax);
    }
}
