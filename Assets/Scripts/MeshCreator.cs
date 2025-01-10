using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class HandData
{
    [Tooltip("食指指尖的追踪物体")]
    public GameObject indexTip;

    [Tooltip("拇指指尖的追踪物体")]
    public GameObject thumbTip;

    [HideInInspector]
    public bool isDragging = false;

    [HideInInspector]
    public Vector3 lastPosition;

    [HideInInspector]
    public float currentQuadWidth;

    [HideInInspector]
    public MeshFilter currentMeshFilter;

    [HideInInspector]
    public MeshRenderer currentMeshRenderer;

    [HideInInspector]
    public List<Vector3> vertices = new List<Vector3>();

    [HideInInspector]
    public List<int> triangles = new List<int>();

    [HideInInspector]
    public float lastWidth;

    [HideInInspector]
    public Vector3 lastRight = Vector3.right;
}

public class MeshCreator : MonoBehaviour
{
    [Header("手势追踪设置")]
    [Space(10)]
    [Tooltip("左手追踪数据")]
    [SerializeField] private HandData leftHand = new HandData();

    [Tooltip("右手追踪数据")]
    [SerializeField] private HandData rightHand = new HandData();

    [Header("Mesh生成设置")]
    [Space(10)]
    [Tooltip("生成Mesh使用的默认材质")]
    [SerializeField] private Material defaultMeshMaterial;

    [Tooltip("生成新Mesh的最小移动距离（米）")]
    [Range(0.001f, 0.1f)]
    [SerializeField] private float minMoveDistance = 0.005f;

    [Tooltip("手指捏合的最小距离（米），小于此距离视为捏合")]
    [Range(0.005f, 0.05f)]
    [SerializeField] private float minFingerDistance = 0.01f;

    [Tooltip("相邻段宽度最大变化比例")]
    [Range(0.1f, 1f)]
    [SerializeField] private float maxWidthChangeRatio = 0.2f;

    [Tooltip("边缘共享的最大距离（米）")]
    [Range(0.001f, 0.1f)]
    [SerializeField] private float edgeMergeDistance = 0.01f;

    [HideInInspector]
    private List<GameObject> createdMeshes = new List<GameObject>();

    private Camera mainCamera;

    private void Start()
    {
        mainCamera = Camera.main;
    }

    private void Update()
    {
        // 处理左手
        if (leftHand.indexTip != null && leftHand.thumbTip != null)
        {
            UpdateHand(leftHand);
        }

        // 处理右手
        if (rightHand.indexTip != null && rightHand.thumbTip != null)
        {
            UpdateHand(rightHand);
        }
    }

    private bool IsPointInCameraView(Vector3 point)
    {
        Vector3 viewportPoint = mainCamera.WorldToViewportPoint(point);
        return viewportPoint.z > 0 && viewportPoint.x >= 0 && viewportPoint.x <= 1 && viewportPoint.y >= 0 && viewportPoint.y <= 1;
    }

    private float LimitWidthChange(float currentWidth, float lastWidth)
    {
        float maxChange = lastWidth * maxWidthChangeRatio;
        float minAllowedWidth = lastWidth - maxChange;
        float maxAllowedWidth = lastWidth + maxChange;
        return Mathf.Clamp(currentWidth, minAllowedWidth, maxAllowedWidth);
    }

    private float CalculateScreenSpaceWidth(HandData hand)
    {
        // 将两个手指位置转换到屏幕空间
        Vector2 indexScreenPos = mainCamera.WorldToScreenPoint(hand.indexTip.transform.position);
        Vector2 thumbScreenPos = mainCamera.WorldToScreenPoint(hand.thumbTip.transform.position);

        // 计算屏幕空间的距离
        float screenDistance = Vector2.Distance(indexScreenPos, thumbScreenPos);

        // 将屏幕空间距离转换回世界空间
        // 使用食指位置的深度来确保比例正确
        float indexDepth = (hand.indexTip.transform.position - mainCamera.transform.position).magnitude;
        float worldSpaceWidth = screenDistance * indexDepth / mainCamera.pixelWidth;

        return worldSpaceWidth;
    }

