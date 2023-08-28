// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

// Copyright (C)2023 Nick Kastellanos

using System;
using Microsoft.Xna.Platform.Graphics;
using MonoGame.Framework.Utilities;


namespace Microsoft.Xna.Framework.Graphics
{
	public partial class TextureCube : Texture
	{
        private ITextureCubeStrategy _strategyTextureCube;

        private int _size;

        /// <summary>
        /// Gets the width and height of the cube map face in pixels.
        /// </summary>
        /// <value>The width and height of a cube map face in pixels.</value>
        public int Size { get { return _size; } }
		
		public TextureCube(GraphicsDevice graphicsDevice, int size, bool mipMap, SurfaceFormat format)
            : this(graphicsDevice, size, mipMap, format, false)
		{
        }

        protected TextureCube(GraphicsDevice graphicsDevice, int size, bool mipMap, SurfaceFormat format, bool renderTarget)
        {
            if (graphicsDevice == null)
                throw new ArgumentNullException("graphicsDevice", FrameworkResources.ResourceCreationWhenDeviceIsNull);
            if (graphicsDevice.Strategy.GraphicsProfile == GraphicsProfile.Reach && size > 512)
                throw new NotSupportedException("Reach profile supports a maximum TextureCube size of 512");
            if (graphicsDevice.Strategy.GraphicsProfile == GraphicsProfile.HiDef && size > 4096)
                throw new NotSupportedException("HiDef profile supports a maximum TextureCube size of 4096");
            if (graphicsDevice.Strategy.GraphicsProfile == GraphicsProfile.FL10_0 && size > 8192)
                throw new NotSupportedException("FL10_0 profile supports a maximum TextureCube size of 8192");
            if (graphicsDevice.Strategy.GraphicsProfile == GraphicsProfile.FL10_1 && size > 8192)
                throw new NotSupportedException("FL10_1 profile supports a maximum TextureCube size of 8192");
            if (graphicsDevice.Strategy.GraphicsProfile == GraphicsProfile.FL11_0 && size > 16384)
                throw new NotSupportedException("FL11_0 profile supports a maximum TextureCube size of 16384");
            if (graphicsDevice.Strategy.GraphicsProfile == GraphicsProfile.FL11_1 && size > 16384)
                throw new NotSupportedException("FL11_1 profile supports a maximum TextureCube size of 16384");
            if (graphicsDevice.Strategy.GraphicsProfile == GraphicsProfile.Reach && !MathHelper.IsPowerOfTwo(size))
                throw new NotSupportedException("Reach profile requires TextureCube sizes to be powers of two");
            if (graphicsDevice.Strategy.GraphicsProfile == GraphicsProfile.Reach && (format == SurfaceFormat.NormalizedByte2 || format == SurfaceFormat.NormalizedByte4 || format == SurfaceFormat.Rgba1010102 || format == SurfaceFormat.Rg32 || format == SurfaceFormat.Rgba64 || format == SurfaceFormat.Alpha8 || format == SurfaceFormat.Single || format == SurfaceFormat.Vector2 || format == SurfaceFormat.Vector4 || format == SurfaceFormat.HalfSingle || format == SurfaceFormat.HalfVector2 || format == SurfaceFormat.HalfVector4 || format == SurfaceFormat.HdrBlendable))
                throw new NotSupportedException("Reach profile does not support Texture2D format "+ format);
            if (size <= 0)
                throw new ArgumentOutOfRangeException("size","Cube size must be greater than zero");

            _strategyTextureCube = graphicsDevice.Strategy.MainContext.Strategy.CreateTextureCubeStrategy(size, mipMap, format);

            this.GraphicsDevice = graphicsDevice;
			this._size = size;
            this._format = format;
            this._levelCount = mipMap ? CalculateMipLevels(size) : 1;

            PlatformConstructTextureCube(graphicsDevice, size, mipMap, format, renderTarget);
        }

        /// <summary>
        /// Gets a copy of cube texture data specifying a cubemap face.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="cubeMapFace">The cube map face.</param>
        /// <param name="data">The data.</param>
        public void GetData<T>(CubeMapFace cubeMapFace, T[] data) where T : struct
        {
            if (data == null)
                throw new ArgumentNullException("data");
            GetData(cubeMapFace, 0, null, data, 0, data.Length);
        }

