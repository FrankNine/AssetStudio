namespace AssetStudio;

using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

public class MinMaxAABB
{
    public Vector3 m_Min;
    public Vector3 m_Max;

    public MinMaxAABB(BinaryReader reader)
    {
        m_Min = reader.ReadVector3();
        m_Max = reader.ReadVector3();
    }
}

public class CompressedMesh
{
    public PackedFloatVector m_Vertices;
    public PackedFloatVector m_UV;
    public PackedFloatVector m_BindPoses;
    public PackedFloatVector m_Normals;
    public PackedFloatVector m_Tangents;
    public PackedIntVector m_Weights;
    public PackedIntVector m_NormalSigns;
    public PackedIntVector m_TangentSigns;
    public PackedFloatVector m_FloatColors;
    public PackedIntVector m_BoneIndices;
    public PackedIntVector m_Triangles;
    public PackedIntVector m_Colors;
    public uint m_UVInfo;

    public CompressedMesh(ObjectReader reader)
    {
        var version = reader.version;

        m_Vertices = new PackedFloatVector(reader);
        m_UV = new PackedFloatVector(reader);
        if (version < 5)
        {
            m_BindPoses = new PackedFloatVector(reader);
        }
        m_Normals = new PackedFloatVector(reader);
        m_Tangents = new PackedFloatVector(reader);
        m_Weights = new PackedIntVector(reader);
        m_NormalSigns = new PackedIntVector(reader);
        m_TangentSigns = new PackedIntVector(reader);
        if (version >= 5)
        {
            m_FloatColors = new PackedFloatVector(reader);
        }
        m_BoneIndices = new PackedIntVector(reader);
        m_Triangles = new PackedIntVector(reader);
        if (version >= (3, 5)) //3.5 and up
        {
            if (version < 5)
            {
                m_Colors = new PackedIntVector(reader);
            }
            else
            {
                m_UVInfo = reader.ReadUInt32();
            }
        }
    }
}

public class StreamInfo
{
    public uint m_ChannelMask;
    public uint m_Offset;
    public uint m_Stride;
    public uint m_Align;
    public byte m_DividerOp;
    public ushort m_Frequency;

    public StreamInfo() { }

    public StreamInfo(ObjectReader reader)
    {
        var version = reader.version;

        m_ChannelMask = reader.ReadUInt32();
        m_Offset = reader.ReadUInt32();

        if (version < 4) //4.0 down
        {
            m_Stride = reader.ReadUInt32();
            m_Align = reader.ReadUInt32();
        }
        else
        {
            m_Stride = reader.ReadByte();
            m_DividerOp = reader.ReadByte();
            m_Frequency = reader.ReadUInt16();
        }
    }
}

public class ChannelInfo
{
    public byte m_Stream;
    public byte m_Offset;
    public byte m_Format;
    public byte m_Dimension;

    public ChannelInfo() { }

    public ChannelInfo(ObjectReader reader)
    {
        m_Stream = reader.ReadByte();
        m_Offset = reader.ReadByte();
        m_Format = reader.ReadByte();
        m_Dimension = (byte)(reader.ReadByte() & 0xF);
    }
}

public class VertexData
{
    public uint m_CurrentChannels;
    public uint m_VertexCount;
    public ChannelInfo[] m_Channels;
    public StreamInfo[] m_Streams;
    public byte[] m_DataSize;

    public VertexData(ObjectReader reader)
    {
        var version = reader.version;

        if (version < 2018)//2018 down
        {
            m_CurrentChannels = reader.ReadUInt32();
        }

        m_VertexCount = reader.ReadUInt32();

        if (version >= 4) //4.0 and up
        {
            var m_ChannelsSize = reader.ReadInt32();
            m_Channels = new ChannelInfo[m_ChannelsSize];
            for (int i = 0; i < m_ChannelsSize; i++)
            {
                m_Channels[i] = new ChannelInfo(reader);
            }
        }

        if (version < 5) //5.0 down
        {
            if (version < 4)
            {
                m_Streams = new StreamInfo[4];
            }
            else
            {
                m_Streams = new StreamInfo[reader.ReadInt32()];
            }

            for (int i = 0; i < m_Streams.Length; i++)
            {
                m_Streams[i] = new StreamInfo(reader);
            }

            if (version < 4) //4.0 down
            {
                GetChannels(version);
            }
        }
        else //5.0 and up
        {
            GetStreams(version);
        }

        m_DataSize = reader.ReadUInt8Array();
        reader.AlignStream();
    }

    private void GetStreams(UnityVersion version)
    {
        var streamCount = m_Channels.Max(x => x.m_Stream) + 1;
        m_Streams = new StreamInfo[streamCount];
        uint offset = 0;
        for (int s = 0; s < streamCount; s++)
        {
            uint chnMask = 0;
            uint stride = 0;
            for (int chn = 0; chn < m_Channels.Length; chn++)
            {
                var m_Channel = m_Channels[chn];
                if (m_Channel.m_Stream == s)
                {
                    if (m_Channel.m_Dimension > 0)
                    {
                        chnMask |= 1u << chn;
                        stride += m_Channel.m_Dimension * MeshHelper.GetFormatSize(MeshHelper.ToVertexFormat(m_Channel.m_Format, version));
                    }
                }
            }
            m_Streams[s] = new StreamInfo
            {
                m_ChannelMask = chnMask,
                m_Offset = offset,
                m_Stride = stride,
                m_DividerOp = 0,
                m_Frequency = 0
            };
            offset += m_VertexCount * stride;
            //static size_t AlignStreamSize (size_t size) { return (size + (kVertexStreamAlign-1)) & ~(kVertexStreamAlign-1); }
            offset = (offset + (16u - 1u)) & ~(16u - 1u);
        }
    }

