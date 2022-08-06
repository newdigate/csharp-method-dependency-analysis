using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
namespace Newdigate.MethodCallAnalysis.Core;

public interface ISymbolFinder {
    ISymbol? Find(ExpressionSyntax syntax);
}
