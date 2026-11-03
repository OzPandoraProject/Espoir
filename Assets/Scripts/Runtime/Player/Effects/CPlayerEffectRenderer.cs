using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Camera))]

public class CPlayerEffectRenderer : MonoBehaviour
{
    internal const int EFFECT_LAYER = 30;
    private const int _BLOOM_LEVELS = 5;
    private const int _BLOOM_HEIGHT = 540;

    [SerializeField] private Shader _compositeShader;
    [SerializeField] private Shader _distortionShader;
    [SerializeField] private Shader _depthShader;

    [Header("검기 색상")]
    [SerializeField] private Color _dark = new Color32(180, 42, 6, 255);
    [SerializeField] private Color _bright = new Color32(255, 100, 24, 255);
    [SerializeField] private Color _core = new Color32(255, 226, 168, 255);
    [SerializeField, Range(0f, 1f)] private float _luminanceBlend = 0.5f;
    [SerializeField, Min(0f)] private float _bloomStrength = 0.55f;
    [SerializeField, Min(0f)] private float _bloomThreshold = 1f;

    private readonly RenderTexture[] _BLURRED = new RenderTexture[_BLOOM_LEVELS];
    private readonly float[][] _WEIGHTS = new float[_BLOOM_LEVELS][];
    private static readonly int[] _BLOOM_IDS =
    {
        Shader.PropertyToID("_B1"), Shader.PropertyToID("_B2"), Shader.PropertyToID("_B3"),
        Shader.PropertyToID("_B4"), Shader.PropertyToID("_B5")
    };

    private Camera _camera;
    private Camera _effectCamera;
    private Material _composite;
    private Material _depthCopy;
    private CommandBuffer _depthCommand;
    private int _previousMask;
    private Vector3 _screenCue;
    private Vector3 _basePosition;
    private float _baseFov;
    private Vector3 _renderPosition;
    private float _renderFov;
    private bool _hasPunch;

    private void OnEnable()
    {
        _camera = GetComponent<Camera>();
        _previousMask = _camera.cullingMask;

        if (_compositeShader == null || _distortionShader == null || _depthShader == null)
        {
            CLogger.Error($"{name}: 플레이어 이펙트 셰이더 연결을 확인해주세요.");
            enabled = false;
            return;
        }

        _camera.cullingMask &= ~(1 << EFFECT_LAYER);
        _camera.depthTextureMode |= DepthTextureMode.Depth;
        _camera.allowHDR = true;

        _composite = new Material(_compositeShader) { hideFlags = HideFlags.HideAndDontSave };
        _depthCopy = new Material(_depthShader) { hideFlags = HideFlags.HideAndDontSave };
        _depthCommand = new CommandBuffer { name = "Player Effect Depth" };
        _depthCommand.Blit(null, BuiltinRenderTextureType.CameraTarget, _depthCopy);

        for (int i = 0; i < _BLOOM_LEVELS; i++)
        {
            int radius = 3 + i * 2;
            float[] weights = new float[11];

            for (int tap = 0; tap < radius; tap++)
            {
                weights[tap] = Mathf.Exp(-0.5f * tap * tap / (radius * radius));
            }

            _WEIGHTS[i] = weights;
        }
    }

    private void OnDisable()
    {
        RestoreCamera();
        _camera.cullingMask = _previousMask;
        _depthCommand?.Release();

        if (_effectCamera != null)
        {
            Destroy(_effectCamera.gameObject);
        }

        if (_composite != null)
        {
            Destroy(_composite);
        }

        if (_depthCopy != null)
        {
            Destroy(_depthCopy);
        }
    }

    private void OnPreCull()
    {
        CPlayerEffectStart.GetCameraCues(out Vector2 punch, out _screenCue);
        _basePosition = transform.position;
        _baseFov = _camera.fieldOfView;
        _renderPosition = _basePosition;
        _renderFov = _baseFov;
        if (punch.x < 0.01f && punch.y < 0.001f) return;

        float time = Time.unscaledTime * 40f;
        Vector3 shake = new Vector3(Mathf.PerlinNoise(time, 0f), Mathf.PerlinNoise(time, 1f),
            Mathf.PerlinNoise(time, 2f)) - Vector3.one * 0.5f;
        _renderPosition += shake * punch.y;
        _renderFov = Mathf.Max(1f, _baseFov - punch.x);
        _hasPunch = true;
        transform.position = _renderPosition;
        _camera.fieldOfView = _renderFov;
    }

    private void OnPostRender()
    {
        RestoreCamera();
    }

