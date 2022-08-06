using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
namespace type_deinference;

public interface ISymbolFinder {
    ISymbol? Find(ExpressionSyntax syntax);
}