    private void GetChannels(UnityVersion version)
    {
        m_Channels = new ChannelInfo[6];
        for (int i = 0; i < 6; i++)
        {
            m_Channels[i] = new ChannelInfo();
        }
        for (var s = 0; s < m_Streams.Length; s++)
        {
            var m_Stream = m_Streams[s];
            var channelMask = new BitArray(new[] { (int)m_Stream.m_ChannelMask });
            byte offset = 0;
            for (int i = 0; i < 6; i++)
            {
                if (channelMask.Get(i))
                {
                    var m_Channel = m_Channels[i];
                    m_Channel.m_Stream = (byte)s;
                    m_Channel.m_Offset = offset;
                    switch (i)
                    {
                        case 0: //kShaderChannelVertex
                        case 1: //kShaderChannelNormal
                            m_Channel.m_Format = 0; //kChannelFormatFloat
                            m_Channel.m_Dimension = 3;
                            break;
                        case 2: //kShaderChannelColor
                            m_Channel.m_Format = 2; //kChannelFormatColor
                            m_Channel.m_Dimension = 4;
                            break;
                        case 3: //kShaderChannelTexCoord0
                        case 4: //kShaderChannelTexCoord1
                            m_Channel.m_Format = 0; //kChannelFormatFloat
                            m_Channel.m_Dimension = 2;
                            break;
                        case 5: //kShaderChannelTangent
                            m_Channel.m_Format = 0; //kChannelFormatFloat
                            m_Channel.m_Dimension = 4;
                            break;
                    }
                    offset += (byte)(m_Channel.m_Dimension * MeshHelper.GetFormatSize(MeshHelper.ToVertexFormat(m_Channel.m_Format, version)));
                }
            }
        }
    }
}

public class BoneWeights4
{
    public float[] m_Weight;
    public int[] m_BoneIndex;

    public BoneWeights4()
    {
        m_Weight = new float[4];
        m_BoneIndex = new int[4];
    }

    public BoneWeights4(ObjectReader reader)
    {
        m_Weight = reader.ReadSingleArray(4);
        m_BoneIndex = reader.ReadInt32Array(4);
    }
}

public class BlendShapeVertex
{
    public Vector3 m_Vertex;
    public Vector3 m_Normal;
    public Vector3 m_Ttangent;
    public uint m_Index;

    public BlendShapeVertex(ObjectReader reader)
    {
        m_Vertex = reader.ReadVector3();
        m_Normal = reader.ReadVector3();
        m_Ttangent = reader.ReadVector3();
        m_Index = reader.ReadUInt32();
    }
}

public class MeshBlendShape
{
    public uint m_FirstVertex;
    public uint m_VertexCount;
    public bool m_HasNormals;
    public bool m_HasTangents;

    public MeshBlendShape(ObjectReader reader)
    {
        var version = reader.version;

        if (version < (4, 3)) //4.3 down
        {
            var m_Name = reader.ReadAlignedString();
        }
        m_FirstVertex = reader.ReadUInt32();
        m_VertexCount = reader.ReadUInt32();
        if (version < (4, 3)) //4.3 down
        {
            var m_AabbMinDelta = reader.ReadVector3();
            var m_AabbMaxDelta = reader.ReadVector3();
        }
        m_HasNormals = reader.ReadBoolean();
        m_HasTangents = reader.ReadBoolean();
        if (version >= (4, 3)) //4.3 and up
        {
            reader.AlignStream();
        }
    }
}

public class MeshBlendShapeChannel
{
    public string m_Name;
    public uint m_NameHash;
    public int m_FrameIndex;
    public int m_FrameCount;

    public MeshBlendShapeChannel(ObjectReader reader)
    {
        m_Name = reader.ReadAlignedString();
        m_NameHash = reader.ReadUInt32();
        m_FrameIndex = reader.ReadInt32();
        m_FrameCount = reader.ReadInt32();
    }
}

public class BlendShapeData
{
    public BlendShapeVertex[] m_Vertices;
    public MeshBlendShape[] m_Shapes;
    public MeshBlendShapeChannel[] m_Channels;
    public float[] m_FullWeights;

    public BlendShapeData(ObjectReader reader)
    {
        var version = reader.version;

        if (version >= (4, 3)) //4.3 and up
        {
            int numVerts = reader.ReadInt32();
            m_Vertices = new BlendShapeVertex[numVerts];
            for (int i = 0; i < numVerts; i++)
            {
                m_Vertices[i] = new BlendShapeVertex(reader);
            }

            int numShapes = reader.ReadInt32();
            m_Shapes = new MeshBlendShape[numShapes];
            for (int i = 0; i < numShapes; i++)
            {
                m_Shapes[i] = new MeshBlendShape(reader);
            }

            int numChannels = reader.ReadInt32();
            m_Channels = new MeshBlendShapeChannel[numChannels];
            for (int i = 0; i < numChannels; i++)
            {
                m_Channels[i] = new MeshBlendShapeChannel(reader);
            }

            m_FullWeights = reader.ReadSingleArray();
        }
        else
        {
            var m_ShapesSize = reader.ReadInt32();
            var m_Shapes = new MeshBlendShape[m_ShapesSize];
            for (int i = 0; i < m_ShapesSize; i++)
            {
                m_Shapes[i] = new MeshBlendShape(reader);
            }
            reader.AlignStream();
            var m_ShapeVerticesSize = reader.ReadInt32();
            var m_ShapeVertices = new BlendShapeVertex[m_ShapeVerticesSize]; //MeshBlendShapeVertex
            for (int i = 0; i < m_ShapeVerticesSize; i++)
            {
                m_ShapeVertices[i] = new BlendShapeVertex(reader);
            }
        }
    }
}

public enum GfxPrimitiveType
{
    Triangles = 0,
    TriangleStrip = 1,
    Quads = 2,
    Lines = 3,
    LineStrip = 4,
    Points = 5
};

public class SubMesh
{
    public uint m_FirstByte;
    public uint m_IndexCount;
    public GfxPrimitiveType m_Topology;
    public uint m_TriangleCount;
    public uint m_BaseVertex;
    public uint m_FirstVertex;
    public uint m_VertexCount;
    public AABB m_LocalAABB;

