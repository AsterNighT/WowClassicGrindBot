﻿namespace Core
{
    public class StartupConfigPathing
    {
        public const string Position = "Pathing";

        public enum Types
        {
            Local,
            RemoteV1,
            RemoteV3
        }


#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public StartupConfigPathing()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {

        }

        public StartupConfigPathing(string Mode, string hostv1, int portv1, string hostv3, int portv3)
        {
            this.Mode = Mode;

            this.hostv1 = hostv1;
            this.portv1 = portv1;

            this.hostv3 = hostv3;
            this.portv3 = portv3;
        }

        public Types Type => System.Enum.TryParse(Mode, out Types m) ? m : Types.Local;

        public string Mode { get; init; }

        public string hostv1 { get; init; }
        public int portv1 { get; init; }

        public string hostv3 { get; init; }
        public int portv3 { get; init; }
    }
}
