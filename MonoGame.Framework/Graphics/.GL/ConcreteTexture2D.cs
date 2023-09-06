﻿// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

// Copyright (C)2023 Nick Kastellanos

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Framework.Utilities;
using MonoGame.OpenGL;
using GLPixelFormat = MonoGame.OpenGL.PixelFormat;


namespace Microsoft.Xna.Platform.Graphics
{
    internal class ConcreteTexture2D : ConcreteTexture, ITexture2DStrategy
    {
        private readonly int _width;
        private readonly int _height;
        private readonly int _arraySize;

        internal ConcreteTexture2D(GraphicsContextStrategy contextStrategy, int width, int height, bool mipMap, SurfaceFormat format, int arraySize, bool shared)
            : base(contextStrategy, format, Texture.CalculateMipLevels(mipMap, width, height))
        {
            this._width  = width;
            this._height = height;
            this._arraySize = arraySize;
        }


        #region ITexture2DStrategy
        public int Width { get { return _width; } }
        public int Height { get { return _height; } }
        public int ArraySize { get { return _arraySize; } }

        public Rectangle Bounds
        {
            get { return new Rectangle(0, 0, this._width, this._height); }
        }

        public IntPtr GetSharedHandle()
        {
            throw new NotImplementedException();
        }

        public void SetData<T>(int level, T[] data, int startIndex, int elementCount)
            where T : struct
        {
            Threading.EnsureUIThread();

            int w, h;
            Texture.GetSizeForLevel(Width, Height, level, out w, out h);

            int elementSizeInByte = ReflectionHelpers.SizeOf<T>();
            GCHandle dataHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
            // Use try..finally to make sure dataHandle is freed in case of an error
            try
            {
                int startBytes = startIndex * elementSizeInByte;
                IntPtr dataPtr = new IntPtr(dataHandle.AddrOfPinnedObject().ToInt64() + startBytes);

                // Store the current bound texture.
                int prevTexture = ConcreteTexture2D.GetBoundTexture2D();

                System.Diagnostics.Debug.Assert(_glTexture < 0);
                if (prevTexture != _glTexture)
                {
                    GL.BindTexture(TextureTarget.Texture2D, _glTexture);
                    GraphicsExtensions.CheckGLError();
                }

                GL.PixelStore(PixelStoreParameter.UnpackAlignment, Math.Min(this.Format.GetSize(), 8));

                if (_glFormat == GLPixelFormat.CompressedTextureFormats)
                {
                    GL.CompressedTexImage2D(
                        TextureTarget.Texture2D, level, _glInternalFormat, w, h, 0, elementCount * elementSizeInByte, dataPtr);
                }
                else
                {
                    GL.TexImage2D(
                        TextureTarget.Texture2D, level, _glInternalFormat, w, h, 0,_glFormat, _glType, dataPtr);
                }
                GraphicsExtensions.CheckGLError();

#if !ANDROID
                // Required to make sure that any texture uploads on a thread are completed
                // before the main thread tries to use the texture.
                GL.Finish();
                GraphicsExtensions.CheckGLError();
#endif
                // Restore the bound texture.
                if (prevTexture != _glTexture)
                {
                    GL.BindTexture(TextureTarget.Texture2D, prevTexture);
                    GraphicsExtensions.CheckGLError();
                }
            }
            finally
            {
                dataHandle.Free();
            }
        }

        public void SetData<T>(int level, int arraySlice, Rectangle checkedRect, T[] data, int startIndex, int elementCount)
            where T : struct
        {
            Threading.EnsureUIThread();

            int elementSizeInByte = ReflectionHelpers.SizeOf<T>();
            GCHandle dataHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
            // Use try..finally to make sure dataHandle is freed in case of an error
            try
            {
                int startBytes = startIndex * elementSizeInByte;
                IntPtr dataPtr = new IntPtr(dataHandle.AddrOfPinnedObject().ToInt64() + startBytes);

                // Store the current bound texture.
                int prevTexture = ConcreteTexture2D.GetBoundTexture2D();

                System.Diagnostics.Debug.Assert(_glTexture < 0);
                if (prevTexture != _glTexture)
                {
                    GL.BindTexture(TextureTarget.Texture2D, _glTexture);
                    GraphicsExtensions.CheckGLError();
                }

                GL.PixelStore(PixelStoreParameter.UnpackAlignment, Math.Min(this.Format.GetSize(), 8));

                if (_glFormat == GLPixelFormat.CompressedTextureFormats)
                {
                    GL.CompressedTexSubImage2D(
                        TextureTarget.Texture2D, level, checkedRect.X, checkedRect.Y, checkedRect.Width, checkedRect.Height,
                        _glInternalFormat, elementCount * elementSizeInByte, dataPtr);
                }
                else
                {
                    GL.TexSubImage2D(
                        TextureTarget.Texture2D, level, checkedRect.X, checkedRect.Y, checkedRect.Width, checkedRect.Height,
                        _glFormat, _glType, dataPtr);
                }
                GraphicsExtensions.CheckGLError();

#if !ANDROID
                // Required to make sure that any texture uploads on a thread are completed
                // before the main thread tries to use the texture.
                GL.Finish();
                GraphicsExtensions.CheckGLError();
#endif
                // Restore the bound texture.
                if (prevTexture != _glTexture)
                {
                    GL.BindTexture(TextureTarget.Texture2D, prevTexture);
                    GraphicsExtensions.CheckGLError();
                }
            }
            finally
            {
                dataHandle.Free();
            }
        }

