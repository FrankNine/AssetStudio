namespace AssetStudio;

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;

public class Keyframe<T>
{
    public float m_Time;
    public T m_Value;
    public T m_InSlope;
    public T m_OutSlope;
    public int m_WeightedMode;
    public T m_InWeight;
    public T m_OutWeight;

    public Keyframe() { }

    public Keyframe(ObjectReader reader, Func<T> readerFunc)
    {
        m_Time = reader.ReadSingle();
        m_Value = readerFunc();
        m_InSlope = readerFunc();
        m_OutSlope = readerFunc();
        if (reader.version >= 2018) //2018 and up
        {
            m_WeightedMode = reader.ReadInt32();
            m_InWeight = readerFunc();
            m_OutWeight = readerFunc();
        }
    }
}

public class AnimationCurve<T>
{
    public Keyframe<T>[] m_Curve;
    public int m_PreInfinity;
    public int m_PostInfinity;
    public int m_RotationOrder;

    public AnimationCurve(ObjectReader reader, Func<T> readerFunc)
    {
        int numCurves = reader.ReadInt32();
        m_Curve = new Keyframe<T>[numCurves];
        for (int i = 0; i < numCurves; i++)
        {
            m_Curve[i] = new Keyframe<T>(reader, readerFunc);
        }

        m_PreInfinity = reader.ReadInt32();
        m_PostInfinity = reader.ReadInt32();
        if (reader.version >= (5, 3)) //5.3 and up
        {
            m_RotationOrder = reader.ReadInt32();
        }
    }
}

public class QuaternionCurve
{
    public AnimationCurve<Quaternion> m_Curve;
    public string m_Path;

    public QuaternionCurve(ObjectReader reader)
    {
        m_Curve = new AnimationCurve<Quaternion>(reader, reader.ReadQuaternion);
        m_Path = reader.ReadAlignedString();
    }
}

public class PackedFloatVector
{
    public uint m_NumItems;
    public float m_Range;
    public float m_Start;
    public byte[] m_Data;
    public byte m_BitSize;

    public PackedFloatVector(ObjectReader reader)
    {
        m_NumItems = reader.ReadUInt32();
        m_Range = reader.ReadSingle();
        m_Start = reader.ReadSingle();

        int numData = reader.ReadInt32();
        m_Data = reader.ReadBytes(numData);
        reader.AlignStream();

        m_BitSize = reader.ReadByte();
        reader.AlignStream();
    }

    public float[] UnpackFloats(int itemCountInChunk, int chunkStride, int start = 0, int numChunks = -1)
    {
        int bitPos = m_BitSize * start;
        int indexPos = bitPos / 8;
        bitPos %= 8;

        float scale = 1.0f / m_Range;
        if (numChunks == -1)
            numChunks = (int)m_NumItems / itemCountInChunk;
        var end = chunkStride * numChunks / 4;
        var data = new List<float>();
        for (var index = 0; index != end; index += chunkStride / 4)
        {
            for (int i = 0; i < itemCountInChunk; ++i)
            {
                uint x = 0;

                int bits = 0;
                while (bits < m_BitSize)
                {
                    x |= (uint)((m_Data[indexPos] >> bitPos) << bits);
                    int num = Math.Min(m_BitSize - bits, 8 - bitPos);
                    bitPos += num;
                    bits += num;
                    if (bitPos == 8)
                    {
                        indexPos++;
                        bitPos = 0;
                    }
                }
                x &= (uint)(1 << m_BitSize) - 1u;
                data.Add(x / (scale * ((1 << m_BitSize) - 1)) + m_Start);
            }
        }

        return data.ToArray();
    }
}

public class PackedIntVector
{
    public uint m_NumItems;
    public byte[] m_Data;
    public byte m_BitSize;

    public PackedIntVector(ObjectReader reader)
    {
        m_NumItems = reader.ReadUInt32();

        int numData = reader.ReadInt32();
        m_Data = reader.ReadBytes(numData);
        reader.AlignStream();

        m_BitSize = reader.ReadByte();
        reader.AlignStream();
    }

    public int[] UnpackInts()
    {
        var data = new int[m_NumItems];
        int indexPos = 0;
        int bitPos = 0;
        for (int i = 0; i < m_NumItems; i++)
        {
            int bits = 0;
            data[i] = 0;
            while (bits < m_BitSize)
            {
                data[i] |= (m_Data[indexPos] >> bitPos) << bits;
                int num = Math.Min(m_BitSize - bits, 8 - bitPos);
                bitPos += num;
                bits += num;
                if (bitPos == 8)
                {
                    indexPos++;
                    bitPos = 0;
                }
            }
            data[i] &= (1 << m_BitSize) - 1;
        }
        return data;
    }
}