    public SubMesh(ObjectReader reader)
    {
        var m_Version = reader.version;

        m_FirstByte = reader.ReadUInt32();
        m_IndexCount = reader.ReadUInt32();
        m_Topology = (GfxPrimitiveType)reader.ReadInt32();

        if (m_Version < 4) //4.0 down
        {
            m_TriangleCount = reader.ReadUInt32();
        }

        if (m_Version >= (2017, 3)) //2017.3 and up
        {
            m_BaseVertex = reader.ReadUInt32();
        }

        if (m_Version >= 3) //3.0 and up
        {
            m_FirstVertex = reader.ReadUInt32();
            m_VertexCount = reader.ReadUInt32();
            m_LocalAABB = new AABB(reader);
        }
    }
}

public class VGPackedHierarchyNode
{
    public Vector4[] m_LODBounds = new Vector4[8];
    public Vector3[] m_BoxBoundsCenter = new Vector3[8];
    public uint[] m_MinLODError_MaxParentLODError = new uint[8];
    public Vector3[] m_BoxBoundsExtent = new Vector3[8];
    public uint[] m_ChildStartReference = new uint[8];
    public uint[] m_ResourcePageIndex_NumPages_GroupPartSize = new uint[8];

    public VGPackedHierarchyNode(BinaryReader reader)
    {
        for (var i = 0; i < 8; i++)
        {
            m_LODBounds[i] = reader.ReadVector4();
            m_BoxBoundsCenter[i] = reader.ReadVector3();
            m_MinLODError_MaxParentLODError[i] = reader.ReadUInt32();
            m_BoxBoundsExtent[i] = reader.ReadVector3();
            m_ChildStartReference[i] = reader.ReadUInt32();
            m_ResourcePageIndex_NumPages_GroupPartSize[i] = reader.ReadUInt32();
        }
    }
}

public class VGPageStreamingState
{
    public uint m_BulkOffset;
    public uint m_BulkSize;
    public uint m_PageSize;
    public uint m_DependenciesStart;
    public uint m_DependenciesNum;
    public uint m_Flags;

    public VGPageStreamingState(BinaryReader reader)
    {
        m_BulkOffset = reader.ReadUInt32();
        m_BulkSize = reader.ReadUInt32();
        m_PageSize = reader.ReadUInt32();
        m_DependenciesStart = reader.ReadUInt32();
        m_DependenciesNum = reader.ReadUInt32();
        m_Flags = reader.ReadUInt32();
    }
}

public sealed class Mesh : NamedObject
{
    private bool m_Use16BitIndices = true;
    public SubMesh[] m_SubMeshes;
    private uint[] m_IndexBuffer;
    public BlendShapeData m_Shapes;
    public Matrix4x4[] m_BindPose;
    public uint[] m_BoneNameHashes;
    public int m_VertexCount;
    public float[] m_Vertices;
    public BoneWeights4[] m_Skin;
    public float[] m_Normals;
    public float[] m_Colors;
    public float[] m_UV0;
    public float[] m_UV1;
    public float[] m_UV2;
    public float[] m_UV3;
    public float[] m_UV4;
    public float[] m_UV5;
    public float[] m_UV6;
    public float[] m_UV7;
    public float[] m_Tangents;
    private VertexData m_VertexData;
    private CompressedMesh m_CompressedMesh;
    private StreamingInfo m_StreamData;

    public List<uint> m_Indices = new List<uint>();

