namespace AssetStudio;

public class StreamingInfo
{
    public long m_Offset; //ulong
    public uint m_Size;
    public string m_Path;

    public StreamingInfo() { }

    public StreamingInfo(ObjectReader reader)
    {
        var version = reader.version;

        if (version >= 2020) //2020.1 and up
        {
            m_Offset = reader.ReadInt64();
        }
        else
        {
            m_Offset = reader.ReadUInt32();
        }
        m_Size = reader.ReadUInt32();
        m_Path = reader.ReadAlignedString();
    }
}