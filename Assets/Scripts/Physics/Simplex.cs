using System.Collections.Generic;
using UnityEngine;

public class Simplex
{
    private Vector3[] _points = new Vector3[4];
    private int _size = 0;

    public IReadOnlyList<Vector3> Points => new List<Vector3>(_points).GetRange(0, _size);

    public int Count => _size;

    public void Push(Vector3 point)
    {
        _points[3] = _points[2];
        _points[2] = _points[1];
        _points[1] = _points[0];
        _points[0] = point;

        _size = Mathf.Min(_size + 1, 4);
    }

    public bool NextSimplex(ref Vector3 direction)
    {
        switch (_size)
        {
            case 2: return Line(ref direction);
            case 3: return Triangle(ref direction);
            case 4: return Tetrahedron(ref direction);
        }

        return false;
    }

    private bool Line(ref Vector3 direction)
    {
        var a = _points[0];
        var b = _points[1];
        var ab = b - a;
        var ao = -a;

        if (SameDirection(ab, ao))
        {
            direction = Vector3.Cross(Vector3.Cross(ab, ao), ab);
        }
        else
        {
            SetPoints(a);
            direction = ao;
        }

        return false;
    }

    private bool Triangle(ref Vector3 direction)
    {
        var a = _points[0];
        var b = _points[1];
        var c = _points[2];

        var ab = b - a;
        var ac = c - a;
        var ao = -a;

        var abc = Vector3.Cross(ab, ac);

        if (SameDirection(Vector3.Cross(abc, ac), ao))
        {
            if (SameDirection(ac, ao))
            {
                SetPoints(a, c);
                direction = Vector3.Cross(Vector3.Cross(ac, ao), ac);
            }
            else
            {
                SetPoints(a, b);
                return Line(ref direction);
            }
        }
        else
        {
            if (SameDirection(Vector3.Cross(ab, abc), ao))
            {
                SetPoints(a, b);
                return Line(ref direction);
            }
            else
            {
                if (SameDirection(abc, ao))
                {
                    direction = abc;
                }
                else
                {
                    SetPoints(a, c, b);
                    direction = -abc;
                }
            }
        }

        return false;
    }

    private bool Tetrahedron(ref Vector3 direction)
    {
        var a = _points[0];
        var b = _points[1];
        var c = _points[2];
        var d = _points[3];

        var ab = b - a;
        var ac = c - a;
        var ad = d - a;
        var ao = -a;

        var abc = Vector3.Cross(ab, ac);
        var acd = Vector3.Cross(ac, ad);
        var adb = Vector3.Cross(ad, ab);

        if (SameDirection(abc, ao))
        {
            SetPoints(a, b, c);
            return Triangle(ref direction);
        }

        if (SameDirection(acd, ao))
        {
            SetPoints(a, c, d);
            return Triangle(ref direction);
        }

        if (SameDirection(adb, ao))
        {
            SetPoints(a, d, b);
            return Triangle(ref direction);
        }

        return true;
    }

    private bool SameDirection(Vector3 dir, Vector3 ao)
        => Vector3.Dot(dir, ao) > 0;

    private void SetPoints(Vector3 a)
    {
        _points[0] = a;
        _size = 1;
    }

    private void SetPoints(Vector3 a, Vector3 b)
    {
        _points[0] = a;
        _points[1] = b;
        _size = 2;
    }

    private void SetPoints(Vector3 a, Vector3 b, Vector3 c)
    {
        _points[0] = a;
        _points[1] = b;
        _points[2] = c;
        _size = 3;
    }
}