	    public void GetData<T>(CubeMapFace cubeMapFace, T[] data, int startIndex, int elementCount) where T : struct
	    {
	        GetData(cubeMapFace, 0, null, data, startIndex, elementCount);
	    }

	    public void GetData<T>(CubeMapFace cubeMapFace, int level, Rectangle? rect, T[] data, int startIndex, int elementCount) where T : struct
	    {
            Rectangle checkedRect;
            ValidateParams(level, rect, data, startIndex, elementCount, out checkedRect);
	        PlatformGetData(cubeMapFace, level, checkedRect, data, startIndex, elementCount);
	    }

		public void SetData<T>(CubeMapFace face, T[] data) where T : struct
		{
            if (data == null)
                throw new ArgumentNullException("data");
            SetData(face, 0, null, data, 0, data.Length);
		}

        public void SetData<T>(CubeMapFace face, T[] data, int startIndex, int elementCount) where T : struct
		{
            SetData(face, 0, null, data, startIndex, elementCount);
		}
		
        public void SetData<T>(CubeMapFace face, int level, Rectangle? rect, T[] data, int startIndex, int elementCount) where T : struct
        {
            Rectangle checkedRect;
            ValidateParams(level, rect, data, startIndex, elementCount, out checkedRect);
            PlatformSetData(face, level, checkedRect, data, startIndex, elementCount);
		}

        private void ValidateParams<T>(int level, Rectangle? rect, T[] data, int startIndex,
            int elementCount, out Rectangle checkedRect) where T : struct
        {
            var textureBounds = new Rectangle(0, 0, Math.Max(Size >> level, 1), Math.Max(Size >> level, 1));
            checkedRect = rect ?? textureBounds;
            if (level < 0 || level >= LevelCount)
                throw new ArgumentException("level must be smaller than the number of levels in this texture.");
            if (!textureBounds.Contains(checkedRect) || checkedRect.Width <= 0 || checkedRect.Height <= 0)
                throw new ArgumentException("Rectangle must be inside the texture bounds", "rect");
            if (data == null)
                throw new ArgumentNullException("data");
            var tSize = ReflectionHelpers.SizeOf<T>();
            var fSize = Format.GetSize();
            if (tSize > fSize || fSize % tSize != 0)
                throw new ArgumentException("Type T is of an invalid size for the format of this texture.", "T");
            if (startIndex < 0 || startIndex >= data.Length)
                throw new ArgumentException("startIndex must be at least zero and smaller than data.Length.", "startIndex");
            if (data.Length < startIndex + elementCount)
                throw new ArgumentException("The data array is too small.");

            int dataByteSize;
            if (Format.IsCompressedFormat())
            {
                // round x and y down to next multiple of four; width and height up to next multiple of four
                var roundedWidth = (checkedRect.Width + 3) & ~0x3;
                var roundedHeight = (checkedRect.Height + 3) & ~0x3;
                checkedRect = new Rectangle(checkedRect.X & ~0x3, checkedRect.Y & ~0x3,
#if OPENGL
                    // OpenGL only: The last two mip levels require the width and height to be
                    // passed as 2x2 and 1x1, but there needs to be enough data passed to occupy
                    // a 4x4 block.
                    checkedRect.Width < 4 && textureBounds.Width < 4 ? textureBounds.Width : roundedWidth,
                    checkedRect.Height < 4 && textureBounds.Height < 4 ? textureBounds.Height : roundedHeight);
#else
                    roundedWidth, roundedHeight);
#endif
                dataByteSize = roundedWidth * roundedHeight * fSize / 16;
            }
            else
            {
                dataByteSize = checkedRect.Width * checkedRect.Height * fSize;
            }
            if (elementCount * tSize != dataByteSize)
                throw new ArgumentException(string.Format("elementCount is not the right size, " +
                                            "elementCount * sizeof(T) is {0}, but data size is {1}.",
                                            elementCount * tSize, dataByteSize), "elementCount");
        }
	}
}

