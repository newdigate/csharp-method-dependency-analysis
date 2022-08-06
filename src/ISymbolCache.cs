using Microsoft.CodeAnalysis;
namespace type_deinference;

public interface ISymbolCache {
    void Add(ISymbol symbol);
}
