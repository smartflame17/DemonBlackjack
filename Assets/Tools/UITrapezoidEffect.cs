using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Graphic))]
public class UITrapezoidEffect : BaseMeshEffect
{
    [Tooltip("Positive values move the top corners inward.")]
    [SerializeField] private float topInset = 12f;

    [Tooltip("Positive values move the bottom corners inward.")]
    [SerializeField] private float bottomInset = 0f;

    public override void ModifyMesh(VertexHelper vertexHelper)
    {
        if (!IsActive() || vertexHelper.currentVertCount == 0)
            return;

        var vertices = new List<UIVertex>();
        vertexHelper.GetUIVertexStream(vertices);

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        foreach (UIVertex vertex in vertices)
        {
            minX = Mathf.Min(minX, vertex.position.x);
            maxX = Mathf.Max(maxX, vertex.position.x);
            minY = Mathf.Min(minY, vertex.position.y);
            maxY = Mathf.Max(maxY, vertex.position.y);
        }

        float centerX = (minX + maxX) * 0.5f;
        float height = Mathf.Max(maxY - minY, 0.001f);

        for (int i = 0; i < vertices.Count; i++)
        {
            UIVertex vertex = vertices[i];

            float verticalT = Mathf.InverseLerp(minY, maxY, vertex.position.y);
            float inset = Mathf.Lerp(bottomInset, topInset, verticalT);

            if (vertex.position.x < centerX)
                vertex.position.x += inset;
            else if (vertex.position.x > centerX)
                vertex.position.x -= inset;

            vertices[i] = vertex;
        }

        vertexHelper.Clear();
        vertexHelper.AddUIVertexTriangleStream(vertices);
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();

        if (graphic != null)
            graphic.SetVerticesDirty();
    }
#endif
}