    public Mesh(ObjectReader reader) : base(reader)
    {
        if (version < (3, 5)) //3.5 down
        {
            m_Use16BitIndices = reader.ReadInt32() > 0;
        }

        if (version <= (2, 5)) //2.5 and down
        {
            int m_IndexBuffer_size = reader.ReadInt32();

            if (m_Use16BitIndices)
            {
                m_IndexBuffer = new uint[m_IndexBuffer_size / 2];
                for (int i = 0; i < m_IndexBuffer_size / 2; i++)
                {
                    m_IndexBuffer[i] = reader.ReadUInt16();
                }
                reader.AlignStream();
            }
            else
            {
                m_IndexBuffer = reader.ReadUInt32Array(m_IndexBuffer_size / 4);
            }
        }

        int m_SubMeshesSize = reader.ReadInt32();
        m_SubMeshes = new SubMesh[m_SubMeshesSize];
        for (int i = 0; i < m_SubMeshesSize; i++)
        {
            m_SubMeshes[i] = new SubMesh(reader);
        }

        if (version >= (4, 1)) //4.1 and up
        {
            m_Shapes = new BlendShapeData(reader);
        }

        if (version >= (4, 3)) //4.3 and up
        {
            m_BindPose = reader.ReadMatrixArray();
            m_BoneNameHashes = reader.ReadUInt32Array();
            var m_RootBoneNameHash = reader.ReadUInt32();
        }

        if (version >= (2, 6)) //2.6.0 and up
        {
            if (version >= 2019) //2019 and up
            {
                var m_BonesAABBSize = reader.ReadInt32();
                var m_BonesAABB = new MinMaxAABB[m_BonesAABBSize];
                for (int i = 0; i < m_BonesAABBSize; i++)
                {
                    m_BonesAABB[i] = new MinMaxAABB(reader);
                }

                var m_VariableBoneCountWeights = reader.ReadUInt32Array();
            }

            var m_MeshCompression = reader.ReadByte();
            if (version >= 4)
            {
                if (version < 5)
                {
                    var m_StreamCompression = reader.ReadByte();
                }
                var m_IsReadable = reader.ReadBoolean();
                var m_KeepVertices = reader.ReadBoolean();
                var m_KeepIndices = reader.ReadBoolean();
            }
            if (version.IsTuanjie)
            {
                var m_LightmapUseUV1 = reader.ReadInt32();
                var m_fileScale = reader.ReadSingle();
                var NumInputTriangles = reader.ReadUInt32();
                var NumInputVertices = reader.ReadUInt32();
                var NumInputMeshes = reader.ReadUInt16();
                var NumInputTexCoords = reader.ReadUInt16();
                var ResourceFlags = reader.ReadUInt32();
                var RootClusterPageSize = reader.ReadInt32();
                reader.Position += RootClusterPageSize; //skip byte[] RootClusterPage
                var ImposterAtlasSize = reader.ReadInt32();
                reader.Position += ImposterAtlasSize * 2; //skip ushort[] ImposterAtlas
                var HierarchyNodesSize = reader.ReadInt32();
                for (var i = 0; i < HierarchyNodesSize; i++)
                {
                    _ = new VGPackedHierarchyNode(reader);
                }
                var HierarchyRootOffsetsSize = reader.ReadInt32();
                reader.Position += HierarchyRootOffsetsSize * 4; //skip uint[] HierarchyRootOffsets
                var PageStreamingStatesSize = reader.ReadInt32();
                for (var i = 0; i < PageStreamingStatesSize; i++)
                {
                    _ = new VGPageStreamingState(reader);
                }
                var PageDependenciesSize = reader.ReadInt32();
                reader.Position += PageDependenciesSize * 4; //skip uint[] PageDependencies
                var streamableClusterPageSize = reader.ReadInt32();
                reader.Position += streamableClusterPageSize; //skip byte[] streamableClusterPageSize
            }
            reader.AlignStream();

            //Unity fixed it in 2017.3.1p1 and later versions
            if (version >= (2017, 4) //2017.4
                || version == (2017, 3, 1) && version.IsPatch //fixed after 2017.3.1px
                || version == (2017, 3) && m_MeshCompression == 0)//2017.3.xfx with no compression
            {
                var m_IndexFormat = reader.ReadInt32();
                m_Use16BitIndices = m_IndexFormat == 0;
            }

            int m_IndexBuffer_size = reader.ReadInt32();
            if (m_Use16BitIndices)
            {
                m_IndexBuffer = new uint[m_IndexBuffer_size / 2];
                for (int i = 0; i < m_IndexBuffer_size / 2; i++)
                {
                    m_IndexBuffer[i] = reader.ReadUInt16();
                }
                reader.AlignStream();
            }
            else
            {
                m_IndexBuffer = reader.ReadUInt32Array(m_IndexBuffer_size / 4);
            }
        }

        if (version < (3, 5)) //3.4.2 and earlier
        {
            m_VertexCount = reader.ReadInt32();
            m_Vertices = reader.ReadSingleArray(m_VertexCount * 3); //Vector3

            m_Skin = new BoneWeights4[reader.ReadInt32()];
            for (int s = 0; s < m_Skin.Length; s++)
            {
                m_Skin[s] = new BoneWeights4(reader);
            }

            m_BindPose = reader.ReadMatrixArray();

            m_UV0 = reader.ReadSingleArray(reader.ReadInt32() * 2); //Vector2

            m_UV1 = reader.ReadSingleArray(reader.ReadInt32() * 2); //Vector2

            if (version <= (2, 5)) //2.5 and down
            {
                int m_TangentSpace_size = reader.ReadInt32();
                m_Normals = new float[m_TangentSpace_size * 3];
                m_Tangents = new float[m_TangentSpace_size * 4];
                for (int v = 0; v < m_TangentSpace_size; v++)
                {
                    m_Normals[v * 3] = reader.ReadSingle();
                    m_Normals[v * 3 + 1] = reader.ReadSingle();
                    m_Normals[v * 3 + 2] = reader.ReadSingle();
                    m_Tangents[v * 3] = reader.ReadSingle();
                    m_Tangents[v * 3 + 1] = reader.ReadSingle();
                    m_Tangents[v * 3 + 2] = reader.ReadSingle();
                    m_Tangents[v * 3 + 3] = reader.ReadSingle(); //handedness
                }
            }
            else //2.6.0 and later
            {
                m_Tangents = reader.ReadSingleArray(reader.ReadInt32() * 4); //Vector4

                m_Normals = reader.ReadSingleArray(reader.ReadInt32() * 3); //Vector3
            }
        }
        else
        {
            if (version < (2018, 2)) //2018.2 down
            {
                m_Skin = new BoneWeights4[reader.ReadInt32()];
                for (int s = 0; s < m_Skin.Length; s++)
                {
                    m_Skin[s] = new BoneWeights4(reader);
                }
            }

            if (version <= (4, 2)) //4.2 and down
            {
                m_BindPose = reader.ReadMatrixArray();
            }

            m_VertexData = new VertexData(reader);
        }

        if (version >= (2, 6)) //2.6.0 and later
        {
            m_CompressedMesh = new CompressedMesh(reader);
        }

        reader.Position += 24; //AABB m_LocalAABB

        if (version <= (3, 4)) //3.4.2 and earlier
        {
            int m_Colors_size = reader.ReadInt32();
            m_Colors = new float[m_Colors_size * 4];
            for (int v = 0; v < m_Colors_size * 4; v++)
            {
                m_Colors[v] = (float)reader.ReadByte() / 0xFF;
            }

            int m_CollisionTriangles_size = reader.ReadInt32();
            reader.Position += m_CollisionTriangles_size * 4; //UInt32 indices
            int m_CollisionVertexCount = reader.ReadInt32();
        }

        int m_MeshUsageFlags = reader.ReadInt32();

        if (version >= (2022, 1)) //2022.1 and up
        {
            int m_CookingOptions = reader.ReadInt32();
        }

        if (version >= 5) //5.0 and up
        {
            var m_BakedConvexCollisionMesh = reader.ReadUInt8Array();
            reader.AlignStream();
            var m_BakedTriangleCollisionMesh = reader.ReadUInt8Array();
            reader.AlignStream();
        }

        if (version >= (2018, 2)) //2018.2 and up
        {
            var m_MeshMetrics = new float[2];
            m_MeshMetrics[0] = reader.ReadSingle();
            m_MeshMetrics[1] = reader.ReadSingle();
        }

        if (version >= (2018, 3)) //2018.3 and up
        {
            reader.AlignStream();
            m_StreamData = new StreamingInfo(reader);
        }

        ProcessData();
    }

