# csharp-method-dependency-analysis
1) determine recursive method chains in a class 
2) TODO: group methods of a class by their dependencies... 

* uses roslyn to parse, query and analize instances of `InvocationExpressionSyntax`...
* uses msbuild to load solutions and projects

## determine recursive method chains in a class 
* given a class
``` c#
using System;
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
```
* output 
``` dot
digraph G {
	 "NumberWang.Wang()" -> "NumberWang.Wong()" -> "NumberWang.Wang()" [color=green, label="1"];
	 "NumberWang.Wang()" -> "NumberWang.Weng()" -> "NumberWang.Wanganum()" -> "NumberWang.Wang()" [color=grey, label="2"];
	node [shape = circle, style=filled, color=green];
	 1 -> "NumberWang.Wang()" [color=green] 
	node [shape = circle, style=filled, color=grey];
	 2 -> "NumberWang.Wang()" [color=grey] 
	 "NumberWang.Wong()" -> "NumberWang.Wang()" -> "NumberWang.Wong()" [color=turquoise3, label="3"];
	node [shape = circle, style=filled, color=turquoise3];
	 3 -> "NumberWang.Wong()" [color=turquoise3] 
	 "NumberWang.Weng()" -> "NumberWang.Wanganum()" -> "NumberWang.Wang()" -> "NumberWang.Weng()" [color=goldenrod2, label="4"];
	node [shape = circle, style=filled, color=goldenrod2];
	 4 -> "NumberWang.Weng()" [color=goldenrod2] 
	 "NumberWang.Wanganum()" -> "NumberWang.Wang()" -> "NumberWang.Weng()" -> "NumberWang.Wanganum()" [color=deepskyblue, label="5"];
	node [shape = circle, style=filled, color=deepskyblue];
	 5 -> "NumberWang.Wanganum()" [color=deepskyblue] 
}
```
![dotgraph](docs/graphviz.svg)