public class PackedQuatVector
{
    public uint m_NumItems;
    public byte[] m_Data;

    public PackedQuatVector(ObjectReader reader)
    {
        m_NumItems = reader.ReadUInt32();

        int numData = reader.ReadInt32();
        m_Data = reader.ReadBytes(numData);

        reader.AlignStream();
    }

    public Quaternion[] UnpackQuats()
    {
        var data = new Quaternion[m_NumItems];
        int indexPos = 0;
        int bitPos = 0;

        for (int i = 0; i < m_NumItems; i++)
        {
            uint flags = 0;

            int bits = 0;
            while (bits < 3)
            {
                flags |= (uint)((m_Data[indexPos] >> bitPos) << bits);
                int num = Math.Min(3 - bits, 8 - bitPos);
                bitPos += num;
                bits += num;
                if (bitPos == 8)
                {
                    indexPos++;
                    bitPos = 0;
                }
            }
            flags &= 7;


            var q = new Quaternion();
            float sum = 0;
            for (int j = 0; j < 4; j++)
            {
                if ((flags & 3) != j)
                {
                    int bitSize = ((flags & 3) + 1) % 4 == j ? 9 : 10;
                    uint x = 0;

                    bits = 0;
                    while (bits < bitSize)
                    {
                        x |= (uint)((m_Data[indexPos] >> bitPos) << bits);
                        int num = Math.Min(bitSize - bits, 8 - bitPos);
                        bitPos += num;
                        bits += num;
                        if (bitPos == 8)
                        {
                            indexPos++;
                            bitPos = 0;
                        }
                    }
                    x &= (uint)((1 << bitSize) - 1);
                    q[j] = x / (0.5f * ((1 << bitSize) - 1)) - 1;
                    sum += q[j] * q[j];
                }
            }

            int lastComponent = (int)(flags & 3);
            q[lastComponent] = MathF.Sqrt(1 - sum);
            if ((flags & 4) != 0u)
                q[lastComponent] = -q[lastComponent];
            data[i] = q;
        }

        return data;
    }
}

public class CompressedAnimationCurve
{
    public string m_Path;
    public PackedIntVector m_Times;
    public PackedQuatVector m_Values;
    public PackedFloatVector m_Slopes;
    public int m_PreInfinity;
    public int m_PostInfinity;

    public CompressedAnimationCurve(ObjectReader reader)
    {
        m_Path = reader.ReadAlignedString();
        m_Times = new PackedIntVector(reader);
        m_Values = new PackedQuatVector(reader);
        m_Slopes = new PackedFloatVector(reader);
        m_PreInfinity = reader.ReadInt32();
        m_PostInfinity = reader.ReadInt32();
    }
}

public class Vector3Curve
{
    public AnimationCurve<Vector3> m_Curve;
    public string m_Path;

    public Vector3Curve(ObjectReader reader)
    {
        m_Curve = new AnimationCurve<Vector3>(reader, reader.ReadVector3);
        m_Path = reader.ReadAlignedString();
    }
}

public class FloatCurve
{
    public AnimationCurve<float> m_Curve;
    public string m_Attribute;
    public string m_Path;
    public ClassIDType m_ClassID;
    public PPtr<MonoScript> m_Script;
    public int m_Flags;

    public FloatCurve(ObjectReader reader)
    {
        m_Curve = new AnimationCurve<float>(reader, reader.ReadSingle);
        m_Attribute = reader.ReadAlignedString();
        m_Path = reader.ReadAlignedString();
        m_ClassID = (ClassIDType)reader.ReadInt32();
        m_Script = new PPtr<MonoScript>(reader);
        if (reader.version >= (2022, 2)) //2022.2 and up
        {
            m_Flags = reader.ReadInt32();
        }
    }
}

public class PPtrKeyframe
{
    public float m_Time;
    public PPtr<Object> m_Value;

    public PPtrKeyframe(ObjectReader reader)
    {
        m_Time = reader.ReadSingle();
        m_Value = new PPtr<Object>(reader);
    }
}

public class PPtrCurve
{
    public PPtrKeyframe[] m_Curve;
    public string m_Attribute;
    public string m_Path;
    public int m_ClassID;
    public PPtr<MonoScript> m_Script;
    public int m_Flags;

