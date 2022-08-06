using Microsoft.CodeAnalysis;
namespace Newdigate.MethodCallAnalysis.Core;

public interface ISymbolCache {
    void Add(ISymbol symbol);
}