    private void ProcessData()
    {
        if (!string.IsNullOrEmpty(m_StreamData?.m_Path))
        {
            if (m_VertexData.m_VertexCount > 0)
            {
                var resourceReader = new ResourceReader(m_StreamData.m_Path, assetsFile, m_StreamData.m_Offset, m_StreamData.m_Size);
                m_VertexData.m_DataSize = resourceReader.GetData();
            }
        }
        if (version >= (3, 5)) //3.5 and up
        {
            ReadVertexData();
        }

        if (version >= (2, 6)) //2.6.0 and later
        {
            DecompressCompressedMesh();
        }

        GetTriangles();
    }

    private void ReadVertexData()
    {
        m_VertexCount = (int)m_VertexData.m_VertexCount;

        for (var chn = 0; chn < m_VertexData.m_Channels.Length; chn++)
        {
            var m_Channel = m_VertexData.m_Channels[chn];
            if (m_Channel.m_Dimension > 0)
            {
                var m_Stream = m_VertexData.m_Streams[m_Channel.m_Stream];
                var channelMask = new BitArray(new[] { (int)m_Stream.m_ChannelMask });
                if (channelMask.Get(chn))
                {
                    if (version < 2018 && chn == 2 && m_Channel.m_Format == 2) //kShaderChannelColor && kChannelFormatColor
                    {
                        m_Channel.m_Dimension = 4;
                    }

                    var vertexFormat = MeshHelper.ToVertexFormat(m_Channel.m_Format, version);
                    var componentByteSize = (int)MeshHelper.GetFormatSize(vertexFormat);
                    var componentBytes = new byte[m_VertexCount * m_Channel.m_Dimension * componentByteSize];
                    for (int v = 0; v < m_VertexCount; v++)
                    {
                        var vertexOffset = (int)m_Stream.m_Offset + m_Channel.m_Offset + (int)m_Stream.m_Stride * v;
                        for (int d = 0; d < m_Channel.m_Dimension; d++)
                        {
                            var componentOffset = vertexOffset + componentByteSize * d;
                            Buffer.BlockCopy(m_VertexData.m_DataSize, componentOffset, componentBytes, componentByteSize * (v * m_Channel.m_Dimension + d), componentByteSize);
                        }
                    }

                    if (reader.Endian == EndianType.BigEndian && componentByteSize > 1) //swap bytes
                    {
                        for (var i = 0; i < componentBytes.Length / componentByteSize; i++)
                        {
                            var buff = new byte[componentByteSize];
                            Buffer.BlockCopy(componentBytes, i * componentByteSize, buff, 0, componentByteSize);
                            buff = buff.Reverse().ToArray();
                            Buffer.BlockCopy(buff, 0, componentBytes, i * componentByteSize, componentByteSize);
                        }
                    }

                    int[] componentsIntArray = null;
                    float[] componentsFloatArray = null;
                    if (MeshHelper.IsIntFormat(vertexFormat))
                        componentsIntArray = MeshHelper.BytesToIntArray(componentBytes, vertexFormat);
                    else
                        componentsFloatArray = MeshHelper.BytesToFloatArray(componentBytes, vertexFormat);

                    if (version >= 2018)
                    {
                        switch (chn)
                        {
                            case 0: //kShaderChannelVertex
                                m_Vertices = componentsFloatArray;
                                break;
                            case 1: //kShaderChannelNormal
                                m_Normals = componentsFloatArray;
                                break;
                            case 2: //kShaderChannelTangent
                                m_Tangents = componentsFloatArray;
                                break;
                            case 3: //kShaderChannelColor
                                m_Colors = componentsFloatArray;
                                break;
                            case 4: //kShaderChannelTexCoord0
                                m_UV0 = componentsFloatArray;
                                break;
                            case 5: //kShaderChannelTexCoord1
                                m_UV1 = componentsFloatArray;
                                break;
                            case 6: //kShaderChannelTexCoord2
                                m_UV2 = componentsFloatArray;
                                break;
                            case 7: //kShaderChannelTexCoord3
                                m_UV3 = componentsFloatArray;
                                break;
                            case 8: //kShaderChannelTexCoord4
                                m_UV4 = componentsFloatArray;
                                break;
                            case 9: //kShaderChannelTexCoord5
                                m_UV5 = componentsFloatArray;
                                break;
                            case 10: //kShaderChannelTexCoord6
                                m_UV6 = componentsFloatArray;
                                break;
                            case 11: //kShaderChannelTexCoord7
                                m_UV7 = componentsFloatArray;
                                break;
                            //2018.2 and up
                            case 12: //kShaderChannelBlendWeight
                                if (m_Skin == null)
                                {
                                    InitMSkin();
                                }
                                for (int i = 0; i < m_VertexCount; i++)
                                {
                                    for (int j = 0; j < m_Channel.m_Dimension; j++)
                                    {
                                        m_Skin[i].m_Weight[j] = componentsFloatArray[i * m_Channel.m_Dimension + j];
                                    }
                                }
                                break;
                            case 13: //kShaderChannelBlendIndices
                                if (m_Skin == null)
                                {
                                    InitMSkin();
                                }
                                for (int i = 0; i < m_VertexCount; i++)
                                {
                                    for (int j = 0; j < m_Channel.m_Dimension; j++)
                                    {
                                        m_Skin[i].m_BoneIndex[j] = componentsIntArray[i * m_Channel.m_Dimension + j];
                                    }
                                }
                                break;
                        }
                    }
                    else
                    {
                        switch (chn)
                        {
                            case 0: //kShaderChannelVertex
                                m_Vertices = componentsFloatArray;
                                break;
                            case 1: //kShaderChannelNormal
                                m_Normals = componentsFloatArray;
                                break;
                            case 2: //kShaderChannelColor
                                m_Colors = componentsFloatArray;
                                break;
                            case 3: //kShaderChannelTexCoord0
                                m_UV0 = componentsFloatArray;
                                break;
                            case 4: //kShaderChannelTexCoord1
                                m_UV1 = componentsFloatArray;
                                break;
                            case 5:
                                if (version >= 5) //kShaderChannelTexCoord2
                                {
                                    m_UV2 = componentsFloatArray;
                                }
                                else //kShaderChannelTangent
                                {
                                    m_Tangents = componentsFloatArray;
                                }
                                break;
                            case 6: //kShaderChannelTexCoord3
                                m_UV3 = componentsFloatArray;
                                break;
                            case 7: //kShaderChannelTangent
                                m_Tangents = componentsFloatArray;
                                break;
                        }
                    }
                }
            }
        }
    }

