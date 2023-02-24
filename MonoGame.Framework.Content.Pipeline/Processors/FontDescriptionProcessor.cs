﻿// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

// Copyright (C)2021 Nick Kastellanos

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using MonoGame.Framework.Utilities;
using SharpFont;
using Glyph = Microsoft.Xna.Framework.Content.Pipeline.Graphics.Glyph;


namespace Microsoft.Xna.Framework.Content.Pipeline.Processors
{
    [ContentProcessor(DisplayName = "Sprite Font Description - MonoGame")]
    public class FontDescriptionProcessor : ContentProcessor<FontDescription, SpriteFontContent>
    {
        [DefaultValue(true)]
        public virtual bool PremultiplyAlpha { get; set; }

        [DefaultValue(typeof(TextureProcessorOutputFormat), "Compressed")]
        public virtual TextureProcessorOutputFormat TextureFormat { get; set; }

        public FontDescriptionProcessor()
        {
            PremultiplyAlpha = true;
            TextureFormat = TextureProcessorOutputFormat.Compressed;
        }

        public override SpriteFontContent Process(FontDescription input, ContentProcessorContext context)
        {
            var output = new SpriteFontContent(input);

            var fontFile = FindFont(input, context);

            if (string.IsNullOrWhiteSpace(fontFile))
                fontFile = FindFontFile(input, context);

            if (!File.Exists(fontFile))
                throw new PipelineException("Could not find \"" + input.FontName + "\" font from file \""+ fontFile +"\".");

            var extensions = new List<string> { ".ttf", ".ttc", ".otf" };
            string fileExtension = Path.GetExtension(fontFile).ToLowerInvariant();
            if (!extensions.Contains(fileExtension))
                throw new PipelineException("Unknown file extension " + fileExtension);

            context.Logger.LogMessage("Building Font {0}", fontFile);

            // Get the platform specific texture profile.
            var texProfile = TextureProfile.ForPlatform(context.TargetPlatform);

            var characters = new List<char>(input.Characters);
            // add default character
            if (input.DefaultCharacter != null)
            {
                if (!characters.Contains(input.DefaultCharacter.Value))
                    characters.Add(input.DefaultCharacter.Value);
            }
            characters.Sort();

            FontContent font = ImportFont(input, context, fontFile, characters);

            // Validate.
            if (font.Glyphs.Count == 0)
                throw new PipelineException("Font does not contain any glyphs.");

            // Optimize glyphs.
            foreach (Glyph glyph in font.Glyphs.Values)
                glyph.Crop();

            // We need to know how to pack the glyphs.
            bool requiresPot, requiresSquare;
            texProfile.Requirements(context, TextureFormat, out requiresPot, out requiresSquare);

            var face = GlyphPacker.ArrangeGlyphs(font.Glyphs.Values, requiresPot, requiresSquare);

            // calculate line spacing.
            output.VerticalLineSpacing = (int)font.MetricsHeight;
            // The LineSpacing from XNA font importer is +1px that SharpFont.
            output.VerticalLineSpacing += 1;

            float glyphHeightEx = -(font.MetricsDescender/2f);
            // The above value of glyphHeightEx match the XNA importer,
            // however the height of MeasureString() does not match VerticalLineSpacing.
            //glyphHeightEx = 0;
            float baseline = font.MetricsHeight + font.MetricsDescender + (font.MetricsDescender/2f) + glyphHeightEx;

            foreach (char ch in font.Glyphs.Keys)
            {
                Glyph glyph = font.Glyphs[ch];

                output.CharacterMap.Add(ch);

                var texRect = glyph.Subrect;
                output.Glyphs.Add(texRect);

                Rectangle cropping;
                cropping.X = glyph.XOffset + glyph.FontBitmapLeft;
                cropping.Y = glyph.YOffset + (int)(-glyph.GlyphMetricTopBearing + baseline);
                cropping.Width  = (int)glyph.XAdvance;
                cropping.Height = (int)Math.Ceiling(font.MetricsHeight + glyphHeightEx);
                output.Cropping.Add(cropping);

                // Set the optional character kerning.
                if (input.UseKerning)
                    output.Kerning.Add(glyph.Kerning.ToVector3());
                else
                    output.Kerning.Add(new Vector3(0, glyph.Width, 0));
            }

            output.Texture.Faces[0].Add(face);

            ProcessPremultiplyAlpha(face);

            // Perform the final texture conversion.
            texProfile.ConvertTexture(context, output.Texture, TextureFormat, true);

            return output;
        }

