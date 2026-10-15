using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;

public class CQuaterView : MonoBehaviour
{
    #region 인스팩터
    [Header("Inspector")]
    [SerializeField] private GameObject _player;
    [SerializeField] private Transform _cameraTr;

    [Header("QuaterView Info")]
    [SerializeField] private Vector3 _quaterOffset = new Vector3(10f, 8f, 10f);
    [SerializeField] private float _quaterHeight = 1.5f;
    [SerializeField] private float _quaterSharpness = 5.0f;

    [Header("QuaterView Zoom")]
    [SerializeField] private Vector3 _quaterMinOffset = new Vector3(10f, 8f, 10f);
    [SerializeField] private Vector3 _quaterMaxOffset = new Vector3(15f, 12f, 15f);

    [SerializeField] private float _zoomSharpness = 10f;

    #endregion

    private Vector3 _currentOffset;
    private float _zoomDamped = 0f;

    private void Start()
    {
        if(_cameraTr == null)
        {
            _cameraTr = Camera.main.transform;
        }

        InitQuaterView();
    }

    private void Update()
    {


    }

    private void LateUpdate()
    {

        FollowQuaterView();
       
    }
    

    private void InitQuaterView()
    {
        Vector3 desiredPos;
        Quaternion desiredRot;

        desiredPos = _player.transform.position + _quaterOffset; 
        
        Vector3 lookPlayerPos = desiredPos + Vector3.up * _quaterHeight;

        desiredRot = Quaternion.LookRotation(lookPlayerPos - desiredPos, Vector3.up);

        ApplyCam(desiredPos, desiredRot, _quaterSharpness);
    }

    private void FollowQuaterView()
    {
        Vector3 desiredPos;
        Quaternion desiredRot;

        float scroll = Input.GetAxis("Mouse ScrollWheel");


        if (Mathf.Abs(scroll) > 0.01f)
        {
            CLogger.Log("Scroll 움직였어.");
            _zoomDamped += _zoomSharpness * (-scroll);
            float t = Mathf.Clamp(_zoomDamped, 0, 1);
            if(_zoomDamped < -1)
            {
                _zoomDamped = -1;
            }

            else if(_zoomDamped > 1)
            {
                _zoomDamped = 1;
            }

                _quaterOffset = Vector3.Lerp(_quaterMinOffset, _quaterMaxOffset, t);

        }



        desiredPos = _player.transform.position + _quaterOffset;

        Vector3 lookPlayerPos = _player.transform.position + Vector3.up * _quaterHeight * _quaterSharpness;

        desiredRot = Quaternion.LookRotation(lookPlayerPos - desiredPos, Vector3.up);

        ApplyCam(desiredPos, desiredRot, _quaterSharpness);

    }

    private void ApplyCam(Vector3 desiredPos, Quaternion desiredRot, float sharpness)
    {
        if(_cameraTr == null)
        {
            CLogger.Log("CQuaterView 스크립트 카메라 비어있음.");
            return;
        }


            float t = 1 - Mathf.Exp(-sharpness * Time.deltaTime);
            _cameraTr.position = Vector3.Lerp(_cameraTr.position, desiredPos, t);
            _cameraTr.rotation = Quaternion.Slerp(_cameraTr.rotation, desiredRot, t);
        
    }

}