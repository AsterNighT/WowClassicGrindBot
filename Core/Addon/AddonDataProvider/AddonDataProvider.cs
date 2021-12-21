﻿using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using SharedLib;
using Game;

namespace Core
{
    public sealed class AddonDataProvider : IAddonDataProvider
    {
        private readonly DataFrame[] frames;
        private readonly Color[] FrameColor;

        private readonly IWowScreen wowScreen;
        private readonly int width;
        private readonly int height;
        private readonly DirectBitmapCapturer capturer;
        private Rectangle rectangle;
        private Point p;

        private readonly Color firstColor = Color.FromArgb(255, 0, 0, 0);
        private readonly Color lastColor = Color.FromArgb(255, 30, 132, 129);

        public AddonDataProvider(IWowScreen wowScreen, List<DataFrame> frames)
        {
            this.wowScreen = wowScreen;

            this.frames = frames.ToArray();
            this.FrameColor = new Color[this.frames.Length];

            this.width = frames.Last().point.X + 1;
            this.height = frames.Max(f => f.point.Y) + 1;

            wowScreen.GetPosition(out var p);
            wowScreen.GetRectangle(out rectangle);
            rectangle.Width = width;
            rectangle.Height = height;
            rectangle = new Rectangle(p.X, p.Y, width, height);
            capturer = new DirectBitmapCapturer(rectangle);
        }

        public void Update()
        {
            wowScreen.GetPosition(out p);
            rectangle.X = p.X;
            rectangle.Y = p.Y;
            capturer.Capture(rectangle);

            if (!Visible()) return;

            for (int i = 0; i < frames.Length; i++)
            {
                FrameColor[frames[i].index] = capturer.GetColorAt(frames[i].point);
            }
        }

        private bool Visible()
        {
            return capturer.GetColorAt(frames[0].point) == firstColor &&
                capturer.GetColorAt(frames[frames.Length-1].point) == lastColor;
        }

        public Color GetColor(int index)
        {
            return FrameColor[index];
        }

        public int GetInt(int index)
        {
            return (FrameColor[index].R * 65536) + (FrameColor[index].G * 256) + FrameColor[index].B;
        }

        public void Dispose()
        {
            capturer?.Dispose();
        }
    }
}