        private string FindFont(FontDescription input, ContentProcessorContext context)
        {
            if (CurrentPlatform.OS == OS.Windows)
            {
                var fontsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts");
                foreach (var key in new RegistryKey[] { Registry.LocalMachine, Registry.CurrentUser })
                {
                    var subkey = key.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts", false);
                    foreach (string font in subkey.GetValueNames().OrderBy(x => x))
                    {
                        if (font.StartsWith(input.FontName, StringComparison.OrdinalIgnoreCase))
                        {
                            string fontPath = subkey.GetValue(font).ToString();
                            // The registry value might have trailing NUL characters
                            fontPath.TrimEnd(new char[] { '\0' });

                            return Path.IsPathRooted(fontPath) ? fontPath : Path.Combine(fontsDirectory, fontPath);
                        }
                    }
                }
            }
            else if (CurrentPlatform.OS == OS.Linux)
            {
                string s, e;
                ExternalTool.Run("/bin/bash", string.Format("-c \"fc-match -f '%{{file}}:%{{family}}\\n' '{0}:style={1}'\"", input.FontName, input.Style.ToString()), out s, out e);
                s = s.Trim();

                var split = s.Split(':');
                if (split.Length >= 2)
                {
                    // check font family, fontconfig might return a fallback
                    if (split[1].Contains(","))
                    {
                        // this file defines multiple family names
                        var families = split[1].Split(',');
                        foreach (var f in families)
                        {
                            if (input.FontName.Equals(f, StringComparison.InvariantCultureIgnoreCase))
                                return split[0];
                        }
                    }
                    else
                    {
                        if (input.FontName.Equals(split[1], StringComparison.InvariantCultureIgnoreCase))
                            return split[0];
                    }
                }
            }

            return String.Empty;
        }

        private string FindFontFile(FontDescription input, ContentProcessorContext context)
        {
            var extensions = new string[] { "", ".ttf", ".ttc", ".otf" };
            var directories = new List<string>();

            directories.Add(Path.GetDirectoryName(input.Identity.SourceFilename));

            // Add special per platform directories
            if (CurrentPlatform.OS == OS.Windows)
            {
                var fontsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts");
                directories.Add(fontsDirectory);
            }
            else if (CurrentPlatform.OS == OS.MacOSX)
            {
                directories.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Library", "Fonts"));
                directories.Add("/Library/Fonts");
                directories.Add("/System/Library/Fonts/Supplemental");
            }

            foreach (var dir in directories)
            {
                foreach (var ext in extensions)
                {
                    var fontFile = Path.Combine(dir, input.FontName + ext);
                    if (File.Exists(fontFile))
                        return fontFile;
                }
            }

            return String.Empty;
        }

        // Uses FreeType to rasterize TrueType fonts into a series of glyph bitmaps.
        private static FontContent ImportFont(FontDescription input, ContentProcessorContext context, string fontName, List<char> characters)
        {
            FontContent fontContent = new FontContent();

            using (Library sharpFontLib = new Library())
            using (var face = sharpFontLib.NewFace(fontName, 0))
            {
                int fixedSize = ((int)input.Size) << 6;
                const uint dpi = 0;
                face.SetCharSize(0, fixedSize, dpi, dpi);

                // Rasterize each character in turn.
                foreach (char character in characters)
                {
                    if (fontContent.Glyphs.ContainsKey(character))
                        continue;

                    var glyph = ImportGlyph(input, context, face, character);
                    fontContent.Glyphs.Add(character, glyph);
                }


                fontContent.MetricsHeight = face.Size.Metrics.Height >> 6;
                fontContent.MetricsAscender  = face.Size.Metrics.Ascender >> 6;
                fontContent.MetricsDescender = face.Size.Metrics.Descender >> 6;

#if DEBUG
                fontContent.FaceUnderlinePosition = face.UnderlinePosition >> 6;
                fontContent.FaceUnderlineThickness = face.UnderlineThickness >> 6;
#endif

                return fontContent;
            }
        }

