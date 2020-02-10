using ThirdParty.Delaunator;
using System;
using System.Collections.Generic;

namespace FiscalShock.Graphs {
    /// <summary>
    /// Interface and extension of the Delaunator library
    /// </summary>
    public class Delaunay {
        public Triangulation dt { get; }

        public Delaunay(List<double> input) {
            dt = new Triangulation(input);
        }
    }
}