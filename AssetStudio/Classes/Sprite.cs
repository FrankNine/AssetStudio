namespace AssetStudio;

using System;
using System.IO;
using System.Collections.Generic;

public class SecondarySpriteTexture
{
    public PPtr<Texture2D> m_Texture;
    public string m_Name;

    public SecondarySpriteTexture(ObjectReader reader)
    {
        m_Texture = new PPtr<Texture2D>(reader);
        m_Name = reader.ReadAlignedString();
    }
}

public enum SpritePackingRotation
{
    None = 0,
    FlipHorizontal = 1,
    FlipVertical = 2,
    Rotate180 = 3,
    Rotate90 = 4
};

public enum SpritePackingMode
{
    Tight = 0,
    Rectangle
};

public enum SpriteMeshType
{
    FullRect,
    Tight
};

public class SpriteSettings
{
    public uint m_SettingsRaw;

    public uint m_Packed;
    public SpritePackingMode m_PackingMode;
    public SpritePackingRotation m_PackingRotation;
    public SpriteMeshType m_MeshType;

    public SpriteSettings(BinaryReader reader)
    {
        m_SettingsRaw = reader.ReadUInt32();

        m_Packed = m_SettingsRaw & 1; //1
        m_PackingMode = (SpritePackingMode)((m_SettingsRaw >> 1) & 1); //1
        m_PackingRotation = (SpritePackingRotation)((m_SettingsRaw >> 2) & 0xf); //4
        m_MeshType = (SpriteMeshType)((m_SettingsRaw >> 6) & 1); //1
        //reserved
    }
}

public class SpriteVertex
{
    public Vector3 m_Pos;
    public Vector2 m_Uv;

    public SpriteVertex(ObjectReader reader)
    {
        var version = reader.version;

        m_Pos = reader.ReadVector3();
        if (version <= (4, 3)) //4.3 and down
        {
            m_Uv = reader.ReadVector2();
        }
    }
}

public class SpriteRenderData
{
    public PPtr<Texture2D> m_Texture;
    public PPtr<Texture2D> m_AlphaTexture;
    public SecondarySpriteTexture[] m_SecondaryTextures;
    public SubMesh[] m_SubMeshes;
    public byte[] m_IndexBuffer;
    public VertexData m_VertexData;
    public SpriteVertex[] vertices;
    public ushort[] indices;
    public Matrix4x4[] m_Bindpose;
    public BoneWeights4[] m_SourceSkin;
    public Rectf m_TextureRect;
    public Vector2 m_TextureRectOffset;
    public Vector2 m_AtlasRectOffset;
    public SpriteSettings m_SettingsRaw;
    public Vector4 m_UvTransform;
    public float m_DownscaleMultiplier;

    public SpriteRenderData(ObjectReader reader)
    {
        var version = reader.version;

        m_Texture = new PPtr<Texture2D>(reader);
        m_AlphaTexture = version >= (5, 2) ? new PPtr<Texture2D>(reader) : new PPtr<Texture2D>(); //5.2 and up

        if (version >= 2019) //2019 and up
        {
            var secondaryTexturesSize = reader.ReadInt32();
            m_SecondaryTextures = new SecondarySpriteTexture[secondaryTexturesSize];
            for (int i = 0; i < secondaryTexturesSize; i++)
            {
                m_SecondaryTextures[i] = new SecondarySpriteTexture(reader);
            }
        }

        if (version >= (5, 6)) //5.6 and up
        {
            var m_SubMeshesSize = reader.ReadInt32();
            m_SubMeshes = new SubMesh[m_SubMeshesSize];
            for (int i = 0; i < m_SubMeshesSize; i++)
            {
                m_SubMeshes[i] = new SubMesh(reader);
            }

            m_IndexBuffer = reader.ReadUInt8Array();
            reader.AlignStream();

            m_VertexData = new VertexData(reader);
        }
        else
        {
            var verticesSize = reader.ReadInt32();
            vertices = new SpriteVertex[verticesSize];
            for (int i = 0; i < verticesSize; i++)
            {
                vertices[i] = new SpriteVertex(reader);
            }

            indices = reader.ReadUInt16Array();
            reader.AlignStream();
        }

        if (version >= 2018) //2018 and up
        {
            m_Bindpose = reader.ReadMatrixArray();

            if (version < (2018, 2)) //2018.2 down
            {
                var m_SourceSkinSize = reader.ReadInt32();
                for (int i = 0; i < m_SourceSkinSize; i++)
                {
                    m_SourceSkin[i] = new BoneWeights4(reader);
                }
            }
        }

        m_TextureRect = new Rectf(reader);
        m_TextureRectOffset = reader.ReadVector2();
        if (version >= (5, 6)) //5.6 and up
        {
            m_AtlasRectOffset = reader.ReadVector2();
        }

        m_SettingsRaw = new SpriteSettings(reader);
        if (version >= (4, 5)) //4.5 and up
        {
            m_UvTransform = reader.ReadVector4();
        }

        if (version >= 2017) //2017 and up
        {
            m_DownscaleMultiplier = reader.ReadSingle();
        }
    }
}

public class Rectf
{
    public float m_X;
    public float m_Y;
    public float m_Width;
    public float m_Height;

    public Rectf(BinaryReader reader)
    {
        m_X = reader.ReadSingle();
        m_Y = reader.ReadSingle();
        m_Width = reader.ReadSingle();
        m_Height = reader.ReadSingle();
    }
}

public sealed class Sprite : NamedObject
{
    public Rectf m_Rect;
    public Vector2 m_Offset;
    public Vector4 m_Border;
    public float m_PixelsToUnits;
    public Vector2 m_Pivot = new Vector2(0.5f, 0.5f);
    public uint m_Extrude;
    public bool m_IsPolygon;
    public KeyValuePair<Guid, long> m_RenderDataKey;
    public string[] m_AtlasTags;
    public PPtr<SpriteAtlas> m_SpriteAtlas;
    public SpriteRenderData m_RD;
    public Vector2[][] m_PhysicsShape;

    public Sprite(ObjectReader reader) : base(reader)
    {
        m_Rect = new Rectf(reader);
        m_Offset = reader.ReadVector2();
        if (version >= (4, 5)) //4.5 and up
        {
            m_Border = reader.ReadVector4();
        }

        m_PixelsToUnits = reader.ReadSingle();
        if (version >= (5, 4, 2)
            || version == (5, 4, 1) && version.IsPatch && version.Build >= 3) //5.4.1p3 and up
        {
            m_Pivot = reader.ReadVector2();
        }

        m_Extrude = reader.ReadUInt32();
        if (version >= (5, 3)) //5.3 and up
        {
            m_IsPolygon = reader.ReadBoolean();
            reader.AlignStream();
        }

        if (version >= 2017) //2017 and up
        {
            var first = new Guid(reader.ReadBytes(16));
            var second = reader.ReadInt64();
            m_RenderDataKey = new KeyValuePair<Guid, long>(first, second);

            m_AtlasTags = reader.ReadStringArray();

            m_SpriteAtlas = new PPtr<SpriteAtlas>(reader);
        }

        m_RD = new SpriteRenderData(reader);

        if (version >= 2017) //2017 and up
        {
            var m_PhysicsShapeSize = reader.ReadInt32();
            m_PhysicsShape = new Vector2[m_PhysicsShapeSize][];
            for (int i = 0; i < m_PhysicsShapeSize; i++)
            {
                m_PhysicsShape[i] = reader.ReadVector2Array();
            }
        }

        //vector m_Bones 2018 and up
    }
}