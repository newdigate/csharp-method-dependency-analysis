using Microsoft.CodeAnalysis;
namespace Newdigate.MethodCallAnalysis.Core;

public interface IMethodSymbolAnnotater {
    string Annotate(ISymbol symbol);
}
