namespace AssetStudio;

public sealed class BuildSettings : Object
{
    public string[] m_Levels;
    public string[] m_Scenes;

    public BuildSettings(ObjectReader reader) : base(reader)
    {
        if (reader.version < (5, 1)) //5.1 down
        {
            m_Levels = reader.ReadStringArray();
        }
        else
        {
            m_Scenes = reader.ReadStringArray();
        }
    }
}