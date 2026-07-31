using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.VFX;

[StructLayout(LayoutKind.Sequential)]
[VFXType(VFXTypeAttribute.Usage.GraphicsBuffer)]
public struct BoidData
{
    // position.w: 0 = prey, 1 = predator.
    public Vector4 position;
    public Vector4 velocity;
}