    public PPtrCurve(ObjectReader reader)
    {
        int numCurves = reader.ReadInt32();
        m_Curve = new PPtrKeyframe[numCurves];
        for (int i = 0; i < numCurves; i++)
        {
            m_Curve[i] = new PPtrKeyframe(reader);
        }

        m_Attribute = reader.ReadAlignedString();
        m_Path = reader.ReadAlignedString();
        m_ClassID = reader.ReadInt32();
        m_Script = new PPtr<MonoScript>(reader);
        if (reader.version >= (2022, 2)) //2022.2 and up
        {
            m_Flags = reader.ReadInt32();
        }
    }
}

public class AABB
{
    public Vector3 m_Center;
    public Vector3 m_Extent;

    public AABB(ObjectReader reader)
    {
        m_Center = reader.ReadVector3();
        m_Extent = reader.ReadVector3();
    }
}

public class xForm
{
    public Vector3 m_T;
    public Quaternion m_Q;
    public Vector3 m_S;

    public xForm(ObjectReader reader)
    {
        var version = reader.version;
        m_T = version >= (5, 4) ? reader.ReadVector3() : (Vector3)reader.ReadVector4();//5.4 and up
        m_Q = reader.ReadQuaternion();
        m_S = version >= (5, 4) ? reader.ReadVector3() : (Vector3)reader.ReadVector4();//5.4 and up
    }
}

public class HandPose
{
    public xForm m_GrabX;
    public float[] m_DoFArray;
    public float m_Override;
    public float m_CloseOpen;
    public float m_InOut;
    public float m_Grab;

    public HandPose() { }

    public HandPose(ObjectReader reader)
    {
        m_GrabX = new xForm(reader);
        m_DoFArray = reader.ReadSingleArray();
        m_Override = reader.ReadSingle();
        m_CloseOpen = reader.ReadSingle();
        m_InOut = reader.ReadSingle();
        m_Grab = reader.ReadSingle();
    }
}

public class HumanGoal
{
    public xForm m_X;
    public float m_WeightT;
    public float m_WeightR;
    public Vector3 m_HintT;
    public float m_HintWeightT;

    public HumanGoal(ObjectReader reader)
    {
        var version = reader.version;
        m_X = new xForm(reader);
        m_WeightT = reader.ReadSingle();
        m_WeightR = reader.ReadSingle();
        if (version >= 5)//5.0 and up
        {
            m_HintT = version >= (5, 4) ? reader.ReadVector3() : (Vector3)reader.ReadVector4();//5.4 and up
            m_HintWeightT = reader.ReadSingle();
        }
    }
}

public class HumanPose
{
    public xForm m_RootX;
    public Vector3 m_LookAtPosition;
    public Vector4 m_LookAtWeight;
    public HumanGoal[] m_GoalArray;
    public HandPose m_LeftHandPose;
    public HandPose m_RightHandPose;
    public float[] m_DoFArray;
    public Vector3[] m_TDoFArray;

    public HumanPose(ObjectReader reader)
    {
        var version = reader.version;
        m_RootX = new xForm(reader);
        m_LookAtPosition = version >= (5, 4) ? reader.ReadVector3() : (Vector3)reader.ReadVector4();//5.4 and up
        m_LookAtWeight = reader.ReadVector4();

        int numGoals = reader.ReadInt32();
        m_GoalArray = new HumanGoal[numGoals];
        for (int i = 0; i < numGoals; i++)
        {
            m_GoalArray[i] = new HumanGoal(reader);
        }

        m_LeftHandPose = new HandPose(reader);
        m_RightHandPose = new HandPose(reader);

        m_DoFArray = reader.ReadSingleArray();

        if (version >= (5, 2))//5.2 and up
        {
            int numTDof = reader.ReadInt32();
            m_TDoFArray = new Vector3[numTDof];
            for (int i = 0; i < numTDof; i++)
            {
                m_TDoFArray[i] = version >= (5, 4) ? reader.ReadVector3() : (Vector3)reader.ReadVector4();//5.4 and up
            }
        }
    }
}

public class StreamedClip
{
    public uint[] m_Data;
    public uint m_CurveCount;

    public StreamedClip() { }

    public StreamedClip(ObjectReader reader)
    {
        var version = reader.version;
        m_Data = reader.ReadUInt32Array();
        if (version.IsInRange((2022, 3, 19), 2023) //2022.3.19f1 to 2023
            || version >= (2023, 2, 8)) //2023.2.8f1 and up
        {
            m_CurveCount = reader.ReadUInt16();
            var m_DiscreteCurveCount = reader.ReadUInt16();
        }
        else
        {
            m_CurveCount = reader.ReadUInt32();
        }
    }

