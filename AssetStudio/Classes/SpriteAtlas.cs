namespace AssetStudio;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

public class SpriteAtlasData
{
    public PPtr<Texture2D> m_Texture;
    public PPtr<Texture2D> m_AlphaTexture;
    public Rectf m_TextureRect;
    public Vector2 m_TextureRectOffset;
    public Vector2 m_AtlasRectOffset;
    public Vector4 m_UvTransform;
    public float m_DownscaleMultiplier;
    public SpriteSettings m_SettingsRaw;
    public SecondarySpriteTexture[] m_SecondaryTextures;

    public SpriteAtlasData(ObjectReader reader)
    {
        var version = reader.version;
        m_Texture = new PPtr<Texture2D>(reader);
        m_AlphaTexture = new PPtr<Texture2D>(reader);
        m_TextureRect = new Rectf(reader);
        m_TextureRectOffset = reader.ReadVector2();
        if (version >= (2017, 2)) //2017.2 and up
        {
            m_AtlasRectOffset = reader.ReadVector2();
        }
        m_UvTransform = reader.ReadVector4();
        m_DownscaleMultiplier = reader.ReadSingle();
        m_SettingsRaw = new SpriteSettings(reader);
        if (version >= (2020, 2)) //2020.2 and up
        {
            var secondaryTexturesSize = reader.ReadInt32();
            m_SecondaryTextures = new SecondarySpriteTexture[secondaryTexturesSize];
            for (int i = 0; i < secondaryTexturesSize; i++)
            {
                m_SecondaryTextures[i] = new SecondarySpriteTexture(reader);
            }
            reader.AlignStream();
        }
    }
}

public sealed class SpriteAtlas : NamedObject
{
    public PPtr<Sprite>[] m_PackedSprites;
    [JsonConverter(typeof(JsonConverterHelper.RenderDataMapConverter))]
    public Dictionary<KeyValuePair<Guid, long>, SpriteAtlasData> m_RenderDataMap;
    public bool m_IsVariant;

    public SpriteAtlas(ObjectReader reader) : base(reader)
    {
        var m_PackedSpritesSize = reader.ReadInt32();
        m_PackedSprites = new PPtr<Sprite>[m_PackedSpritesSize];
        for (int i = 0; i < m_PackedSpritesSize; i++)
        {
            m_PackedSprites[i] = new PPtr<Sprite>(reader);
        }

        var m_PackedSpriteNamesToIndex = reader.ReadStringArray();

        var m_RenderDataMapSize = reader.ReadInt32();
        m_RenderDataMap = new Dictionary<KeyValuePair<Guid, long>, SpriteAtlasData>(m_RenderDataMapSize);
        for (int i = 0; i < m_RenderDataMapSize; i++)
        {
            var first = new Guid(reader.ReadBytes(16));
            var second = reader.ReadInt64();
            var value = new SpriteAtlasData(reader);
            m_RenderDataMap.Add(new KeyValuePair<Guid, long>(first, second), value);
        }
        var m_Tag = reader.ReadAlignedString();
        m_IsVariant = reader.ReadBoolean();
        reader.AlignStream();
    }
}