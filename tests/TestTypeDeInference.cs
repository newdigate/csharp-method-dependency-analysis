using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
namespace type_deinference;

public class TestTypeDeInference {

    private readonly MethodCallAnalizer _methodCallAnalizer;
    
    private readonly IEnumerable<MetadataReference> defaultReferences;

    public TestTypeDeInference() {
        string assemlyLoc = typeof(Enumerable).GetTypeInfo().Assembly.Location;
        DirectoryInfo? coreDir = Directory.GetParent(assemlyLoc);
        if (coreDir == null)
            throw new ApplicationException("Unable to locate core framework directory...");

        defaultReferences = 
            new[] { 
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Console).Assembly.Location),
                MetadataReference.CreateFromFile(coreDir.FullName + Path.DirectorySeparatorChar + "System.Runtime.dll"),
            };
        _methodCallAnalizer = new MethodCallAnalizer(defaultReferences);
    }

    [Fact] 
    public void TestDeInferVariableDeclarationStatement() {
        const string source = @"using System;
public partial class NumberWang { 
    public void Wang() {
        Wong();
        Weng();
    }
}
public partial class NumberWang { 
    public void Wong() {
        Wang();
    }
}
public partial class NumberWang { 
    public void Weng() {
        Wanganum();
    }
    public void Wanganum() {
        Wang();
    }
}
";
        int counter = 0;
        Dictionary<CyclicMethodAnalysisResult, int> map = new Dictionary<CyclicMethodAnalysisResult, int>();
        IDictionary<ISymbol, IList<ISymbol>> methodDependencies = _methodCallAnalizer.AnalizeMethodCalls(source);
               Console.WriteLine("digraph G {");
        foreach (ISymbol methodSymbol in methodDependencies.Keys)
        {
            List<ISymbol> visitedSymbols = new List<ISymbol>();
            IList<ISymbol> rootDependenciesForMethod = methodDependencies[methodSymbol];
            IEnumerable<CyclicMethodAnalysisResult> analysis = CheckForCyclicMethodCalls(methodSymbol, methodDependencies, visitedSymbols, rootDependenciesForMethod);
            foreach (CyclicMethodAnalysisResult result in analysis.OrderBy( r => r.RecursionRoutes.Count() )) {
                int index;
                if (map.ContainsKey(result)) {
                    index = map[result];
                } else 
                {
                    index = Interlocked.Increment(ref counter);
                    map[result] = index;
                }
                Console.Write($"\t \"{result.Symbol}\" -> ");
                var filterRecursionRoutes =  new List<ISymbol>();
                foreach(ISymbol symbol in result.RecursionRoutes) {
                    filterRecursionRoutes.Add(symbol);
                    if (analysis.Any( r => r.Symbol == symbol) )
                        break;
                }
                Console.WriteLine($"{string.Join(" -> ", filterRecursionRoutes.Select(v => $"\"{v.ToString()}\""))} [color={RandomColor(result)}, label=\"{index}\"];");
            }
            
            foreach (CyclicMethodAnalysisResult result in analysis) {
                int index = -1;
                if (map.ContainsKey(result)) {
                    index = map[result];
                } 
                Console.WriteLine($"\tnode [shape = circle, style=filled, color={RandomColor(result)}];");
                Console.WriteLine($"\t {index} -> \"{result.Symbol}\" [color={RandomColor(result)}] ");
            }
            
        }
        Console.WriteLine("}");
    }
    private static string RandomColor(CyclicMethodAnalysisResult result) {
        string[] colors = {"green", "red", "blue", "grey", "yellow", "purple", "salmon2", 
                            "deepskyblue", "goldenrod2", "burlywood2", "gold1", "greenyellow", 
                            "darkseagreen", "dodgerblue1", "thistle2","darkolivegreen3", "chocolate", 
                            "turquoise3", "steelblue3","navy","darkseagreen4","blanchedalmond","lightskyblue1","aquamarine2","lemonchiffon"  };
        return colors[result.GetHashCode() % colors.Length];
    }

    private static IEnumerable<CyclicMethodAnalysisResult> CheckForCyclicMethodCalls(ISymbol methodSymbol, IDictionary<ISymbol, IList<ISymbol>> methodDependencies, List<ISymbol> visitedSymbols, IList<ISymbol> rootDependenciesForMethod)
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
                visitedSymbols.Add(dependency);
                CyclicMethodAnalysisResult recursion = new CyclicMethodAnalysisResult(methodSymbol, new List<ISymbol>(visitedSymbols));
                result.Add(recursion);
                return result;
            }
        }
        return result;
    }
}

public class CyclicMethodAnalysisResult {

    private readonly ISymbol _methodSymbol;
    private readonly IList<ISymbol> _recursionRoute;
    public IList<ISymbol> RecursionRoutes { get { return _recursionRoute;}}
    public ISymbol Symbol { get { return _methodSymbol;}}
    
    public CyclicMethodAnalysisResult(ISymbol symbol, IList<ISymbol> recursionRoute)
    {
        _methodSymbol = symbol;
        _recursionRoute = recursionRoute;
    }

    public override int GetHashCode()
    {
        return base.GetHashCode() + _methodSymbol.GetHashCode();
    }


}


