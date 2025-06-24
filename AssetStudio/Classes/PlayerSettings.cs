namespace AssetStudio;

public sealed class PlayerSettings : Object
{
    public string m_CompanyName;
    public string m_ProductName;

    public PlayerSettings(ObjectReader reader) : base(reader)
    {
        if (version >= (3, 0))
        {
            if (version >= (5, 4)) //5.4.0 and up
            {
                var m_ProductGUID = reader.ReadBytes(16);
            }

            var m_AndroidProfiler = reader.ReadBoolean();
            //bool AndroidFilterTouchesWhenObscured 2017.2 and up
            //bool AndroidEnableSustainedPerformanceMode 2018 and up
            reader.AlignStream();
            int m_DefaultScreenOrientation = reader.ReadInt32();
            int m_TargetDevice = reader.ReadInt32();
            if (version < (5, 3)) //5.3 down
            {
                if (version < 5) //5.0 down
                {
                    int m_TargetPlatform = reader.ReadInt32(); //4.0 and up targetGlesGraphics
                    if (version >= (4, 6)) //4.6 and up
                    {
                        var m_TargetIOSGraphics = reader.ReadInt32();
                    }
                }
                int m_TargetResolution = reader.ReadInt32();
            }
            else
            {
                var m_UseOnDemandResources = reader.ReadBoolean();
                reader.AlignStream();
            }
            if (version >= (3, 5)) //3.5 and up
            {
                var m_AccelerometerFrequency = reader.ReadInt32();
            }
        }
        m_CompanyName = reader.ReadAlignedString();
        m_ProductName = reader.ReadAlignedString();
    }
}