    public class StreamedCurveKey
    {
        public int m_Index;
        public float[] m_Coeff;

        public float m_Value;
        public float m_OutSlope;
        public float m_InSlope;

        public StreamedCurveKey(BinaryReader reader)
        {
            m_Index = reader.ReadInt32();
            m_Coeff = reader.ReadSingleArray(4);

            m_OutSlope = m_Coeff[2];
            m_Value = m_Coeff[3];
        }

        public float CalculateNextInSlope(float dx, StreamedCurveKey rhs)
        {
            //Stepped
            if (m_Coeff[0] == 0f && m_Coeff[1] == 0f && m_Coeff[2] == 0f)
            {
                return float.PositiveInfinity;
            }

            dx = Math.Max(dx, 0.0001f);
            var dy = rhs.m_Value - m_Value;
            var length = 1.0f / (dx * dx);
            var d1 = m_OutSlope * dx;
            var d2 = dy + dy + dy - d1 - d1 - m_Coeff[1] / length;
            return d2 / dx;
        }
    }

    public class StreamedFrame
    {
        public float m_Time;
        public StreamedCurveKey[] m_KeyList;

        public StreamedFrame(BinaryReader reader)
        {
            m_Time = reader.ReadSingle();

            int numKeys = reader.ReadInt32();
            m_KeyList = new StreamedCurveKey[numKeys];
            for (int i = 0; i < numKeys; i++)
            {
                m_KeyList[i] = new StreamedCurveKey(reader);
            }
        }
    }

    public List<StreamedFrame> ReadData()
    {
        var m_FrameList = new List<StreamedFrame>();
        var m_Buffer = new byte[m_Data.Length * 4];
        Buffer.BlockCopy(m_Data, 0, m_Buffer, 0, m_Buffer.Length);
        using (var reader = new BinaryReader(new MemoryStream(m_Buffer)))
        {
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                m_FrameList.Add(new StreamedFrame(reader));
            }
        }

        for (int frameIndex = 2; frameIndex < m_FrameList.Count - 1; frameIndex++)
        {
            var frame = m_FrameList[frameIndex];
            foreach (var curveKey in frame.m_KeyList)
            {
                for (int i = frameIndex - 1; i >= 0; i--)
                {
                    var preFrame = m_FrameList[i];
                    var preCurveKey = preFrame.m_KeyList.FirstOrDefault(x => x.m_Index == curveKey.m_Index);
                    if (preCurveKey != null)
                    {
                        curveKey.m_InSlope = preCurveKey.CalculateNextInSlope(frame.m_Time - preFrame.m_Time, curveKey);
                        break;
                    }
                }
            }
        }
        return m_FrameList;
    }
}

public class DenseClip
{
    public int m_FrameCount;
    public uint m_CurveCount;
    public float m_SampleRate;
    public float m_BeginTime;
    public float[] m_SampleArray;

    public DenseClip(ObjectReader reader)
    {
        m_FrameCount = reader.ReadInt32();
        m_CurveCount = reader.ReadUInt32();
        m_SampleRate = reader.ReadSingle();
        m_BeginTime = reader.ReadSingle();
        m_SampleArray = reader.ReadSingleArray();
    }
}

public class ConstantClip
{
    public float[] m_Data;

    public ConstantClip(ObjectReader reader) 
        => m_Data = reader.ReadSingleArray();
}

public class ValueConstant
{
    public uint m_ID;
    public uint m_TypeID;
    public uint m_Type;
    public uint m_Index;

    public ValueConstant(ObjectReader reader)
    {
        m_ID = reader.ReadUInt32();
        if (reader.version < (5, 5)) //5.5 down
        {
            m_TypeID = reader.ReadUInt32();
        }
        m_Type = reader.ReadUInt32();
        m_Index = reader.ReadUInt32();
    }
}

public class ValueArrayConstant
{
    public ValueConstant[] m_ValueArray;

    public ValueArrayConstant(ObjectReader reader)
    {
        int numVals = reader.ReadInt32();
        m_ValueArray = new ValueConstant[numVals];
        for (int i = 0; i < numVals; i++)
        {
            m_ValueArray[i] = new ValueConstant(reader);
        }
    }
}

public class OffsetPtr
{
    public Clip m_Data;

    public OffsetPtr(ObjectReader reader) 
        => m_Data = new Clip(reader);
}

