using System;
using System.Linq;
using System.Collections.Generic;
// using unityengine.something for vector2

/// <summary>
/// C# implementation of Guibas & Stolfi's Delaunay triangulation algorithm.
/// The divide-and-conquer algorithm requires all points for the Delaunay triangulation to be known. So, it only tries to make connections between those given points. The incremental algorithm allows the addition of new sites to an existing triangulation. Since this requires verifying Delaunayhood and fixing it if required, it is slower.
///
///
/// References:
///
/// Guibas, L., and Stolfi, J. (1985). Primitives for the Manipulation of General Subdivisions and the Computation of Voronoi Diagrams. Retrieved from https://dl-acm-org.leo.lib.unomaha.edu/doi/pdf/10.1145/282918.282923?download=true.
///     Original paper on the divide-and-conquer and incremental algorithms to compute a Delaunay triangulation, hereafter referred to as "G&S."
///
/// Heckbert, P. (2001). Quad-Edge Data Structure and Library. Retrieved from https://www.cs.cmu.edu/afs/andrew/scs/cs/15-463/2001/pub/src/a2/quadedge.html.
///     An explanation and implementation of the quad-edge data structure introduced in G&S in C++. The actual C++ code is by Andrew Bernard, based on existing code by Dani Lischinski.
///
/// Cheung, B. (2019). bennycheung/PyDelaunay: Python implementation of Delaunay and Voronoi Tessellation. Retrieved from https://github.com/bennycheung/PyDelaunay.
///     Incremental G&S algorithm in Python. MIT-licensed.
/// </summary>


namespace FiscalShock.GraphUtils {
    public class LinAlg {
        /// <summary>
        /// Determinant of a 4x4 matrix for InCircle test based on 4 points using Laplace expansion
        /// There's probably a cleaner formula out there...
        /// | Xa  Ya  (Xa^2 + Ya^2)  1 |
        /// | Xb  Yb  (Xb^2 + Yb^2)  1 |
        /// | Xc  Yc  (Xc^2 + Yc^2)  1 |
        /// | Xd  Yd  (Xd^2 + Yd^2)  1 |
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        /// <param name="d"></param>
        /// <returns></returns>
        public boolean determinant4(Vector2 a, Vector2 b, Vector2 c, Vector2 d) {
            double m02 = a.X*a.X + a.Y*a.Y;
            double m12 = b.X*b.X + b.Y*b.Y;
            double m22 = c.X*c.X + c.Y*c.Y;
            double m32 = d.X*d.X + d.Y*d.Y;
            return (1 * m12 * c.Y * d.X
                  - m02 * 1 * c.Y * d.X
                  - 1 * b.Y * m22 * d.X
                  + a.Y * 1 * m22 * d.X
                  + m02 * b.Y * 1 * d.X
                  - a.Y * m12 * 1 * d.X
                  - 1 * m12 * c.X * d.Y
                  + m02 * 1 * c.X * d.Y
                  + 1 * b.X * m22 * d.Y
                  - a.X * 1 * m22 * d.Y
                  - m02 * b.X * 1 * d.Y
                  + a.X * m12 * 1 * d.Y
                  + 1 * b.Y * c.X * m32
                  - a.Y * 1 * c.X * m32
                  - 1 * b.X * c.Y * m32
                  + a.X * 1 * c.Y * m32
                  + a.Y * b.X * 1 * m32
                  - a.X * b.Y * 1 * m32
                  - m02 * b.Y * c.X * 1
                  + a.Y * m12 * c.X * 1
                  + m02 * b.X * c.Y * 1
                  - a.X * m12 * c.Y * 1
                  - a.Y * b.X * m22 * 1
                  + a.X * b.Y * m22 * 1
            );
        }

        /// <summary>
        /// Area of the triangle abc is the following determinant:
        /// | Xa  Ya  1 |
        /// | Xb  Yb  1 |
        /// | Xc  Yc  1 |
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        /// <returns></returns>
        public double areaOfTriangle(Vector2 a, Vector2 b, Vector2 c) {
            return (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        }
    }

    public class Edge {
        // ID of this edge within the QuadEdge
        public uint id { get; set; }

        // Next edge
        public Edge next { get; set; }

        // Endpoints of the edge
        public Vector2 tail { get; set; }
        public Vector2 head { get; set; }

        public QuadEdge quad { get; set; }

        /// <summary>
        /// Get the right-to-left dual of this edge
        /// </summary>
        public Edge rotate() {  // Rot
            return quad.edges[
                   id < 3?
                   id + 1 : id - 3
            ];
        }

        /// <summary>
        /// Get the left-to-right dual of this edge
        /// </summary>
        /// <returns></returns>
        public Edge inverseRotate() {  // InvRot
            return quad.edges[
                   id > 0?
                   id - 1 : id + 3
            ];
        }

        /// <summary>
        /// Get the edge with head (destination) and tail (origin) flipped
        /// </summary>
        /// <returns></returns>
        public Edge symmetric() {  // Sym
            return quad.edges[
                   id < 2 ?
                   id + 2 : id - 2
            ];
        }

