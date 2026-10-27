using UnityEngine;

[ExecuteAlways]
[DefaultExecutionOrder(1000)]
public class CToonHeadTracker : MonoBehaviour
{
    [SerializeField] private Transform _headBone;
    [SerializeField] private Vector3 _centerLocal;
    [SerializeField] private Vector3 _forwardLocal = Vector3.forward;
    [SerializeField] private Vector3 _upLocal = Vector3.up;
    [SerializeField] private float _radius = 0.09f;

    private static readonly int _CENTER = Shader.PropertyToID("_ToonHeadCenter");
    private static readonly int _UP = Shader.PropertyToID("_ToonHeadUp");
    private static readonly int _FORWARD = Shader.PropertyToID("_ToonHeadForward");
    private static readonly int _RADIUS = Shader.PropertyToID("_ToonHeadRadius");

    private Renderer[] _renderers;
    private MaterialPropertyBlock _properties;

    private void OnEnable()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        _properties = new MaterialPropertyBlock();
    }

    private void LateUpdate()
    {
        if (_headBone == null)
        {
            return;
        }

        Vector3 forward = _headBone.TransformDirection(_forwardLocal).normalized;
        Vector3 up = _headBone.TransformDirection(_upLocal).normalized;
        Vector3 center = _headBone.TransformPoint(_centerLocal);

        foreach (Renderer target in _renderers)
        {
            if (target == null)
            {
                continue;
            }

            target.GetPropertyBlock(_properties);
            _properties.SetVector(_CENTER, center);
            _properties.SetVector(_UP, up);
            _properties.SetVector(_FORWARD, forward);
            _properties.SetFloat(_RADIUS, _radius);
            target.SetPropertyBlock(_properties);
        }
    }
}
