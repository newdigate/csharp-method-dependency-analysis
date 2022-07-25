using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
namespace type_deinference;

public class SolutionMethodAnalysis {
    private readonly IMethodCallAnalizer _typeDeInference;

    public SolutionMethodAnalysis(IMethodCallAnalizer typeDeInference)
    {
        _typeDeInference = typeDeInference;
    }
    
    public IDictionary<ISymbol, IList<ISymbol>> AnalizeMethodCallsForSolution(string solutionPath) {
        EnsureMsBuildRegistration();
        using (MSBuildWorkspace workspace = MSBuildWorkspace.Create())
        {
            workspace.WorkspaceFailed += (sender, workspaceFailedArgs) => Console.WriteLine(workspaceFailedArgs.Diagnostic.Message);
            IDictionary<ISymbol, IList<ISymbol>> result = new Dictionary<ISymbol, IList<ISymbol>>();

            Solution solution = workspace.OpenSolutionAsync(solutionPath).Result;
            foreach (ProjectId projectId in workspace.CurrentSolution.GetProjectDependencyGraph().GetTopologicallySortedProjects()) {
                Project? project = workspace.CurrentSolution.GetProject(projectId);
                if (project == null) continue;
                IDictionary<ISymbol, IList<ISymbol>> substitutions =  AnalizeMethodCallsForProject(project);
                result = result.Union(substitutions).ToDictionary(k => k.Key, v => v.Value);
            }

            return result;
        }    
    }
    

    public IDictionary<ISymbol, IList<ISymbol>> AnalizeMethodCallsForProject(string projectPath) {
        EnsureMsBuildRegistration();
        using(MSBuildWorkspace workspace = MSBuildWorkspace.Create()) 
        {
            workspace.WorkspaceFailed += (sender, workspaceFailedArgs) => Console.WriteLine(workspaceFailedArgs.Diagnostic.Message);

            Project project = workspace.OpenProjectAsync(projectPath).Result;
            return AnalizeMethodCallsForProject(project);
        }
    } 

    public IDictionary<ISymbol, IList<ISymbol>> AnalizeMethodCallsForProject(Project project) {

        IDictionary<ISymbol, IList<ISymbol>> empty = new Dictionary<ISymbol, IList<ISymbol>>();
        
        Compilation? compilation = project.GetCompilationAsync().Result;
               
        if (compilation == null) return empty;
 
        foreach (var d in compilation.GetDiagnostics())
        {
            Console.WriteLine(CSharpDiagnosticFormatter.Instance.Format(d));
        }
        IDictionary<SyntaxTree, CompilationUnitSyntax> roots = new Dictionary<SyntaxTree, CompilationUnitSyntax>();
        foreach (SyntaxTree tree in compilation?.SyntaxTrees) {
            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
            roots[tree] = root;
        }

        return
            _typeDeInference
                .AnalizeMethodCalls(compilation, roots);
    }

    private void EnsureMsBuildRegistration() {
        if (!MSBuildLocator.IsRegistered)
            MSBuildLocator.RegisterDefaults();
    }
}
