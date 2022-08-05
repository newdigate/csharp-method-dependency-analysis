using System.Text;
using Microsoft.CodeAnalysis;
namespace type_deinference;

public class DotGraphMethodCallAnalysisOutput {
    private readonly IMethodSymbolAnnotater _methodSymbolAnnotater;

    public DotGraphMethodCallAnalysisOutput(IMethodSymbolAnnotater methodSymbolAnnotater)
    {
        _methodSymbolAnnotater = methodSymbolAnnotater;
    }

    public string Process(IDictionary<ISymbol, IList<ISymbol>> symbolDependencies) {
        StringBuilder builder = new StringBuilder();
        Dictionary<CyclicMethodAnalysisResult, int> map = new Dictionary<CyclicMethodAnalysisResult, int>();
        builder.AppendLine("digraph G {");
        foreach (ISymbol methodSymbol in symbolDependencies.Keys)
        {
            List<ISymbol> visitedSymbols = new List<ISymbol>();
            IList<ISymbol> rootDependenciesForMethod = symbolDependencies[methodSymbol];
            foreach (ISymbol result in rootDependenciesForMethod) {
                builder.AppendLine($"\t \"{_methodSymbolAnnotater.Annotate(methodSymbol)}\" -> \"{_methodSymbolAnnotater.Annotate(result)}\"");
            }
        }
        builder.Append("}");
        return builder.ToString();
    }
}
