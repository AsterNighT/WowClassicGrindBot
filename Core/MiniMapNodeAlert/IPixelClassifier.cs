﻿namespace Core
{
    public interface IPixelClassifier
    {
        bool IsMatch(byte red, byte green, byte blue);

        int MaxBlue { get; }

        int MinRedGreen { get; }
    }
}