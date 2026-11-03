using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using TMPro;
using UnityEngine;

public class CPreviewPattern : MonoBehaviour
{

    [Header("패턴 범위 설정")]
    [SerializeField] private Vector3[] _polygonPoint;
    [SerializeField] private int[] _polygonTriangles;

    [SerializeField] private float _polygonSides;
    [SerializeField] private float _polygonRadius;
    [SerializeField] private float _centerRadius;


    [SerializeField] private float _atkRadiusMin = 5.0f;
    [SerializeField] private float _atkRadiusMax = 15.0f;
    
    [SerializeField] private float _startDegree = 15f;           // 호의 길이를 위한 각도
    [SerializeField] private float _endDegree= 75f;
    [SerializeField] private float _arcRadius = 10f;

    private Vector3 _startVec;
    private Vector3 _endVec;

    private Mesh _mesh;


    void Start()
    {
        _mesh = new Mesh();
        this.GetComponent<MeshFilter>().mesh = _mesh;
        
    }

    // 패턴 구역 채우기 
    private void DrawFilled(int sides, float radius)
    {
        _polygonPoint = GetCirclePoint(sides,radius).ToArray();
        _polygonTriangles = DrawFilledTriangles(_polygonPoint);

        _mesh.Clear();

        _mesh.vertices = _polygonPoint;
        _mesh.triangles = _polygonTriangles;
        

    }


    private int[] DrawFilledTriangles(Vector3[] points)
    {


        int triangleAmount = points.Length - 2;
        
        List<int> newTriangles = new List<int>();

        for(int i = 0; i < triangleAmount; i++)
        {
            newTriangles.Add(0);
            newTriangles.Add(i+2);
            newTriangles.Add(i + 1);

        }

        return newTriangles.ToArray();

    }

    // 원형 모양으로 공격범위 그리기
    private List<Vector3> GetCirclePoint(int sides, float radius)
    {
        List<Vector3> points = new List<Vector3>();

        float pointFound = (float)1 / sides;

        float TAU = 2 * Mathf.PI;

        float tickDegree = pointFound * TAU;

        for (int i = 0; i < sides; i++)
        {

            float currentRadian = tickDegree * i;
            points.Add(new Vector3(Mathf.Cos(currentRadian) * radius, 0f, Mathf.Sin(currentRadian) * radius));

        }

        return points;

    }




    // 피자 모양으로 공격범위 그리기
    private List<Vector3> GetArcPoint(int sides, float radius)
    {
        List<Vector3> arcPoints = new List<Vector3>();

        float pointFound = (float) 1 / sides;

        GrowingArc();

        float degree = Vector3.SignedAngle(_startVec, _endVec, Vector3.up);

        float tickDegree = pointFound * degree;

        for(int i = 0; i < sides; i++)
        {

            float currentRadian = tickDegree * i;
            arcPoints.Add(new Vector3(Mathf.Cos(currentRadian) * radius, 0f, Mathf.Sin(currentRadian) * radius) );

        }

        return arcPoints;
    }





    private void GrowingArc()
    {
        float radianR = _startDegree * Mathf.Deg2Rad;
        float startX = Mathf.Cos(radianR);
        float startY = Mathf.Sin(radianR);

        float radianL = _endDegree * Mathf.Deg2Rad;
        float endX = Mathf.Cos(radianL);
        float endY = Mathf.Sin(radianL);

        Vector3 startAcr = new Vector3(startX, 0f, startY);
        Vector3 lastAcr = new Vector3(endX, 0f, endY);

        _startVec = startAcr - this.transform.position;
        _endVec = lastAcr - this.transform.position;


    }


    private void DrawDonut(int sides, float outerRadius, float innerRadius)
    {
        List<Vector3> pointsList = new List<Vector3>();
        List<Vector3> outerPoints = GetCirclePoint(sides, outerRadius);
        pointsList.AddRange(outerPoints);

        List<Vector3> innerPoints = GetCirclePoint(sides, innerRadius);
        pointsList.AddRange(innerPoints);

        _polygonPoint = pointsList.ToArray();
        _polygonTriangles = DrawDonutTriangles(_polygonPoint);

        _mesh.Clear();
        _mesh.vertices = _polygonPoint;
        _mesh.triangles = _polygonTriangles;


    }


    private int[] DrawDonutTriangles(Vector3[] points)
    {
        int sides = points.Length / 2;
        int triangleAmount = sides * 2;

        List<int> newTriangles = new List<int>();

        for(int i = 0; i < sides; i++)
        {
            int outerIndex = i;
            int innerIndex = i + sides;

            newTriangles.Add(outerIndex);
            newTriangles.Add(innerIndex+2);
            newTriangles.Add((i+1) % sides);

            newTriangles.Add(outerIndex);
            newTriangles.Add(sides + ((sides + i - 1) % sides));
            newTriangles.Add(outerIndex + sides);

        }

        return newTriangles.ToArray();

    }




}
