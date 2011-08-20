using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Imml.Numerics;
using Imml.Drawing;

public static class Extensions
{
    public const float TwoPI = 6.283185307179586476925286766559F;

    public static UnityEngine.Vector3 ToUnityVector(this Vector3 vector3)
    {
        return new UnityEngine.Vector3(vector3.X, vector3.Y, vector3.Z);
    }

    public static UnityEngine.Color ToUnityColor(this Color3 color3, float alpha)
    {
        return new UnityEngine.Color(color3.R, color3.G, color3.B, alpha);
    }

    /// <summary>
    /// Unifies the specified vector3 so that the components are within the range 0 and TwoPI.
    /// </summary>
    /// <param name="vector3">The vector3.</param>
    /// <returns></returns>
    public static Vector3 Unify(this Vector3 vector3)
    {
        return new Vector3(vector3.X % TwoPI, vector3.Y % TwoPI, vector3.Z % TwoPI);
    }
}