public class Clip
{
    public StreamedClip m_StreamedClip;
    public DenseClip m_DenseClip;
    public ConstantClip m_ConstantClip;
    public ValueArrayConstant m_Binding;

    public Clip(ObjectReader reader)
    {
        var version = reader.version;
        m_StreamedClip = new StreamedClip(reader);
        m_DenseClip = new DenseClip(reader);
        if (version >= (4, 3)) //4.3 and up
        {
            m_ConstantClip = new ConstantClip(reader);
        }
        if (version < (2018, 3)) //2018.3 down
        {
            m_Binding = new ValueArrayConstant(reader);
        }
    }

    public AnimationClipBindingConstant ConvertValueArrayToGenericBinding()
    {
        var bindings = new AnimationClipBindingConstant();
        var genericBindings = new List<GenericBinding>();
        var values = m_Binding;
        for (int i = 0; i < values.m_ValueArray.Length;)
        {
            var curveID = values.m_ValueArray[i].m_ID;
            var curveTypeID = values.m_ValueArray[i].m_TypeID;
            var binding = new GenericBinding();
            genericBindings.Add(binding);
            if (curveTypeID == 4174552735) //CRC(PositionX))
            {
                binding.m_Path = curveID;
                binding.m_Attribute = 1; //kBindTransformPosition
                binding.m_TypeID = ClassIDType.Transform;
                i += 3;
            }
            else if (curveTypeID == 2211994246) //CRC(QuaternionX))
            {
                binding.m_Path = curveID;
                binding.m_Attribute = 2; //kBindTransformRotation
                binding.m_TypeID = ClassIDType.Transform;
                i += 4;
            }
            else if (curveTypeID == 1512518241) //CRC(ScaleX))
            {
                binding.m_Path = curveID;
                binding.m_Attribute = 3; //kBindTransformScale
                binding.m_TypeID = ClassIDType.Transform;
                i += 3;
            }
            else
            {
                binding.m_TypeID = ClassIDType.Animator;
                binding.m_Path = 0;
                binding.m_Attribute = curveID;
                i++;
            }
        }
        bindings.m_GenericBindings = genericBindings.ToArray();
        return bindings;
    }
}

public class ValueDelta
{
    public float m_Start;
    public float m_Stop;

    public ValueDelta() { }

    public ValueDelta(ObjectReader reader)
    {
        m_Start = reader.ReadSingle();
        m_Stop = reader.ReadSingle();
    }
}

public class ClipMuscleConstant
{
    public HumanPose m_DeltaPose;
    public xForm m_StartX;
    public xForm m_StopX;
    public xForm m_LeftFootStartX;
    public xForm m_RightFootStartX;
    public xForm m_MotionStartX;
    public xForm m_MotionStopX;
    public Vector3 m_AverageSpeed;
    public OffsetPtr m_Clip;
    public float m_StartTime;
    public float m_StopTime;
    public float m_OrientationOffsetY;
    public float m_Level;
    public float m_CycleOffset;
    public float m_AverageAngularSpeed;
    public int[] m_IndexArray;
    public ValueDelta[] m_ValueArrayDelta;
    public float[] m_ValueArrayReferencePose;
    public bool m_Mirror;
    public bool m_LoopTime;
    public bool m_LoopBlend;
    public bool m_LoopBlendOrientation;
    public bool m_LoopBlendPositionY;
    public bool m_LoopBlendPositionXZ;
    public bool m_StartAtOrigin;
    public bool m_KeepOriginalOrientation;
    public bool m_KeepOriginalPositionY;
    public bool m_KeepOriginalPositionXZ;
    public bool m_HeightFromFeet;

    public ClipMuscleConstant() { }

