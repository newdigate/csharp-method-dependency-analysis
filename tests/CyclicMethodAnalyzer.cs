using Microsoft.CodeAnalysis;
namespace type_deinference;

public class CyclicMethodAnalyzer : ICyclicMethodAnalyzer {
    public IEnumerable<CyclicMethodAnalysisResult> CheckForCyclicMethodCalls(ISymbol methodSymbol, IDictionary<ISymbol, IList<ISymbol>> methodDependencies, List<ISymbol> visitedSymbols, IList<ISymbol> rootDependenciesForMethod)
    {
        //Console.WriteLine($"{methodSymbol}: {string.Join("->", visitedSymbols.Select( s => s.Name) )}");
        List<ISymbol> originalSymbols = visitedSymbols;
        List<CyclicMethodAnalysisResult> result = new List<CyclicMethodAnalysisResult>();
        foreach (ISymbol dependency in rootDependenciesForMethod)
        {
            visitedSymbols = new List<ISymbol>(originalSymbols);
            if (dependency == methodSymbol) {
                visitedSymbols.Add(dependency);
                CyclicMethodAnalysisResult recursion = new CyclicMethodAnalysisResult(methodSymbol, new List<ISymbol>(visitedSymbols));
                result.Add(recursion);
                return result;
            } 
            else if (!visitedSymbols.Contains(dependency))
            {
                visitedSymbols.Add(dependency);
                if (methodDependencies.ContainsKey(dependency))
                {
                    IEnumerable<CyclicMethodAnalysisResult> recursive = CheckForCyclicMethodCalls(methodSymbol, methodDependencies, visitedSymbols, methodDependencies[dependency] );
                    if (recursive.Any()) {
                        result.AddRange(recursive);
                        //break;
                    }
                }
            }
            else
            {
                /*
                visitedSymbols.Add(dependency);
                CyclicMethodAnalysisResult recursion = new CyclicMethodAnalysisResult(methodSymbol, new List<ISymbol>(visitedSymbols));
                result.Add(recursion);
                return result;
                */
            }
        }
        return result;
    }

}