        /// <summary>
        /// Get the next edge, counterclockwise, around the tail (origin) of this edge
        /// </summary>
        /// <returns></returns>
        public Edge nextFromTail() {  // Onext
            return next;
        }

        /// <summary>
        /// Get the next edge, clockwise, around the tail of this edge
        /// </summary>
        /// <returns></returns>
        public Edge prevFromTail() {  // Oprev
            // Rot -> Onext -> Rot
            return rotate().nextFromTail().rotate();
        }

        /// <summary>
        /// Get the next edge, counterclockwise, around the head (destination) of this edge
        /// </summary>
        /// <returns></returns>
        public Edge nextFromHead() {  // Dnext
            // Sym -> Onext -> Sym
            return symmetric().nextFromTail().symmetric();
        }

        /// <summary>
        /// Get the next edge, clockwise, around the head of this edge
        /// </summary>
        /// <returns></returns>
        public Edge prevFromHead() {  // Dprev
            // InvRot -> Onext -> InvRot
            return inverseRotate().nextFromTail().inverseRotate();
        }

        /// <summary>
        /// Get the edge bounding the left face, counterclockwise, after this edge
        /// </summary>
        /// <returns></returns>
        public Edge nextFromLeft() {  // Lnext
            // InvRot -> Onext -> Rot
            return inverseRotate().nextFromTail().rotate();
        }

        /// <summary>
        /// Get the edge bounding the left face, counterclockwise, before this edge
        /// </summary>
        /// <returns></returns>
        public Edge prevFromLeft() {  // Lprev
            // Onext -> Sym
            return symmetric().nextFromTail();
        }

        /// <summary>
        /// Get the edge bounding the right face, counterclockwise, after this edge
        /// </summary>
        /// <returns></returns>
        public Edge nextFromRight() {  // Rnext
            // Rot -> Onext -> InvRot
            return rotate().nextFromTail().inverseRotate();
        }

        /// <summary>
        /// Get the edge bounding the right face, counterclockwise, before this edge
        /// </summary>
        /// <returns></returns>
        public Edge prevFromRight() {  // Rprev
            // Sym -> Onext
            return symmetric().nextFromTail();
        }
    }

    public class QuadEdge {
        // Edges of the QuadEdge. Different ways to represent the edge and its dual (e*).
        // 0    e
        // 1    e.rotate()          e*, rotated counterclockwise 90 degrees
        // 2    e.symmetric()       e, with head and tail flipped
        // 3    e.inverseRotate()   e*, rotated clockwise 90 degrees
        public Edge[] edges;

        public QuadEdge(Vector2 origin, Vector2 destination) {
            edges = new Edge[4];

            int i = 0;
            foreach (Edge e in edges) {
                e.id = i;

                e.next = (i != 0?
                          edges[(4 - i)] : e);
                i++;
            }
        }

        /// <summary>
        /// Set the endpoints for the primal edges of the quad edge
        ///
        /// We can't set the endpoints of the duals (1, 3) without already knowing the triangulation. But the D&C algorithm also states that it doesn't need the endpoints of the dual edges to run, so we shouldn't encounter problems.
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="destination"></param>
        public void setEndpoints(Vector2 origin, Vector2 destination) {
            edges[0].tail = origin;
            edges[0].head = destination;
            edges[2].tail = destination;
            edges[2].head = origin;
        }

        /// <summary>
        /// Get the representative edge
        /// </summary>
        /// <returns></returns>
        public Edge getEdge() {
            return edges[0];
        }

        /// <summary>
        /// G&S, pp. 98-102
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        public static void splice(Edge a, Edge b) {
            Edge alpha = edges[a.id].nextFromTail().rotate();
            Edge beta = edges[b.id].nextFromTail().rotate();

            Edge[] tmp = new Edge {
                a.nextFromTail(),
                b.nextFromTail(),
                alpha.nextFromTail(),
                beta.nextFromTail()
            };

            edges[b.id].next = tmp[1];
            edges[a.id].next = tmp[2];
            edges[beta.id].next = tmp[3];
            edges[alpha.id].next = tmp[4];
        }


        /// <summary>
        /// G&S, pp. 102-103
        /// </summary>
        public static void removeEdge(Edge e) {
            splice(e, e.prevFromTail());
            splice(e.symmetric(), e.symmetric().prevFromTail());
        }

        /// <summary>
        /// G&S, p. 103
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public Edge connect(Edge a, Edge b) {
            // Edge e = makeEdge somethingorother
            e.tail = a.head;
            e.head = b.tail;
            splice(e, a.nextFromLeft());
            splice(symmetric(e), b);
            return e;
        }

    }

    public class Delaunay {
        public List<Vector2> delaunayPoints;

        /// <summary>
        /// G&S, p. 113
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        /// <returns></returns>
        private boolean ccw(Vector2 a, Vector2 b, Vector2 c) {
            return LinAlg.areaOfTriangle(a, b, c) > 0;
        }