    public ClipMuscleConstant(ObjectReader reader)
    {
        var version = reader.version;
        m_DeltaPose = new HumanPose(reader);
        m_StartX = new xForm(reader);
        if (version >= (5, 5)) //5.5 and up
        {
            m_StopX = new xForm(reader);
        }
        m_LeftFootStartX = new xForm(reader);
        m_RightFootStartX = new xForm(reader);
        if (version < 5)//5.0 down
        {
            m_MotionStartX = new xForm(reader);
            m_MotionStopX = new xForm(reader);
        }
        m_AverageSpeed = version >= (5, 4) ? reader.ReadVector3() : (Vector3)reader.ReadVector4();//5.4 and up
        m_Clip = new OffsetPtr(reader);
        m_StartTime = reader.ReadSingle();
        m_StopTime = reader.ReadSingle();
        m_OrientationOffsetY = reader.ReadSingle();
        m_Level = reader.ReadSingle();
        m_CycleOffset = reader.ReadSingle();
        m_AverageAngularSpeed = reader.ReadSingle();

        m_IndexArray = reader.ReadInt32Array();
        if (version < (4, 3)) //4.3 down
        {
            var m_AdditionalCurveIndexArray = reader.ReadInt32Array();
        }
        int numDeltas = reader.ReadInt32();
        m_ValueArrayDelta = new ValueDelta[numDeltas];
        for (int i = 0; i < numDeltas; i++)
        {
            m_ValueArrayDelta[i] = new ValueDelta(reader);
        }
        if (version >= (5, 3))//5.3 and up
        {
            m_ValueArrayReferencePose = reader.ReadSingleArray();
        }

        m_Mirror = reader.ReadBoolean();
        if (version >= (4, 3)) //4.3 and up
        {
            m_LoopTime = reader.ReadBoolean();
        }
        m_LoopBlend = reader.ReadBoolean();
        m_LoopBlendOrientation = reader.ReadBoolean();
        m_LoopBlendPositionY = reader.ReadBoolean();
        m_LoopBlendPositionXZ = reader.ReadBoolean();
        if (version >= (5, 5))//5.5 and up
        {
            m_StartAtOrigin = reader.ReadBoolean();
        }
        m_KeepOriginalOrientation = reader.ReadBoolean();
        m_KeepOriginalPositionY = reader.ReadBoolean();
        m_KeepOriginalPositionXZ = reader.ReadBoolean();
        m_HeightFromFeet = reader.ReadBoolean();
        reader.AlignStream();
    }
}

public class GenericBinding
{
    public uint m_Path;
    public uint m_Attribute;
    public PPtr<Object> m_Script;
    public ClassIDType m_TypeID;
    public byte m_CustomType;
    public byte m_IsPPtrCurve;
    public byte m_IsIntCurve;
    public byte m_IsSerializeReferenceCurve;

    public GenericBinding() { }

    public GenericBinding(ObjectReader reader)
    {
        var version = reader.version;
        m_Path = reader.ReadUInt32();
        m_Attribute = reader.ReadUInt32();
        m_Script = new PPtr<Object>(reader);
        if (version >= (5, 6)) //5.6 and up
        {
            m_TypeID = (ClassIDType)reader.ReadInt32();
        }
        else
        {
            m_TypeID = (ClassIDType)reader.ReadUInt16();
        }
        m_CustomType = reader.ReadByte();
        m_IsPPtrCurve = reader.ReadByte();
        if (version >= (2022, 1)) //2022.1 and up
        {
            m_IsIntCurve = reader.ReadByte();
        }
        if (version >= (2022, 2)) //2022.2 and up
        {
            m_IsSerializeReferenceCurve = reader.ReadByte();
        }
        reader.AlignStream();
    }
}

public class AnimationClipBindingConstant
{
    public GenericBinding[] m_GenericBindings;
    public PPtr<Object>[] m_PptrCurveMapping;

    public AnimationClipBindingConstant() { }

    public AnimationClipBindingConstant(ObjectReader reader)
    {
        int numBindings = reader.ReadInt32();
        m_GenericBindings = new GenericBinding[numBindings];
        for (int i = 0; i < numBindings; i++)
        {
            m_GenericBindings[i] = new GenericBinding(reader);
        }

        int numMappings = reader.ReadInt32();
        m_PptrCurveMapping = new PPtr<Object>[numMappings];
        for (int i = 0; i < numMappings; i++)
        {
            m_PptrCurveMapping[i] = new PPtr<Object>(reader);
        }
    }

    public GenericBinding FindBinding(int index)
    {
        int curves = 0;
        foreach (var b in m_GenericBindings)
        {
            if (b.m_TypeID == ClassIDType.Transform)
            {
                switch (b.m_Attribute)
                {
                    case 1: //kBindTransformPosition
                    case 3: //kBindTransformScale
                    case 4: //kBindTransformEuler
                        curves += 3;
                        break;
                    case 2: //kBindTransformRotation
                        curves += 4;
                        break;
                    default:
                        curves += 1;
                        break;
                }
            }
            else
            {
                curves += 1;
            }
            if (curves > index)
            {
                return b;
            }
        }

        return null;
    }
}

public class AnimationEvent
{
    public float m_Time;
    public string m_FunctionName;
    public string m_Data;
    public PPtr<Object> m_ObjectReferenceParameter;
    public float m_FloatParameter;
    public int m_IntParameter;
    public int m_MessageOptions;

