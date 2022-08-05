using System.Text;
using Microsoft.CodeAnalysis;
namespace type_deinference;

public class DotGraphRecursionAnalysisOutput {

    private readonly IRandomColorProvider _randomColorProvider;
    private readonly ICyclicMethodAnalyzer _cyclicMethodAnalyzer;
    private readonly IMethodSymbolAnnotater _methodSymbolAnnotater;

    public DotGraphRecursionAnalysisOutput(IRandomColorProvider randomColorProvider, ICyclicMethodAnalyzer cyclicMethodAnalyzer, IMethodSymbolAnnotater methodSymbolAnnotater)
    {
        _randomColorProvider = randomColorProvider;
        _cyclicMethodAnalyzer = cyclicMethodAnalyzer;
        _methodSymbolAnnotater = methodSymbolAnnotater;
    }


    public string Process(IDictionary<ISymbol, IList<ISymbol>> symbolDependencies) {
        StringBuilder builder = new StringBuilder();
        int counter = 0;
        Dictionary<CyclicMethodAnalysisResult, int> map = new Dictionary<CyclicMethodAnalysisResult, int>();
        builder.AppendLine("digraph G {");
        foreach (ISymbol methodSymbol in symbolDependencies.Keys)
        {
            List<ISymbol> visitedSymbols = new List<ISymbol>();
            IList<ISymbol> rootDependenciesForMethod = symbolDependencies[methodSymbol];
            IEnumerable<CyclicMethodAnalysisResult> analysis = _cyclicMethodAnalyzer.CheckForCyclicMethodCalls(methodSymbol, symbolDependencies, visitedSymbols, rootDependenciesForMethod);
            foreach (CyclicMethodAnalysisResult result in analysis.Where( r => r.RecursionRoutes.Last() == r.Symbol).OrderBy( r => r.RecursionRoutes.Count() )) {
                int index;
                if (map.ContainsKey(result)) {
                    index = map[result];
                } else 
                {
                    index = Interlocked.Increment(ref counter);
                    map[result] = index;
                }

                builder.Append($"\t \"{_methodSymbolAnnotater.Annotate(result.Symbol)}\" -> ");
                builder.AppendLine($"{string.Join(" -> ", result.RecursionRoutes.Select(v => $"\"{_methodSymbolAnnotater.Annotate(v)}\""))} [color={_randomColorProvider.RandomColor(result)}, label=\"{index}\"];");
            }
        }
        builder.Append("}");
        return builder.ToString();
    }
}
