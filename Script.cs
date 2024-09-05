namespace Datus.LockStep
{
    using System;
    using System.IO;
    using System.Text.RegularExpressions;

    class Script: IComparable<Script>
    {
        public static readonly Regex UpRegex = new Regex(@"^\d{14}\.up(?:\..*)?\.psql$");
        public static readonly Regex DownRegex = new Regex(@"^\d{14}\.down(?:\..*)?\.psql$");
        public static readonly Regex StampRegex = new Regex(@"^\d{14}$");

        private Script(string filepath, string filename, Direction direction)
        {
            FilePath = filepath;
            Direction = direction;
            FileName = Path.GetFileName(FilePath);
            Stamp = FileName.Substring(0, 14); 
        }

        public Direction Direction { get; private set; }
        public string Stamp { get; private set; }
        public string FilePath { get; private set; }
        public string FileName { get; private set; }

        public bool IsRunnableWithTargetStamp(string targetStamp)
        {
            if (targetStamp == null) {
                return true;
            } else if (Direction == Direction.Up) {
                // Up is INCLUSIVE.
                return String.CompareOrdinal(Stamp, targetStamp) < 1;
            } else {
                //  Down is EXCLUSIVE.
                return String.CompareOrdinal(Stamp, targetStamp) > 0;
            }
        }

        public string Read()
        {
            return File.ReadAllText(FilePath);
        }

        public static Script FromFile(string filepath, Direction direction)
        {
            string filename = Path.GetFileName(filepath);
            var regex = direction == Direction.Up ? UpRegex : DownRegex;
            if (!regex.IsMatch(filename)) return null;
            return new Script(filepath, filename, direction);
        }

        int IComparable<Script>.CompareTo(Script other)
        {
            return Direction == Direction.Up ? Stamp.CompareTo(other.Stamp) : other.Stamp.CompareTo(Stamp); 
        }
    }
}