    public AnimationEvent(ObjectReader reader)
    {
        var version = reader.version;
        m_Time = reader.ReadSingle();
        m_FunctionName = reader.ReadAlignedString();
        m_Data = reader.ReadAlignedString();
        if (version >= (2, 6)) //2.6 and up
        {
            m_ObjectReferenceParameter = new PPtr<Object>(reader);
            m_FloatParameter = reader.ReadSingle();
            if (version >= 3) //3 and up
            {
                m_IntParameter = reader.ReadInt32();
            }
        }
        m_MessageOptions = reader.ReadInt32();
    }
}

public enum AnimationType
{
    Legacy = 1,
    Generic = 2,
    Humanoid = 3
};

public sealed class AnimationClip : NamedObject
{
    public AnimationType m_AnimationType;
    public bool m_Legacy;
    public bool m_Compressed;
    public bool m_UseHighQualityCurve;
    public QuaternionCurve[] m_RotationCurves;
    public CompressedAnimationCurve[] m_CompressedRotationCurves;
    public Vector3Curve[] m_EulerCurves;
    public Vector3Curve[] m_PositionCurves;
    public Vector3Curve[] m_ScaleCurves;
    public FloatCurve[] m_FloatCurves;
    public PPtrCurve[] m_PPtrCurves;
    public float m_SampleRate;
    public int m_WrapMode;
    public AABB m_Bounds;
    public uint m_MuscleClipSize;
    public ClipMuscleConstant m_MuscleClip;
    public AnimationClipBindingConstant m_ClipBindingConstant;
    public AnimationEvent[] m_Events;
    public byte[] m_AnimData;
    public StreamingInfo m_StreamingInfo;

    public AnimationClip() { }

    public AnimationClip(ObjectReader reader, byte[] type, JsonSerializerOptions jsonOptions, ObjectInfo objInfo) : base(reader)
    {
        var parsedAnimClip = JsonSerializer.Deserialize<AnimationClip>(type, jsonOptions);
        m_AnimationType = parsedAnimClip.m_AnimationType;
        if (version >= 5)//5.0 and up
        {
            m_Legacy = parsedAnimClip.m_Legacy;
        }
        else if (version >= 4)//4.0 and up
        {
            m_Legacy = m_AnimationType == AnimationType.Legacy;
        }
        else
        {
            m_Legacy = true;
        }
        m_Compressed = parsedAnimClip.m_Compressed;
        m_UseHighQualityCurve = parsedAnimClip.m_UseHighQualityCurve;
        m_RotationCurves = parsedAnimClip.m_RotationCurves;
        m_CompressedRotationCurves = parsedAnimClip.m_CompressedRotationCurves;
        m_EulerCurves = parsedAnimClip.m_EulerCurves;
        m_PositionCurves = parsedAnimClip.m_PositionCurves;
        m_ScaleCurves = parsedAnimClip.m_ScaleCurves;
        m_FloatCurves = parsedAnimClip.m_FloatCurves;
        m_PPtrCurves = parsedAnimClip.m_PPtrCurves;
        m_SampleRate = parsedAnimClip.m_SampleRate;
        m_WrapMode = parsedAnimClip.m_WrapMode;
        m_Bounds = parsedAnimClip.m_Bounds;
        m_MuscleClipSize = parsedAnimClip.m_MuscleClipSize;
        m_MuscleClip = parsedAnimClip.m_MuscleClip;
        m_ClipBindingConstant = parsedAnimClip.m_ClipBindingConstant;
        m_Events = parsedAnimClip.m_Events;
        if (!reader.version.IsTuanjie) 
            return;
        m_AnimData = parsedAnimClip.m_AnimData;
        m_StreamingInfo = parsedAnimClip.m_StreamingInfo;
        if (!(m_AnimData?.Length > 0)) 
            return;
        m_MuscleClipSize = (uint)m_AnimData.Length;
        using (var muscleStream = new MemoryStream(m_AnimData))
        {
            using (var muscleReader = new EndianBinaryReader(muscleStream, EndianType.LittleEndian))
            {
                _ = muscleReader.ReadUInt32();
                var objReader = new ObjectReader(muscleReader, assetsFile, objInfo);
                m_MuscleClip = new ClipMuscleConstant(objReader);
            }
        }
    }

