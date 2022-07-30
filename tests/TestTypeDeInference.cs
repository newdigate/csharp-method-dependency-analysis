using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
namespace type_deinference;

public class TestTypeDeInference {

    private readonly MethodCallAnalizer _methodCallAnalizer;
    private readonly ClassDependencyAnalyzer _classDependencyAnalyzer;
    
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
                MetadataReference.CreateFromFile(typeof(SyntaxTree).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Microsoft.Build.Locator.MSBuildLocator).Assembly.Location),
                MetadataReference.CreateFromFile(coreDir.FullName + Path.DirectorySeparatorChar + "System.Runtime.dll"),
            };
        ICSharpCompilationProvider compy = new CSharpCompilationProvider(defaultReferences);
        _methodCallAnalizer = new MethodCallAnalizer(compy);
        _classDependencyAnalyzer = new ClassDependencyAnalyzer(compy, _methodCallAnalizer);
    }

    [Fact]
    public void TestSolutionMethodAnalysis() {
        SolutionMethodAnalysis solutionMethodAnalysis = new SolutionMethodAnalysis(_methodCallAnalizer);
        IDictionary<ISymbol, IList<ISymbol>> result = solutionMethodAnalysis.AnalizeMethodCallsForSolution("/Users/nicholasnewdigate/Development/github/newdigate/csharp-method-dependency-analysis-2/MethodAnalysis.sln");
        string dot = new DotGraphMethodCallAnalysisOutput().Process(result);
        Console.WriteLine(dot);
    }

    [Fact]
    public void TestInterfaceMethodAnalysis() {
        const string source = @"
public interface IWang {
    void DoWang();
}

public interface IWong {
    void DoWong();
}

public class Wang : IWang { 
    private readonly IWong _wong;

    public Wang(IWong wong) {
        _wong = wong;
    }

    public void DoWang() {
        _wong.DoWong();
    }
}

public class Wong : IWong { 
    private readonly IWang _wang;

    public Wong(IWang wang) {
        _wang = wang;
    }

    public void DoWong() {
        _wang.DoWang();
    }
}
";
        Dictionary<string, string> interfaceToClassMapping = new Dictionary<string, string>();
        interfaceToClassMapping["IWang"] = "Wang";
        interfaceToClassMapping["IWong"] = "Wong";

        IDictionary<ISymbol, IDictionary<ISymbol, IList<ISymbol>>> classDependencies = _classDependencyAnalyzer.AnalizeClassCalls(source);
        IDictionary<ISymbol, IDictionary<ISymbol, IList<ISymbol>>> implicitClassDependencies = new Dictionary<ISymbol, IDictionary<ISymbol, IList<ISymbol>>>();
        Console.WriteLine("digraph G {");
        foreach (ISymbol classSymbol in classDependencies.Keys)
        foreach (ISymbol methodSymbol in classDependencies[classSymbol].Keys)
        {
            
            List<ISymbol> visitedSymbols = new List<ISymbol>();
            IDictionary<ISymbol, IList<ISymbol>> methodSymbolDependenciesForClass = classDependencies[classSymbol];

            foreach(ISymbol methodWithDependencies in methodSymbolDependenciesForClass.Keys) {
                IList<ISymbol> dependenciesForMethod = methodSymbolDependenciesForClass[methodWithDependencies];

                foreach (ISymbol dependency in dependenciesForMethod) {
                    if(dependency is IMethodSymbol dependencyMethodSymbol) {
                        ISymbol containingType = dependencyMethodSymbol.ContainingType;
                        ISymbol? implementingType = containingType;
                        if (containingType is ITypeSymbol typeSymbol) {
                            if (typeSymbol.TypeKind == TypeKind.Interface) {
                                string nameOfInterfaceToMap = typeSymbol.Name;
                                if (interfaceToClassMapping.ContainsKey(nameOfInterfaceToMap)) {
                                    string mappedClassName = interfaceToClassMapping[nameOfInterfaceToMap];
                                    implementingType = classDependencies.Keys.FirstOrDefault( t => t.Name == mappedClassName );
                                    Console.WriteLine($"\"{classSymbol.Name}.{methodSymbol.Name}\" -> \"{implementingType}.{dependency.Name}\"");
                                }
                            }
                        }
                        // 
                    }
                }
            }
        }
        Console.WriteLine("}");
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
            foreach (CyclicMethodAnalysisResult result in analysis.Where( r => r.RecursionRoutes.Last() == r.Symbol).OrderBy( r => r.RecursionRoutes.Count() )) {
                int index;
                if (map.ContainsKey(result)) {
                    index = map[result];
                } else 
                {
                    index = Interlocked.Increment(ref counter);
                    map[result] = index;
                }
                Console.Write($"\t \"{result.Symbol}\" -> ");
                Console.WriteLine($"{string.Join(" -> ", result.RecursionRoutes.Select(v => $"\"{v.ToString()}\""))} [color={RandomColor(result)}, label=\"{index}\"];");
            }
            
            foreach (CyclicMethodAnalysisResult result in analysis.Where( r => r.RecursionRoutes.Last() == r.Symbol)) {
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


public class DotGraphRecursionAnalysisOutput {
    private string Annotate(ISymbol symbol) {
        return symbol.ToString().Replace(symbol.ContainingNamespace.ToString() + "." + symbol.ContainingType.Name + ".", "");
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
            IEnumerable<CyclicMethodAnalysisResult> analysis = CheckForCyclicMethodCalls(methodSymbol, symbolDependencies, visitedSymbols, rootDependenciesForMethod);
            foreach (CyclicMethodAnalysisResult result in analysis.Where( r => r.RecursionRoutes.Last() == r.Symbol).OrderBy( r => r.RecursionRoutes.Count() )) {
                int index;
                if (map.ContainsKey(result)) {
                    index = map[result];
                } else 
                {
                    index = Interlocked.Increment(ref counter);
                    map[result] = index;
                }

                builder.Append($"\t \"{Annotate(result.Symbol)}\" -> ");
                builder.AppendLine($"{string.Join(" -> ", result.RecursionRoutes.Select(v => $"\"{Annotate(v)}\""))} [color={RandomColor(result)}, label=\"{index}\"];");
            }
        }
        builder.Append("}");
        return builder.ToString();
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
                    }
                }
            }
        }
        return result;
    }

    private static string RandomColor(CyclicMethodAnalysisResult result) {
        string[] colors = {"green", "red", "blue", "grey", "yellow", "purple", "salmon2", 
                            "deepskyblue", "goldenrod2", "burlywood2", "gold1", "greenyellow", 
                            "darkseagreen", "dodgerblue1", "thistle2","darkolivegreen3", "chocolate", 
                            "turquoise3", "steelblue3","navy","darkseagreen4","blanchedalmond","lightskyblue1","aquamarine2","lemonchiffon"  };
        return colors[result.GetHashCode() % colors.Length];
    }

}

public class DotGraphMethodCallAnalysisOutput {
    private string Annotate(ISymbol symbol) {
        return symbol.ToString().Replace(symbol.ContainingNamespace.ToString() + "." + symbol.ContainingType.Name + ".", "");
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
                builder.AppendLine($"\t \"{Annotate(methodSymbol)}\" -> \"{Annotate(result)}\"");
            }
        }
        builder.Append("}");
        return builder.ToString();
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
                    }
                }
            }
        }
        return result;
    }

    private static string RandomColor(CyclicMethodAnalysisResult result) {
        string[] colors = {"green", "red", "blue", "grey", "yellow", "purple", "salmon2", 
                            "deepskyblue", "goldenrod2", "burlywood2", "gold1", "greenyellow", 
                            "darkseagreen", "dodgerblue1", "thistle2","darkolivegreen3", "chocolate", 
                            "turquoise3", "steelblue3","navy","darkseagreen4","blanchedalmond","lightskyblue1","aquamarine2","lemonchiffon"  };
        return colors[result.GetHashCode() % colors.Length];
    }

}
