using System.Collections.Generic;
using UnityEngine;

public class CPlayerDashGhostPose : ScriptableObject
{
    [SerializeField] private string[] _bones;
    [SerializeField] private Quaternion[] _rotations;

    private Dictionary<string, Quaternion> _rotationByBone;

    internal bool TryGetRotation(string boneName, out Quaternion rotation)
    {
        if (_rotationByBone == null)
        {
            _rotationByBone = new Dictionary<string, Quaternion>(_bones.Length);

            for (int i = 0; i < _bones.Length; i++)
            {
                _rotationByBone[_bones[i]] = _rotations[i];
            }
        }

        return _rotationByBone.TryGetValue(boneName, out rotation);
    }
}