    private void UpdateHand(HandData hand)
    {
        if (!hand.indexTip.activeInHierarchy || !hand.thumbTip.activeInHierarchy)
        {
            hand.isDragging = false;
            return;
        }

        // 使用新的屏幕空间宽度计算方法
        float fingerDistance = CalculateScreenSpaceWidth(hand);

        if (fingerDistance < minFingerDistance)
        {
            hand.isDragging = false;
            return;
        }

        // 更新宽度
        hand.currentQuadWidth = fingerDistance;

        Vector3 indexPosition = hand.indexTip.transform.position;

        if (!hand.isDragging)
        {
            StartDragging(hand, indexPosition);
        }
        else
        {
            float moveDistance = Vector3.Distance(indexPosition, hand.lastPosition);
            if (moveDistance >= minMoveDistance)
            {
                UpdateMeshGeneration(hand, indexPosition);
            }
        }
    }

    private void UpdateMeshGeneration(HandData hand, Vector3 position)
    {
        if (hand.currentMeshFilter == null) return;

        // 实时更新宽度
        float currentWidth = Vector3.Distance(hand.indexTip.transform.position, hand.thumbTip.transform.position);
        hand.currentQuadWidth = currentWidth;

        // 如果是第一个顶点
        if (hand.vertices.Count == 0)
        {
            GenerateFirstQuad(hand, position);
        }
        else
        {
            UpdateLastQuad(hand, position);
        }

        hand.lastPosition = position;
        hand.lastWidth = currentWidth;
    }

    private void GenerateFirstQuad(HandData hand, Vector3 position)
    {
        Vector3 cameraPosition = mainCamera.transform.position;
        Vector3 toCamera = (cameraPosition - position).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, toCamera).normalized;

        // 生成初始四边形
        Vector3 halfWidth = right * (hand.currentQuadWidth * 0.5f);

        hand.vertices.Add(position + halfWidth);  // 左上
        hand.vertices.Add(position - halfWidth);  // 左下