        // Rasterizes a single character glyph.
        private static Glyph ImportGlyph(FontDescription input, ContentProcessorContext context, Face face, char character)
        {
            LoadFlags loadFlags = LoadFlags.Default;
            LoadTarget loadTarget = LoadTarget.Mono;
            RenderMode renderMode = RenderMode.Mono;

            uint glyphIndex = face.GetCharIndex(character);
            face.LoadGlyph(glyphIndex, loadFlags, loadTarget);
            face.Glyph.RenderGlyph(renderMode);

            // Render the character.
            BitmapContent glyphBitmap = null;
            if (face.Glyph.Bitmap.Width > 0 && face.Glyph.Bitmap.Rows > 0)
            {
                glyphBitmap = new PixelBitmapContent<byte>(face.Glyph.Bitmap.Width, face.Glyph.Bitmap.Rows);

                //if the character bitmap has 1bpp we have to expand the buffer data to get the 8bpp pixel data
                //each byte in bitmap.bufferdata contains the value of to 8 pixels in the row
                //if bitmap is of width 10, each row has 2 bytes with 10 valid bits, and the last 6 bits of 2nd byte must be discarded
                if (face.Glyph.Bitmap.PixelMode == PixelMode.Mono)
                {
                    byte[] alphaData = ConvertMonoToAlpha(face.Glyph);
                    glyphBitmap.SetPixelData(alphaData);
                }
                else if (face.Glyph.Bitmap.PixelMode == PixelMode.Gray)
                {
                    glyphBitmap.SetPixelData(face.Glyph.Bitmap.BufferData);
                }
                else
                {
                    throw new PipelineException(string.Format("Glyph PixelMode {0} is not supported.", face.Glyph.Bitmap.PixelMode));
                }
            }
            else
            {
                var gHA = face.Glyph.Metrics.HorizontalAdvance >> 6;
                var gVA = face.Size.Metrics.Height >> 6;

                gHA = gHA > 0 ? gHA : gVA;
                gVA = gVA > 0 ? gVA : gHA;

                glyphBitmap = new PixelBitmapContent<byte>(gHA, gVA);
            }

            var kerning = new GlyphKerning();
            kerning.LeftBearing  = (face.Glyph.Metrics.HorizontalBearingX >> 6);
            kerning.AdvanceWidth = (face.Glyph.Metrics.Width >> 6);
            kerning.RightBearing = (face.Glyph.Metrics.HorizontalAdvance >> 6) - (kerning.LeftBearing + kerning.AdvanceWidth);
            kerning.LeftBearing  -= face.Glyph.BitmapLeft;
            kerning.AdvanceWidth += face.Glyph.BitmapLeft;

            // Construct the output Glyph object.
            return new Glyph(glyphIndex, glyphBitmap)
            {
                FontBitmapLeft = face.Glyph.BitmapLeft,
                FontBitmapTop = face.Glyph.BitmapTop,

                XOffset  = 0,
                YOffset  = 0,
                XAdvance = (face.Glyph.Metrics.HorizontalAdvance >> 6),
                Kerning  = kerning,

                GlyphMetricTopBearing = (face.Glyph.Metrics.HorizontalBearingY >> 6),
#if DEBUG
                GlyphMetricLeftBearing = (face.Glyph.Metrics.HorizontalBearingX >> 6),
                GlyphMetricWidth = (face.Glyph.Metrics.Width >> 6),
                GlyphMetricXAdvance = (face.Glyph.Metrics.HorizontalAdvance >> 6),
#endif
            };
        }

        private static unsafe byte[] ConvertMonoToAlpha(GlyphSlot glyph)
        {
            FTBitmap bitmap = glyph.Bitmap;
            int cols = bitmap.Width;
            int rows = bitmap.Rows;
            int stride = bitmap.Pitch;

            // SharpFont 2.5.3 doesn't return the entire bitmapdata when Pitch > 1.
            if (glyph.Library.Version != new Version(2,5,3))
                System.Diagnostics.Debug.Assert(bitmap.BufferData.Length == rows * bitmap.Pitch);


            byte* pGlyphData = (byte*)bitmap.Buffer.ToPointer();
            byte[] alphaData = new byte[cols * rows];
            fixed (byte* pAlphaData = alphaData)
            {
                for (int y = 0; y < rows; y++)
                {
                    for (int x = 0; x < cols; x++)
                    {
                        byte b = pGlyphData[(x >> 3) + y * stride];
                        b = (byte)(b & (0x80 >> (x & 0x07)));
                        pAlphaData[x + y * cols] = (b == (byte)0) ? (byte)0 : (byte)255;
                    }
                }
            }

            return alphaData;
        }

        private unsafe void ProcessPremultiplyAlpha(BitmapContent bmp)
        {
            if (PremultiplyAlpha)
            {
                byte[] data = bmp.GetPixelData();
                fixed (byte* pdata = data)
                {
                    int count = data.Length / 4;
                    for (int idx = 0; idx < count; idx++)
                    {
                        byte r = pdata[idx * 4 + 0];
                        //byte g = pdata[idx * 4 + 1];
                        //byte b = pdata[idx * 4 + 2];
                        //byte a = pdata[idx * 4 + 3];

                    // Special case of simply copying the R component into the A, since R is the value of white alpha we want
                        pdata[idx * 4 + 0] = r;
                        pdata[idx * 4 + 1] = r;
                        pdata[idx * 4 + 2] = r;
                        pdata[idx * 4 + 3] = r;
                }
                }
                bmp.SetPixelData(data);
            }
            else
            {
                byte[] data = bmp.GetPixelData();
                fixed (byte* pdata = data)
                {
                    int count = data.Length / 4;
                    for (int idx = 0; idx < count; idx++)
                {
                        byte r = pdata[idx * 4 + 0];
                        //byte g = pdata[idx * 4 + 1];
                        //byte b = pdata[idx * 4 + 2];
                        //byte a = pdata[idx * 4 + 3];

                    // Special case of simply moving the R component into the A and setting RGB to solid white, since R is the value of white alpha we want
                        pdata[idx * 4 + 0] = 255;
                        pdata[idx * 4 + 1] = 255;
                        pdata[idx * 4 + 2] = 255;
                        pdata[idx * 4 + 3] = r;
                    }
                }
                bmp.SetPixelData(data);
            }
        }
    }
}