    private void DecompressCompressedMesh()
    {
        //Vertex
        if (m_CompressedMesh.m_Vertices.m_NumItems > 0)
        {
            m_VertexCount = (int)m_CompressedMesh.m_Vertices.m_NumItems / 3;
            m_Vertices = m_CompressedMesh.m_Vertices.UnpackFloats(3, 3 * 4);
        }
        //UV
        if (m_CompressedMesh.m_UV.m_NumItems > 0)
        {
            var m_UVInfo = m_CompressedMesh.m_UVInfo;
            if (m_UVInfo != 0)
            {
                const int kInfoBitsPerUV = 4;
                const int kUVDimensionMask = 3;
                const int kUVChannelExists = 4;
                const int kMaxTexCoordShaderChannels = 8;

                int uvSrcOffset = 0;
                for (int uv = 0; uv < kMaxTexCoordShaderChannels; uv++)
                {
                    var texCoordBits = m_UVInfo >> (uv * kInfoBitsPerUV);
                    texCoordBits &= (1u << kInfoBitsPerUV) - 1u;
                    if ((texCoordBits & kUVChannelExists) != 0)
                    {
                        var uvDim = 1 + (int)(texCoordBits & kUVDimensionMask);
                        var m_UV = m_CompressedMesh.m_UV.UnpackFloats(uvDim, uvDim * 4, uvSrcOffset, m_VertexCount);
                        SetUV(uv, m_UV);
                        uvSrcOffset += uvDim * m_VertexCount;
                    }
                }
            }
            else
            {
                m_UV0 = m_CompressedMesh.m_UV.UnpackFloats(2, 2 * 4, 0, m_VertexCount);
                if (m_CompressedMesh.m_UV.m_NumItems >= m_VertexCount * 4)
                {
                    m_UV1 = m_CompressedMesh.m_UV.UnpackFloats(2, 2 * 4, m_VertexCount * 2, m_VertexCount);
                }
            }
        }
        //BindPose
        if (version < 5)
        {
            if (m_CompressedMesh.m_BindPoses.m_NumItems > 0)
            {
                m_BindPose = new Matrix4x4[m_CompressedMesh.m_BindPoses.m_NumItems / 16];
                var m_BindPoses_Unpacked = m_CompressedMesh.m_BindPoses.UnpackFloats(16, 4 * 16);
                var buffer = new float[16];
                for (int i = 0; i < m_BindPose.Length; i++)
                {
                    Array.Copy(m_BindPoses_Unpacked, i * 16, buffer, 0, 16);
                    m_BindPose[i] = new Matrix4x4(buffer);
                }
            }
        }
        //Normal
        if (m_CompressedMesh.m_Normals.m_NumItems > 0)
        {
            var normalData = m_CompressedMesh.m_Normals.UnpackFloats(2, 4 * 2);
            var signs = m_CompressedMesh.m_NormalSigns.UnpackInts();
            m_Normals = new float[m_CompressedMesh.m_Normals.m_NumItems / 2 * 3];
            for (int i = 0; i < m_CompressedMesh.m_Normals.m_NumItems / 2; ++i)
            {
                var x = normalData[i * 2 + 0];
                var y = normalData[i * 2 + 1];
                var zsqr = 1 - x * x - y * y;
                float z;
                if (zsqr >= 0f)
                    z = MathF.Sqrt(zsqr);
                else
                {
                    z = 0;
                    var normal = new Vector3(x, y, z);
                    normal.Normalize();
                    x = normal.X;
                    y = normal.Y;
                    z = normal.Z;
                }
                if (signs[i] == 0)
                    z = -z;
                m_Normals[i * 3] = x;
                m_Normals[i * 3 + 1] = y;
                m_Normals[i * 3 + 2] = z;
            }
        }
        //Tangent
        if (m_CompressedMesh.m_Tangents.m_NumItems > 0)
        {
            var tangentData = m_CompressedMesh.m_Tangents.UnpackFloats(2, 4 * 2);
            var signs = m_CompressedMesh.m_TangentSigns.UnpackInts();
            m_Tangents = new float[m_CompressedMesh.m_Tangents.m_NumItems / 2 * 4];
            for (int i = 0; i < m_CompressedMesh.m_Tangents.m_NumItems / 2; ++i)
            {
                var x = tangentData[i * 2 + 0];
                var y = tangentData[i * 2 + 1];
                var zsqr = 1 - x * x - y * y;
                float z;
                if (zsqr >= 0f)
                    z = MathF.Sqrt(zsqr);
                else
                {
                    z = 0;
                    var vector3f = new Vector3(x, y, z);
                    vector3f.Normalize();
                    x = vector3f.X;
                    y = vector3f.Y;
                    z = vector3f.Z;
                }
                if (signs[i * 2 + 0] == 0)
                    z = -z;
                var w = signs[i * 2 + 1] > 0 ? 1.0f : -1.0f;
                m_Tangents[i * 4] = x;
                m_Tangents[i * 4 + 1] = y;
                m_Tangents[i * 4 + 2] = z;
                m_Tangents[i * 4 + 3] = w;
            }
        }
        //FloatColor
        if (version >= 5)
        {
            if (m_CompressedMesh.m_FloatColors.m_NumItems > 0)
            {
                m_Colors = m_CompressedMesh.m_FloatColors.UnpackFloats(1, 4);
            }
        }
        //Skin
        if (m_CompressedMesh.m_Weights.m_NumItems > 0)
        {
            var weights = m_CompressedMesh.m_Weights.UnpackInts();
            var boneIndices = m_CompressedMesh.m_BoneIndices.UnpackInts();

            InitMSkin();

            int bonePos = 0;
            int boneIndexPos = 0;
            int j = 0;
            int sum = 0;

            for (int i = 0; i < m_CompressedMesh.m_Weights.m_NumItems; i++)
            {
                //read bone index and weight.
                m_Skin[bonePos].m_Weight[j] = weights[i] / 31.0f;
                m_Skin[bonePos].m_BoneIndex[j] = boneIndices[boneIndexPos++];
                j++;
                sum += weights[i];

                //the weights add up to one. fill the rest for this vertex with zero, and continue with next one.
                if (sum >= 31)
                {
                    for (; j < 4; j++)
                    {
                        m_Skin[bonePos].m_Weight[j] = 0;
                        m_Skin[bonePos].m_BoneIndex[j] = 0;
                    }
                    bonePos++;
                    j = 0;
                    sum = 0;
                }
                //we read three weights, but they don't add up to one. calculate the fourth one, and read
                //missing bone index. continue with next vertex.
                else if (j == 3)
                {
                    m_Skin[bonePos].m_Weight[j] = (31 - sum) / 31.0f;
                    m_Skin[bonePos].m_BoneIndex[j] = boneIndices[boneIndexPos++];
                    bonePos++;
                    j = 0;
                    sum = 0;
                }
            }
        }
        //IndexBuffer
        if (m_CompressedMesh.m_Triangles.m_NumItems > 0)
        {
            m_IndexBuffer = Array.ConvertAll(m_CompressedMesh.m_Triangles.UnpackInts(), x => (uint)x);
        }
        //Color
        if (m_CompressedMesh.m_Colors?.m_NumItems > 0)
        {
            m_CompressedMesh.m_Colors.m_NumItems *= 4;
            m_CompressedMesh.m_Colors.m_BitSize /= 4;
            var tempColors = m_CompressedMesh.m_Colors.UnpackInts();
            m_Colors = new float[m_CompressedMesh.m_Colors.m_NumItems];
            for (int v = 0; v < m_CompressedMesh.m_Colors.m_NumItems; v++)
            {
                m_Colors[v] = tempColors[v] / 255f;
            }
        }
    }

