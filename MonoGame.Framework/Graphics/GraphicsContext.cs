﻿// Copyright (C)2022 Nick Kastellanos

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Platform.Graphics;


namespace Microsoft.Xna.Framework.Graphics
{
    internal sealed class GraphicsContext : IDisposable
    {
        private GraphicsDevice _device;
        private GraphicsContextStrategy _strategy;
        private bool _isDisposed = false;


        internal GraphicsMetrics _graphicsMetrics;

        internal GraphicsContextStrategy Strategy { get { return _strategy; } }

        internal GraphicsContext(GraphicsDevice device, GraphicsContextStrategy strategy)
        {
            _device = device;
            _strategy = strategy;

        }


        /// <summary>
        /// The rendering information for debugging and profiling.
        /// The metrics are reset every frame after draw within <see cref="GraphicsDevice.Present"/>. 
        /// </summary>
        public GraphicsMetrics Metrics
        {
            get { return _graphicsMetrics; }
            set { _graphicsMetrics = value; }
        }

        #region IDisposable Members

        ~GraphicsContext()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThrowIfDisposed();

                _strategy.Dispose();

                _strategy = null;
                _device = null;
                _isDisposed = true;
            }
        }

        //[Conditional("DEBUG")]
        private void ThrowIfDisposed()
        {
            if (!_isDisposed)
                return;

            throw new ObjectDisposedException("Object is Disposed.");
        }

        #endregion IDisposable Members

    }
}
