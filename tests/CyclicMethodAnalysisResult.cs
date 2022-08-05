using Microsoft.CodeAnalysis;
namespace type_deinference;

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