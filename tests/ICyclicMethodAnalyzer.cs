using Microsoft.CodeAnalysis;
namespace type_deinference;

public interface ICyclicMethodAnalyzer {
    IEnumerable<CyclicMethodAnalysisResult> CheckForCyclicMethodCalls(ISymbol methodSymbol, IDictionary<ISymbol, IList<ISymbol>> methodDependencies, List<ISymbol> visitedSymbols, IList<ISymbol> rootDependenciesForMethod);
}