    private void GetTriangles()
    {
        foreach (var m_SubMesh in m_SubMeshes)
        {
            var firstIndex = m_SubMesh.m_FirstByte / 2;
            if (!m_Use16BitIndices)
            {
                firstIndex /= 2;
            }
            var indexCount = m_SubMesh.m_IndexCount;
            var topology = m_SubMesh.m_Topology;
            if (topology == GfxPrimitiveType.Triangles)
            {
                for (int i = 0; i < indexCount; i += 3)
                {
                    m_Indices.Add(m_IndexBuffer[firstIndex + i]);
                    m_Indices.Add(m_IndexBuffer[firstIndex + i + 1]);
                    m_Indices.Add(m_IndexBuffer[firstIndex + i + 2]);
                }
            }
            else if (version < 4 || topology == GfxPrimitiveType.TriangleStrip)
            {
                // de-stripify :
                uint triIndex = 0;
                for (int i = 0; i < indexCount - 2; i++)
                {
                    var a = m_IndexBuffer[firstIndex + i];
                    var b = m_IndexBuffer[firstIndex + i + 1];
                    var c = m_IndexBuffer[firstIndex + i + 2];

                    // skip degenerates
                    if (a == b || a == c || b == c)
                        continue;

                    // do the winding flip-flop of strips :
                    if ((i & 1) == 1)
                    {
                        m_Indices.Add(b);
                        m_Indices.Add(a);
                    }
                    else
                    {
                        m_Indices.Add(a);
                        m_Indices.Add(b);
                    }
                    m_Indices.Add(c);
                    triIndex += 3;
                }
                //fix indexCount
                m_SubMesh.m_IndexCount = triIndex;
            }
            else if (topology == GfxPrimitiveType.Quads)
            {
                for (int q = 0; q < indexCount; q += 4)
                {
                    m_Indices.Add(m_IndexBuffer[firstIndex + q]);
                    m_Indices.Add(m_IndexBuffer[firstIndex + q + 1]);
                    m_Indices.Add(m_IndexBuffer[firstIndex + q + 2]);
                    m_Indices.Add(m_IndexBuffer[firstIndex + q]);
                    m_Indices.Add(m_IndexBuffer[firstIndex + q + 2]);
                    m_Indices.Add(m_IndexBuffer[firstIndex + q + 3]);
                }
                //fix indexCount
                m_SubMesh.m_IndexCount = indexCount / 2 * 3;
            }
            else
            {
                throw new NotSupportedException("Failed getting triangles. Submesh topology is lines or points.");
            }
        }
    }

    private void InitMSkin()
    {
        m_Skin = new BoneWeights4[m_VertexCount];
        for (int i = 0; i < m_VertexCount; i++)
        {
            m_Skin[i] = new BoneWeights4();
        }
    }

