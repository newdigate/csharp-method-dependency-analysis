using Microsoft.CodeAnalysis;
namespace type_deinference;

public interface IMethodSymbolAnnotater {
    string Annotate(ISymbol symbol);
}
