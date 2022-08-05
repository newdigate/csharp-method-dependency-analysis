using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
namespace type_deinference;

public class TestTypeDeInference {

    private readonly MethodCallAnalizer _methodCallAnalizer;
    private readonly ClassDependencyAnalyzer _classDependencyAnalyzer;
    private readonly IEnumerable<MetadataReference> defaultReferences;
    private readonly IDictionary<string, ISymbol> _symbolCache = new Dictionary<string, ISymbol>();
    private readonly IRandomColorProvider _randomColorProvider = new RandomColorProvider();
    private readonly ICyclicMethodAnalyzer _cyclicMethodAnalyzer = new CyclicMethodAnalyzer();
    private readonly IMethodSymbolAnnotater _annotater = new MethodSymbolAnnotater();

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
                MetadataReference.CreateFromFile(typeof(NuGet.Frameworks.CompatibilityTable).Assembly.Location),
                MetadataReference.CreateFromFile(coreDir.FullName + Path.DirectorySeparatorChar + "System.Runtime.dll"),
            };
        ICSharpCompilationProvider compy = new CSharpCompilationProvider(defaultReferences);
        _methodCallAnalizer = new MethodCallAnalizer(compy, new SymbolFinder( es => {
            string fullName = es.WithoutTrivia().ToString();
            if (_symbolCache.ContainsKey(fullName))
                return _symbolCache[fullName];
            return null;
        } ) );
        _classDependencyAnalyzer = new ClassDependencyAnalyzer(compy, _methodCallAnalizer, new SymbolCache(_symbolCache));
    }

    [Fact]
    public void TestSolutionMethodAnalysis() {
        SolutionMethodAnalysis solutionMethodAnalysis = new SolutionMethodAnalysis(_methodCallAnalizer);
        IDictionary<ISymbol, IList<ISymbol>> result = solutionMethodAnalysis.AnalizeMethodCallsForSolution("/Users/nicholasnewdigate/Development/github/newdigate/csharp-method-dependency-analysis-2/MethodAnalysis.sln");
        string dot = new DotGraphMethodCallAnalysisOutput(_annotater).Process(result);
        Console.WriteLine(dot);
    }

    [Fact]
    public void TestSolutionInterfaceMethodAnalysis() {
        SolutionInterfaceMethodAnalysis solutionMethodAnalysis = new SolutionInterfaceMethodAnalysis(_classDependencyAnalyzer);
        IDictionary<ISymbol, IDictionary<ISymbol, IList<ISymbol>>> result = solutionMethodAnalysis.AnalizeMethodCallsForSolution("/Users/nicholasnewdigate/Development/github/newdigate/csharp-method-dependency-analysis-2/MethodAnalysis.sln");
        string dot = new DotGraphClassDependencyAnalysisOutput(_annotater).Process(result);
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
                                    Console.WriteLine($"\"{_annotater.Annotate(methodSymbol)}\" -> \"{_annotater.Annotate(dependency)}\"");
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
            IEnumerable<CyclicMethodAnalysisResult> analysis = _cyclicMethodAnalyzer.CheckForCyclicMethodCalls(methodSymbol, methodDependencies, visitedSymbols, rootDependenciesForMethod);
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
                Console.WriteLine($"{string.Join(" -> ", result.RecursionRoutes.Select(v => $"\"{v.ToString()}\""))} [color={_randomColorProvider.RandomColor(result)}, label=\"{index}\"];");
            }
            
            foreach (CyclicMethodAnalysisResult result in analysis.Where( r => r.RecursionRoutes.Last() == r.Symbol)) {
                int index = -1;
                if (map.ContainsKey(result)) {
                    index = map[result];
                } 
                Console.WriteLine($"\tnode [shape = circle, style=filled, color={_randomColorProvider.RandomColor(result)}];");
                Console.WriteLine($"\t {index} -> \"{result.Symbol}\" [color={_randomColorProvider.RandomColor(result)}] ");
            }
            
        }
        Console.WriteLine("}");
    }

}
