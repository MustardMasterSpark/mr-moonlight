using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DDT
{
    public class GraphLineRendererUI : Graphic
    {
        public List<Vector2> m_Points;
        private float LineThickness = 1f;
        public int MaxPointsToDraw = 0;

        protected override void OnPopulateMesh(VertexHelper VertHelper)
        {
            VertHelper.Clear();

            if (m_Points.Count < 2)
                return;

            for (int i = 0; i < m_Points.Count - 1; i++)
            {
                if (i >= MaxPointsToDraw - 1)
                    return;

                // Create a line segment between the next two points
                CreateLineSegment(m_Points[i], m_Points[i + 1], VertHelper);

                int Index = i * 5;

                VertHelper.AddTriangle(Index, Index + 1, Index + 3);
                VertHelper.AddTriangle(Index + 3, Index + 2, Index);

                if (i != 0)
                {
                    VertHelper.AddTriangle(Index, Index - 1, Index - 3);
                    VertHelper.AddTriangle(Index + 1, Index - 1, Index - 2);
                }
            }
        }

        private void CreateLineSegment(Vector3 Point_1, Vector3 Point_2, VertexHelper VertHelper)
        {
            // Create vertex template
            UIVertex Vertex = UIVertex.simpleVert;
            Vertex.color = color;

            // Seg begin
            Vector2 Dir = GetPerpendicularVector(Point_1, Point_2);
            Vertex.position = Dir * -LineThickness / 2;
            Vertex.position += Point_1;
            VertHelper.AddVert(Vertex);

            Vertex.position = Dir * LineThickness / 2;
            Vertex.position += Point_1;
            VertHelper.AddVert(Vertex);

            // Seg end
            Vertex.position = Dir * -LineThickness / 2;
            Vertex.position += Point_2;
            VertHelper.AddVert(Vertex);
            Vertex.position = Dir * LineThickness / 2;
            Vertex.position += Point_2;
            VertHelper.AddVert(Vertex);

            // Seg end point
            Vertex.position = Point_2;
            VertHelper.AddVert(Vertex);
        }

        private Vector3 GetPerpendicularVector(Vector2 Origin, Vector2 Target)
        {
            Vector2 Di = (Target - Origin).normalized;
            return new Vector2(Di.y, -Di.x);
        }
    }
}