        // 初始只添加两个顶点，等待下一个位置再完成四边形
        hand.lastRight = right;
    }

    private void UpdateLastQuad(HandData hand, Vector3 position)
    {
        Vector3 cameraPosition = mainCamera.transform.position;
        Vector3 toCamera = (cameraPosition - position).normalized;
        Vector3 forward = (position - hand.lastPosition).normalized;
        Vector3 right = Vector3.Cross(forward, toCamera).normalized;

        Vector3 halfWidth = right * (hand.currentQuadWidth * 0.5f);

        int lastVertexCount = hand.vertices.Count;

        // 添加新的两个顶点，与上一个quad共用起始边
        hand.vertices.Add(position + halfWidth);  // 右上
        hand.vertices.Add(position - halfWidth);  // 右下

        // 添加三角形，使用共用的边
        hand.triangles.Add(lastVertexCount - 2); // 上一个左上
        hand.triangles.Add(lastVertexCount - 1); // 上一个左下
        hand.triangles.Add(lastVertexCount);     // 新的右上

        hand.triangles.Add(lastVertexCount - 1); // 上一个左下
        hand.triangles.Add(lastVertexCount + 1); // 新的右下
        hand.triangles.Add(lastVertexCount);     // 新的右上

        // 更新mesh
        Mesh mesh = hand.currentMeshFilter.mesh;
        mesh.Clear();
        mesh.SetVertices(hand.vertices);
        mesh.SetTriangles(hand.triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        UpdateMeshCollider(hand);
    }

    private void StartDragging(HandData hand, Vector3 position)
    {
        hand.isDragging = true;
        CreateNewMeshObject(hand);

        hand.lastPosition = position;
        hand.vertices.Clear();
        hand.triangles.Clear();

        // 初始化宽度
        hand.currentQuadWidth = Vector3.Distance(hand.indexTip.transform.position, hand.thumbTip.transform.position);
        hand.lastWidth = hand.currentQuadWidth;
    }

    private void CreateNewMeshObject(HandData hand)
    {
        GameObject meshObject = new GameObject($"GeneratedMesh_{createdMeshes.Count}");
        meshObject.transform.SetParent(transform);
        meshObject.layer = LayerMask.NameToLayer("ARMesh");

        MeshCollider collider = meshObject.AddComponent<MeshCollider>();
        collider.convex = true;
        collider.isTrigger = false;

        hand.currentMeshFilter = meshObject.AddComponent<MeshFilter>();
        hand.currentMeshRenderer = meshObject.AddComponent<MeshRenderer>();
        hand.currentMeshRenderer.material = defaultMeshMaterial;

        hand.currentMeshFilter.mesh = new Mesh();
        createdMeshes.Add(meshObject);
    }

    private void UpdateMeshCollider(HandData hand)
    {
        if (hand.currentMeshFilter != null && hand.currentMeshFilter.mesh != null)
        {
            MeshCollider collider = hand.currentMeshFilter.GetComponent<MeshCollider>();
            if (collider != null)
            {
                collider.sharedMesh = null;
                collider.sharedMesh = hand.currentMeshFilter.mesh;
            }
        }
    }

    // 新增：尝试合并相近的mesh
    private void TryMergeMeshes()
    {
        for (int i = createdMeshes.Count - 1; i >= 0; i--)
        {
            if (createdMeshes[i] == null) continue;

            MeshFilter currentMeshFilter = createdMeshes[i].GetComponent<MeshFilter>();
            if (currentMeshFilter == null || currentMeshFilter.mesh == null) continue;

            for (int j = i - 1; j >= 0; j--)
            {
                if (createdMeshes[j] == null) continue;

                MeshFilter otherMeshFilter = createdMeshes[j].GetComponent<MeshFilter>();
                if (otherMeshFilter == null || otherMeshFilter.mesh == null) continue;

                if (ShouldMergeMeshes(currentMeshFilter.mesh, otherMeshFilter.mesh, createdMeshes[i].transform, createdMeshes[j].transform))
                {
                    MergeMeshes(createdMeshes[i], createdMeshes[j]);
                    break;
                }
            }
        }

        // 清理空的mesh对象
        createdMeshes.RemoveAll(mesh => mesh == null);
    }

    private bool ShouldMergeMeshes(Mesh mesh1, Mesh mesh2, Transform transform1, Transform transform2)
    {
        Vector3[] vertices1 = mesh1.vertices;
        Vector3[] vertices2 = mesh2.vertices;

        // 检查两个mesh是否有足够接近的顶点
        for (int i = 0; i < vertices1.Length; i++)
        {
            Vector3 worldVertex1 = transform1.TransformPoint(vertices1[i]);

            for (int j = 0; j < vertices2.Length; j++)
            {
                Vector3 worldVertex2 = transform2.TransformPoint(vertices2[j]);

                if (Vector3.Distance(worldVertex1, worldVertex2) < edgeMergeDistance)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void MergeMeshes(GameObject targetObj, GameObject sourceObj)
    {
        MeshFilter targetMeshFilter = targetObj.GetComponent<MeshFilter>();
        MeshFilter sourceMeshFilter = sourceObj.GetComponent<MeshFilter>();

        if (targetMeshFilter == null || sourceMeshFilter == null) return;

        // 合并mesh
        CombineInstance[] combine = new CombineInstance[2];

        combine[0].mesh = targetMeshFilter.sharedMesh;
        combine[0].transform = targetObj.transform.localToWorldMatrix;

        combine[1].mesh = sourceMeshFilter.sharedMesh;
        combine[1].transform = sourceObj.transform.localToWorldMatrix;

        Mesh newMesh = new Mesh();
        newMesh.CombineMeshes(combine, true, true);

        // 优化mesh
        newMesh.Optimize();

        // 更新目标mesh
        targetMeshFilter.mesh = newMesh;

        // 更新碰撞体
        MeshCollider targetCollider = targetObj.GetComponent<MeshCollider>();
        if (targetCollider != null)
        {
            targetCollider.sharedMesh = newMesh;
        }

        // 销毁源mesh对象
        GameObject.Destroy(sourceObj);
        createdMeshes.Remove(sourceObj);
    }
}