    public AnimationClip(ObjectReader reader) : base(reader)
    {
        if (version >= 5)//5.0 and up
        {
            m_Legacy = reader.ReadBoolean();
        }
        else if (version >= 4)//4.0 and up
        {
            m_AnimationType = (AnimationType)reader.ReadInt32();
            m_Legacy = m_AnimationType == AnimationType.Legacy;
        }
        else
        {
            m_Legacy = true;
        }
        if (version >= (2, 6)) //2.6 and up
        {
            m_Compressed = reader.ReadBoolean();
        }
        if (version >= (4, 3))//4.3 and up
        {
            m_UseHighQualityCurve = reader.ReadBoolean();
        }
        reader.AlignStream();
        int numRCurves = reader.ReadInt32();
        m_RotationCurves = new QuaternionCurve[numRCurves];
        for (int i = 0; i < numRCurves; i++)
        {
            m_RotationCurves[i] = new QuaternionCurve(reader);
        }

        if (version >= (2, 6)) //2.6 and up
        {
            int numCRCurves = reader.ReadInt32();
            m_CompressedRotationCurves = new CompressedAnimationCurve[numCRCurves];
            for (int i = 0; i < numCRCurves; i++)
            {
                m_CompressedRotationCurves[i] = new CompressedAnimationCurve(reader);
            }
        }

        if (!version.IsTuanjie)
        {
            if (version >= (5, 3)) //5.3 and up
            {
                int numEulerCurves = reader.ReadInt32();
                m_EulerCurves = new Vector3Curve[numEulerCurves];
                for (int i = 0; i < numEulerCurves; i++)
                {
                    m_EulerCurves[i] = new Vector3Curve(reader);
                }
            }

            int numPCurves = reader.ReadInt32();
            m_PositionCurves = new Vector3Curve[numPCurves];
            for (int i = 0; i < numPCurves; i++)
            {
                m_PositionCurves[i] = new Vector3Curve(reader);
            }

            int numSCurves = reader.ReadInt32();
            m_ScaleCurves = new Vector3Curve[numSCurves];
            for (int i = 0; i < numSCurves; i++)
            {
                m_ScaleCurves[i] = new Vector3Curve(reader);
            }
        }

        int numFCurves = reader.ReadInt32();
        m_FloatCurves = new FloatCurve[numFCurves];
        for (int i = 0; i < numFCurves; i++)
        {
            m_FloatCurves[i] = new FloatCurve(reader);
        }

        if (version >= (4, 3)) //4.3 and up
        {
            int numPtrCurves = reader.ReadInt32();
            m_PPtrCurves = new PPtrCurve[numPtrCurves];
            for (int i = 0; i < numPtrCurves; i++)
            {
                m_PPtrCurves[i] = new PPtrCurve(reader);
            }
        }

        m_SampleRate = reader.ReadSingle();
        if (version >= (2, 6)) //2.6 and up
        {
            m_WrapMode = reader.ReadInt32();
        }
        if (version >= (3, 4)) //3.4 and up
        {
            m_Bounds = new AABB(reader);
        }
        if (version >= 4)//4.0 and up
        {
            m_MuscleClipSize = reader.ReadUInt32();
            if (!version.IsTuanjie)
            {
                m_MuscleClip = new ClipMuscleConstant(reader);
            }
            else if (m_MuscleClipSize > 0)
            {
                _ = reader.ReadInt32();
                m_MuscleClip = new ClipMuscleConstant(reader); //m_AnimData (Tuanjie)
                m_StreamingInfo = new StreamingInfo(reader);
            }
        }
        if (version >= (4, 3)) //4.3 and up
        {
            m_ClipBindingConstant = new AnimationClipBindingConstant(reader);
        }
        if (version >= (2018, 3)) //2018.3 and up
        {
            var m_HasGenericRootTransform = reader.ReadBoolean();
            var m_HasMotionFloatCurves = reader.ReadBoolean();
            reader.AlignStream();
        }
        int numEvents = reader.ReadInt32();
        m_Events = new AnimationEvent[numEvents];
        for (int i = 0; i < numEvents; i++)
        {
            m_Events[i] = new AnimationEvent(reader);
        }
        if (version >= 2017) //2017 and up
        {
            reader.AlignStream();
        }
    }

    public class EqComparer : IEqualityComparer<AnimationClip>
    {
        public bool Equals(AnimationClip clipA, AnimationClip clipB)
            => clipA?.m_PathID == clipB?.m_PathID && 
               clipA?.byteSize == clipB?.byteSize;

        public int GetHashCode(AnimationClip obj)
        {
            var result = obj.m_PathID * 31;
            result = result * 31 + obj.byteSize;
            return result.GetHashCode();
        }
    }
}