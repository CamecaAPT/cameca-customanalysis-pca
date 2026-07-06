using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cameca.CustomAnalysis.Pca.Models;

public class ComponentDataModel
{
    public string Name { get; }

    public float[] Scores { get; }

    public float[] Loads { get; }

    public ComponentDataModel(string name, float[] scores, float[] loads)
    {
        Name = name;
        Scores = scores;
        Loads = loads;
    }
}
