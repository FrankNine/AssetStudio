namespace AssetStudio;

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

public enum SpriteMaskMode
{
    Off,
    On,
    MaskOnly,
    Export
}

public static class SpriteHelper
{
    public static Image<Bgra32> GetImage(this Sprite m_Sprite, SpriteMaskMode spriteMaskMode = SpriteMaskMode.On)
    {
        if (m_Sprite.m_SpriteAtlas != null && m_Sprite.m_SpriteAtlas.TryGet(out var m_SpriteAtlas))
        {
            if (m_SpriteAtlas.m_RenderDataMap.TryGetValue(m_Sprite.m_RenderDataKey, out var spriteAtlasData) && spriteAtlasData.m_Texture.TryGet(out var m_Texture2D))
            {
                return CutImage(m_Sprite, m_Texture2D, spriteAtlasData.m_TextureRect, spriteAtlasData.m_TextureRectOffset, spriteAtlasData.m_DownscaleMultiplier, spriteAtlasData.m_SettingsRaw);
            }
        }
        else
        {
            if (m_Sprite.m_RD.m_Texture.TryGet(out var m_Texture2D) && m_Sprite.m_RD.m_AlphaTexture.TryGet(out var m_AlphaTexture2D) && spriteMaskMode != SpriteMaskMode.Off)
            {
                Image<Bgra32> tex = null;
                if (spriteMaskMode != SpriteMaskMode.MaskOnly)
                {
                    tex = CutImage(m_Sprite, m_Texture2D, m_Sprite.m_RD.m_TextureRect, m_Sprite.m_RD.m_TextureRectOffset, m_Sprite.m_RD.m_DownscaleMultiplier, m_Sprite.m_RD.m_SettingsRaw);
                }
                var alphaTex = CutImage(m_Sprite, m_AlphaTexture2D, m_Sprite.m_RD.m_TextureRect, m_Sprite.m_RD.m_TextureRectOffset, m_Sprite.m_RD.m_DownscaleMultiplier, m_Sprite.m_RD.m_SettingsRaw);

                switch (spriteMaskMode)
                {
                    case SpriteMaskMode.On:
                        tex.ApplyRGBMask(alphaTex, isPreview: true);
                        return tex;
                    case SpriteMaskMode.Export:
                        tex.ApplyRGBMask(alphaTex);
                        return tex;
                    case SpriteMaskMode.MaskOnly:
                        return alphaTex;
                }
            }
            else if (m_Sprite.m_RD.m_Texture.TryGet(out m_Texture2D))
            {
                return CutImage(m_Sprite, m_Texture2D, m_Sprite.m_RD.m_TextureRect, m_Sprite.m_RD.m_TextureRectOffset, m_Sprite.m_RD.m_DownscaleMultiplier, m_Sprite.m_RD.m_SettingsRaw);
            }
        }
        return null;
    }

    private static void ApplyRGBMask(this Image<Bgra32> tex, Image<Bgra32> texMask, bool isPreview = false)
    {
        using (texMask)
        {
            if (tex.Width != texMask.Width || tex.Height != texMask.Height)
            {
                var resampler = isPreview ? KnownResamplers.NearestNeighbor : KnownResamplers.Bicubic;
                texMask.Mutate(x => x.Resize(tex.Width, tex.Height, resampler));
            }

            tex.ProcessPixelRows(texMask, (sourceTex, targetTexMask) =>
            {
                for (int y = 0; y < texMask.Height; y++)
                {
                    var texRow = sourceTex.GetRowSpan(y);
                    var maskRow = targetTexMask.GetRowSpan(y);
                    for (int x = 0; x < maskRow.Length; x++)
                    {
                        var grayscale = (byte)((maskRow[x].R + maskRow[x].G + maskRow[x].B) / 3);
                        texRow[x].A = grayscale;
                    }
                }
            });
        }
    }