        private boolean rightOf(Vector2 point, Edge e) {
            return ccw(point, e.head, e.tail);
        }

        private boolean leftOf(Vector2 point, Edge e) {
            return ccw(point, e.tail, e.head);
        }

        /// <summary>
        /// G&S, p. 113
        /// Is this edge above the base line?
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        private boolean isEdgeValid(Edge e) {
            return rightOf(e.head, baseLine.head, baseLine.tail);
        }

        private boolean inCircle(Vector2 a, Vector2 b, Vector2 c, Vector2 d) {
            return LinAlg.determinant4(a, b, c, d) > 0;
        }

        /// <summary>
        ///
        /// </summary>c#
        /// <param name="points"></param>
        public Delaunay(List<Vector2> points) {
            // Sort the input list by x, then y
            delaunayPoints = points.OrderBy(p => p.x).ThenBy(p => p.y).ToList();
        }

        private List<Edge> triangulate(List<Vector2> points) {
            Edge baseLine;  // basel is updated during this function so it should be local

            if (points.Count == 2) {
                // Only two points, so make an edge from points[0] to points[1]
                // a = make edge;
                Edge a;
                a.tail = points[0];
                a.head = points[1];
                return (new List<Edge> { a, a.symmetric() });
            } else if (points.Count == 3) {
                // Connect (a) points[0] to points[1] and (b) points[1] to points[2]
                Edge a, b;
                QuadEdge.splice(a.symmetric(), b);
                a.tail = points[0];
                a.head = b.tail = points[1];
                b.head = points[2];
                // Two edges, so we can make a triangle here
                if (ccw(points[0], points[1], points[2])) {
                    Edge c = QuadEdge.connect(b, a);
                    return (new List<Edge> { a, b.symmetric() });
                } else if (ccw(points[0], points[2], points[1])) {  // It's a different order in G&S than the first check
                    Edge c = QuadEdge.connect(b, a);
                    return (new List<Edge> { c.symmetric(), c });
                } else {  // collinear case
                    return (new List<Edge> { a, b.symmetric() });
                }
            } else {  // divide-and-conquer
                // split into L and R
                uint leftHalfEnd = (points.Count & 1 == 1)?  // if odd, ceil
                                       points.Count/2 + 1 : points.Count/2;
                List<Vector2> leftHalf = points.GetRange(0, leftHalfEnd);
                List<Vector2> rightHalf = points.GetRange(leftHalfEnd + 1, points.Count);
                List<Edge> ld = triangulate(leftHalf);  // ld[0] = ldo, ld[1] = ldi
                List<Edge> rd = triangulate(rightHalf);  // rd[0] = rdo, rd[1] = rdi

                bool foundTangent = false;
                while (!foundTangent) {
                    if (leftOf(rd[1].tail, ld[1])) {
                        ld[1] = ld[1].nextFromLeft();
                    } else if (rightOf(ld[1].tail, rd[1])) {
                        rd[1] = rd[1].prevFromRight();
                    } else {
                        foundTangent = true;  // stop looking, lower common tangent found
                    }
                }

                // Set up the initial basel and update weird ldo/rdo
                baseLine = QuadEdge.connect(rd[1].symmetric(), ld[1]);
                if (ld[1].tail == ld[0].tail) {
                    ld[0] = baseLine.symmetric();
                }
                if (rd[1].tail == rd[0].tail) {
                    rd[0] = baseLine;
                }

                foundTangent = false;
                while (!foundTangent) {
                    leftCandidate = findCandidate(true);
                    rightCandidate = findCandidate(false);

                    if (!isEdgeValid(leftCandidate) && !isEdgeValid(rightCandidate)) {
                        foundTangent = true;  // upper common tangent found
                    }

                    if (!isEdgeValid(leftCandidate)
                       || (isEdgeValid(rightCandidate) && inCircle(leftCandidate.head, leftCandidate.tail, rightCandidate.tail, rightCandidate.head))) {
                        // Add cross edge basel from rcand.Dest to basel.Dest
                        baseLine = QuadEdge.connect(rightCandidate, baseLine.symmetric());
                    } else { // pick lcand
                        baseLine = QuadEdge.connect(baseLine.symmetric(), leftCandidate.symmetric());
                    }
                }
                return new List<Edge> { ld[0], rd[0] };
            }
        }

        // this is ugly
        private Edge findCandidate(boolean isLeftCandidate) {
            Edge candidate;
            Vector2 candidatePoint;
            if (isLeftCandidate) {
                candidate = baseLine.symmetric().nextFromTail();
                candidatePoint = candidate.nextFromTail().head;
            } else { // rcand
                candidate = baseLine.prevFromTail();
                candidatePoint = candidate.prevFromTail().head;
            }

            if (isEdgeValid(candidate)) {
                while (inCircle(baseLine.head, baseLine.tail, candidate.head, candidatePoint)) {
                    Edge temp = isLeftCandidate? candidate.nextFromTail() : candidate.prevFromTail();
                    QuadEdge.removeEdge(candidate);
                    candidate = temp;
                }
            }

            return candidate;
        }
    }
}