// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.IO;
using Foundation;
using UIKit;

namespace Microsoft.Xna.Framework
{
    partial class TitleContainer
    {
        private bool _supportRetina;
        private int _retinaScale;

        private void PlatformInit()
        {
            _location = NSBundle.MainBundle.ResourcePath;

            _supportRetina = UIScreen.MainScreen.Scale >= 2.0f;
            _retinaScale = (int)Math.Round(UIScreen.MainScreen.Scale);
        }

        private Stream PlatformOpenStream(string safeName)
        {
            string absolutePath = Path.Combine(Location, safeName);
            if (_supportRetina)
            {
                for (int scale = _retinaScale; scale >= 2; scale--)
                {
                    // Insert the @#x immediately prior to the extension. If this file exists
                    // and we are on a Retina device, return this file instead.
                    string absolutePathX = Path.Combine(Path.GetDirectoryName(absolutePath),
                                                        Path.GetFileNameWithoutExtension(absolutePath)
                                                        + "@" + scale + "x" + Path.GetExtension(absolutePath));
                    if (File.Exists(absolutePathX))
                        return File.OpenRead(absolutePathX);
                }
            }
            return File.OpenRead(absolutePath);
        }
    }
}