    private static Image<Bgra32> CutImage(Sprite m_Sprite, Texture2D m_Texture2D, Rectf textureRect, Vector2 textureRectOffset, float downscaleMultiplier, SpriteSettings settingsRaw)
    {
        var originalImage = m_Texture2D.ConvertToImage(false);
        if (originalImage != null)
        {
            if (downscaleMultiplier > 0f && downscaleMultiplier != 1f)
            {
                var width = (int)(m_Texture2D.m_Width / downscaleMultiplier);
                var height = (int)(m_Texture2D.m_Height / downscaleMultiplier);
                originalImage.Mutate(x => x.Resize(width, height));
            }
            var rectX = (int)MathF.Floor(textureRect.m_X);
            var rectY = (int)MathF.Floor(textureRect.m_Y);
            var rectRight = (int)MathF.Ceiling(textureRect.m_X + textureRect.m_Width);
            var rectBottom = (int)MathF.Ceiling(textureRect.m_Y + textureRect.m_Height);
            rectRight = Math.Min(rectRight, originalImage.Width);
            rectBottom = Math.Min(rectBottom, originalImage.Height);
            var rect = new Rectangle(rectX, rectY, rectRight - rectX, rectBottom - rectY);
            var spriteImage = originalImage.Clone(x => x.Crop(rect));
            originalImage.Dispose();
            if (settingsRaw.m_Packed == 1)
            {
                //RotateAndFlip
                switch (settingsRaw.m_PackingRotation)
                {
                    case SpritePackingRotation.FlipHorizontal:
                        spriteImage.Mutate(x => x.Flip(FlipMode.Horizontal));
                        break;
                    case SpritePackingRotation.FlipVertical:
                        spriteImage.Mutate(x => x.Flip(FlipMode.Vertical));
                        break;
                    case SpritePackingRotation.Rotate180:
                        spriteImage.Mutate(x => x.Rotate(180));
                        break;
                    case SpritePackingRotation.Rotate90:
                        spriteImage.Mutate(x => x.Rotate(270));
                        break;
                }
            }

            //Tight
            if (settingsRaw.m_PackingMode == SpritePackingMode.Tight)
            {
                try
                {
                    var matrix = Matrix3x2.CreateScale(m_Sprite.m_PixelsToUnits);
                    matrix *= Matrix3x2.CreateTranslation(m_Sprite.m_Rect.m_Width * m_Sprite.m_Pivot.X - textureRectOffset.X, m_Sprite.m_Rect.m_Height * m_Sprite.m_Pivot.Y - textureRectOffset.Y);
                    var triangles = GetTriangles(m_Sprite.m_RD);
                    var points = triangles.Select(x => x.Select(y => new PointF(y.X, y.Y)).ToArray());
                    var pathBuilder = new PathBuilder(matrix);
                    foreach (var p in points)
                    {
                        pathBuilder.AddLines(p);
                        pathBuilder.CloseFigure();
                    }
                    var path = pathBuilder.Build();
                    var options = new DrawingOptions
                    {
                        GraphicsOptions = new GraphicsOptions
                        {
                            Antialias = false,
                            AlphaCompositionMode = PixelAlphaCompositionMode.DestOut
                        }
                    };
                    if (triangles.Length < 1024)
                    {
                        var rectP = new RectangularPolygon(0, 0, rect.Width, rect.Height);
                        try
                        {
                            spriteImage.Mutate(x => x.Fill(options, SixLabors.ImageSharp.Color.Red, rectP.Clip(path)));
                            spriteImage.Mutate(x => x.Flip(FlipMode.Vertical));
                            return spriteImage;
                        }
                        catch (ArgumentOutOfRangeException)
                        {
                            // ignored
                        }
                    }
                    using (var mask = new Image<Bgra32>(rect.Width, rect.Height, SixLabors.ImageSharp.Color.Black))
                    {
                        mask.Mutate(x => x.Fill(options, SixLabors.ImageSharp.Color.Red, path));
                        var brush = new ImageBrush(mask);
                        spriteImage.Mutate(x => x.Fill(options, brush));
                        spriteImage.Mutate(x => x.Flip(FlipMode.Vertical));
                        return spriteImage;
                    }
                }
                catch (Exception e)
                {
                    Logger.Warning($"{m_Sprite.m_Name} Unable to render the packed sprite correctly.\n{e}");
                }
            }

            //Rectangle
            spriteImage.Mutate(x => x.Flip(FlipMode.Vertical));
            return spriteImage;
        }

        return null;
    }

    private static Vector2[][] GetTriangles(SpriteRenderData m_RD)
    {
        if (m_RD.vertices != null) //5.6 down
        {
            var vertices = m_RD.vertices.Select(x => (Vector2)x.m_Pos).ToArray();
            var triangleCount = m_RD.indices.Length / 3;
            var triangles = new Vector2[triangleCount][];
            for (int i = 0; i < triangleCount; i++)
            {
                var first = m_RD.indices[i * 3];
                var second = m_RD.indices[i * 3 + 1];
                var third = m_RD.indices[i * 3 + 2];
                var triangle = new[] { vertices[first], vertices[second], vertices[third] };
                triangles[i] = triangle;
            }
            return triangles;
        }
        else //5.6 and up
        {
            var triangles = new List<Vector2[]>();
            var m_VertexData = m_RD.m_VertexData;
            var m_Channel = m_VertexData.m_Channels[0]; //kShaderChannelVertex
            var m_Stream = m_VertexData.m_Streams[m_Channel.m_Stream];
            using (var vertexReader = new BinaryReader(new MemoryStream(m_VertexData.m_DataSize)))
            {
                using (var indexReader = new BinaryReader(new MemoryStream(m_RD.m_IndexBuffer)))
                {
                    foreach (var subMesh in m_RD.m_SubMeshes)
                    {
                        vertexReader.BaseStream.Position = m_Stream.m_Offset + subMesh.m_FirstVertex * m_Stream.m_Stride + m_Channel.m_Offset;

                        var vertices = new Vector2[subMesh.m_VertexCount];
                        for (int v = 0; v < subMesh.m_VertexCount; v++)
                        {
                            vertices[v] = vertexReader.ReadVector3();
                            vertexReader.BaseStream.Position += m_Stream.m_Stride - 12;
                        }

                        indexReader.BaseStream.Position = subMesh.m_FirstByte;

                        var triangleCount = subMesh.m_IndexCount / 3u;
                        for (int i = 0; i < triangleCount; i++)
                        {
                            var first = indexReader.ReadUInt16() - subMesh.m_FirstVertex;
                            var second = indexReader.ReadUInt16() - subMesh.m_FirstVertex;
                            var third = indexReader.ReadUInt16() - subMesh.m_FirstVertex;
                            var triangle = new[] { vertices[first], vertices[second], vertices[third] };
                            triangles.Add(triangle);
                        }
                    }
                }
            }
            return triangles.ToArray();
        }
    }
}