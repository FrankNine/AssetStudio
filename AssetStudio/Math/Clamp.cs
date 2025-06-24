namespace AssetStudio;

using System;
using System.Runtime.CompilerServices;

public static class MathHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Clamp(float value, float minValue, float maxValue) 
        => Math.Clamp(value, minValue, maxValue);
}