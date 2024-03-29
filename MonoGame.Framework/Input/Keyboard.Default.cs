﻿// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System.Collections.Generic;

namespace Microsoft.Xna.Framework.Input
{
    public sealed partial class Keyboard
    {
        private List<Keys> _keys;

        private KeyboardState PlatformGetState()
        {
            return new KeyboardState(_keys);
        }

        internal void SetKeys(List<Keys> keys)
        {
            _keys = keys;
        }
    }
}
