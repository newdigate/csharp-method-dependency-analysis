using Microsoft.CodeAnalysis;
namespace type_deinference;

public class SymbolCache : ISymbolCache {
    private readonly IDictionary<string, ISymbol> _symbolCache;

    public SymbolCache(IDictionary<string, ISymbol> symbolCache)
    {
        _symbolCache = symbolCache;
    }

    public void Add(ISymbol symbol) {
        _symbolCache[symbol.Name] = symbol;
    }
}
