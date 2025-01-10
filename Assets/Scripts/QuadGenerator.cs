using UnityEngine;
using System.Collections.Generic;

public class QuadGenerator
{
    public static void GenerateQuad(Vector3 startPoint, Vector3 endPoint, List<Vector3> vertices, List<int> triangles, float width = 1f, Camera camera = null)
    {
        // 使用手指之间的方向作为up方向
        Vector3 fingerDirection = (endPoint - startPoint).normalized;

        // 使用手指方向和世界up方向计算right方向
        Vector3 right = Vector3.Cross(fingerDirection, Vector3.up).normalized;

        // 如果手指方向接近垂直，使用世界forward作为替代
        if (right.magnitude < 0.01f)
        {
            right = Vector3.Cross(fingerDirection, Vector3.forward).normalized;
        }

        // 使用这个right向量来确定四边形的宽度方向
        Vector3 offset = right * width * 0.5f;

        int vertexOffset = vertices.Count;

        // 检查是否已经有顶点（是否需要与前一个四边形连接）
        if (vertexOffset >= 2)
        {
            // 复用前一个四边形的最后两个顶点
            Vector3 newTopVertex = endPoint + offset;
            Vector3 newBottomVertex = endPoint - offset;

            // 添加新的顶点
            vertices.Add(newTopVertex);    // 新的右上顶点
            vertices.Add(newBottomVertex); // 新的右下顶点

            // 使用前一个四边形的最后两个顶点和新顶点创建三角形
            // 第一个三角形
            triangles.Add(vertexOffset - 2); // 前一个四边形的右上顶点
            triangles.Add(vertexOffset - 1); // 前一个四边形的右下顶点
            triangles.Add(vertexOffset);     // 新的右上顶点

            // 第二个三角形
            triangles.Add(vertexOffset - 1); // 前一个四边形的右下顶点
            triangles.Add(vertexOffset + 1); // 新的右下顶点
            triangles.Add(vertexOffset);     // 新的右上顶点
        }
        else
        {
            // 第一个四边形，添加所有四个顶点
            vertices.Add(startPoint + offset);  // 左上
            vertices.Add(startPoint - offset);  // 左下
            vertices.Add(endPoint + offset);    // 右上
            vertices.Add(endPoint - offset);    // 右下

            // 添加两个三角形
            triangles.Add(vertexOffset);     // 左上
            triangles.Add(vertexOffset + 1); // 左下
            triangles.Add(vertexOffset + 2); // 右上

            triangles.Add(vertexOffset + 1); // 左下
            triangles.Add(vertexOffset + 3); // 右下
            triangles.Add(vertexOffset + 2); // 右上
        }
    }

    public static void UpdateMesh(Mesh mesh, List<Vector3> vertices, List<int> triangles)
    {
        if (vertices.Count < 4 || triangles.Count < 6)
        {
            Debug.LogWarning("顶点或三角形数量不足，无法更新mesh");
            return;
        }

        mesh.Clear();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }
}