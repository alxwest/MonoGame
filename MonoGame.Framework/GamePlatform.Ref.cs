// Copyright (C)2023 Nick Kastellanos

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Microsoft.Xna.Platform
{
    sealed class ConcreteGame : GameStrategy
    {
        public ConcreteGame(Game game) : base(game)
        {
        }

        internal override void Run()
        {
            throw new PlatformNotSupportedException();
        }

        public override void Tick()
        {
            throw new PlatformNotSupportedException();
        }

        public override void BeforeInitialize()
        {
            throw new PlatformNotSupportedException();
        }

        public override void TickExiting()
        {
            throw new PlatformNotSupportedException();
        }

        public override bool BeforeUpdate(GameTime gameTime)
        {
            throw new PlatformNotSupportedException();
        }

        public override bool BeforeDraw(GameTime gameTime)
        {
            throw new PlatformNotSupportedException();
        }

        public override void EnterFullScreen()
        {
            throw new PlatformNotSupportedException();
        }

        public override void ExitFullScreen()
        {
            throw new PlatformNotSupportedException();
        }

        internal override void OnPresentationChanged(PresentationParameters pp)
        {
            throw new PlatformNotSupportedException();
        }

        public override void EndScreenDeviceChange(string screenDeviceName, int clientWidth, int clientHeight)
        {
            throw new PlatformNotSupportedException();
        }

        public override void BeginScreenDeviceChange(bool willBeFullScreen)
        {
            throw new PlatformNotSupportedException();
        }

        public override void Log(string message)
        {
            throw new PlatformNotSupportedException();
        }

        public override void EndDraw()
        {
            throw new PlatformNotSupportedException();
        }
    }
}
