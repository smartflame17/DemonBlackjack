using UnityEngine;
using Unity.Cinemachine;

public class CinemachineCameraShake : MonoBehaviour
{
    public static CinemachineCameraShake Instance { get; private set; }

    [SerializeField] private CinemachineCamera Camera;
    [SerializeField] private CinemachineBasicMultiChannelPerlin perlin;
    [SerializeField] private float ShakeDuration = 0.5f;
    [SerializeField] private float ShakeAmplitude = 1.2f;

    private float _shakeTimer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
        }
        Camera = GetComponent<CinemachineCamera>();
    }

    public void ShakeCamera(float intensity)
    {
        ShakeCamera(intensity, ShakeDuration);
    }

    public void ShakeCamera(float intensity, float duration)
    {
        ShakeAmplitude = intensity;
        perlin.AmplitudeGain = ShakeAmplitude;
        ShakeDuration = duration;
        _shakeTimer = ShakeDuration;
    }

    // Update is called once per frame
    void Update()
    {
        if (_shakeTimer > 0)
        {
            _shakeTimer -= Time.deltaTime;
            perlin.AmplitudeGain = Mathf.Lerp(ShakeAmplitude, 0f, 1 - (_shakeTimer / ShakeDuration));
        }
    }
}
