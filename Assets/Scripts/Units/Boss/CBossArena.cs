using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class CBossArena : MonoBehaviour
{
    [SerializeField] private bool _circular;

    private BoxCollider _box;

    private void Awake()
    {
        _box = GetComponent<BoxCollider>();
    }

    public bool Contains(Vector3 worldPosition)
    {
        Vector3 offset = transform.InverseTransformPoint(worldPosition) - _box.center;
        Vector3 halfSize = _box.size * 0.5f;

        if (_circular)
        {
            float radius = Mathf.Min(halfSize.x, halfSize.z);
            return offset.x * offset.x + offset.z * offset.z <= radius * radius
                && Mathf.Abs(offset.y) <= halfSize.y;
        }

        return Mathf.Abs(offset.x) <= halfSize.x
            && Mathf.Abs(offset.y) <= halfSize.y
            && Mathf.Abs(offset.z) <= halfSize.z;
    }

    public Vector3 GetRandomGroundCandidate(float padding)
    {
        Vector3 halfSize = _box.size * 0.5f;
        float x = Mathf.Max(0f, halfSize.x - padding);
        float z = Mathf.Max(0f, halfSize.z - padding);

        Vector3 localPoint = new Vector3(
            _box.center.x + Random.Range(-x, x),
            0f,
            _box.center.z + Random.Range(-z, z));

        return transform.TransformPoint(localPoint);
    }
}