    private void SetUV(int uv, float[] m_UV)
    {
        switch (uv)
        {
            case 0:
                m_UV0 = m_UV;
                break;
            case 1:
                m_UV1 = m_UV;
                break;
            case 2:
                m_UV2 = m_UV;
                break;
            case 3:
                m_UV3 = m_UV;
                break;
            case 4:
                m_UV4 = m_UV;
                break;
            case 5:
                m_UV5 = m_UV;
                break;
            case 6:
                m_UV6 = m_UV;
                break;
            case 7:
                m_UV7 = m_UV;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public float[] GetUV(int uv)
    {
        switch (uv)
        {
            case 0:
                return m_UV0;
            case 1:
                return m_UV1;
            case 2:
                return m_UV2;
            case 3:
                return m_UV3;
            case 4:
                return m_UV4;
            case 5:
                return m_UV5;
            case 6:
                return m_UV6;
            case 7:
                return m_UV7;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}

public static class MeshHelper
{
    public enum VertexChannelFormat
    {
        Float,
        Float16,
        Color,
        Byte,
        UInt32
    }

    public enum VertexFormat2017
    {
        Float,
        Float16,
        Color,
        UNorm8,
        SNorm8,
        UNorm16,
        SNorm16,
        UInt8,
        SInt8,
        UInt16,
        SInt16,
        UInt32,
        SInt32
    }

    public enum VertexFormat
    {
        Float,
        Float16,
        UNorm8,
        SNorm8,
        UNorm16,
        SNorm16,
        UInt8,
        SInt8,
        UInt16,
        SInt16,
        UInt32,
        SInt32
    }

    public static VertexFormat ToVertexFormat(int format, UnityVersion version)
    {
        if (version < 2017)
        {
            switch ((VertexChannelFormat)format)
            {
                case VertexChannelFormat.Float:
                    return VertexFormat.Float;
                case VertexChannelFormat.Float16:
                    return VertexFormat.Float16;
                case VertexChannelFormat.Color: //in 4.x is size 4
                    return VertexFormat.UNorm8;
                case VertexChannelFormat.Byte:
                    return VertexFormat.UInt8;
                case VertexChannelFormat.UInt32: //in 5.x
                    return VertexFormat.UInt32;
                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, null);
            }
        }
        else if (version < 2019)
        {
            switch ((VertexFormat2017)format)
            {
                case VertexFormat2017.Float:
                    return VertexFormat.Float;
                case VertexFormat2017.Float16:
                    return VertexFormat.Float16;
                case VertexFormat2017.Color:
                case VertexFormat2017.UNorm8:
                    return VertexFormat.UNorm8;
                case VertexFormat2017.SNorm8:
                    return VertexFormat.SNorm8;
                case VertexFormat2017.UNorm16:
                    return VertexFormat.UNorm16;
                case VertexFormat2017.SNorm16:
                    return VertexFormat.SNorm16;
                case VertexFormat2017.UInt8:
                    return VertexFormat.UInt8;
                case VertexFormat2017.SInt8:
                    return VertexFormat.SInt8;
                case VertexFormat2017.UInt16:
                    return VertexFormat.UInt16;
                case VertexFormat2017.SInt16:
                    return VertexFormat.SInt16;
                case VertexFormat2017.UInt32:
                    return VertexFormat.UInt32;
                case VertexFormat2017.SInt32:
                    return VertexFormat.SInt32;
                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, null);
            }
        }
        else
        {
            return (VertexFormat)format;
        }
    }


    public static uint GetFormatSize(VertexFormat format)
    {
        switch (format)
        {
            case VertexFormat.Float:
            case VertexFormat.UInt32:
            case VertexFormat.SInt32:
                return 4u;
            case VertexFormat.Float16:
            case VertexFormat.UNorm16:
            case VertexFormat.SNorm16:
            case VertexFormat.UInt16:
            case VertexFormat.SInt16:
                return 2u;
            case VertexFormat.UNorm8:
            case VertexFormat.SNorm8:
            case VertexFormat.UInt8:
            case VertexFormat.SInt8:
                return 1u;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
        }
    }

    public static bool IsIntFormat(VertexFormat format)
    {
        return format >= VertexFormat.UInt8;
    }

    public static float[] BytesToFloatArray(byte[] inputBytes, VertexFormat format)
    {
        var size = GetFormatSize(format);
        var len = inputBytes.Length / size;
        var result = new float[len];
        for (int i = 0; i < len; i++)
        {
            switch (format)
            {
                case VertexFormat.Float:
                    result[i] = BitConverter.ToSingle(inputBytes, i * 4);
                    break;
                case VertexFormat.Float16:
                    result[i] = Half.ToHalf(inputBytes, i * 2);
                    break;
                case VertexFormat.UNorm8:
                    result[i] = inputBytes[i] / 255f;
                    break;
                case VertexFormat.SNorm8:
                    result[i] = Math.Max((sbyte)inputBytes[i] / 127f, -1f);
                    break;
                case VertexFormat.UNorm16:
                    result[i] = BitConverter.ToUInt16(inputBytes, i * 2) / 65535f;
                    break;
                case VertexFormat.SNorm16:
                    result[i] = Math.Max(BitConverter.ToInt16(inputBytes, i * 2) / 32767f, -1f);
                    break;
            }
        }
        return result;
    }

    public static int[] BytesToIntArray(byte[] inputBytes, VertexFormat format)
    {
        var size = GetFormatSize(format);
        var len = inputBytes.Length / size;
        var result = new int[len];
        for (int i = 0; i < len; i++)
        {
            switch (format)
            {
                case VertexFormat.UInt8:
                case VertexFormat.SInt8:
                    result[i] = inputBytes[i];
                    break;
                case VertexFormat.UInt16:
                case VertexFormat.SInt16:
                    result[i] = BitConverter.ToInt16(inputBytes, i * 2);
                    break;
                case VertexFormat.UInt32:
                case VertexFormat.SInt32:
                    result[i] = BitConverter.ToInt32(inputBytes, i * 4);
                    break;
            }
        }
        return result;
    }
}