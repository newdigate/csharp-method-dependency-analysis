using Microsoft.CodeAnalysis;
namespace Newdigate.MethodCallAnalysis.Core;

public interface ICyclicMethodAnalyzer {
    IEnumerable<CyclicMethodAnalysisResult> CheckForCyclicMethodCalls(ISymbol methodSymbol, IDictionary<ISymbol, IList<ISymbol>> methodDependencies, List<ISymbol> visitedSymbols, IList<ISymbol> rootDependenciesForMethod);
}
