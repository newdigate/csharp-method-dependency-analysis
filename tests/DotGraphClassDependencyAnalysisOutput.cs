using System.Text;
using Microsoft.CodeAnalysis;
namespace type_deinference;

public class DotGraphClassDependencyAnalysisOutput {
    private readonly IMethodSymbolAnnotater _methodSymbolAnnotater;

    public DotGraphClassDependencyAnalysisOutput(IMethodSymbolAnnotater methodSymbolAnnotater)
    {
        _methodSymbolAnnotater = methodSymbolAnnotater;
    }

    public string Process(IDictionary<ISymbol, IDictionary<ISymbol, IList<ISymbol>>> symbolDependencies) {
        StringBuilder builder = new StringBuilder();
        Dictionary<CyclicMethodAnalysisResult, int> map = new Dictionary<CyclicMethodAnalysisResult, int>();
        builder.AppendLine("digraph G {");
        foreach (ISymbol classSymbol in symbolDependencies.Keys)
        foreach (ISymbol methodSymbol in symbolDependencies[classSymbol].Keys)
        {
            List<ISymbol> visitedSymbols = new List<ISymbol>();
            IList<ISymbol> rootDependenciesForMethod = symbolDependencies[classSymbol][methodSymbol];
            foreach (ISymbol result in rootDependenciesForMethod) {
                if (result is IMethodSymbol resultAsMethodSymbol) {
                    if ( symbolDependencies.ContainsKey( resultAsMethodSymbol.ContainingType ) ) {
                        builder.AppendLine($"\t \"{_methodSymbolAnnotater.Annotate(methodSymbol)}\" -> \"{_methodSymbolAnnotater.Annotate(result)}\"");
                    }
                }
            }
        }
        builder.Append("}");
        return builder.ToString();
    }
}