    private void RestoreCamera()
    {
        if (!_hasPunch) return;

        transform.position = _basePosition;
        _camera.fieldOfView = _baseFov;
        _hasPunch = false;
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (CPlayerEffectStart.ActiveCount == 0 && CPlayerDashGhost.ActiveCount == 0)
        {
            Graphics.Blit(source, destination);
            return;
        }

        Texture sceneDepth = Shader.GetGlobalTexture("_CameraDepthTexture");
        PrepareEffectCamera();
        CPlayerEffectStart.Sort(_camera);

        RenderTexture layer = GetTarget(source.width, source.height, 24);
        RenderTexture offset = null;

        try
        {
            if (CPlayerEffectStart.DistortionCount > 0)
            {
                offset = GetTarget(source.width / 2, source.height / 2);
                _effectCamera.backgroundColor = Color.clear;
                _effectCamera.targetTexture = offset;
                _effectCamera.RenderWithShader(_distortionShader, "EspoirDistort");
            }

            _effectCamera.backgroundColor = Color.black;
            _effectCamera.targetTexture = layer;
            _depthCopy.SetTexture("_SceneDepth", sceneDepth);
            _effectCamera.AddCommandBuffer(CameraEvent.BeforeForwardAlpha, _depthCommand);

            try
            {
                _effectCamera.Render();
            }
            
            finally
            {
                _effectCamera.RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha, _depthCommand);
            }

            _composite.SetTexture("_Offset", offset != null ? (Texture)offset : Texture2D.blackTexture);
            _composite.SetTexture("_Map", source);
            _composite.SetFloat("_Impact", _screenCue.x);
            _composite.SetFloat("_ImpactInvert", _screenCue.y);
            _composite.SetFloat("_White", _screenCue.z);
            SetPalette();
            Composite(layer, destination);
        }
        
        finally
        {
            _effectCamera.targetTexture = null;
            Shader.SetGlobalTexture("_CameraDepthTexture", sceneDepth);
            RenderTexture.ReleaseTemporary(layer);

            if (offset != null)
            {
                RenderTexture.ReleaseTemporary(offset);
            }
        }
    }

    private void PrepareEffectCamera()
    {
        if (_effectCamera == null)
        {
            GameObject cameraObject = new GameObject("Player Effect Camera") { hideFlags = HideFlags.HideAndDontSave };
            _effectCamera = cameraObject.AddComponent<Camera>();
            _effectCamera.enabled = false;
        }

        _effectCamera.CopyFrom(_camera);
        _effectCamera.transform.SetPositionAndRotation(_renderPosition, _camera.transform.rotation);
        _effectCamera.fieldOfView = _renderFov;
        _effectCamera.cullingMask = 1 << EFFECT_LAYER;
        _effectCamera.clearFlags = CameraClearFlags.SolidColor;
        _effectCamera.rect = new Rect(0f, 0f, 1f, 1f);
        _effectCamera.allowMSAA = false;
        _effectCamera.renderingPath = RenderingPath.Forward;
        _effectCamera.depthTextureMode = DepthTextureMode.None;
        _effectCamera.useOcclusionCulling = false;
    }

    private void SetPalette()
    {
        _composite.SetColor("_Dark", NormalizeHue(_dark));
        _composite.SetColor("_Bright", NormalizeHue(_bright));
        Color core = _core.linear;
        core.a = _luminanceBlend;
        _composite.SetColor("_Core", core);
        _composite.SetFloat("_Threshold", _bloomThreshold);
        _composite.SetFloat("_Strength", _bloomStrength);
        _composite.SetFloat("_Radius", 0.35f);
    }

    private static Color NormalizeHue(Color color)
    {
        color = color.linear;
        float peak = color.maxColorComponent;
        return peak > 0f ? color / peak : Color.black;
    }

    private void Composite(RenderTexture layer, RenderTexture destination)
    {
        int h = Mathf.Min(_BLOOM_HEIGHT, layer.height);
        int w = Mathf.Max(1, Mathf.RoundToInt(h * (float)layer.width / layer.height));
        RenderTexture bright = GetTarget(w, h);
        RenderTexture bloom = GetTarget(w, h);

        try
        {
            Graphics.Blit(layer, bright, _composite, 0);
            RenderTexture input = bright;

            for (int i = 0; i < _BLOOM_LEVELS; i++)
            {
                RenderTexture horizontal = GetTarget(w, h);
                _BLURRED[i] = GetTarget(w, h);

                try
                {
                    _composite.SetFloat("_Kernel", 3 + i * 2);
                    _composite.SetFloatArray("_Coef", _WEIGHTS[i]);
                    _composite.SetVector("_Step", new Vector4(1f / w, 0f));
                    Graphics.Blit(input, horizontal, _composite, 1);
                    _composite.SetVector("_Step", new Vector4(0f, 1f / h));
                    Graphics.Blit(horizontal, _BLURRED[i], _composite, 1);
                }
                
                finally
                {
                    RenderTexture.ReleaseTemporary(horizontal);
                }

                _composite.SetTexture(_BLOOM_IDS[i], _BLURRED[i]);
                input = _BLURRED[i];
                w = Mathf.Max(1, Mathf.RoundToInt(w / 2f));
                h = Mathf.Max(1, Mathf.RoundToInt(h / 2f));
            }

            Graphics.Blit(null, bloom, _composite, 2);
            _composite.SetTexture("_Bloom", bloom);
            Graphics.Blit(layer, destination, _composite, 3);
        }
       
        finally
        {
            RenderTexture.ReleaseTemporary(bright);
            RenderTexture.ReleaseTemporary(bloom);

            for (int i = 0; i < _BLOOM_LEVELS; i++)
            {
                if (_BLURRED[i] != null)
                {
                    RenderTexture.ReleaseTemporary(_BLURRED[i]);
                    _BLURRED[i] = null;
                }
            }
        }
    }

    private static RenderTexture GetTarget(int width, int height, int depth = 0)
    {
        RenderTexture target = RenderTexture.GetTemporary(Mathf.Max(1, width), Mathf.Max(1, height), depth,
            RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        target.filterMode = FilterMode.Bilinear;
        target.wrapMode = TextureWrapMode.Clamp;
       
        return target;
    }
}