        public void GetData<T>(int level, int arraySlice, Rectangle checkedRect, T[] data, int startIndex, int elementCount)
            where T : struct
        {
            Threading.EnsureUIThread();

#if GLES
            // TODO: check for non renderable formats (formats that can't be attached to FBO)

            int framebufferId = 0;
            framebufferId = GL.GenFramebuffer();
            GraphicsExtensions.CheckGLError();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, framebufferId);
            GraphicsExtensions.CheckGLError();
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _glTexture, 0);
            GraphicsExtensions.CheckGLError();

            GL.ReadPixels(checkedRect.X, checkedRect.Y, checkedRect.Width, checkedRect.Height, _glFormat, _glType, data);
            GraphicsExtensions.CheckGLError();
            GL.DeleteFramebuffer(framebufferId);
#else
            int tSizeInByte = ReflectionHelpers.SizeOf<T>();
            GL.BindTexture(TextureTarget.Texture2D, _glTexture);
            GL.PixelStore(PixelStoreParameter.PackAlignment, Math.Min(tSizeInByte, 8));

            if (_glFormat == GLPixelFormat.CompressedTextureFormats)
            {
                // Note: for compressed format Format.GetSize() returns the size of a 4x4 block
                int pixelToT = Format.GetSize() / tSizeInByte;
                int tFullWidth = Math.Max(this.Width >> level, 1) / 4 * pixelToT;
                T[] temp = new T[Math.Max(this.Height >> level, 1) / 4 * tFullWidth];
                GL.GetCompressedTexImage(TextureTarget.Texture2D, level, temp);
                GraphicsExtensions.CheckGLError();

                int rowCount = checkedRect.Height / 4;
                int tRectWidth = checkedRect.Width / 4 * Format.GetSize() / tSizeInByte;
                for (int r = 0; r < rowCount; r++)
                {
                    int tempStart = checkedRect.X / 4 * pixelToT + (checkedRect.Top / 4 + r) * tFullWidth;
                    int dataStart = startIndex + r * tRectWidth;
                    Array.Copy(temp, tempStart, data, dataStart, tRectWidth);
                }
            }
            else
            {
                // we need to convert from our format size to the size of T here
                int tFullWidth = Math.Max(this.Width >> level, 1) * Format.GetSize() / tSizeInByte;
                T[] temp = new T[Math.Max(this.Height >> level, 1) * tFullWidth];
                GL.GetTexImage(TextureTarget.Texture2D, level, _glFormat, _glType, temp);
                GraphicsExtensions.CheckGLError();

                int pixelToT = Format.GetSize() / tSizeInByte;
                int rowCount = checkedRect.Height;
                int tRectWidth = checkedRect.Width * pixelToT;
                for (int r = 0; r < rowCount; r++)
                {
                    int tempStart = checkedRect.X * pixelToT + (r + checkedRect.Top) * tFullWidth;
                    int dataStart = startIndex + r * tRectWidth;
                    Array.Copy(temp, tempStart, data, dataStart, tRectWidth);
                }
            }
#endif
        }
        #endregion #region ITexture2DStrategy


        private static int GetBoundTexture2D()
        {
            int currentBoundTexture2D = 0;
            GL.GetInteger(GetPName.TextureBinding2D, out currentBoundTexture2D);
            GraphicsExtensions.LogGLError("GetBoundTexture2D(), GL.GetInteger()");
            return currentBoundTexture2D;
        }
    }
}
